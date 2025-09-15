using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FuzzySharp;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Hashing;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core.Services.Hashing
{
    /// <summary>
    /// Enhanced CTPH hashing service that integrates with existing FuzzyHashService for text fallback
    /// </summary>
    public class EnhancedCTPhHashingService
    {
        private readonly ICTPhHashingService _ctphService;
        private readonly FuzzyHashService _fuzzyHashService;
        private readonly ILogger<EnhancedCTPhHashingService> _logger;
        private readonly IConfigurationService _configService;

        public EnhancedCTPhHashingService(
            ICTPhHashingService ctphService,
            FuzzyHashService fuzzyHashService,
            ILogger<EnhancedCTPhHashingService> logger,
            IConfigurationService configService)
        {
            _ctphService = ctphService ?? throw new ArgumentNullException(nameof(ctphService));
            _fuzzyHashService = fuzzyHashService ?? throw new ArgumentNullException(nameof(fuzzyHashService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Compares subtitle text with CTPH fallback to fuzzy string matching
        /// </summary>
        /// <param name="subtitleText">The subtitle text to match</param>
        /// <param name="enableTextFallback">Whether to enable text search fallback</param>
        /// <returns>Enhanced comparison result with fallback information</returns>
        public async Task<EnhancedComparisonResult> CompareSubtitleWithFallback(string subtitleText, bool enableTextFallback = true)
        {
            var operationId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "EnhancedSubtitleComparison",
                ["OperationId"] = operationId,
                ["TextLength"] = subtitleText?.Length ?? 0,
                ["FallbackEnabled"] = enableTextFallback
            });

            try
            {
                _logger.LogInformation("Starting enhanced subtitle comparison - Operation: {OperationId}, TextLength: {TextLength}, FallbackEnabled: {FallbackEnabled}",
                    operationId, subtitleText?.Length ?? 0, enableTextFallback);

                if (string.IsNullOrWhiteSpace(subtitleText))
                {
                    return EnhancedComparisonResult.Failure("EMPTY_SUBTITLE_TEXT");
                }

                // Load configuration
                var configResult = await _configService.LoadConfiguration();
                var config = configResult.Configuration;
                if (config == null)
                {
                    _logger.LogWarning("Configuration not available, using default values - Operation: {OperationId}", operationId);
                    // Fall back to default behavior if config is not available
                    return EnhancedComparisonResult.Failure("CONFIGURATION_UNAVAILABLE");
                }

                var ctphThreshold = (double)config.FuzzyHashThreshold / 100.0; // Convert to 0-1 range
                var textFallbackHashThreshold = 0.3; // 30% hash similarity for candidates - could be configurable in future

                // Step 1: Try CTPH-style fast hash matching using existing FuzzyHashService
                var hashMatches = await _fuzzyHashService.FindMatches(subtitleText, ctphThreshold);

                var hashMatchTime = stopwatch.Elapsed;
                _logger.LogDebug("CTPH-style hash matching completed - Operation: {OperationId}, MatchesFound: {MatchesFound}, Duration: {Duration}ms, Threshold: {Threshold:P2}",
                    operationId, hashMatches.Count, hashMatchTime.TotalMilliseconds, ctphThreshold);

                // Check if we have good hash matches above threshold
                if (hashMatches.Count > 0)
                {
                    var bestHashMatch = hashMatches.First();
                    var hashSimilarityScore = (int)(bestHashMatch.Confidence * 100);

                    if (hashSimilarityScore >= config.FuzzyHashThreshold)
                    {
                        stopwatch.Stop();
                        _logger.LogInformation("High-confidence hash match found - Operation: {OperationId}, Series: {Series} S{Season}E{Episode}, Score: {Score}%, Duration: {Duration}ms",
                            operationId, bestHashMatch.Subtitle.Series, bestHashMatch.Subtitle.Season, bestHashMatch.Subtitle.Episode,
                            hashSimilarityScore, stopwatch.ElapsedMilliseconds);

                        return EnhancedComparisonResult.Success(
                            hashSimilarityScore, true, stopwatch.Elapsed,
                            bestHashMatch.Subtitle.Series, bestHashMatch.Subtitle.Season, bestHashMatch.Subtitle.Episode,
                            bestHashMatch.Subtitle.EpisodeName);
                    }
                }

                // Step 2: If hash matching didn't find good matches and text fallback is enabled, try text comparison
                if (enableTextFallback && (hashMatches.Count == 0 || hashMatches.First().Confidence < ctphThreshold))
                {
                    _logger.LogInformation("Hash matching below threshold, attempting text fallback - Operation: {OperationId}, BestHashConfidence: {BestHashConfidence:P2}, HashThreshold: {HashThreshold:P2}",
                        operationId, hashMatches.Count > 0 ? hashMatches.First().Confidence : 0, ctphThreshold);

                    var textFallbackStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        // Get candidates for text comparison - use lower threshold for hash to get more candidates
                        var textCandidates = await _fuzzyHashService.FindMatches(subtitleText, textFallbackHashThreshold);

                        if (textCandidates.Count > 0)
                        {
                            // Group by series to focus text comparison
                            var seriesGroups = textCandidates
                                .GroupBy(c => c.Subtitle.Series)
                                .OrderByDescending(g => g.Max(c => c.Confidence))
                                .Take(3); // Focus on top 3 series

                            var bestTextMatch = await FindBestTextMatch(subtitleText, seriesGroups);

                            textFallbackStopwatch.Stop();

                            if (bestTextMatch != null)
                            {
                                var hashSimilarity = hashMatches.Count > 0 ? (int)(hashMatches.First().Confidence * 100) : 0;

                                _logger.LogInformation("Text fallback found match - Operation: {OperationId}, Series: {Series} S{Season}E{Episode}, HashScore: {HashScore}%, TextScore: {TextScore}%",
                                    operationId, bestTextMatch.Series, bestTextMatch.Season, bestTextMatch.Episode,
                                    hashSimilarity, bestTextMatch.TextSimilarityScore);

                                stopwatch.Stop();

                                return EnhancedComparisonResult.SuccessWithTextFallback(
                                    hashSimilarity, bestTextMatch.TextSimilarityScore,
                                    bestTextMatch.TextSimilarityScore >= 75, // 75% threshold for text matching
                                    hashMatchTime, textFallbackStopwatch.Elapsed,
                                    bestTextMatch.Series, bestTextMatch.Season, bestTextMatch.Episode, bestTextMatch.EpisodeName);
                            }
                        }

                        textFallbackStopwatch.Stop();
                        _logger.LogInformation("Text fallback completed but no matches found - Operation: {OperationId}, CandidatesChecked: {CandidatesChecked}, Duration: {Duration}ms",
                            operationId, textCandidates.Count, textFallbackStopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        textFallbackStopwatch.Stop();
                        _logger.LogWarning(ex, "Text fallback failed - Operation: {OperationId}, Duration: {Duration}ms",
                            operationId, textFallbackStopwatch.ElapsedMilliseconds);
                    }
                }

                stopwatch.Stop();

                // Return result with best hash match if available, even if below threshold
                if (hashMatches.Count > 0)
                {
                    var bestMatch = hashMatches.First();
                    var hashSimilarityScore = (int)(bestMatch.Confidence * 100);

                    return EnhancedComparisonResult.Success(
                        hashSimilarityScore, false, stopwatch.Elapsed,
                        bestMatch.Subtitle.Series, bestMatch.Subtitle.Season, bestMatch.Subtitle.Episode,
                        bestMatch.Subtitle.EpisodeName);
                }

                return EnhancedComparisonResult.NoMatch(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error in enhanced subtitle comparison - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Finds the best text match among candidate episodes using fuzzy string comparison
        /// </summary>
        private async Task<TextMatchResult?> FindBestTextMatch(
            string inputText,
            IEnumerable<IGrouping<string, (LabelledSubtitle Subtitle, double Confidence)>> seriesGroups)
        {
            TextMatchResult? bestMatch = null;
            var bestScore = 0;

            foreach (var seriesGroup in seriesGroups)
            {
                var series = seriesGroup.Key;
                _logger.LogDebug("Checking text similarity for series: {Series}, Episodes: {EpisodeCount}",
                    series, seriesGroup.Count());

                foreach (var candidate in seriesGroup.Take(5)) // Top 5 episodes per series
                {
                    var textScore = await CompareTextsAsync(inputText, candidate.Subtitle.SubtitleText);

                    if (textScore > bestScore)
                    {
                        bestScore = textScore;
                        bestMatch = new TextMatchResult
                        {
                            Series = candidate.Subtitle.Series,
                            Season = candidate.Subtitle.Season,
                            Episode = candidate.Subtitle.Episode,
                            EpisodeName = candidate.Subtitle.EpisodeName,
                            TextSimilarityScore = textScore
                        };
                    }

                    // Early exit if we find a very good match
                    if (textScore >= 95)
                    {
                        _logger.LogDebug("Excellent text match found, stopping search: {Score}%", textScore);
                        break;
                    }
                }

                if (bestScore >= 95) break; // Exit outer loop too
            }

            return bestMatch;
        }

        /// <summary>
        /// Compares two texts using fuzzy string matching
        /// </summary>
        private async Task<int> CompareTextsAsync(string text1, string text2)
        {
            // Use Task.Run to make this async and avoid blocking
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                    return 0;

                try
                {
                    return FuzzySharp.Fuzz.TokenSetRatio(text1, text2);
                }
                catch
                {
                    return 0;
                }
            });
        }
    }

    /// <summary>
    /// Enhanced comparison result with text fallback information
    /// </summary>
    public class EnhancedComparisonResult
    {
        public int HashSimilarityScore { get; set; }
        public int TextSimilarityScore { get; set; }
        public bool IsMatch { get; set; }
        public bool UsedTextFallback { get; set; }
        public TimeSpan HashComparisonTime { get; set; }
        public TimeSpan TextFallbackTime { get; set; }
        public string? MatchedSeries { get; set; }
        public string? MatchedSeason { get; set; }
        public string? MatchedEpisode { get; set; }
        public string? MatchedEpisodeName { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public static EnhancedComparisonResult Success(
            int hashSimilarityScore, bool isMatch, TimeSpan hashTime,
            string? series = null, string? season = null, string? episode = null, string? episodeName = null)
        {
            return new EnhancedComparisonResult
            {
                HashSimilarityScore = hashSimilarityScore,
                IsMatch = isMatch,
                UsedTextFallback = false,
                HashComparisonTime = hashTime,
                MatchedSeries = series,
                MatchedSeason = season,
                MatchedEpisode = episode,
                MatchedEpisodeName = episodeName,
                IsSuccess = true
            };
        }

        public static EnhancedComparisonResult SuccessWithTextFallback(
            int hashSimilarityScore, int textSimilarityScore, bool isMatch,
            TimeSpan hashTime, TimeSpan textTime,
            string? series = null, string? season = null, string? episode = null, string? episodeName = null)
        {
            return new EnhancedComparisonResult
            {
                HashSimilarityScore = hashSimilarityScore,
                TextSimilarityScore = textSimilarityScore,
                IsMatch = isMatch,
                UsedTextFallback = true,
                HashComparisonTime = hashTime,
                TextFallbackTime = textTime,
                MatchedSeries = series,
                MatchedSeason = season,
                MatchedEpisode = episode,
                MatchedEpisodeName = episodeName,
                IsSuccess = true
            };
        }

        public static EnhancedComparisonResult Failure(string errorMessage)
        {
            return new EnhancedComparisonResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        public static EnhancedComparisonResult NoMatch(TimeSpan duration)
        {
            return new EnhancedComparisonResult
            {
                IsSuccess = true,
                IsMatch = false,
                HashComparisonTime = duration
            };
        }
    }

    /// <summary>
    /// Internal class for text match results
    /// </summary>
    internal class TextMatchResult
    {
        public required string Series { get; set; }
        public required string Season { get; set; }
        public required string Episode { get; set; }
        public string? EpisodeName { get; set; }
        public int TextSimilarityScore { get; set; }
    }
}