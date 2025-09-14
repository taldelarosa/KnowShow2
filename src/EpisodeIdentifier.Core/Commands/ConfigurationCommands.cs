using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using System.IO.Abstractions;
using System.Diagnostics;

namespace EpisodeIdentifier.Core.Commands;

/// <summary>
/// CLI commands for configuration validation and hash testing operations.
/// Provides utilities for validating configuration files and testing fuzzy hash functionality.
/// </summary>
public static class ConfigurationCommands
{
    /// <summary>
    /// Creates the configuration validation and testing command group.
    /// </summary>
    /// <returns>Command group containing config validation and hash testing commands.</returns>
    public static Command CreateConfigCommands()
    {
        var configCommand = new Command("config", "Configuration management and testing utilities");

        // Add subcommands
        configCommand.AddCommand(CreateValidateCommand());
        configCommand.AddCommand(CreateTestHashCommand());
        configCommand.AddCommand(CreateCompareHashCommand());

        return configCommand;
    }

    /// <summary>
    /// Creates the configuration validation command.
    /// </summary>
    private static Command CreateValidateCommand()
    {
        var validateCommand = new Command("validate", "Validate configuration file and display results");

        var configPathOption = new Option<FileInfo?>(
            "--config-path",
            "Path to configuration file (default: episodeidentifier.config.json in application directory)")
        { IsRequired = false };
        validateCommand.Add(configPathOption);

        var verboseOption = new Option<bool>(
            "--verbose",
            "Show detailed validation information including all configuration values")
        { IsRequired = false };
        validateCommand.Add(verboseOption);

        validateCommand.SetHandler(async (configPath, verbose) =>
        {
            await HandleValidateCommand(configPath, verbose);
        }, configPathOption, verboseOption);

        return validateCommand;
    }

    /// <summary>
    /// Creates the fuzzy hash testing command.
    /// </summary>
    private static Command CreateTestHashCommand()
    {
        var testHashCommand = new Command("test-hash", "Compute and display fuzzy hash for a file");

        var filePathOption = new Option<FileInfo>(
            "--file",
            "Path to file for fuzzy hash computation")
        { IsRequired = true };
        testHashCommand.Add(filePathOption);

        var verboseOption = new Option<bool>(
            "--verbose",
            "Show detailed hash computation information including timing")
        { IsRequired = false };
        testHashCommand.Add(verboseOption);

        testHashCommand.SetHandler(async (filePath, verbose) =>
        {
            await HandleTestHashCommand(filePath, verbose);
        }, filePathOption, verboseOption);

        return testHashCommand;
    }

    /// <summary>
    /// Creates the hash comparison command.
    /// </summary>
    private static Command CreateCompareHashCommand()
    {
        var compareHashCommand = new Command("compare-hash", "Compare two files using fuzzy hashing");

        var file1Option = new Option<FileInfo>(
            "--file1",
            "Path to first file for comparison")
        { IsRequired = true };
        compareHashCommand.Add(file1Option);

        var file2Option = new Option<FileInfo>(
            "--file2",
            "Path to second file for comparison")
        { IsRequired = true };
        compareHashCommand.Add(file2Option);

        var thresholdOption = new Option<int>(
            "--threshold",
            "Similarity threshold for match determination (0-100, default: 50)")
        { IsRequired = false };
        thresholdOption.SetDefaultValue(50);
        compareHashCommand.Add(thresholdOption);

        var verboseOption = new Option<bool>(
            "--verbose",
            "Show detailed comparison information including individual hashes")
        { IsRequired = false };
        compareHashCommand.Add(verboseOption);

        compareHashCommand.SetHandler(async (file1, file2, threshold, verbose) =>
        {
            await HandleCompareHashCommand(file1, file2, threshold, verbose);
        }, file1Option, file2Option, thresholdOption, verboseOption);

        return compareHashCommand;
    }

    /// <summary>
    /// Handles the configuration validation command.
    /// </summary>
    private static async Task HandleValidateCommand(FileInfo? configPath, bool verbose)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        try
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                if (verbose)
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                }
            });

            // Create configuration service
            var logger = loggerFactory.CreateLogger<ConfigurationService>();
            var configService = new ConfigurationService(logger);

            // Load and validate configuration
            var result = await configService.LoadConfiguration();

            if (result.IsValid && result.Configuration != null)
            {
                var output = new
                {
                    status = "success",
                    message = "Configuration is valid",
                    configuration = verbose ? new
                    {
                        hashingAlgorithm = result.Configuration.HashingAlgorithm.ToString(),
                        fuzzyHashThreshold = result.Configuration.FuzzyHashThreshold,
                        matchConfidenceThreshold = result.Configuration.MatchConfidenceThreshold,
                        renameConfidenceThreshold = result.Configuration.RenameConfidenceThreshold,
                        filenamePatterns = result.Configuration.FilenamePatterns,
                        filenameTemplate = result.Configuration.FilenameTemplate,
                        version = result.Configuration.Version,
                        validationTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    } : null
                };

                Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
                Environment.Exit(0);
            }
            else
            {
                var output = new
                {
                    status = "error",
                    message = "Configuration validation failed",
                    errors = result.Errors,
                    validationTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            var output = new
            {
                status = "error",
                message = "Unexpected error during configuration validation",
                error = ex.Message,
                validationTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the fuzzy hash testing command.
    /// </summary>
    private static async Task HandleTestHashCommand(FileInfo filePath, bool verbose)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        try
        {
            // Validate file exists
            if (!filePath.Exists)
            {
                var errorOutput = new
                {
                    status = "error",
                    message = "File not found",
                    filePath = filePath.FullName,
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(errorOutput, jsonOptions));
                Environment.Exit(1);
                return;
            }

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                if (verbose)
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                }
            });

            // Create hashing service
            var logger = loggerFactory.CreateLogger<CTPhHashingService>();
            var fileSystem = new System.IO.Abstractions.FileSystem();
            var hashingService = new CTPhHashingService(logger, fileSystem);

            // Compute hash with timing
            var stopwatch = Stopwatch.StartNew();
            var hash = await hashingService.ComputeFuzzyHash(filePath.FullName);
            stopwatch.Stop();

            if (!string.IsNullOrEmpty(hash))
            {
                var output = new
                {
                    status = "success",
                    message = "Fuzzy hash computed successfully",
                    filePath = filePath.FullName,
                    fileSize = filePath.Length,
                    fuzzyHash = hash,
                    computationTime = new
                    {
                        milliseconds = stopwatch.ElapsedMilliseconds,
                        formatted = $"{stopwatch.ElapsedMilliseconds}ms"
                    },
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
                Environment.Exit(0);
            }
            else
            {
                var output = new
                {
                    status = "error",
                    message = "Failed to compute fuzzy hash",
                    filePath = filePath.FullName,
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            var output = new
            {
                status = "error",
                message = "Unexpected error during hash computation",
                filePath = filePath.FullName,
                error = ex.Message,
                testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the hash comparison command.
    /// </summary>
    private static async Task HandleCompareHashCommand(FileInfo file1, FileInfo file2, int threshold, bool verbose)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        try
        {
            // Validate files exist
            if (!file1.Exists)
            {
                var errorOutput = new
                {
                    status = "error",
                    message = "First file not found",
                    filePath = file1.FullName,
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(errorOutput, jsonOptions));
                Environment.Exit(1);
                return;
            }

            if (!file2.Exists)
            {
                var errorOutput = new
                {
                    status = "error",
                    message = "Second file not found",
                    filePath = file2.FullName,
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(errorOutput, jsonOptions));
                Environment.Exit(1);
                return;
            }

            // Validate threshold
            if (threshold < 0 || threshold > 100)
            {
                var errorOutput = new
                {
                    status = "error",
                    message = "Invalid threshold value. Must be between 0 and 100",
                    threshold = threshold,
                    testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Console.WriteLine(JsonSerializer.Serialize(errorOutput, jsonOptions));
                Environment.Exit(1);
                return;
            }

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                if (verbose)
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                }
            });

            // Create hashing service
            var logger = loggerFactory.CreateLogger<CTPhHashingService>();
            var fileSystem = new System.IO.Abstractions.FileSystem();
            var hashingService = new CTPhHashingService(logger, fileSystem);

            // Compare files with timing
            var stopwatch = Stopwatch.StartNew();
            var comparisonResult = await hashingService.CompareFiles(file1.FullName, file2.FullName);
            stopwatch.Stop();

            var isMatch = comparisonResult.SimilarityScore >= threshold;
            var output = new
            {
                status = "success",
                message = "File comparison completed successfully",
                comparison = new
                {
                    file1 = new
                    {
                        path = file1.FullName,
                        size = file1.Length,
                        hash = verbose ? comparisonResult.Hash1 : null
                    },
                    file2 = new
                    {
                        path = file2.FullName,
                        size = file2.Length,
                        hash = verbose ? comparisonResult.Hash2 : null
                    },
                    similarityScore = comparisonResult.SimilarityScore,
                    threshold = threshold,
                    isMatch = isMatch,
                    matchResult = isMatch ? "MATCH" : "NO_MATCH"
                },
                timing = new
                {
                    comparisonTimeMs = comparisonResult.ComparisonTime.TotalMilliseconds,
                    totalTimeMs = stopwatch.ElapsedMilliseconds,
                    formatted = $"{stopwatch.ElapsedMilliseconds}ms total"
                },
                testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
            Environment.Exit(isMatch ? 0 : 2); // Use exit code 2 for "no match" to distinguish from errors
        }
        catch (Exception ex)
        {
            var output = new
            {
                status = "error",
                message = "Unexpected error during file comparison",
                file1 = file1.FullName,
                file2 = file2.FullName,
                error = ex.Message,
                testTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
            Environment.Exit(1);
        }
    }
}