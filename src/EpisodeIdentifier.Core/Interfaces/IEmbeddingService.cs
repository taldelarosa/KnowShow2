using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service for generating semantic embeddings from subtitle text using ONNX sentence transformer model.
/// Implements all-MiniLM-L6-v2 model to generate 384-dimensional embeddings.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate 384-dimensional embedding from cleaned subtitle text.
    /// Uses ONNX Runtime to perform inference with all-MiniLM-L6-v2 model.
    /// </summary>
    /// <param name="cleanText">Cleaned subtitle text (CleanText from SubtitleNormalizationService)</param>
    /// <returns>384-dimensional float32 array representing semantic content</returns>
    /// <exception cref="ArgumentNullException">Thrown when cleanText is null</exception>
    /// <exception cref="ArgumentException">Thrown when cleanText is empty or whitespace</exception>
    /// <exception cref="InvalidOperationException">Thrown when ONNX model not loaded or tokenizer unavailable</exception>
    float[] GenerateEmbedding(string cleanText);

    /// <summary>
    /// Generate embeddings for multiple subtitle texts in batch.
    /// More efficient than individual calls due to batching optimizations.
    /// </summary>
    /// <param name="cleanTexts">List of cleaned subtitle texts to embed</param>
    /// <returns>List of 384-dimensional embeddings in same order as input</returns>
    /// <exception cref="ArgumentNullException">Thrown when cleanTexts is null</exception>
    /// <exception cref="ArgumentException">Thrown when cleanTexts is empty or contains null/empty entries</exception>
    /// <exception cref="InvalidOperationException">Thrown when ONNX model not loaded</exception>
    List<float[]> BatchGenerateEmbeddings(List<string> cleanTexts);

    /// <summary>
    /// Check if ONNX model and tokenizer are loaded and ready for inference.
    /// </summary>
    /// <returns>True if model is loaded and operational, false otherwise</returns>
    bool IsModelLoaded();

    /// <summary>
    /// Get information about the loaded embedding model.
    /// Includes model name, variant, dimension, file paths, and verification info.
    /// </summary>
    /// <returns>Model metadata, or null if model not yet loaded</returns>
    ModelInfo? GetModelInfo();
}
