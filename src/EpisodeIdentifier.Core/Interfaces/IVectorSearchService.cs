using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service for vector similarity search using vectorlite SQLite extension.
/// Performs fast cosine similarity search with HNSW indexing.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Search for similar subtitles using cosine similarity on embeddings.
    /// Returns top K most similar results ranked by similarity score.
    /// </summary>
    /// <param name="queryEmbedding">384-dimensional query embedding to search for</param>
    /// <param name="topK">Number of top results to return (default: 10)</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0-1.0) to include in results</param>
    /// <param name="seriesFilter">Optional series name to filter results (case-insensitive)</param>
    /// <param name="seasonFilter">Optional season string (e.g., "09") to filter results</param>
    /// <returns>List of matching subtitles ordered by similarity (highest first)</returns>
    /// <exception cref="ArgumentNullException">Thrown when queryEmbedding is null</exception>
    /// <exception cref="ArgumentException">Thrown when queryEmbedding is not 384 dimensions or topK is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when vectorlite extension not loaded</exception>
    List<VectorSimilarityResult> SearchBySimilarity(
        float[] queryEmbedding, 
        int topK = 10, 
        double minSimilarity = 0.0,
        string? seriesFilter = null,
        string? seasonFilter = null);

    /// <summary>
    /// Check if vectorlite SQLite extension is loaded and operational.
    /// </summary>
    /// <returns>True if extension is loaded, false otherwise</returns>
    bool IsVectorliteLoaded();

    /// <summary>
    /// Get statistics about the vector index (entry count, dimension, etc.).
    /// </summary>
    /// <returns>Index statistics including size, dimension, and last rebuild time</returns>
    VectorIndexStats GetIndexStats();

    /// <summary>
    /// Rebuild the vector index from SubtitleHashes.Embedding column.
    /// Should be called after bulk database updates or migration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when vectorlite extension not loaded</exception>
    void RebuildIndex();
}

/// <summary>
/// Statistics about the vector similarity index.
/// </summary>
public class VectorIndexStats
{
    /// <summary>
    /// Total number of vectors in the index.
    /// </summary>
    public int TotalVectors { get; init; }

    /// <summary>
    /// Embedding dimension (should be 384).
    /// </summary>
    public int Dimension { get; init; }

    /// <summary>
    /// When the index was last rebuilt.
    /// </summary>
    public DateTime? LastRebuild { get; init; }

    /// <summary>
    /// Index size in bytes (approximate).
    /// </summary>
    public long IndexSizeBytes { get; init; }
}
