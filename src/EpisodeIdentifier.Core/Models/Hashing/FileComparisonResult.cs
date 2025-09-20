using System;
using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models.Hashing
{
    /// <summary>
    /// Represents the result of comparing two files using CTPH fuzzy hashing
    /// </summary>
    public class FileComparisonResult
    {
        /// <summary>
        /// Fuzzy hash of the first file
        /// </summary>
        public string Hash1 { get; set; } = string.Empty;

        /// <summary>
        /// Fuzzy hash of the second file
        /// </summary>
        public string Hash2 { get; set; } = string.Empty;

        /// <summary>
        /// Similarity score between the two hashes (0-100)
        /// </summary>
        [Range(0, 100)]
        public int SimilarityScore { get; set; }

        /// <summary>
        /// Whether the files are considered similar based on threshold
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Time taken to perform the comparison
        /// </summary>
        public TimeSpan ComparisonTime { get; set; }

        /// <summary>
        /// Indicates if a text search fallback was used when CTPH hashing was below threshold
        /// </summary>
        public bool UsedTextFallback { get; set; }

        /// <summary>
        /// Text similarity score from fuzzy string comparison (0-100), only set when text fallback is used
        /// </summary>
        [Range(0, 100)]
        public int TextSimilarityScore { get; set; }

        /// <summary>
        /// Time taken to perform the text fallback comparison
        /// </summary>
        public TimeSpan TextFallbackTime { get; set; }

        /// <summary>
        /// Series name from database match (only set when text fallback finds a match)
        /// </summary>
        public string? MatchedSeries { get; set; }

        /// <summary>
        /// Season from database match (only set when text fallback finds a match)
        /// </summary>
        public string? MatchedSeason { get; set; }

        /// <summary>
        /// Episode from database match (only set when text fallback finds a match)
        /// </summary>
        public string? MatchedEpisode { get; set; }

        /// <summary>
        /// Creates a successful comparison result
        /// </summary>
        public static FileComparisonResult Success(string hash1, string hash2, int similarityScore,
            bool isMatch, TimeSpan comparisonTime)
        {
            return new FileComparisonResult
            {
                Hash1 = hash1,
                Hash2 = hash2,
                SimilarityScore = similarityScore,
                IsMatch = isMatch,
                ComparisonTime = comparisonTime,
                UsedTextFallback = false
            };
        }

        /// <summary>
        /// Creates a successful comparison result with text fallback information
        /// </summary>
        public static FileComparisonResult SuccessWithTextFallback(string hash1, string hash2, int hashSimilarityScore,
            int textSimilarityScore, bool isMatch, TimeSpan comparisonTime, TimeSpan textFallbackTime,
            string? matchedSeries = null, string? matchedSeason = null, string? matchedEpisode = null)
        {
            return new FileComparisonResult
            {
                Hash1 = hash1,
                Hash2 = hash2,
                SimilarityScore = hashSimilarityScore,
                TextSimilarityScore = textSimilarityScore,
                IsMatch = isMatch,
                ComparisonTime = comparisonTime,
                TextFallbackTime = textFallbackTime,
                UsedTextFallback = true,
                MatchedSeries = matchedSeries,
                MatchedSeason = matchedSeason,
                MatchedEpisode = matchedEpisode
            };
        }

        /// <summary>
        /// Creates a failed comparison result
        /// </summary>
        public static FileComparisonResult Failure(string errorHash = "ERROR")
        {
            return new FileComparisonResult
            {
                Hash1 = errorHash,
                Hash2 = errorHash,
                SimilarityScore = 0,
                IsMatch = false,
                ComparisonTime = TimeSpan.Zero
            };
        }
    }
}
