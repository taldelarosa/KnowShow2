using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Legacy configuration service implementation for backward compatibility.
/// This maintains the old API while the new fuzzy hashing plus system is being integrated.
/// </summary>
public class AppConfigService : IAppConfigService
{
    private readonly ILogger<AppConfigService> _logger;
    private const string DefaultConfigFileName = "episodeidentifier.config.json";
    private DateTime _lastConfigFileWriteTime = DateTime.MinValue;
    private int _lastKnownMaxConcurrency = 1; // Track MaxConcurrency for hot-reload change detection
    public AppConfig Config { get; private set; } = new();
    public Models.Configuration.ConfigurationResult? LastConfigurationResult { get; private set; }

    public AppConfigService(ILogger<AppConfigService> logger)
    {
        _logger = logger;
    }

    public async Task LoadConfigurationAsync(string? configPath = null)
    {
        configPath ??= DefaultConfigFileName;

        try
        {
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Configuration file '{ConfigPath}' not found, using default settings", configPath);
                await SaveConfigurationAsync(configPath); // Create default config file
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (config != null)
            {
                Config = config;
                
                // Track file timestamp and MaxConcurrency for hot-reload detection
                _lastConfigFileWriteTime = File.GetLastWriteTimeUtc(configPath);
                _lastKnownMaxConcurrency = Config.MaxConcurrency;
                
                _logger.LogInformation("Configuration loaded from '{ConfigPath}'", configPath);
                LogCurrentSettings();
            }
            else
            {
                _logger.LogWarning("Failed to deserialize configuration from '{ConfigPath}', using defaults", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from '{ConfigPath}', using defaults", configPath);
            
            // Enhanced configuration validation error logging
            LogConfigurationValidationError("Configuration Load", ex, configPath);
        }
    }

    public async Task SaveConfigurationAsync(string? configPath = null)
    {
        configPath ??= DefaultConfigFileName;

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(Config, jsonOptions);
            await File.WriteAllTextAsync(configPath, jsonContent);

            _logger.LogInformation("Configuration saved to '{ConfigPath}'", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to '{ConfigPath}'", configPath);
        }
    }

    private void LogCurrentSettings()
    {
        _logger.LogDebug("Current configuration:");
        _logger.LogDebug("  Match Confidence Threshold: {MatchThreshold:P1}", Config.MatchConfidenceThreshold);
        _logger.LogDebug("  Rename Confidence Threshold: {RenameThreshold:P1}", Config.RenameConfidenceThreshold);
        _logger.LogDebug("  Filename Template: {Template}", Config.FilenameTemplate);
        _logger.LogDebug("  Primary Pattern: {Pattern}", Config.FilenamePatterns.PrimaryPattern);
        _logger.LogDebug("  Secondary Pattern: {Pattern}", Config.FilenamePatterns.SecondaryPattern);
        _logger.LogDebug("  Tertiary Pattern: {Pattern}", Config.FilenamePatterns.TertiaryPattern);
        _logger.LogDebug("  Max Concurrency: {MaxConcurrency}", Config.MaxConcurrency);
    }

    /// <summary>
    /// Logs detailed configuration validation errors for troubleshooting.
    /// </summary>
    /// <param name="operation">The operation that caused the error (e.g., "Configuration Load", "Validation")</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="configPath">The configuration file path</param>
    private void LogConfigurationValidationError(string operation, Exception exception, string? configPath = null)
    {
        _logger.LogError("Configuration validation error during {Operation}", operation);
        _logger.LogError("Error Type: {ExceptionType}", exception.GetType().Name);
        _logger.LogError("Error Message: {Message}", exception.Message);
        
        if (!string.IsNullOrEmpty(configPath))
        {
            _logger.LogError("Configuration Path: {ConfigPath}", configPath);
            
            // Log file existence and basic info for troubleshooting
            try
            {
                if (File.Exists(configPath))
                {
                    var fileInfo = new FileInfo(configPath);
                    _logger.LogError("File Size: {Size} bytes", fileInfo.Length);
                    _logger.LogError("Last Modified: {LastModified}", fileInfo.LastWriteTime);
                }
                else
                {
                    _logger.LogError("Configuration file does not exist");
                }
            }
            catch (Exception fileEx)
            {
                _logger.LogError(fileEx, "Unable to get file information for troubleshooting");
            }
        }
        
        // Log specific validation error details based on exception type
        if (exception is JsonException jsonEx)
        {
            _logger.LogError("JSON Parsing Error - Line: {Line}, Position: {Position}", 
                jsonEx.LineNumber, jsonEx.BytePositionInLine);
            
            // Suggest common fixes for JSON errors
            _logger.LogWarning("Common JSON validation issues:");
            _logger.LogWarning("  - Check for missing commas between properties");
            _logger.LogWarning("  - Verify all strings are properly quoted");
            _logger.LogWarning("  - Ensure numeric values are not quoted (maxConcurrency should be a number)");
            _logger.LogWarning("  - Validate JSON structure with a JSON validator");
        }
        else if (exception is ArgumentException || exception is InvalidOperationException)
        {
            _logger.LogError("Configuration Value Error: {Message}", exception.Message);
            _logger.LogWarning("Check that all configuration values are within valid ranges:");
            _logger.LogWarning("  - maxConcurrency: 1-100");
            _logger.LogWarning("  - matchConfidenceThreshold: 0.0-1.0");
            _logger.LogWarning("  - renameConfidenceThreshold: 0.0-1.0");
        }
        
        _logger.LogInformation("Using default configuration values due to validation errors");
        _logger.LogInformation("To resolve: Fix the configuration file or delete it to regenerate defaults");
    }

    // IAppConfigService extended members for concurrent processing support

    /// <summary>
    /// Gets the maximum number of concurrent operations allowed.
    /// Reads from the legacy configuration with validation and fallback to default (1).
    /// </summary>
    public int MaxConcurrency 
    { 
        get 
        {
            var value = Config.MaxConcurrency;
            
            // Validate range 1-100 as per specification
            if (value < 1 || value > 100)
            {
                _logger.LogWarning("Invalid MaxConcurrency value {Value} in legacy configuration, using default (1)", value);
                return 1;
            }
            
            return value;
        }
    }

    /// <summary>
    /// Loads configuration using modern configuration result pattern.
    /// Delegates to legacy LoadConfigurationAsync method with enhanced maxConcurrency validation.
    /// Ensures fresh configuration load for each call.
    /// </summary>
    /// <returns>Configuration result with legacy config wrapped and validated.</returns>
    public async Task<Models.Configuration.ConfigurationResult> LoadConfiguration()
    {
        try
        {
            // Always load configuration fresh to ensure tests and hot-reload scenarios work correctly
            await LoadConfigurationAsync();
            
            // Perform maxConcurrency validation after loading configuration
            var validationErrors = new List<string>();
            var originalMaxConcurrency = Config.MaxConcurrency;
            var validatedMaxConcurrency = originalMaxConcurrency;
            
            // Validate MaxConcurrency range (1-100) with fallback to default (1)
            if (originalMaxConcurrency < 1 || originalMaxConcurrency > 100)
            {
                validationErrors.Add($"MaxConcurrency value {originalMaxConcurrency} is outside valid range (1-100). Using default value 1.");
                validatedMaxConcurrency = 1; // Fallback to default
                _logger.LogWarning("MaxConcurrency value {Value} is outside valid range, falling back to default (1)", originalMaxConcurrency);
            }
            
            // Apply fallback if validation found issues
            if (validatedMaxConcurrency != originalMaxConcurrency)
            {
                Config.MaxConcurrency = validatedMaxConcurrency;
                _logger.LogInformation("MaxConcurrency fallback applied from {Original} to default (1)", originalMaxConcurrency);
            }
            else
            {
                _logger.LogDebug("MaxConcurrency value {Value} is valid", validatedMaxConcurrency);
            }

            // Update tracking for hot-reload detection
            _lastKnownMaxConcurrency = validatedMaxConcurrency;
            
            // Create modern configuration with validated values
            var modernConfig = new Models.Configuration.Configuration
            {
                MaxConcurrency = validatedMaxConcurrency // Use validated MaxConcurrency
            };
            
            var result = Models.Configuration.ConfigurationResult.Success(modernConfig);
            // Populate metadata so consumers can apply fallback behavior consistently
            result.OriginalMaxConcurrency = originalMaxConcurrency;
            result.OriginalMaxConcurrencyOutOfRange = originalMaxConcurrency < 1 || originalMaxConcurrency > 100;
            result.WasMaxConcurrencyDefaulted = validatedMaxConcurrency == 1 && result.OriginalMaxConcurrencyOutOfRange;
            result.WasMaxConcurrencyClamped = false; // legacy path defaults directly to 1 when invalid
            result.WasLenientParse = false;
            result.WasReloadOperation = false;
            
            // Add validation warnings to result if any corrections were made
            if (validationErrors.Any())
            {
                result.Errors.AddRange(validationErrors);
                _logger.LogInformation("Configuration loaded with {WarningCount} validation corrections", validationErrors.Count);
            }
            
            LastConfigurationResult = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration via legacy service");
            
            // Enhanced validation error logging for modern configuration interface
            LogConfigurationValidationError("Modern Configuration Load", ex);
            
            var failure = Models.Configuration.ConfigurationResult.Failure($"Legacy configuration load failed: {ex.Message}");
            LastConfigurationResult = failure;
            return failure;
        }
    }

    /// <summary>
    /// Reloads configuration if changed, with specific detection for maxConcurrency changes.
    /// Enhanced legacy service now supports basic hot-reload detection for concurrent processing.
    /// </summary>
    /// <returns>True if configuration was reloaded, false if no changes detected.</returns>
    public async Task<bool> ReloadIfChanged()
    {
        const string configPath = DefaultConfigFileName;
        
        try
        {
            if (!File.Exists(configPath))
            {
                _logger.LogDebug("Configuration file '{ConfigPath}' not found during reload check", configPath);
                return false;
            }

            var currentWriteTime = File.GetLastWriteTimeUtc(configPath);
            
            // Check if file has been modified since last load
            if (currentWriteTime <= _lastConfigFileWriteTime)
            {
                _logger.LogTrace("Configuration file unchanged, skipping reload - Path: {ConfigPath}", configPath);
                return false;
            }

            _logger.LogInformation("Configuration file change detected - Path: {ConfigPath}, PreviousTime: {PreviousTime}, CurrentTime: {CurrentTime}",
                configPath, _lastConfigFileWriteTime, currentWriteTime);

            // Store previous MaxConcurrency before reloading
            var previousMaxConcurrency = Config.MaxConcurrency;

            // Reload the configuration
            await LoadConfigurationAsync(configPath);

            // Detect MaxConcurrency changes
            var newMaxConcurrency = Config.MaxConcurrency;
            var maxConcurrencyChanged = previousMaxConcurrency != newMaxConcurrency;

            if (maxConcurrencyChanged)
            {
                _logger.LogWarning("MaxConcurrency changed during hot-reload - Previous: {PreviousMaxConcurrency}, New: {NewMaxConcurrency}, Impact: Concurrent processing behavior will be updated",
                    previousMaxConcurrency, newMaxConcurrency);

                // Log specific guidance for MaxConcurrency changes
                if (newMaxConcurrency > previousMaxConcurrency)
                {
                    _logger.LogInformation("Increased concurrency detected - New MaxConcurrency ({NewValue}) is higher than previous ({PreviousValue}). This may improve processing throughput for bulk operations.",
                        newMaxConcurrency, previousMaxConcurrency);
                }
                else if (newMaxConcurrency < previousMaxConcurrency)
                {
                    _logger.LogInformation("Decreased concurrency detected - New MaxConcurrency ({NewValue}) is lower than previous ({PreviousValue}). This may reduce system resource usage but could slow bulk processing.",
                        newMaxConcurrency, previousMaxConcurrency);
                }
            }

            _logger.LogInformation("Legacy configuration hot-reload completed successfully - MaxConcurrencyChanged: {MaxConcurrencyChanged}, NewMaxConcurrency: {NewMaxConcurrency}",
                maxConcurrencyChanged, newMaxConcurrency);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during legacy configuration reload check - Path: {ConfigPath}", configPath);
            
            // Enhanced validation error logging for hot-reload failures
            LogConfigurationValidationError("Hot-Reload", ex, configPath);
            return false;
        }
    }

    /// <summary>
    /// Validates a modern configuration object.
    /// Legacy service provides basic validation for MaxConcurrency and other properties.
    /// </summary>
    /// <param name="config">Modern configuration to validate.</param>
    /// <returns>Validation result with errors if any found.</returns>
    public Models.Configuration.ConfigurationResult ValidateConfiguration(Models.Configuration.Configuration config)
    {
        if (config == null)
        {
            var nullError = "Configuration cannot be null";
            _logger.LogError("Configuration validation failed: {Error}", nullError);
            LogConfigurationValidationError("Null Configuration Validation", 
                new ArgumentNullException(nameof(config), nullError));
            return Models.Configuration.ConfigurationResult.Failure(nullError);
        }

        var errors = new List<string>();

        // Validate MaxConcurrency range with detailed logging
        if (config.MaxConcurrency < 1 || config.MaxConcurrency > 100)
        {
            var error = $"MaxConcurrency must be between 1 and 100, but was {config.MaxConcurrency}";
            errors.Add(error);
            _logger.LogWarning("Configuration validation error: {Error}", error);
            
            // Log suggested correction
            var suggestedValue = config.MaxConcurrency < 1 ? 1 : 100;
            _logger.LogInformation("Suggested correction: Set maxConcurrency to {SuggestedValue}", suggestedValue);
        }

        // Additional legacy validation could be added here for other properties
        // For now, MaxConcurrency is the main concern for concurrent processing

        if (errors.Any())
        {
            _logger.LogError("Configuration validation failed with {ErrorCount} error(s)", errors.Count);
            foreach (var error in errors)
            {
                _logger.LogError("  - {ValidationError}", error);
            }
            
            LogConfigurationValidationError("Configuration Property Validation", 
                new InvalidOperationException($"Validation failed with {errors.Count} error(s)"));
            
            return Models.Configuration.ConfigurationResult.Failure(errors);
        }

        _logger.LogDebug("Configuration validation passed successfully");
        return Models.Configuration.ConfigurationResult.Success(config);
    }
}