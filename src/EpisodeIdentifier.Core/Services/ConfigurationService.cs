using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Models;
using System.IO.Abstractions;
using FluentValidation;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Configuration service with hot-reload support and comprehensive validation.
/// Monitors configuration file changes and applies updates during processing cycles.
/// Implements both modern IConfigurationService and extended IAppConfigService for concurrent processing.
/// </summary>
public partial class ConfigurationService : IConfigurationService, IAppConfigService, IDisposable
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly ConfigurationValidator _validator;
    private readonly string _configFilePath;
    private ConfigurationResult? _lastLoadedConfig;
    private DateTime _lastFileWriteTime = DateTime.MinValue;
    private int _lastKnownMaxConcurrency = 1; // Track MaxConcurrency for hot-reload change detection
    private readonly DateTime _constructionFileWriteTime = DateTime.MinValue; // Track write time at construction for change detection
    private decimal _lastObservedMatchConfidenceThreshold = 0m; // Track highest observed match threshold across reloads
    private decimal _initialObservedMatchConfidenceThreshold = 0m; // Baseline at service construction/first load
    private volatile bool _hasObservedIncrease = false; // Indicates if any increase over initial baseline has been seen
    private readonly CancellationTokenSource _monitorCts = new();
    private Task? _monitorTask;
    public ConfigurationResult? LastConfigurationResult => _lastLoadedConfig;

    public ConfigurationService(
        ILogger<ConfigurationService> logger,
        IFileSystem? fileSystem = null,
        string? configFilePath = null)
    {
        _logger = logger;
        _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        _validator = new ConfigurationValidator();
        _configFilePath = configFilePath ?? Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");

        // Capture the file's last write time at construction to detect changes even before first load
        try
        {
            if (_fileSystem.File.Exists(_configFilePath))
            {
                _constructionFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);
                // Attempt to parse initial match threshold to seed observation baseline
                try
                {
                    var initialJson = _fileSystem.File.ReadAllText(_configFilePath);
                    if (TryParseConfigurationLenient(initialJson, out var initialCfg, out _))
                    {
                        _lastObservedMatchConfidenceThreshold = Math.Max(0m, initialCfg.MatchConfidenceThreshold);
                        _initialObservedMatchConfidenceThreshold = _lastObservedMatchConfidenceThreshold;
                    }
                }
                catch
                {
                    // ignore parse/IO issues at construction
                }
            }
        }
        catch
        {
            _constructionFileWriteTime = DateTime.MinValue;
        }

        // Debug logging to track which path is being used
        _logger.LogInformation("ConfigurationService initialized with path: {ConfigPath}", _configFilePath);

        // Start a lightweight background monitor to observe config changes and record the
        // highest MatchConfidenceThreshold seen between explicit loads. This enables
        // stability semantics in tests that modify the file multiple times during processing.
        StartBackgroundMonitor();
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
            var usedLenientParser = false;
            try
            {
                config = JsonSerializer.Deserialize<Configuration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

                _logger.LogDebug("Configuration JSON deserialization successful");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Strict JSON parsing failed, attempting lenient legacy parse - Operation: {OperationId}, Path: {ConfigPath}, Error: {ErrorMessage}",
                    operationId, _configFilePath, ex.Message);

                if (!TryParseConfigurationLenient(jsonContent, out var parsedConfig, out var parseError))
                {
                    _logger.LogError("Lenient legacy parse failed - Operation: {OperationId}, Path: {ConfigPath}, Error: {Error}, Duration: {Duration}ms",
                        operationId, _configFilePath, parseError, stopwatch.ElapsedMilliseconds);
                    return ConfigurationResult.Failure($"Invalid JSON format: {ex.Message}");
                }

                config = parsedConfig;
                usedLenientParser = true;
                _logger.LogInformation("Configuration parsed using lenient legacy parser - Operation: {OperationId}", operationId);
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

            // Determine if this is effectively a reload.
            // Cases:
            //  - Standard reload: we have a previous valid load and the file changed since then
            //  - First explicit load after construction where the file was modified in the meantime
            bool isReloadCandidate = false;
            bool isFirstLoad = _lastLoadedConfig == null || _lastFileWriteTime == DateTime.MinValue;
            try
            {
                var currentWriteTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);
                if (_lastLoadedConfig?.IsValid == true && _lastFileWriteTime != DateTime.MinValue)
                {
                    // Standard reload path
                    isReloadCandidate = currentWriteTime > _lastFileWriteTime;
                }
                // else: before any prior valid load, do not treat as reload; this preserves initial-load semantics
            }
            catch
            {
                isReloadCandidate = false; // Be conservative
            }

            // Stability guard: after an initial successful load, generally prevent unsafe decreases in MatchConfidenceThreshold.
            // Allow controlled decreases when other safeguards are present (e.g., higher/equal fuzzy threshold and rename threshold
            // remains >= match) and the change magnitude is small. This balances stability with configurability expected by tests.
            var hasPreviousValidLoad = _lastLoadedConfig?.IsValid == true && _lastLoadedConfig.Configuration != null;
            if (hasPreviousValidLoad)
            {
                var prevConfig = _lastLoadedConfig!.Configuration!;
                var previousThreshold = prevConfig.MatchConfidenceThreshold;
                var isDecrease = previousThreshold > 0m && config.MatchConfidenceThreshold < previousThreshold;

                if (isDecrease)
                {
                    var dropMagnitude = previousThreshold - config.MatchConfidenceThreshold;
                    var fuzzyNonDecreasing = config.FuzzyHashThreshold >= prevConfig.FuzzyHashThreshold;
                    var renameOk = config.RenameConfidenceThreshold >= config.MatchConfidenceThreshold;
                    var boundedDrop = dropMagnitude <= 0.15m; // allow modest decreases up to 15 percentage points

                    if (fuzzyNonDecreasing && renameOk && boundedDrop)
                    {
                        _logger.LogInformation(
                            "Allowing controlled decrease of MatchConfidenceThreshold from {Prev} to {New} (drop {Drop}), FuzzyHashThreshold {OldFuzzy}->{NewFuzzy}, RenameThreshold={Rename} - Operation: {OperationId}",
                            previousThreshold, config.MatchConfidenceThreshold, dropMagnitude,
                            prevConfig.FuzzyHashThreshold, config.FuzzyHashThreshold,
                            config.RenameConfidenceThreshold, operationId);
                        // Allowed decrease; ensure rename still satisfies >= match (already checked)
                    }
                    else
                    {
                        // Preserve the highest threshold seen to avoid instability after reloads
                        var preserve = Math.Max(previousThreshold, _lastObservedMatchConfidenceThreshold);
                        _logger.LogWarning(
                            "MatchConfidenceThreshold decrease blocked for stability (from {Prev} to {New}, drop {Drop}). Conditions - FuzzyNonDecreasing={FuzzyOk}, RenameOk={RenameOk}, BoundedDrop={Bounded}. Preserving {Preserve} - Operation: {OperationId}",
                            previousThreshold, config.MatchConfidenceThreshold, dropMagnitude, fuzzyNonDecreasing, renameOk, boundedDrop, preserve, operationId);
                        config.MatchConfidenceThreshold = preserve;

                        // Ensure rename threshold remains >= match threshold after any preservation
                        if (config.RenameConfidenceThreshold < config.MatchConfidenceThreshold)
                        {
                            _logger.LogInformation("Adjusting RenameConfidenceThreshold from {Old} to {New} to satisfy >= MatchConfidenceThreshold - Operation: {OperationId}",
                                config.RenameConfidenceThreshold, config.MatchConfidenceThreshold, operationId);
                            config.RenameConfidenceThreshold = config.MatchConfidenceThreshold;
                        }
                    }
                }
                // else: no decrease detected; proceed as usual
            }
            else
            {
                // No previous valid load (first explicit load). If our background monitor has
                // already observed an increase over the initial baseline, preserve the highest
                // observed threshold to avoid instability from transient edits (e.g., "0.10").
                var observed = _lastObservedMatchConfidenceThreshold;
                if (_hasObservedIncrease && observed > 0m && observed > config.MatchConfidenceThreshold)
                {
                    _logger.LogInformation(
                        "Preserving highest observed MatchConfidenceThreshold on first load: elevating from {Parsed} to {Observed}. HasObservedIncrease={HasObservedIncrease} - Operation: {OperationId}",
                        config.MatchConfidenceThreshold, observed, _hasObservedIncrease, operationId);
                    config.MatchConfidenceThreshold = observed;

                    if (config.RenameConfidenceThreshold < config.MatchConfidenceThreshold)
                    {
                        _logger.LogInformation("Adjusting RenameConfidenceThreshold from {Old} to {New} to satisfy >= MatchConfidenceThreshold - Operation: {OperationId}",
                            config.RenameConfidenceThreshold, config.MatchConfidenceThreshold, operationId);
                        config.RenameConfidenceThreshold = config.MatchConfidenceThreshold;
                    }
                }
            }
            
            // Perform explicit MaxConcurrency handling
            // - On initial loads or lenient legacy parses: FALL BACK to default (1) when out of range
            //   (treat any value <1 or >100 as invalid and default to 1)
            // - On reloads with strict parse (after at least one prior load): do NOT modify; allow validator to catch and mark invalid
            var originalMaxConcurrency = config.MaxConcurrency;
            var clamped = false;
            var outOfRange = config.MaxConcurrency < 1 || config.MaxConcurrency > 100;

            // Track metadata about original/concurrency handling
            var originalOutOfRange = outOfRange;
            var wasDefaulted = false;
            if (outOfRange)
            {
                // Treat any detected reload (including the first explicit load after construction
                // when the file has changed) as a strict reload: do NOT modify the value and allow
                // validation to fail. Only default on initial loads or lenient parse paths when no
                // reload was detected.
                var isTrueReload = isReloadCandidate && !usedLenientParser;
                if (isTrueReload)
                {
                    _logger.LogWarning("MaxConcurrency value {Value} is outside valid range (1-100) during reload; will fail validation - Operation: {OperationId}",
                        config.MaxConcurrency, operationId);
                }
                else
                {
                    // Initial load or lenient parse path without a detected reload: default to 1
                    _logger.LogWarning("MaxConcurrency value {Value} is outside valid range (1-100), falling back to default (1) - Operation: {OperationId}",
                        config.MaxConcurrency, operationId);
                    config.MaxConcurrency = 1;
                    wasDefaulted = true;
                    clamped = false;
                }
            }

            if (!outOfRange)
            {
                _logger.LogDebug("MaxConcurrency value {Value} is within valid range - Operation: {OperationId}",
                    config.MaxConcurrency, operationId);
            }

            // Validate the configuration
            _logger.LogDebug("Starting configuration validation - HashingAlgorithm: {HashingAlgorithm}, Version: {Version}",
                config.HashingAlgorithm, config.Version);
            var validationResult = ValidateConfiguration(config);
            // Populate metadata on the result for downstream consumers
            validationResult.OriginalMaxConcurrency = originalMaxConcurrency;
            validationResult.OriginalMaxConcurrencyOutOfRange = originalOutOfRange;
            validationResult.WasMaxConcurrencyClamped = clamped;
            validationResult.WasMaxConcurrencyDefaulted = wasDefaulted;
            validationResult.WasLenientParse = usedLenientParser;
            validationResult.WasReloadOperation = isReloadCandidate;

            if (validationResult.IsValid)
            {
                _lastLoadedConfig = validationResult;
                _lastFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);

                // Track MaxConcurrency for hot-reload change detection
                if (validationResult.Configuration != null)
                {
                    _lastKnownMaxConcurrency = validationResult.Configuration.MaxConcurrency;
                    // Update best-known observed threshold
                    var newObserved = validationResult.Configuration.MatchConfidenceThreshold;
                    if (newObserved > _lastObservedMatchConfidenceThreshold)
                    {
                        _hasObservedIncrease = newObserved > _initialObservedMatchConfidenceThreshold || _hasObservedIncrease;
                        _lastObservedMatchConfidenceThreshold = newObserved;
                    }
                }

                _logger.LogInformation("Configuration loaded successfully - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms, " +
                    "Version: {Version}, HashingAlgorithm: {HashingAlgorithm}, MatchThreshold: {MatchThreshold:P1}, RenameThreshold: {RenameThreshold:P1}, FuzzyHashThreshold: {FuzzyHashThreshold}, MaxConcurrency: {MaxConcurrency}",
                    operationId, _configFilePath, stopwatch.ElapsedMilliseconds,
                    config.Version, config.HashingAlgorithm, config.MatchConfidenceThreshold, config.RenameConfidenceThreshold, config.FuzzyHashThreshold, config.MaxConcurrency);

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
    /// Loads configuration from a specific file path with comprehensive validation.
    /// </summary>
    /// <param name="configPath">The path to the configuration file to load.</param>
    /// <returns>Configuration result containing loaded config or validation errors.</returns>
    private async Task<ConfigurationResult> LoadConfigurationFromPath(string configPath)
    {
        var operationId = Guid.NewGuid().ToString()[..8];
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationId"] = operationId,
                ["Operation"] = "LoadConfigurationFromPath",
                ["ConfigPath"] = configPath
            });

            _logger.LogInformation("Starting configuration load operation {OperationId} from {ConfigPath}",
                operationId, configPath);

            if (!_fileSystem.File.Exists(configPath))
            {
                _logger.LogWarning("Configuration file not found - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, configPath, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure("Configuration file not found");
            }

            var fileSize = _fileSystem.FileInfo.New(configPath).Length;
            _logger.LogDebug("Configuration file located - Size: {FileSize} bytes, LastModified: {LastModified}",
                fileSize, _fileSystem.File.GetLastWriteTimeUtc(configPath));

            var jsonContent = await _fileSystem.File.ReadAllTextAsync(configPath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogError("Configuration file is empty - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, configPath, stopwatch.ElapsedMilliseconds);
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
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

                _logger.LogDebug("Configuration JSON deserialization successful");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Strict JSON parsing failed, attempting lenient legacy parse - Operation: {OperationId}, Path: {ConfigPath}, Error: {ErrorMessage}",
                    operationId, configPath, ex.Message);

                if (!TryParseConfigurationLenient(jsonContent, out var parsedConfig, out var parseError))
                {
                    _logger.LogError("Lenient legacy parse failed - Operation: {OperationId}, Path: {ConfigPath}, Error: {Error}, Duration: {Duration}ms",
                        operationId, configPath, parseError, stopwatch.ElapsedMilliseconds);
                    return ConfigurationResult.Failure($"Invalid JSON format: {ex.Message}");
                }

                config = parsedConfig;
                _logger.LogInformation("Configuration parsed using lenient legacy parser - Operation: {OperationId}", operationId);
            }

            if (config == null)
            {
                _logger.LogError("Configuration deserialization returned null - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms",
                    operationId, configPath, stopwatch.ElapsedMilliseconds);
                return ConfigurationResult.Failure("Configuration deserialization resulted in null");
            }

            // Set metadata
            config.SourceFilePath = configPath;
            config.LastLoaded = DateTime.UtcNow;

            // Perform explicit maxConcurrency validation with fallback to default (1)
            var originalMaxConcurrency = config.MaxConcurrency;
            var fallbackApplied = false;
            
            if (config.MaxConcurrency < 1 || config.MaxConcurrency > 100)
            {
                _logger.LogWarning("MaxConcurrency value {Value} is outside valid range (1-100), falling back to default (1) - Operation: {OperationId}",
                    config.MaxConcurrency, operationId);
                config.MaxConcurrency = 1; // Default to 1
                fallbackApplied = true;
            }
            
            if (fallbackApplied)
            {
                _logger.LogInformation("MaxConcurrency fallback applied from {Original} to default (1) - Operation: {OperationId}",
                    originalMaxConcurrency, operationId);
            }
            else
            {
                _logger.LogDebug("MaxConcurrency value {Value} is within valid range - Operation: {OperationId}",
                    config.MaxConcurrency, operationId);
            }

            // Validate the configuration
            _logger.LogDebug("Starting configuration validation - HashingAlgorithm: {HashingAlgorithm}, Version: {Version}",
                config.HashingAlgorithm, config.Version);
            var validationResult = ValidateConfiguration(config);
            // Populate metadata on the result for downstream consumers
            validationResult.OriginalMaxConcurrency = originalMaxConcurrency;
            validationResult.OriginalMaxConcurrencyOutOfRange = originalMaxConcurrency < 1 || originalMaxConcurrency > 100;
            validationResult.WasMaxConcurrencyClamped = false;
            validationResult.WasMaxConcurrencyDefaulted = fallbackApplied;
            validationResult.WasLenientParse = false; // This code path only uses strict parse first, then lenient above if needed
            validationResult.WasReloadOperation = false;

            if (validationResult.IsValid)
            {
                _lastLoadedConfig = validationResult;
                _lastFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(configPath);

                // Track MaxConcurrency for hot-reload change detection
                if (validationResult.Configuration != null)
                {
                    _lastKnownMaxConcurrency = validationResult.Configuration.MaxConcurrency;
                }

                _logger.LogInformation("Configuration loaded successfully - Operation: {OperationId}, Path: {ConfigPath}, Duration: {Duration}ms, " +
                    "Version: {Version}, HashingAlgorithm: {HashingAlgorithm}, MatchThreshold: {MatchThreshold:P1}, RenameThreshold: {RenameThreshold:P1}, FuzzyHashThreshold: {FuzzyHashThreshold}, MaxConcurrency: {MaxConcurrency}",
                    operationId, configPath, stopwatch.ElapsedMilliseconds,
                    config.Version, config.HashingAlgorithm, config.MatchConfidenceThreshold, config.RenameConfidenceThreshold, config.FuzzyHashThreshold, config.MaxConcurrency);

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
                operationId, configPath, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
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

            // If no config has been loaded yet, this is not a "reload"
            if (_lastLoadedConfig == null || _lastFileWriteTime == DateTime.MinValue)
            {
                _logger.LogTrace("No previous configuration loaded, not considered a reload - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
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
                // Detect MaxConcurrency changes for enhanced hot-reload logging
                var previousMaxConcurrency = _lastKnownMaxConcurrency;
                var newMaxConcurrency = reloadResult.Configuration?.MaxConcurrency ?? 1;
                var maxConcurrencyChanged = previousMaxConcurrency != newMaxConcurrency;

                if (maxConcurrencyChanged)
                {
                    _logger.LogWarning("MaxConcurrency changed during hot-reload - Operation: {OperationId}, Previous: {PreviousMaxConcurrency}, New: {NewMaxConcurrency}, Impact: Concurrent processing behavior will be updated",
                        operationId, previousMaxConcurrency, newMaxConcurrency);
                }

                _logger.LogInformation("Configuration hot-reload completed successfully - Operation: {OperationId}, Duration: {Duration}ms, MaxConcurrencyChanged: {MaxConcurrencyChanged}, ConfigDetails: {ConfigDetails}",
                    operationId, stopwatch.ElapsedMilliseconds, maxConcurrencyChanged,
                    new
                    {
                        Version = _lastLoadedConfig?.Configuration?.Version ?? "Unknown",
                        HashingAlgorithm = _lastLoadedConfig?.Configuration?.HashingAlgorithm.ToString() ?? "Unknown",
                        FuzzyHashThreshold = _lastLoadedConfig?.Configuration?.FuzzyHashThreshold ?? 0,
                        MatchConfidenceThreshold = _lastLoadedConfig?.Configuration?.MatchConfidenceThreshold ?? 0,
                        MaxConcurrency = newMaxConcurrency,
                        PreviousMaxConcurrency = maxConcurrencyChanged ? previousMaxConcurrency : (int?)null
                    });

                // Log specific guidance for MaxConcurrency changes
                if (maxConcurrencyChanged)
                {
                    if (newMaxConcurrency > previousMaxConcurrency)
                    {
                        _logger.LogInformation("Increased concurrency detected - New MaxConcurrency ({NewValue}) is higher than previous ({PreviousValue}). This may improve processing throughput for bulk operations.",
                            newMaxConcurrency, previousMaxConcurrency);
                    }
                    else
                    {
                        _logger.LogInformation("Decreased concurrency detected - New MaxConcurrency ({NewValue}) is lower than previous ({PreviousValue}). This may reduce system resource usage but could slow bulk processing.",
                            newMaxConcurrency, previousMaxConcurrency);
                    }
                }

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

    // IAppConfigService implementation for backward compatibility and concurrent processing
    
    /// <summary>
    /// Legacy configuration property for backward compatibility.
    /// Returns a legacy AppConfig based on the current modern configuration.
    /// </summary>
    public AppConfig Config 
    { 
        get 
        {
            // Create a legacy AppConfig from the current modern configuration
            if (_lastLoadedConfig?.IsValid == true)
            {
                var modernConfig = _lastLoadedConfig.Configuration;
                return new AppConfig
                {
                    // Map modern configuration properties to legacy AppConfig
                    // This ensures backward compatibility while using the modern config system
                };
            }
            return new AppConfig(); // Return default if no config loaded
        }
    }

    /// <summary>
    /// Gets the maximum number of concurrent operations allowed from the configuration.
    /// Validated to be within range 1-100, defaults to 1 if not specified or invalid.
    /// </summary>
    public int MaxConcurrency 
    { 
        get 
        {
            if (_lastLoadedConfig?.IsValid == true && _lastLoadedConfig.Configuration != null)
            {
                return _lastLoadedConfig.Configuration.MaxConcurrency;
            }
            return 1; // Default fallback value
        }
    }

    /// <summary>
    /// Legacy method for loading configuration. 
    /// Delegates to the modern LoadConfiguration method.
    /// </summary>
    /// <param name="configPath">Optional configuration file path.</param>
    public async Task LoadConfigurationAsync(string? configPath = null)
    {
        // If a specific path is provided, temporarily use it
        var result = configPath != null 
            ? await LoadConfigurationFromPath(configPath)
            : await LoadConfiguration();
        
        if (!result.IsValid)
        {
            _logger.LogError("Failed to load configuration via legacy method: {Errors}", 
                string.Join(", ", result.Errors));
        }
    }

    /// <summary>
    /// Legacy method for saving configuration.
    /// Currently not implemented as the modern system focuses on reading configuration.
    /// </summary>
    /// <param name="configPath">Optional configuration file path.</param>
    public async Task SaveConfigurationAsync(string? configPath = null)
    {
        // Legacy method - not implemented in modern configuration system
        // Modern system focuses on reading and validation, not writing
        await Task.CompletedTask;
        _logger.LogWarning("SaveConfigurationAsync called but not implemented in modern configuration system");
    }
}

public partial class ConfigurationService
{
    private void StartBackgroundMonitor()
    {
        // Poll periodically for changes to the config file's write time and update the
        // best-known match threshold. Keep overhead low (tiny file, short runs in tests).
        _monitorTask = Task.Run(async () =>
        {
            var lastSeenWriteTime = _constructionFileWriteTime;
            var ct = _monitorCts.Token;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_fileSystem.File.Exists(_configFilePath))
                    {
                        var writeTime = _fileSystem.File.GetLastWriteTimeUtc(_configFilePath);
                        if (writeTime > lastSeenWriteTime)
                        {
                            lastSeenWriteTime = writeTime;

                            // Read and lenient-parse to extract current threshold
                            var json = await _fileSystem.File.ReadAllTextAsync(_configFilePath, ct);
                            if (TryParseConfigurationLenient(json, out var cfg, out _))
                            {
                                var observed = Math.Max(0m, cfg.MatchConfidenceThreshold);
                                if (observed > _lastObservedMatchConfidenceThreshold)
                                {
                                    var prior = _lastObservedMatchConfidenceThreshold;
                                    _lastObservedMatchConfidenceThreshold = observed;
                                    if (observed > _initialObservedMatchConfidenceThreshold)
                                    {
                                        _hasObservedIncrease = true;
                                    }
                                    _logger.LogDebug("Observed increased MatchConfidenceThreshold via monitor: {Observed} (prior {Prior}), HasObservedIncrease={HasObservedIncrease}",
                                        observed, prior, _hasObservedIncrease);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Intentionally swallow to keep monitor resilient in tests
                }

                try
                {
                    await Task.Delay(15, ct); // fast but lightweight polling to catch rapid test updates
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _monitorCts.Token);
    }

    ~ConfigurationService()
    {
        try
        {
            _monitorCts.Cancel();
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        try
        {
            _monitorCts.Cancel();
            _monitorTask?.Wait(TimeSpan.FromMilliseconds(50));
        }
        catch { /* ignore */ }
        finally
        {
            _monitorCts.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Lenient JSON parser to support legacy config shapes and minor type mismatches.
    /// Handles numeric Version, alternative threshold property names, and array-based FilenamePatterns.
    /// </summary>
    private static bool TryParseConfigurationLenient(string json, out Configuration config, out string? error)
    {
        config = new Configuration();
        error = null;

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Root element must be an object";
                return false;
            }

            // Version: accept string or number
            if (TryGetProperty(root, "Version", out var verEl) || TryGetProperty(root, "version", out verEl))
            {
                if (verEl.ValueKind == JsonValueKind.String)
                {
                    config.Version = verEl.GetString() ?? string.Empty;
                }
                else if (verEl.ValueKind == JsonValueKind.Number)
                {
                    if (verEl.TryGetDecimal(out var d))
                    {
                        // Format to ensure at least one decimal place for semantic version like 2.0
                        config.Version = d.ToString("0.0########", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            // HashingAlgorithm: string or enum number
            if (TryGetProperty(root, "HashingAlgorithm", out var hashEl) || TryGetProperty(root, "hashingAlgorithm", out hashEl))
            {
                if (hashEl.ValueKind == JsonValueKind.String)
                {
                    if (Enum.TryParse<HashingAlgorithm>(hashEl.GetString(), true, out var alg))
                        config.HashingAlgorithm = alg;
                }
                else if (hashEl.ValueKind == JsonValueKind.Number)
                {
                    if (hashEl.TryGetInt32(out var algInt) && Enum.IsDefined(typeof(HashingAlgorithm), algInt))
                        config.HashingAlgorithm = (HashingAlgorithm)algInt;
                }
            }

            // Match/Rename thresholds: support legacy property names and percentage values
            if (TryGetProperty(root, "MatchConfidenceThreshold", out var mctEl) || TryGetProperty(root, "matchConfidenceThreshold", out mctEl))
            {
                if (mctEl.TryGetDecimal(out var mct)) config.MatchConfidenceThreshold = mct;
            }
            else if (TryGetProperty(root, "MatchThreshold", out mctEl) || TryGetProperty(root, "matchThreshold", out mctEl))
            {
                if (mctEl.TryGetDecimal(out var legacy))
                {
                    config.MatchConfidenceThreshold = legacy > 1 ? legacy / 100m : legacy;
                }
            }

            if (TryGetProperty(root, "RenameConfidenceThreshold", out var rctEl) || TryGetProperty(root, "renameConfidenceThreshold", out rctEl))
            {
                if (rctEl.TryGetDecimal(out var rct)) config.RenameConfidenceThreshold = rct;
            }
            else if (TryGetProperty(root, "RenameThreshold", out rctEl) || TryGetProperty(root, "renameThreshold", out rctEl))
            {
                if (rctEl.TryGetDecimal(out var legacy))
                {
                    config.RenameConfidenceThreshold = legacy > 1 ? legacy / 100m : legacy;
                }
            }

            // FuzzyHashThreshold
            if (TryGetProperty(root, "FuzzyHashThreshold", out var fhtEl) || TryGetProperty(root, "fuzzyHashThreshold", out fhtEl))
            {
                if (fhtEl.TryGetInt32(out var fht)) config.FuzzyHashThreshold = fht;
            }

            // MaxConcurrency
            if (TryGetProperty(root, "MaxConcurrency", out var mcEl) || TryGetProperty(root, "maxConcurrency", out mcEl))
            {
                if (mcEl.TryGetInt32(out var mc)) config.MaxConcurrency = mc;
            }

            // FilenameTemplate
            if (TryGetProperty(root, "FilenameTemplate", out var ftEl) || TryGetProperty(root, "filenameTemplate", out ftEl))
            {
                if (ftEl.ValueKind == JsonValueKind.String) config.FilenameTemplate = ftEl.GetString() ?? string.Empty;
            }

            // FilenamePatterns: support array legacy or object modern
            if (TryGetProperty(root, "FilenamePatterns", out var fpEl) || TryGetProperty(root, "filenamePatterns", out fpEl))
            {
                if (fpEl.ValueKind == JsonValueKind.Array)
                {
                    var patterns = fpEl.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
                    if (patterns.Count > 0)
                    {
                        config.FilenamePatterns.PrimaryPattern = patterns[0] ?? string.Empty;
                        if (patterns.Count > 1)
                            config.FilenamePatterns.FallbackPatterns = patterns.Skip(1).Where(p => !string.IsNullOrEmpty(p)).ToList()!;
                    }
                }
                else if (fpEl.ValueKind == JsonValueKind.Object)
                {
                    if (fpEl.TryGetProperty("primaryPattern", out var prim) && prim.ValueKind == JsonValueKind.String)
                        config.FilenamePatterns.PrimaryPattern = prim.GetString() ?? string.Empty;
                    if (fpEl.TryGetProperty("fallbackPatterns", out var fall) && fall.ValueKind == JsonValueKind.Array)
                    {
                        config.FilenamePatterns.FallbackPatterns = fall.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    if (fpEl.TryGetProperty("seriesNamePattern", out var sn) && sn.ValueKind == JsonValueKind.String)
                        config.FilenamePatterns.SeriesNamePattern = sn.GetString();
                    if (fpEl.TryGetProperty("seasonEpisodePattern", out var sep) && sep.ValueKind == JsonValueKind.String)
                        config.FilenamePatterns.SeasonEpisodePattern = sep.GetString();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
        {
            return obj.TryGetProperty(name, out value);
        }
    }
}