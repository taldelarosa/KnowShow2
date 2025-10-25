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
    private readonly ITextRankService? _textRankService;
    private readonly SubtitleNormalizationService _normalizationService;
    private readonly string? _databasePath;

    public EpisodeIdentificationService(
        ILogger<EpisodeIdentificationService> logger,
        IFileSystem? fileSystem = null,
        EnhancedCTPhHashingService? enhancedCtphService = null,
        IEmbeddingService? embeddingService = null,
        IVectorSearchService? vectorSearchService = null,
        string? databasePath = null,
        ITextRankService? textRankService = null,
        SubtitleNormalizationService? normalizationService = null)
    {
        _logger = logger;
        _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        _enhancedCtphService = enhancedCtphService;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _textRankService = textRankService;
        _databasePath = databasePath;
        _normalizationService = normalizationService ?? new SubtitleNormalizationService(
            _logger as ILogger<SubtitleNormalizationService> ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubtitleNormalizationService>.Instance);
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

            // Normalize subtitle text first (remove timecodes, HTML tags, etc.) - CRITICAL for matching
            var normalized = _normalizationService.CreateNormalizedVersions(subtitleText);
            var cleanSubtitleText = normalized.NoHtmlAndTimecodes;
            
            _logger.LogDebug("Subtitle text normalized - Operation: {OperationId}, Original: {OriginalLength}, Clean: {CleanLength}",
                operationId, subtitleText.Length, cleanSubtitleText.Length);

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
                    // Try embedding first, fallback to fuzzy if ambiguous or low confidence
                    result = await TryEmbeddingIdentification(subtitleText, subtitleType, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                    if (result == null || (result.IsAmbiguous && result.MatchConfidence < 0.60))
                    {
                        _logger.LogInformation("Embedding matching ambiguous or low confidence ({Confidence:P1}), falling back to fuzzy hashing - Operation: {OperationId}", 
                            result?.MatchConfidence ?? 0, operationId);
                        var fuzzyResult = await TryFuzzyHashIdentification(subtitleText, subtitleType, sourceFilePath, fuzzyConfig, minConfidence, operationId, seriesFilter, seasonFilter);
                        if (fuzzyResult != null)
                        {
                            _logger.LogInformation("Fuzzy hash provided better match - Operation: {OperationId}, FuzzyConfidence: {FuzzyConf:P1} vs EmbeddingConfidence: {EmbedConf:P1}",
                                operationId, fuzzyResult.MatchConfidence, result?.MatchConfidence ?? 0);
                            result = fuzzyResult;
                        }
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
            // Normalize subtitle text first (remove timecodes, HTML tags, etc.) - CRITICAL for matching
            var normalized = _normalizationService.CreateNormalizedVersions(subtitleText);
            var cleanSubtitleText = normalized.NoHtmlAndTimecodes;
            
            _logger.LogDebug("Subtitle text normalized for embedding - Operation: {OperationId}, Original: {OriginalLength}, Clean: {CleanLength}",
                operationId, subtitleText.Length, cleanSubtitleText.Length);

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
                operationId, cleanSubtitleText.Length);

            var embeddingStartTime = stopwatch.ElapsedMilliseconds;
            float[] queryEmbedding;

            try
            {
                // Apply TextRank filtering if enabled
                string textForEmbedding = cleanSubtitleText;
                if (config.TextRankFiltering?.Enabled == true && _textRankService != null)
                {
                    var filteringStartTime = stopwatch.ElapsedMilliseconds;
                    var extractionResult = _textRankService.ExtractPlotRelevantSentences(
                        cleanSubtitleText,
                        config.TextRankFiltering.SentencePercentage,
                        config.TextRankFiltering.MinSentences,
                        config.TextRankFiltering.MinPercentage);

                    var filteringDuration = stopwatch.ElapsedMilliseconds - filteringStartTime;
                    
                    _logger.LogDebug("TextRank filtering completed - Operation: {OperationId}, Duration: {Duration}ms, " +
                                    "TotalSentences: {TotalSentences}, SelectedSentences: {SelectedSentences}, " +
                                    "SelectionPercentage: {SelectionPercentage:F1}%, FallbackTriggered: {FallbackTriggered}, FallbackReason: {FallbackReason}",
                        operationId, filteringDuration,
                        extractionResult.TotalSentenceCount, extractionResult.SelectedSentenceCount,
                        extractionResult.SelectionPercentage, extractionResult.FallbackTriggered,
                        extractionResult.FallbackReason ?? "none");

                    textForEmbedding = extractionResult.FilteredText;
                }

                queryEmbedding = _embeddingService.GenerateEmbedding(textForEmbedding);
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
            _logger.LogDebug("Searching for similar embeddings - Operation: {OperationId}, MinSimilarity: {MinSimilarity}, TopK: 10, SeriesFilter: {SeriesFilter}, SeasonFilter: {SeasonFilter}",
                operationId, effectiveMinSimilarity, seriesFilter ?? "none", seasonFilter?.ToString() ?? "none");

            var searchStartTime = stopwatch.ElapsedMilliseconds;
            
            // Convert seasonFilter int to string (without zero-padding to match database format)
            string? seasonFilterString = seasonFilter.HasValue ? seasonFilter.Value.ToString() : null;
            
            var results = _vectorSearchService.SearchBySimilarity(
                queryEmbedding,
                topK: 10,
                minSimilarity: effectiveMinSimilarity,
                seriesFilter: seriesFilter,
                seasonFilter: seasonFilterString);
            var searchDuration = stopwatch.ElapsedMilliseconds - searchStartTime;

            _logger.LogDebug("Vector search completed - Operation: {OperationId}, ResultCount: {ResultCount}, Duration: {Duration}ms",
                operationId, results.Count, searchDuration);

            if (results.Count == 0)
            {
                stopwatch.Stop();
                _logger.LogInformation("No embedding matches found - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                return null;
            }

            // Log all top results for debugging
            _logger.LogInformation("Top {Count} embedding matches for debugging - Operation: {OperationId}:", results.Count, operationId);
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                _logger.LogInformation("  #{Rank}: S{Season}E{Episode} - {EpisodeName} - Similarity: {Similarity:P1}, Confidence: {Confidence:P1}",
                    i + 1, result.Season, result.Episode, result.EpisodeName ?? "Unknown", result.Similarity, result.Confidence);
            }

            // Check if top results are very close (within 2% similarity)
            var topSimilarity = results[0].Similarity;
            var closeResults = results.Where(r => (topSimilarity - r.Similarity) <= 0.02).ToList();
            
            if (closeResults.Count >= 2 && _databasePath != null)
            {
                _logger.LogInformation("Top {Count} results within 2% similarity - applying fast text disambiguation - Operation: {OperationId}", 
                    closeResults.Count, operationId);
                
                // Fast text-based disambiguation
                var reranked = await FastTextDisambiguation(subtitleText, closeResults, operationId);
                if (reranked != null && reranked.Count > 0)
                {
                    results = reranked.Concat(results.Except(closeResults)).ToList();
                    _logger.LogInformation("After text disambiguation, new top match: S{Season}E{Episode} - Operation: {OperationId}", 
                        results[0].Season, results[0].Episode, operationId);
                }
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
    /// Summarization-based disambiguation using chunk embeddings.
    /// Splits text into 25% chunks, generates summaries via embeddings, then compares.
    /// </summary>
    private async Task<List<VectorSimilarityResult>?> FastTextDisambiguation(
        string queryText,
        List<VectorSimilarityResult> candidates,
        Guid operationId)
    {
        if (string.IsNullOrEmpty(_databasePath) || _embeddingService == null)
        {
            return null;
        }

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            // Group by episode to avoid redundant work
            var episodeGroups = candidates.GroupBy(c => new { c.Season, c.Episode }).ToList();
            
            // Generate summary embedding for query text (4 chunks of 25% each)
            var querySummaryEmbedding = await GenerateSummaryEmbedding(queryText, operationId);
            if (querySummaryEmbedding == null)
            {
                _logger.LogWarning("Failed to generate query summary embedding - Operation: {OperationId}", operationId);
                return null;
            }
            
            var scored = new List<(VectorSimilarityResult result, double summaryScore, double finalScore)>();

            foreach (var group in episodeGroups)
            {
                var representative = group.First();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT CleanText FROM SubtitleHashes WHERE Season = @season AND Episode = @episode LIMIT 1";
                command.Parameters.AddWithValue("@season", representative.Season);
                command.Parameters.AddWithValue("@episode", representative.Episode);
                
                var storedText = await command.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(storedText))
                {
                    foreach (var candidate in group)
                    {
                        scored.Add((candidate, 0, candidate.Similarity));
                    }
                    continue;
                }

                // Generate summary embedding for stored text
                var storedSummaryEmbedding = await GenerateSummaryEmbedding(storedText, operationId);
                if (storedSummaryEmbedding == null)
                {
                    _logger.LogWarning("Failed to generate stored summary embedding for S{Season}E{Episode} - Operation: {OperationId}",
                        representative.Season, representative.Episode, operationId);
                    foreach (var candidate in group)
                    {
                        scored.Add((candidate, 0, candidate.Similarity));
                    }
                    continue;
                }
                
                // Calculate cosine similarity between summary embeddings
                var summaryScore = CalculateCosineSimilarity(querySummaryEmbedding, storedSummaryEmbedding);
                
                // Combined score: 30% full-text embedding + 70% summary embedding
                var finalScore = (representative.Similarity * 0.3) + (summaryScore * 0.7);
                
                _logger.LogInformation("S{Season}E{Episode}: FullEmbed={Embed:P1}, SummaryEmbed={Summary:P1}, Final={Final:P1} - Operation: {OperationId}",
                    representative.Season, representative.Episode, representative.Similarity, summaryScore, finalScore, operationId);
                
                foreach (var candidate in group)
                {
                    scored.Add((candidate, summaryScore, finalScore));
                }
            }

            // Sort by final score
            return scored.OrderByDescending(x => x.finalScore).Select(x => x.result).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during summarization-based disambiguation - Operation: {OperationId}", operationId);
            return null;
        }
    }

    /// <summary>
    /// Generate a summary embedding by splitting text into 4 chunks (25% each),
    /// embedding each chunk, then averaging the embeddings.
    /// NOTE: This doesn't generate actual summaries, it just chunks and averages embeddings.
    /// </summary>
    private Task<float[]?> GenerateSummaryEmbedding(string text, Guid operationId)
    {
        if (_embeddingService == null || string.IsNullOrEmpty(text))
        {
            return Task.FromResult<float[]?>(null);
        }

        try
        {
            // Split into 4 equal chunks (25% each)
            var chunkSize = text.Length / 4;
            var chunks = new List<string>();
            
            for (int i = 0; i < 4; i++)
            {
                var start = i * chunkSize;
                var length = (i == 3) ? (text.Length - start) : chunkSize; // Last chunk gets remainder
                var chunk = text.Substring(start, length);
                chunks.Add(chunk);
                
                // Log first 200 chars of each chunk for debugging
                var preview = chunk.Length > 200 ? chunk.Substring(0, 200) + "..." : chunk;
                _logger.LogInformation("Chunk {ChunkNum} (length={Length}): {Preview} - Operation: {OperationId}",
                    i + 1, chunk.Length, preview, operationId);
            }

            // Generate embedding for each chunk
            var chunkEmbeddings = new List<float[]>();
            foreach (var chunk in chunks)
            {
                var embedding = _embeddingService.GenerateEmbedding(chunk);
                chunkEmbeddings.Add(embedding);
            }

            // Average the embeddings to create summary embedding
            var dimensions = chunkEmbeddings[0].Length;
            var summaryEmbedding = new float[dimensions];
            
            for (int i = 0; i < dimensions; i++)
            {
                summaryEmbedding[i] = chunkEmbeddings.Average(e => e[i]);
            }

            return Task.FromResult<float[]?>(summaryEmbedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary embedding - Operation: {OperationId}", operationId);
            return Task.FromResult<float[]?>(null);
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two embedding vectors.
    /// </summary>
    private double CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
        {
            throw new ArgumentException("Embeddings must have the same dimension");
        }

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        if (norm1 == 0 || norm2 == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    /// <summary>
    /// Extract sample from beginning and end of text for better episode discrimination.
    /// Takes first N and last N characters.
    /// </summary>
    private string ExtractSample(string text, int sampleSize)
    {
        if (text.Length <= sampleSize * 2)
        {
            return text;
        }
        
        var beginning = text.Substring(0, sampleSize);
        var end = text.Substring(text.Length - sampleSize, sampleSize);
        return beginning + end;
    }

    /// <summary>
    /// Extract n-grams (substrings of length n) from text.
    /// Returns a set of lowercase n-grams.
    /// </summary>
    private HashSet<string> ExtractNgrams(string text, int n)
    {
        var ngrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
        
        for (int i = 0; i <= cleaned.Length - n; i++)
        {
            ngrams.Add(cleaned.Substring(i, n));
        }
        
        return ngrams;
    }

    /// <summary>
    /// Re-ranks ambiguous embedding matches using character-level text similarity.
    /// Retrieves subtitle text from database and compares with query text using fuzzy matching.
    /// </summary>
    private async Task<List<VectorSimilarityResult>?> RerankByTextSimilarity(
        string queryText,
        List<VectorSimilarityResult> candidates,
        Guid operationId)
    {
        if (string.IsNullOrEmpty(_databasePath))
        {
            _logger.LogWarning("Database path not available for text-based re-ranking - Operation: {OperationId}", operationId);
            return null;
        }

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            // Group candidates by episode to avoid redundant comparisons
            var episodeGroups = candidates
                .GroupBy(c => new { c.Season, c.Episode })
                .ToList();

            _logger.LogInformation("Grouped {TotalCandidates} candidates into {UniqueEpisodes} unique episodes for text comparison - Operation: {OperationId}",
                candidates.Count, episodeGroups.Count, operationId);

            var rerankedCandidates = new List<(VectorSimilarityResult result, double textScore, double combinedScore)>();

            // Sample first 5000 characters for fast comparison (enough to distinguish episodes)
            var querySample = queryText.Length > 5000 ? queryText.Substring(0, 5000) : queryText;
            var queryWords = new HashSet<string>(
                querySample.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant().Trim()), 
                StringComparer.OrdinalIgnoreCase);

            foreach (var group in episodeGroups)
            {
                // Get one representative from the group
                var representative = group.First();
                
                // Retrieve stored subtitle text from database (using CleanText which has normalized formatting)
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT CleanText FROM SubtitleHashes WHERE Id = @id LIMIT 1";
                command.Parameters.AddWithValue("@id", representative.Id);
                
                var storedText = await command.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(storedText))
                {
                    _logger.LogWarning("No subtitle text found for S{Season}E{Episode} - Operation: {OperationId}", 
                        representative.Season, representative.Episode, operationId);
                    
                    // Add all group members with 0 text score
                    foreach (var candidate in group)
                    {
                        rerankedCandidates.Add((candidate, 0, candidate.Similarity));
                    }
                    continue;
                }

                // Fast word-based similarity: count matching words
                var storedSample = storedText.Length > 5000 ? storedText.Substring(0, 5000) : storedText;
                var storedWords = storedSample.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant().Trim());
                
                var matchingWords = storedWords.Count(w => queryWords.Contains(w));
                var textScore = (double)matchingWords / Math.Max(queryWords.Count, 1);
                
                // Combined score: 40% embedding + 60% text (text is more reliable for disambiguation)
                var combinedScore = (representative.Similarity * 0.4) + (textScore * 0.6);
                
                _logger.LogInformation("Text similarity for S{Season}E{Episode}: Embedding={Embedding:P1}, TextWordMatch={Text:P1}, Combined={Combined:P1} - Operation: {OperationId}",
                    representative.Season, representative.Episode, representative.Similarity, textScore, combinedScore, operationId);
                
                // Apply same score to all variants of this episode
                foreach (var candidate in group)
                {
                    rerankedCandidates.Add((candidate, textScore, combinedScore));
                }
            }

            // Sort by combined score
            var sorted = rerankedCandidates
                .OrderByDescending(x => x.combinedScore)
                .Select(x => x.result)
                .ToList();

            _logger.LogInformation("Text-based re-ranking completed - Top match: S{Season}E{Episode} (was embedding rank #{Rank}) - Operation: {OperationId}",
                sorted[0].Season, sorted[0].Episode, candidates.IndexOf(sorted[0]) + 1, operationId);

            return sorted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text-based re-ranking - Operation: {OperationId}", operationId);
            return null;
        }
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
