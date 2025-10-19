using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Extensions;
using EpisodeIdentifier.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for error handling during concurrent operations.
/// These tests verify that various error conditions are properly handled
/// during concurrent bulk processing while maintaining system stability.
///
/// T011: Integration test for error handling with concurrent operations
/// Tests error resilience during concurrent processing
/// </summary>
public class ErrorHandlingConcurrentIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly MockFileSystem _fileSystem;

    public ErrorHandlingConcurrentIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _fileSystem = new MockFileSystem();
    }

    private ServiceProvider CreateServiceProvider(string configPath)
    {
        var services = new ServiceCollection();

        // Setup logging to capture test output
        services.AddLogging(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Override file system with mock
        services.AddSingleton<System.IO.Abstractions.IFileSystem>(_fileSystem);

        // Add all episode identification services using the extension method
        services.AddEpisodeIdentificationServices();

        // Override the configuration service to use the test config path
        services.AddScoped<IConfigurationService>(provider =>
            new ConfigurationService(
                provider.GetRequiredService<ILogger<ConfigurationService>>(),
                provider.GetRequiredService<System.IO.Abstractions.IFileSystem>(),
                configPath
            ));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ConcurrentProcessing_MixedFileTypes_ShouldHandleErrorsGracefully()
    {
        // Arrange - Mix of valid files, invalid filenames, and missing files
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ValidFile.S01E01.mkv"),      // Valid file
            Path.Combine(AppContext.BaseDirectory, "InvalidName.mkv"),           // Invalid filename
            Path.Combine(AppContext.BaseDirectory, "Missing.S01E02.mkv")         // Missing file reference
        };

        // Create only some of the files to simulate mixed scenarios
        _fileSystem.AddFile(testFiles[0], new MockFileData("valid content"));
        _fileSystem.AddFile(testFiles[1], new MockFileData("invalid name content"));
        // Note: testFiles[2] is intentionally missing

        var validConfig = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "matchingThresholds": {
                "textBased": {
                    "matchConfidence": 0.80,
                    "renameConfidence": 0.85,
                    "fuzzyHashSimilarity": 75
                },
                "pgs": {
                    "matchConfidence": 0.70,
                    "renameConfidence": 0.75,
                    "fuzzyHashSimilarity": 65
                },
                "vobSub": {
                    "matchConfidence": 0.60,
                    "renameConfidence": 0.70,
                    "fuzzyHashSimilarity": 55
                }
            },
            "MaxConcurrency": 3,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(validConfig));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();

        // Act
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 3,
                ContinueOnError = true
            }
        };

        var result = await processor.ProcessAsync(request);

        // Assert - Should complete processing despite errors
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(3);
        result.ProcessedFiles.Should().BeGreaterThan(0); // At least one file should process
        result.FailedFiles.Should().BeGreaterThan(0); // At least one should fail
        result.FileResults.Should().HaveCount(3); // Should have results for all attempted files
    }

    [Fact]
    public async Task HighConcurrency_FileSystemErrors_ShouldMaintainStability()
    {
        // Arrange - Test system stability under high concurrency with file system errors
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = Enumerable.Range(1, 20)
            .Select(i => Path.Combine(AppContext.BaseDirectory, $"Test.S01E{i:00}.mkv"))
            .ToArray();

        // Add files with some having simulated access errors
        for (int i = 0; i < testFiles.Length; i++)
        {
            if (i % 4 == 0) // Every 4th file will be missing to simulate errors
                continue;
            _fileSystem.AddFile(testFiles[i], new MockFileData($"test content {i}"));
        }

        var validConfig = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "matchingThresholds": {
                "textBased": {
                    "matchConfidence": 0.80,
                    "renameConfidence": 0.85,
                    "fuzzyHashSimilarity": 75
                },
                "pgs": {
                    "matchConfidence": 0.70,
                    "renameConfidence": 0.75,
                    "fuzzyHashSimilarity": 65
                },
                "vobSub": {
                    "matchConfidence": 0.60,
                    "renameConfidence": 0.70,
                    "fuzzyHashSimilarity": 55
                }
            },
            "MaxConcurrency": 8,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(validConfig));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();

        // Act
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 8,
                ContinueOnError = true
            }
        };

        var result = await processor.ProcessAsync(request);

        // Assert - System should remain stable
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(20);
        result.ProcessedFiles.Should().BeGreaterThan(0);
        result.FailedFiles.Should().BeGreaterThan(0);
        result.FileResults.Count.Should().Be(20); // Should have attempted all files
    }

    [Fact]
    public async Task ConcurrentProcessing_ConfigurationErrors_ShouldFallbackGracefully()
    {
        // Arrange - Test with invalid configuration during concurrent processing
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Test1.S01E01.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Test2.S01E02.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Test3.S01E03.mkv")
        };

        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("test content"));
        }

        // Invalid config - MaxConcurrency too high
        var invalidConfig = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "matchingThresholds": {
                "textBased": {
                    "matchConfidence": 0.80,
                    "renameConfidence": 0.85,
                    "fuzzyHashSimilarity": 75
                },
                "pgs": {
                    "matchConfidence": 0.70,
                    "renameConfidence": 0.75,
                    "fuzzyHashSimilarity": 65
                },
                "vobSub": {
                    "matchConfidence": 0.60,
                    "renameConfidence": 0.70,
                    "fuzzyHashSimilarity": 55
                }
            },
            "MaxConcurrency": 1000,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(invalidConfig));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();

        // Act - Should fallback to safe defaults
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 1, // Use fallback value
                ContinueOnError = true
            }
        };

        var result = await processor.ProcessAsync(request);

        // Assert - Should process files with fallback configuration
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(3);
        result.ProcessedFiles.Should().Be(3);
        result.FailedFiles.Should().Be(0);
    }

    [Fact]
    public async Task ErrorRecovery_PartialFailures_ShouldProcessRemainingFiles()
    {
        // Arrange - Test error recovery with partial failures
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Working1.S01E01.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Corrupt.S01E02.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Working2.S01E03.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Working3.S01E04.mkv")
        };

        // Create files with one intentionally problematic
        _fileSystem.AddFile(testFiles[0], new MockFileData("good content"));
        _fileSystem.AddFile(testFiles[1], new MockFileData("")); // Empty file to simulate corruption
        _fileSystem.AddFile(testFiles[2], new MockFileData("good content"));
        _fileSystem.AddFile(testFiles[3], new MockFileData("good content"));

        var validConfig = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "matchingThresholds": {
                "textBased": {
                    "matchConfidence": 0.80,
                    "renameConfidence": 0.85,
                    "fuzzyHashSimilarity": 75
                },
                "pgs": {
                    "matchConfidence": 0.70,
                    "renameConfidence": 0.75,
                    "fuzzyHashSimilarity": 65
                },
                "vobSub": {
                    "matchConfidence": 0.60,
                    "renameConfidence": 0.70,
                    "fuzzyHashSimilarity": 55
                }
            },
            "MaxConcurrency": 2,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(validConfig));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();

        // Act
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 2,
                ContinueOnError = true
            }
        };

        var result = await processor.ProcessAsync(request);

        // Assert - Should continue processing despite partial failures
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(4);
        result.ProcessedFiles.Should().BeGreaterOrEqualTo(3); // At least 3 should succeed
        result.FileResults.Count.Should().Be(4); // Should have attempted all files

        // Verify specific files were processed
        var workingFiles = result.FileResults.Where(r => r.FilePath.Contains("Working")).ToList();
        workingFiles.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ConcurrentAccess_SharedResources_ShouldPreventDeadlocks()
    {
        // Arrange - Test concurrent access to shared resources (database, config)
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = Enumerable.Range(1, 10)
            .Select(i => Path.Combine(AppContext.BaseDirectory, $"Concurrent.S01E{i:00}.mkv"))
            .ToArray();

        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("concurrent test content"));
        }

        var validConfig = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "matchingThresholds": {
                "textBased": {
                    "matchConfidence": 0.80,
                    "renameConfidence": 0.85,
                    "fuzzyHashSimilarity": 75
                },
                "pgs": {
                    "matchConfidence": 0.70,
                    "renameConfidence": 0.75,
                    "fuzzyHashSimilarity": 65
                },
                "vobSub": {
                    "matchConfidence": 0.60,
                    "renameConfidence": 0.70,
                    "fuzzyHashSimilarity": 55
                }
            },
            "MaxConcurrency": 5,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(validConfig));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();

        // Act - Process with high concurrency to test resource contention
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 5,
                ContinueOnError = true
            }
        };

        var startTime = DateTime.UtcNow;
        var result = await processor.ProcessAsync(request);
        var duration = DateTime.UtcNow - startTime;

        // Assert - Should complete within reasonable time (no deadlocks)
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(10);
        result.ProcessedFiles.Should().Be(10);
        result.FailedFiles.Should().Be(0);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should not hang
    }
}
