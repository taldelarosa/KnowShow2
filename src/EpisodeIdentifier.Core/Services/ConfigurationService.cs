using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Configuration;
using System.IO.Abstractions;
using FluentValidation;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Configuration service with hot-reload support and comprehensive validation.
/// Monitors configuration file changes and applies updates during processing cycles.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly ConfigurationValidator _validator;
    private readonly string _configFilePath;
    private ConfigurationResult? _lastLoadedConfig;
    private DateTime _lastFileWriteTime = DateTime.MinValue;

    public ConfigurationService(
        ILogger<ConfigurationService> logger,
        IFileSystem? fileSystem = null)
    {
        _logger = logger;
        _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        _validator = new ConfigurationValidator();
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
    }

    /// <summary>
    /// Loads configuration from the default config file with comprehensive validation.
    /// </summary>
    /// <returns>Configuration result containing loaded config or validation errors.</returns>
    public async Task<ConfigurationResult> LoadConfiguration()
    {
        var operationId = Guid.NewGuid().ToString()[..8];
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationId"] = operationId,
                ["Operation"] = "LoadConfiguration",
                ["ConfigPath"] = _configFilePath
            });

            _logger.LogInformation("Starting configuration load operation {OperationId} from {ConfigPath}",
                operationId, _configFilePath);

            if (!_fileSystem.File.Exists(_configFilePath))
            {
                _logger.LogWarning("Configuration file not found - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure("Configuration file not found");
            }

            var fileSize = _fileSystem.FileInfo.New(_configFilePath).Length;
            _logger.LogDebug("Configuration file located - Size: {FileSize} bytes, LastModified: {LastModified}",
                fileSize, _fileSystem.File.GetLastWriteTimeUtc(_configFilePath));

            var jsonContent = await _fileSystem.File.ReadAllTextAsync(_configFilePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogError("Configuration file is empty - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure("Configuration file is empty");
            }

            _logger.LogDebug("Configuration content loaded - Length: {ContentLength} characters", jsonContent.Length);

            Configuration? config;
            try
            {
                config = JsonSerializer.Deserialize<Configuration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                _logger.LogDebug("Configuration JSON deserialization successful");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Configuration JSON parsing failed - Operation: {OperationId}, Path: {ConfigPath}, Error: {ErrorMessage}, Duration: {Duration}ms",
                    operationId, _configFilePath, ex.Message, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure($"Invalid JSON format: {ex.Message}");
            }

            if (config == null)
            {
                _logger.LogError("Configuration deserialization returned null - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure("Configuration deserialization resulted in null");
            }

            // Set metadata
            config.SourceFilePath = _configFilePath;
            config.LastLoaded = DateTime.UtcNow;

            // Validate the configuration
            _logger.LogDebug("Starting configuration validation - HashingAlgorithm: {HashingAlgorithm}, Version: {Version}",
                config.HashingAlgorithm, config.Version);
            var validationResult = ValidateConfiguration(config);

            if (validationResult.IsValid)
            {
                _lastLoadedConfig = validationResult;
                _lastFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);

                _logger.LogInformation("Configuration loaded successfully - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms, " +
                    "Version: {Version}, HashingAlgorithm: {HashingAlgorithm}, MatchThreshold: {MatchThreshold:P1}, RenameThreshold: {RenameThreshold:P1}, FuzzyHashThreshold: {FuzzyHashThreshold}",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds,
                    config.Version, config.HashingAlgorithm, config.MatchConfidenceThreshold, config.RenameConfidenceThreshold, config.FuzzyHashThreshold);

                return validationResult;
            }
            else
            {
                _logger.LogWarning("Configuration validation failed - Operation: {OperationId}, ErrorCount: {ErrorCount}, Errors: [{Errors}], Duration: {Duration}ms",
                    operationId, validationResult.Errors.Count, string.Join(", ", validationResult.Errors), stopwatch.ElapsedMilliseconds);
                _logger.LogError("Configuration validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return validationResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected configuration load error - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, _configFilePath, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return ConfigurationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads configuration if the underlying file has changed since last load.
    /// Implements efficient change detection using file timestamps.
    /// </summary>
    /// <returns>True if configuration was reloaded, false if no changes detected.</returns>
    public async Task<bool> ReloadIfChanged()
    {
        var operationId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "ConfigurationReload",
            ["OperationId"] = operationId,
            ["ConfigPath"] = _configFilePath
        });

        try
        {
            if (!_fileSystem.File.Exists(_configFilePath))
            {
                _logger.LogWarning("Configuration file not found during reload check - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds);
                return false;
            }

            var currentWriteTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);
            var lastWriteTime = _lastFileWriteTime;

            if (currentWriteTime <= lastWriteTime)
            {
                _logger.LogTrace("Configuration file unchanged, skipping reload - Operation: {OperationId}, CurrentTime: {CurrentTime}, LastTime: {LastTime}, Duration: {Duration}ms",
                    operationId, currentWriteTime, lastWriteTime, stopwatch.ElapsedMilliseconds);
                return false;
            }

            _logger.LogInformation("Configuration file change detected - Operation: {OperationId}, Path: {ConfigPath}, CurrentTime: {CurrentTime}, LastTime: {LastTime}",
                operationId, _configFilePath, currentWriteTime, lastWriteTime);

            var reloadResult = await LoadConfiguration();
            stopwatch.Stop();

            if (reloadResult.IsValid)
            {
                _logger.LogInformation("Configuration hot-reload completed successfully - Operation: {OperationId}, Duration: {Duration}ms, ConfigDetails: {ConfigDetails}",
                    operationId, stopwatch.ElapsedMilliseconds,
                    new
                    {
                        Version = _lastLoadedConfig?.Configuration?.Version ?? "Unknown",
                        HashingAlgorithm = _lastLoadedConfig?.Configuration?.HashingAlgorithm.ToString() ?? "Unknown",
                        FuzzyHashThreshold = _lastLoadedConfig?.Configuration?.FuzzyHashThreshold ?? 0,
                        MatchConfidenceThreshold = _lastLoadedConfig?.Configuration?.MatchConfidenceThreshold ?? 0
                    });
                return true;
            }
            else
            {
                _logger.LogError("Configuration hot-reload failed, keeping previous configuration - Operation: {OperationId}, Duration: {Duration}ms, ErrorCount: {ErrorCount}, Errors: {Errors}",
                    operationId, stopwatch.ElapsedMilliseconds, reloadResult.Errors.Count, string.Join(", ", reloadResult.Errors));

                // Don't update _lastFileWriteTime so we keep trying to reload
                return false;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during configuration reload check - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, _configFilePath, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Validates a configuration object against all business rules and constraints.
    /// Uses FluentValidation for comprehensive rule checking.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public ConfigurationResult ValidateConfiguration(Configuration config)
    {
        var operationId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "ConfigurationValidation",
            ["OperationId"] = operationId,
            ["ConfigSource"] = config?.GetType().Name ?? "null"
        });

        if (config == null)
        {
            stopwatch.Stop();
            _logger.LogWarning("Configuration validation failed - null configuration provided - Operation: {OperationId}, Duration: {Duration}ms",
                operationId, stopwatch.ElapsedMilliseconds);
            return ConfigurationResult.Failure("Configuration cannot be null");
        }

        try
        {
            _logger.LogDebug("Starting configuration validation - Operation: {OperationId}, ConfigDetails: {ConfigDetails}",
                operationId,
                new
                {
                    Version = config.Version ?? "Unknown",
                    HashingAlgorithm = config.HashingAlgorithm.ToString(),
                    FuzzyHashThreshold = config.FuzzyHashThreshold,
                    FilenamePatterns = config.FilenamePatterns != null ? "Present" : "Missing"
                });

            var validationResult = _validator.Validate(config);
            stopwatch.Stop();

            if (validationResult.IsValid)
            {
                _logger.LogInformation("Configuration validation successful - Operation: {OperationId}, Duration: {Duration}ms, ValidationRules: {RuleCount}",
                    operationId, stopwatch.ElapsedMilliseconds, _validator.GetType().Name);
                return ConfigurationResult.Success(config);
            }
            else
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                var errorsByProperty = validationResult.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Count());

                _logger.LogWarning("Configuration validation failed - Operation: {OperationId}, Duration: {Duration}ms, ErrorCount: {ErrorCount}, ErrorsByProperty: {ErrorsByProperty}, Errors: {Errors}",
                    operationId, stopwatch.ElapsedMilliseconds, errors.Count, errorsByProperty, string.Join("; ", errors));

                return ConfigurationResult.Failure(errors);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during configuration validation - Operation: {OperationId}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return ConfigurationResult.Failure($"Validation error: {ex.Message}");
        }
    }
}