using EpisodeIdentifier.Core.Models.Hashing;

namespace EpisodeIdentifier.Core.Interfaces
{
    /// <summary>
    /// Service for Context Triggered Piecewise Hashing (CTPH) operations using ssdeep fuzzy hashing
    /// </summary>
    public interface ICTPhHashingService
    {
        /// <summary>
        /// Computes the CTPH fuzzy hash for a file
        /// </summary>
        /// <param name="filePath">Path to the file to hash</param>
        /// <returns>The fuzzy hash string, or empty string on error</returns>
        Task<string> ComputeFuzzyHash(string filePath);

        /// <summary>
        /// Compares two files and returns detailed comparison results
        /// </summary>
        /// <param name="filePath1">Path to the first file</param>
        /// <param name="filePath2">Path to the second file</param>
        /// <returns>Detailed comparison result including hashes and similarity</returns>
        Task<FileComparisonResult> CompareFiles(string filePath1, string filePath2);

        /// <summary>
        /// Compares two existing fuzzy hashes and returns similarity score
        /// </summary>
        /// <param name="hash1">First fuzzy hash</param>
        /// <param name="hash2">Second fuzzy hash</param>
        /// <returns>Similarity score (0-100), or 0 on error</returns>
        int CompareFuzzyHashes(string hash1, string hash2);

        /// <summary>
        /// Gets the current similarity threshold used for determining matches
        /// </summary>
        /// <returns>The similarity threshold (0-100)</returns>
        int GetSimilarityThreshold();

        /// <summary>
        /// Compares a file against database with text fallback when CTPH similarity is below threshold
        /// </summary>
        /// <param name="filePath">Path to the file to compare</param>
        /// <param name="enableTextFallback">Whether to enable text search fallback</param>
        /// <returns>Detailed comparison result with potential text fallback information</returns>
        Task<FileComparisonResult> CompareFileWithFallback(string filePath, bool enableTextFallback = true);
    }
}