using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.IO.Abstractions;
using EpisodeIdentifier.Core.Services.Hashing;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service that orchestrates episode identification using CTPH fuzzy hashing.
/// Provides a unified interface for episode identification with text fallback capabilities.
/// </summary>
public class EpisodeIdentificationService : IEpisodeIdentificationService
{
    private readonly ILogger<EpisodeIdentificationService> _logger;
    private readonly CTPhHashingService _ctphHashingService;
    private readonly IFileSystem _fileSystem;
    private readonly EnhancedCTPhHashingService? _enhancedCtphService;
    private readonly IEmbeddingService? _embeddingService;
    private readonly IVectorSearchService? _vectorSearchService;

    public EpisodeIdentificationService(
        ILogger<EpisodeIdentificationService> logger,
        IFileSystem? fileSystem = null,
        EnhancedCTPhHashingService? enhancedCtphService = null,
        IEmbeddingService? embeddingService = null,
        IVectorSearchService? vectorSearchService = null)
    {
        _logger = logger;
        _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        _enhancedCtphService = enhancedCtphService;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
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
    /// <param name="subtitleType">The type of subtitle being processed (affects threshold selection)</param>
    /// <param name="sourceFilePath">Optional path to the source file (used for CTPH hashing)</param>
    /// <param name="minConfidence">Optional minimum confidence threshold</param>
    /// <param name="seriesFilter">Optional series name to filter results (case-insensitive)</param>
    /// <param name="seasonFilter">Optional season number to filter results (requires seriesFilter)</param>
    /// <returns>Episode identification result</returns>
    public async Task<IdentificationResult> IdentifyEpisodeAsync(
        string subtitleText,
        SubtitleType subtitleType = SubtitleType.TextBased,
        string? sourceFilePath = null,
        double? minConfidence = null,
        string? seriesFilter = null,
        int? seasonFilter = null)
    {
        var operationId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "EpisodeIdentification",
            ["OperationId"] = operationId,
            ["SourceFilePath"] = sourceFilePath ?? "none",
            ["SubtitleType"] = subtitleType.ToString(),
            ["MinConfidence"] = minConfidence ?? 0.0,
            ["SubtitleTextLength"] = subtitleText?.Length ?? 0,
            ["SeriesFilter"] = seriesFilter ?? "none",
            ["SeasonFilter"] = seasonFilter?.ToString() ?? "none"
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

            // Load configuration to determine matching strategy
            var configCheckTime = stopwatch.ElapsedMilliseconds;
            var fuzzyConfig = await Program.GetFuzzyHashConfigurationAsync();
            var configCheckDuration = stopwatch.ElapsedMilliseconds - configCheckTime;

            if (fuzzyConfig == null)
            {
                stopwatch.Stop();
                _logger.LogError("Configuration not available - Operation: {OperationId}", operationId);
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "CONFIGURATION_ERROR",
                        Message = "Configuration is not available"
                    }
                };
            }

            // Determine matching strategy (default to embedding)
            var strategy = fuzzyConfig.MatchingStrategy?.ToLower() ?? "embedding";
            _logger.LogDebug("Using matching strategy: {Strategy} - Operation: {OperationId}", strategy, operationId);

            IdentificationResult? result = null;

            // Try matching based on strategy
            switch (strategy)
            {
                case "embedding":
                    // Embedding-only strategy
                    result = await TryEmbeddingIdentification(subtitleText, subtitleType, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    break;

                case "fuzzy":
                    // Fuzzy-only strategy (legacy)
                    result = await TryFuzzyHashIdentification(subtitleText, subtitleType, sourceFilePath, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    break;

                case "hybrid":
                    // Try embedding first, fallback to fuzzy
                    result = await TryEmbeddingIdentification(subtitleText, subtitleType, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    if (result == null)
                    {
                        _logger.LogDebug("Embedding matching failed, falling back to fuzzy hashing - Operation: {OperationId}", operationId);
                        result = await TryFuzzyHashIdentification(subtitleText, subtitleType, sourceFilePath, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown matching strategy '{Strategy}', defaulting to embedding - Operation: {OperationId}", strategy, operationId);
                    result = await TryEmbeddingIdentification(subtitleText, subtitleType, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    break;
            }

            if (result != null)
            {
                stopwatch.Stop();
                _logger.LogInformation("Episode identified using {Method} - Operation: {OperationId}, Series: {Series}, Season: {Season}, Episode: {Episode}, Confidence: {Confidence:P1}, TotalTime: {Duration}ms",
                    result.MatchingMethod, operationId, result.Series, result.Season, result.Episode, result.MatchConfidence, stopwatch.ElapsedMilliseconds);
                return result;
            }

            // No match found with any strategy
            stopwatch.Stop();
            _logger.LogInformation("No match found using {Strategy} strategy - Operation: {OperationId}, Duration: {Duration}ms",
                strategy, operationId, stopwatch.ElapsedMilliseconds);

            return new IdentificationResult
            {
                MatchConfidence = 0,
                AmbiguityNotes = "No matching episode found in database",
                MatchingMethod = strategy
            };
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
        SubtitleType subtitleType,
        string? sourceFilePath,
        Configuration fuzzyConfig,
        double? minConfidence,
        Guid operationId,
        string? seriesFilter = null,
        int? seasonFilter = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "EnhancedCTPhIdentification",
            ["ParentOperationId"] = operationId,
            ["SourceFilePath"] = sourceFilePath ?? "none",
            ["SeriesFilter"] = seriesFilter ?? "none",
            ["SeasonFilter"] = seasonFilter?.ToString() ?? "none"
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

                // Get threshold configuration based on subtitle type
                var thresholds = fuzzyConfig.MatchingThresholds.GetThresholdsForType(subtitleType);
                var configuredThreshold = (double)thresholds.MatchConfidence;
                if (minConfidence.HasValue)
                    configuredThreshold = Math.Max(configuredThreshold, minConfidence.Value);

                _logger.LogDebug("Using thresholds for {SubtitleType}: MatchConfidence={MatchConfidence:P2}, FuzzyHashSimilarity={FuzzyHashSimilarity}",
                    subtitleType, configuredThreshold, thresholds.FuzzyHashSimilarity);

                if (confidence >= configuredThreshold)
                {
                    _logger.LogInformation("Enhanced CTPH match found - Operation: {OperationId}, Series: {Series} S{Season}E{Episode}, Method: {Method}, SubtitleType: {SubtitleType}, HashScore: {HashScore}%, TextScore: {TextScore}%, FinalConfidence: {Confidence:P2}, Threshold: {Threshold:P2}, Duration: {Duration}ms",
                        operationId, comparisonResult.MatchedSeries, comparisonResult.MatchedSeason, comparisonResult.MatchedEpisode,
                        comparisonResult.UsedTextFallback ? "CTPH+TextFallback" : "CTPH",
                        subtitleType, comparisonResult.HashSimilarityScore, comparisonResult.TextSimilarityScore, confidence, configuredThreshold, stopwatch.ElapsedMilliseconds);

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
                    _logger.LogDebug("Enhanced CTPH match found but below confidence threshold - Operation: {OperationId}, SubtitleType: {SubtitleType}, Confidence: {Confidence:P2}, Threshold: {Threshold:P2}, Duration: {Duration}ms",
                        operationId, subtitleType, confidence, configuredThreshold, stopwatch.ElapsedMilliseconds);
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
        SubtitleType subtitleType,
        double? minConfidence,
        Guid operationId)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "CTPhHashMatching",
            ["ParentOperationId"] = operationId,
            ["SourceHash"] = sourceHash,
            ["SubtitleType"] = subtitleType.ToString()
        });

        try
        {
            // TODO: This should integrate with a database of known episode CTPH hashes
            // For now, this is a framework that demonstrates the integration pattern

            var thresholds = fuzzyConfig.MatchingThresholds.GetThresholdsForType(subtitleType);
            var threshold = minConfidence ?? (double)thresholds.MatchConfidence;
            var fuzzyThreshold = thresholds.FuzzyHashSimilarity;

            _logger.LogDebug("Searching for CTPH hash matches - Operation: {OperationId}, Hash: {SourceHash}, SubtitleType: {SubtitleType}, Threshold: {Threshold:P1}, FuzzyThreshold: {FuzzyThreshold}",
                operationId, sourceHash, subtitleType, threshold, fuzzyThreshold);

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
    /// Attempts episode identification using ML embedding-based semantic similarity.
    /// Returns null if embedding services are not available or fail.
    /// </summary>
    private async Task<IdentificationResult?> TryEmbeddingIdentification(
        string subtitleText,
        SubtitleType subtitleType,
        Configuration config,
        double? minConfidence,
        Guid operationId,
        string? seriesFilter = null,
        int? seasonFilter = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "EmbeddingIdentification",
            ["ParentOperationId"] = operationId,
            ["SubtitleType"] = subtitleType.ToString(),
            ["SeriesFilter"] = seriesFilter ?? "none",
            ["SeasonFilter"] = seasonFilter?.ToString() ?? "none"
        });

        try
        {
            // Check if embedding services are available
            if (_embeddingService == null || _vectorSearchService == null)
            {
                stopwatch.Stop();
                _logger.LogDebug("Embedding services not available, skipping embedding identification - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                return null;
            }

            // Generate embedding for input subtitle
            _logger.LogDebug("Generating embedding for subtitle text - Operation: {OperationId}, TextLength: {TextLength}",
                operationId, subtitleText.Length);
            
            var embeddingStartTime = stopwatch.ElapsedMilliseconds;
            float[] queryEmbedding;
            
            try
            {
                queryEmbedding = _embeddingService.GenerateEmbedding(subtitleText);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to generate embedding - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                return null;
            }
            
            var embeddingDuration = stopwatch.ElapsedMilliseconds - embeddingStartTime;
            _logger.LogDebug("Embedding generated - Operation: {OperationId}, Duration: {Duration}ms",
                operationId, embeddingDuration);

            // Get thresholds for this subtitle type
            var sourceFormat = subtitleType switch
            {
                SubtitleType.TextBased => SubtitleSourceFormat.Text,
                SubtitleType.PGS => SubtitleSourceFormat.PGS,
                SubtitleType.VobSub => SubtitleSourceFormat.VobSub,
                _ => SubtitleSourceFormat.Text
            };

            if (config.EmbeddingThresholds == null)
            {
                _logger.LogError("EmbeddingThresholds is not configured - Operation: {OperationId}", operationId);
                return null;
            }
            var threshold = config.EmbeddingThresholds.GetThreshold(sourceFormat);
            var effectiveMinSimilarity = minConfidence ?? threshold.EmbedSimilarity;

            // Search for similar embeddings
            _logger.LogDebug("Searching for similar embeddings - Operation: {OperationId}, MinSimilarity: {MinSimilarity}, TopK: 10",
                operationId, effectiveMinSimilarity);

            var searchStartTime = stopwatch.ElapsedMilliseconds;
            var results = _vectorSearchService.SearchBySimilarity(
                queryEmbedding,
                topK: 10,
                minSimilarity: effectiveMinSimilarity);
            var searchDuration = stopwatch.ElapsedMilliseconds - searchStartTime;

            _logger.LogDebug("Vector search completed - Operation: {OperationId}, ResultCount: {ResultCount}, Duration: {Duration}ms",
                operationId, results.Count, searchDuration);

            // Filter by series/season if provided
            if (!string.IsNullOrEmpty(seriesFilter))
            {
                results = results.Where(r => 
                    r.Series.Equals(seriesFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (seasonFilter.HasValue)
                {
                    var seasonString = seasonFilter.Value.ToString("D2");
                    results = results.Where(r => r.Season == seasonString).ToList();
                }
            }

            if (results.Count == 0)
            {
                stopwatch.Stop();
                _logger.LogInformation("No embedding matches found - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                return null;
            }

            // Get best match
            var bestMatch = results.First();

            // Check if confidence meets minimum threshold
            if (bestMatch.Confidence < (minConfidence ?? threshold.MatchConfidence))
            {
                stopwatch.Stop();
                _logger.LogInformation("Best embedding match below confidence threshold - Operation: {OperationId}, Confidence: {Confidence:P1}, Threshold: {Threshold:P1}, Duration: {Duration}ms",
                    operationId, bestMatch.Confidence, minConfidence ?? threshold.MatchConfidence, stopwatch.ElapsedMilliseconds);
                return null;
            }

            stopwatch.Stop();
            _logger.LogInformation("Episode identified using embedding matching - Operation: {OperationId}, Series: {Series}, Season: {Season}, Episode: {Episode}, Similarity: {Similarity:P1}, Confidence: {Confidence:P1}, Duration: {Duration}ms",
                operationId, bestMatch.Series, bestMatch.Season, bestMatch.Episode, bestMatch.Similarity, bestMatch.Confidence, stopwatch.ElapsedMilliseconds);

            // Convert to IdentificationResult
            return new IdentificationResult
            {
                Series = bestMatch.Series,
                Season = bestMatch.Season,
                Episode = bestMatch.Episode,
                EpisodeName = bestMatch.EpisodeName,
                MatchConfidence = bestMatch.Confidence,
                MatchingMethod = "Embedding",
                AmbiguityNotes = results.Count > 1 
                    ? $"Found {results.Count} similar episodes, selected best match (similarity: {bestMatch.Similarity:P1})"
                    : null
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during embedding identification - Operation: {OperationId}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                operationId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            return null;
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
