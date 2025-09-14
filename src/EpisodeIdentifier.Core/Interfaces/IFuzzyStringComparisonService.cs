using System.Collections.Generic;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces
{
    /// <summary>
    /// Service for fuzzy string comparison used as fallback when CTPH hashing is below threshold
    /// </summary>
    public interface IFuzzyStringComparisonService
    {
        /// <summary>
        /// Compares input text against stored subtitles for a specific series/season/episode
        /// </summary>
        /// <param name="inputText">The text to compare</param>
        /// <param name="series">Series name to filter database results</param>
        /// <param name="season">Season to filter database results (optional)</param>
        /// <param name="episode">Episode to filter database results (optional)</param>
        /// <returns>List of matches with similarity scores</returns>
        Task<List<FuzzyStringMatch>> FindMatches(string inputText, string series, string? season = null, string? episode = null);

        /// <summary>
        /// Compares two strings using fuzzy string comparison
        /// </summary>
        /// <param name="text1">First text to compare</param>
        /// <param name="text2">Second text to compare</param>
        /// <returns>Similarity score (0-100)</returns>
        int CompareStrings(string text1, string text2);

        /// <summary>
        /// Gets the similarity threshold used for determining matches
        /// </summary>
        /// <returns>The similarity threshold (0-100)</returns>
        int GetSimilarityThreshold();
    }

    /// <summary>
    /// Represents the result of a fuzzy string comparison match
    /// </summary>
    public class FuzzyStringMatch
    {
        /// <summary>
        /// The matched subtitle from database
        /// </summary>
        public required LabelledSubtitle Subtitle { get; set; }

        /// <summary>
        /// Similarity score (0-100)
        /// </summary>
        public int SimilarityScore { get; set; }

        /// <summary>
        /// Confidence level (0.0-1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Which text normalization version was used for the best match
        /// </summary>
        public string MatchVersion { get; set; } = string.Empty;
    }
}