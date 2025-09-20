using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FuzzySharp;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Hashing;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core.Services.Hashing
{
    /// <summary>
    /// Service for fuzzy string comparison used as fallback when CTPH hashing is below threshold
    /// </summary>
    public class FuzzyStringComparisonService : IFuzzyStringComparisonService
    {
        private readonly string _dbPath;
        private readonly ILogger<FuzzyStringComparisonService>_logger;
        private readonly SubtitleNormalizationService _normalizationService;
        private const int DEFAULT_SIMILARITY_THRESHOLD = 75;
        private readonly int_similarityThreshold;

        /// <summary>
        /// Creates a new instance of FuzzyStringComparisonService.
        /// </summary>
        /// <param name="dbPath">Path to the database.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="normalizationService">Subtitle normalization service.</param>
        /// <param name="similarityThreshold">Optional similarity threshold (default: 75).</param>
        public FuzzyStringComparisonService(
            string dbPath,
            ILogger<FuzzyStringComparisonService> logger,
            SubtitleNormalizationService normalizationService,
            int similarityThreshold = DEFAULT_SIMILARITY_THRESHOLD)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
            _similarityThreshold = similarityThreshold;
        }

        /// <summary>
        /// Compares input text against stored subtitles for a specific series/season/episode
        /// </summary>
        public async Task<List<FuzzyStringMatch>> FindMatches(string inputText, string series, string? season = null, string? episode = null)
        {
            var operationId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "FuzzyStringFallback",
                ["OperationId"] = operationId,
                ["Series"] = series,
                ["Season"] = season ?? "All",
                ["Episode"] = episode ?? "All"
            });

            _logger.LogInformation("Starting fuzzy string fallback comparison - Operation: {OperationId}, Series: {Series}, Season: {Season}, Episode: {Episode}",
                operationId, series, season, episode);

            var results = new List<FuzzyStringMatch>();

            try
            {
                // Create normalized versions of input text
                var inputNormalized = _normalizationService.CreateNormalizedVersions(inputText);
                var inputVersions = new Dictionary<string, string>
                {
                    { "Original", inputNormalized.Original },
                    { "NoTimecodes", inputNormalized.NoTimecodes },
                    { "NoHtml", inputNormalized.NoHtml },
                    { "Clean", inputNormalized.NoHtmlAndTimecodes }
                };

                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                // Build query with filters
                var whereClause = "WHERE Series = @series";
                var parameters = new Dictionary<string, object> { { "@series", series } };

                if (!string.IsNullOrEmpty(season))
                {
                    whereClause += " AND Season = @season";
                    parameters.Add("@season", season);
                }

                if (!string.IsNullOrEmpty(episode))
                {
                    whereClause += " AND Episode = @episode";
                    parameters.Add("@episode", episode);
                }

                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT Series, Season, Episode, OriginalText, NoTimecodesText, NoHtmlText, CleanText, EpisodeName
                    FROM SubtitleHashes 
                    {whereClause}";

                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }

                _logger.LogDebug("Executing fuzzy string query - Operation: {OperationId}, Query: {Query}",
                    operationId, command.CommandText);

                using var reader = await command.ExecuteReaderAsync();
                int recordsProcessed = 0;

                while (await reader.ReadAsync())
                {
                    recordsProcessed++;

                    var subtitle = new LabelledSubtitle
                    {
                        Series = reader.GetString(0),
                        Season = reader.GetString(1),
                        Episode = reader.GetString(2),
                        SubtitleText = reader.GetString(3), // Use original text for result
                        EpisodeName = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };

                    // Get stored text versions
                    var storedVersions = new Dictionary<string, string>
                    {
                        { "Original", reader.GetString(3) },
                        { "NoTimecodes", reader.IsDBNull(4) ? "" : reader.GetString(4) },
                        { "NoHtml", reader.IsDBNull(5) ? "" : reader.GetString(5) },
                        { "Clean", reader.IsDBNull(6) ? "" : reader.GetString(6) }
                    };

                    // Compare each input version against each stored version
                    var bestScore = 0;
                    var bestVersion = "";

                    foreach (var inputKvp in inputVersions)
                    {
                        foreach (var storedKvp in storedVersions)
                        {
                            if (string.IsNullOrWhiteSpace(inputKvp.Value) || string.IsNullOrWhiteSpace(storedKvp.Value))
                                continue;

                            var score = CompareStrings(inputKvp.Value, storedKvp.Value);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestVersion = $"{inputKvp.Key} vs {storedKvp.Key}";
                            }
                        }
                    }

                    // Only add if above threshold
                    if (bestScore >= _similarityThreshold)
                    {
                        results.Add(new FuzzyStringMatch
                        {
                            Subtitle = subtitle,
                            SimilarityScore = bestScore,
                            Confidence = bestScore / 100.0,
                            MatchVersion = bestVersion
                        });

                        _logger.LogDebug("Fuzzy string match found - Operation: {OperationId}, Series: {Series} S{Season}E{Episode}, Score: {Score}%, Version: {Version}",
                            operationId, subtitle.Series, subtitle.Season, subtitle.Episode, bestScore, bestVersion);
                    }
                }

                stopwatch.Stop();

                // Sort by similarity score (highest first)
                var sortedResults = results.OrderByDescending(r => r.SimilarityScore).ToList();

                _logger.LogInformation("Fuzzy string fallback completed - Operation: {OperationId}, RecordsProcessed: {RecordsProcessed}, MatchesFound: {MatchesFound}, Duration: {Duration}ms",
                    operationId, recordsProcessed, sortedResults.Count, stopwatch.ElapsedMilliseconds);

                return sortedResults;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during fuzzy string fallback comparison - Operation: {OperationId}, Duration: {Duration}ms",
                    operationId, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Compares two strings using fuzzy string comparison
        /// </summary>
        public int CompareStrings(string text1, string text2)
        {
            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return 0;

            try
            {
                // Use FuzzySharp's TokenSetRatio which handles word order differences well
                // and is good for subtitle content comparison
                return Fuzz.TokenSetRatio(text1, text2);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error in string comparison: {Error}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Gets the similarity threshold used for determining matches
        /// </summary>
        public int GetSimilarityThreshold()
        {
            return _similarityThreshold;
        }
    }
}
