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
                ComparisonTime = comparisonTime
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