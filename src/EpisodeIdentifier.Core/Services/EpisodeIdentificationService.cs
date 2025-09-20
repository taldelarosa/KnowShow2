using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.IO.Abstractions;
using EpisodeIdentifier.Core.Services.Hashing;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service that orchestrates episode identification using either legacy fuzzy hashing or CTPH fuzzy hashing
/// based on configuration settings. Provides a unified interface for episode identification with automatic
/// fallback to legacy systems when fuzzy hashing configuration is not available or invalid.
/// </summary>
public class EpisodeIdentificationService : IEpisodeIdentificationService
{
    private readonly ILogger<EpisodeIdentificationService> _logger;
    private readonly ISubtitleMatcher_legacyMatcher;
    private readonly CTPhHashingService _ctphHashingService;
    private readonly IFileSystem_fileSystem;
    private readonly EnhancedCTPhHashingService? _enhancedCtphService;

    public EpisodeIdentificationService(
        ILogger<EpisodeIdentificationService> logger,
        ISubtitleMatcher legacyMatcher,
        IFileSystem? fileSystem = null,
        EnhancedCTPhHashingService? enhancedCtphService = null)
    {
        _logger = logger;
        _legacyMatcher = legacyMatcher;
        _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        _enhancedCtphService = enhancedCtphService;
        _ctphHashingService = new CTPhHashingService(
            _logger as ILogger<CTPhHashingService> ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CTPhHashingService>.Instance,
            _fileSystem);
    }

    /// <summary>
    /// Identifies an episode using the most appropriate hashing method based on configuration.
    /// Uses CTPH fuzzy hashing if enabled and configured, otherwise falls back to legacy hashing.
    /// </summary>
    /// <param name="subtitleText">The subtitle text content to identify</param>
    /// <param name="sourceFilePath">Optional path to the source file (used for CTPH hashing)</param>
    /// <param name="minConfidence">Optional minimum confidence threshold</param>
    /// <returns>Episode identification result</returns>
    public async Task<IdentificationResult> IdentifyEpisodeAsync(
        string subtitleText,
        string? sourceFilePath = null,
        double? minConfidence = null)
    {
        var operationId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "EpisodeIdentification",
            ["OperationId"] = operationId,
            ["SourceFilePath"] = sourceFilePath ?? "none",
            ["MinConfidence"] = minConfidence ?? 0.0,
            ["SubtitleTextLength"] = subtitleText?.Length ?? 0
        });

        if (string.IsNullOrWhiteSpace(subtitleText))
        {
            stopwatch.Stop();
            _logger.LogWarning("Episode identification failed - empty subtitle text - Operation: {OperationId}, Duration: {Duration}ms",
                operationId, stopwatch.ElapsedMilliseconds);
            return CreateFailureResult("Subtitle text cannot be null or empty");
        }

        try
        {
            _logger.LogInformation("Starting episode identification - Operation: {OperationId}, SubtitleLength: {SubtitleLength}, SourceFile: {SourceFilePath}, MinConfidence: {MinConfidence}",
                operationId, subtitleText.Length, sourceFilePath ?? "none", minConfidence ?? 0.0);

            // Check if fuzzy hashing (CTPH) is enabled and available
            var configCheckTime = stopwatch.ElapsedMilliseconds;
            var fuzzyConfig = await Program.GetFuzzyHashConfigurationAsync();
            var isFuzzyHashingEnabled = await Program.IsFuzzyHashingEnabledAsync();
            var configCheckDuration = stopwatch.ElapsedMilliseconds - configCheckTime;

            if (isFuzzyHashingEnabled && fuzzyConfig != null)
            {
                _logger.LogDebug("CTPH fuzzy hashing enabled, attempting fuzzy hash identification - Operation: {OperationId}, ConfigCheckTime: {ConfigCheckTime}ms",
                    operationId, configCheckDuration);

                var fuzzyStartTime = stopwatch.ElapsedMilliseconds;
                var fuzzyResult = await TryFuzzyHashIdentification(subtitleText, sourceFilePath, fuzzyConfig, minConfidence, operationId);
                var fuzzyDuration = stopwatch.ElapsedMilliseconds - fuzzyStartTime;

                if (fuzzyResult != null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Episode identified using CTPH fuzzy hashing - Operation: {OperationId}, Series: {Series}, Season: {Season}, Episode: {Episode}, Confidence: {Confidence:P1}, FuzzyTime: {FuzzyTime}ms, TotalTime: {Duration}ms",
                        operationId, fuzzyResult.Series, fuzzyResult.Season, fuzzyResult.Episode, fuzzyResult.MatchConfidence, fuzzyDuration, stopwatch.ElapsedMilliseconds);
                    
                    // Set matching method information
                    fuzzyResult.MatchingMethod = fuzzyResult.UsedTextFallback ? "TextFallback" : "CTPH";
                    
                    return fuzzyResult;
                }

                _logger.LogDebug("CTPH fuzzy hashing did not produce a match, falling back to legacy identification - Operation: {OperationId}, FuzzyTime: {FuzzyTime}ms",
                    operationId, fuzzyDuration);
            }
            else
            {
                _logger.LogDebug("CTPH fuzzy hashing not enabled or configured, using legacy identification - Operation: {OperationId}, ConfigCheckTime: {ConfigCheckTime}ms",
                    operationId, configCheckDuration);
            }

            // Fall back to legacy subtitle matcher
            _logger.LogDebug("Using legacy fuzzy hash identification - Operation: {OperationId}", operationId);
            var legacyStartTime = stopwatch.ElapsedMilliseconds;
            var legacyResult = await _legacyMatcher.IdentifyEpisode(subtitleText, minConfidence);
            var legacyDuration = stopwatch.ElapsedMilliseconds - legacyStartTime;
            stopwatch.Stop();

            if (legacyResult.MatchConfidence > 0)
            {
                _logger.LogInformation("Episode identified using legacy hashing - Operation: {OperationId}, Series: {Series}, Season: {Season}, Episode: {Episode}, Confidence: {Confidence:P1}, LegacyTime: {LegacyTime}ms, TotalTime: {Duration}ms",
                    operationId, legacyResult.Series, legacyResult.Season, legacyResult.Episode, legacyResult.MatchConfidence, legacyDuration, stopwatch.ElapsedMilliseconds);
                
                // Set matching method information for legacy matches
                legacyResult.MatchingMethod = "Legacy";
                legacyResult.UsedTextFallback = false;
            }
            else
            {
                _logger.LogInformation("No episode match found using any available hashing method - Operation: {OperationId}, LegacyTime: {LegacyTime}ms, TotalTime: {Duration}ms",
                    operationId, legacyDuration, stopwatch.ElapsedMilliseconds);
            }

            return legacyResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during episode identification - Operation: {OperationId}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return CreateFailureResult($"Identification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts episode identification using enhanced CTPH fuzzy hashing with text fallback.
    /// Returns null if fuzzy hashing is not applicable or fails.
    /// </summary>
    private async Task<IdentificationResult?> TryFuzzyHashIdentification(
        string subtitleText,
        string? sourceFilePath,
        Configuration fuzzyConfig,
        double? minConfidence,
        Guid operationId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "EnhancedCTPhIdentification",
            ["ParentOperationId"] = operationId,
            ["SourceFilePath"] = sourceFilePath ?? "none"
        });

        try
        {
            // Check if enhanced service is available
            if (_enhancedCtphService == null)
            {
                stopwatch.Stop();
                _logger.LogDebug("Enhanced CTPH service not available, skipping fuzzy hash identification - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                return null;
            }

            // Use enhanced service with text fallback - it works directly with subtitle text
            var comparisonResult = await _enhancedCtphService.CompareSubtitleWithFallback(subtitleText, enableTextFallback: true);
            stopwatch.Stop();

            if (comparisonResult.IsSuccess && comparisonResult.IsMatch)
            {
                var confidence = comparisonResult.UsedTextFallback ? 
                    (comparisonResult.TextSimilarityScore / 100.0) : 
                    (comparisonResult.HashSimilarityScore / 100.0);

                // Check against minimum confidence threshold from config
                var configuredThreshold = (double)fuzzyConfig.MatchConfidenceThreshold;
                if (minConfidence.HasValue)
                    configuredThreshold = Math.Max(configuredThreshold, minConfidence.Value);

                if (confidence >= configuredThreshold)
                {
                    _logger.LogInformation("Enhanced CTPH match found - Operation: {OperationId}, Series: {Series} S{Season}E{Episode}, Method: {Method}, HashScore: {HashScore}%, TextScore: {TextScore}%, FinalConfidence: {Confidence:P2}, Duration: {Duration}ms",
                        operationId, comparisonResult.MatchedSeries, comparisonResult.MatchedSeason, comparisonResult.MatchedEpisode,
                        comparisonResult.UsedTextFallback ? "CTPH+TextFallback" : "CTPH",
                        comparisonResult.HashSimilarityScore, comparisonResult.TextSimilarityScore, confidence, stopwatch.ElapsedMilliseconds);

                    return new IdentificationResult
                    {
                        Series = comparisonResult.MatchedSeries ?? "Unknown",
                        Season = comparisonResult.MatchedSeason ?? "0",
                        Episode = comparisonResult.MatchedEpisode ?? "0",
                        EpisodeName = comparisonResult.MatchedEpisodeName,
                        MatchConfidence = confidence,
                        MatchingMethod = comparisonResult.UsedTextFallback ? "CTPH+TextFallback" : "CTPH",
                        UsedTextFallback = comparisonResult.UsedTextFallback,
                        HashSimilarityScore = comparisonResult.HashSimilarityScore,
                        TextSimilarityScore = comparisonResult.TextSimilarityScore
                    };
                }
                else
                {
                    _logger.LogDebug("Enhanced CTPH match found but below confidence threshold - Operation: {OperationId}, Confidence: {Confidence:P2}, Threshold: {Threshold:P2}, Duration: {Duration}ms",
                        operationId, confidence, configuredThreshold, stopwatch.ElapsedMilliseconds);
                }
            }
            else if (!comparisonResult.IsSuccess)
            {
                _logger.LogWarning("Enhanced CTPH comparison failed - Operation: {OperationId}, Error: {Error}, Duration: {Duration}ms",
                    operationId, comparisonResult.ErrorMessage, stopwatch.ElapsedMilliseconds);
            }

            _logger.LogDebug("No enhanced CTPH match found - Operation: {OperationId}, Duration: {Duration}ms",
                operationId, stopwatch.ElapsedMilliseconds);

            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during enhanced CTPH identification - Operation: {OperationId}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Finds the best matching episode using CTPH fuzzy hashing.
    /// This is a placeholder implementation that can be extended with actual database integration.
    /// </summary>
    private Task<FuzzyHashMatch?> FindBestFuzzyHashMatch(
        string sourceHash,
        Configuration fuzzyConfig,
        double? minConfidence,
        Guid operationId)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "CTPhHashMatching",
            ["ParentOperationId"] = operationId,
            ["SourceHash"] = sourceHash
        });

        try
        {
            // TODO: This should integrate with a database of known episode CTPH hashes
            // For now, this is a framework that demonstrates the integration pattern

            var threshold = minConfidence ?? (double)fuzzyConfig.MatchConfidenceThreshold;
            var fuzzyThreshold = fuzzyConfig.FuzzyHashThreshold;

            _logger.LogDebug("Searching for CTPH hash matches - Operation: {OperationId}, Hash: {SourceHash}, Threshold: {Threshold:P1}, FuzzyThreshold: {FuzzyThreshold}",
                operationId, sourceHash, threshold, fuzzyThreshold);

            // In a real implementation, this would:
            // 1. Query a database of stored episode CTPH hashes
            // 2. Compare the source hash against all stored hashes using CTPH comparison
            // 3. Return matches above the fuzzy threshold
            // 4. Apply confidence scoring based on similarity percentages

            // Placeholder: Return null to indicate no fuzzy hash database is available yet
            _logger.LogDebug("CTPH hash database integration not yet implemented, no matches found - Operation: {OperationId}", operationId);
            return Task.FromResult<FuzzyHashMatch?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for fuzzy hash matches - Operation: {OperationId}, Hash: {SourceHash}, ExceptionType: {ExceptionType}",
                operationId, sourceHash, ex.GetType().Name);
            return Task.FromResult<FuzzyHashMatch?>(null);
        }
    }

    /// <summary>
    /// Creates a failure result for episode identification.
    /// </summary>
    private static IdentificationResult CreateFailureResult(string errorMessage)
    {
        return new IdentificationResult
        {
            MatchConfidence = 0,
            Series = null,
            Season = null,
            Episode = null,
            Error = new IdentificationError
            {
                Code = "IDENTIFICATION_FAILED",
                Message = errorMessage
            }
        };
    }

    /// <summary>
    /// Disposes of resources used by the service.
    /// </summary>
    public void Dispose()
    {
        // CTPhHashingService doesn't implement IDisposable, so nothing to dispose
    }
}

/// <summary>
/// Result of a fuzzy hash match operation.
/// </summary>
public class FuzzyHashMatch
{
    public string Series { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Episode { get; set; } = string.Empty;
    public string? EpisodeName { get; set; }
    public double Confidence { get; set; }
    public int SimilarityScore { get; set; }
    public string Hash { get; set; } = string.Empty;
}
