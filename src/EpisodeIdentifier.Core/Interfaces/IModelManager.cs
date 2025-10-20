using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service for managing ONNX model download, caching, and verification.
/// Handles automatic model download on first run with SHA256 verification.
/// </summary>
public interface IModelManager
{
    /// <summary>
    /// Ensure ONNX model is available locally, downloading if necessary.
    /// Downloads from Hugging Face on first run, uses cached version on subsequent runs.
    /// Model is cached in ~/.episodeidentifier/models/ directory.
    /// </summary>
    /// <returns>Task that completes when model is available</returns>
    /// <exception cref="HttpRequestException">Thrown when download fails</exception>
    /// <exception cref="InvalidOperationException">Thrown when model verification fails</exception>
    Task EnsureModelAvailable();

    /// <summary>
    /// Load model metadata from disk.
    /// Reads model file and tokenizer to extract information.
    /// </summary>
    /// <returns>Model information including paths, size, and verification hash</returns>
    /// <exception cref="FileNotFoundException">Thrown when model files not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when model format is invalid</exception>
    Task<ModelInfo> LoadModel();

    /// <summary>
    /// Get information about the currently managed model.
    /// Returns cached information without re-reading from disk.
    /// </summary>
    /// <returns>Model metadata, or null if not yet loaded</returns>
    ModelInfo? GetModelInfo();

    /// <summary>
    /// Delete cached model files from disk.
    /// Forces re-download on next EnsureModelAvailable() call.
    /// </summary>
    /// <returns>Task that completes when files are deleted</returns>
    Task DeleteCachedModel();

    /// <summary>
    /// Verify integrity of model file using SHA256 hash.
    /// Compares actual file hash against expected hash.
    /// </summary>
    /// <param name="modelPath">Path to ONNX model file to verify</param>
    /// <returns>True if hash matches, false otherwise</returns>
    /// <exception cref="FileNotFoundException">Thrown when model file not found</exception>
    Task<bool> VerifyModel(string modelPath);

    /// <summary>
    /// Download model file from URL to specified destination.
    /// Shows progress during download for large files.
    /// </summary>
    /// <param name="url">URL to download model from (typically Hugging Face)</param>
    /// <param name="destinationPath">Local path to save model file</param>
    /// <returns>Task that completes when download finishes</returns>
    /// <exception cref="HttpRequestException">Thrown when download fails</exception>
    /// <exception cref="IOException">Thrown when file write fails</exception>
    Task DownloadModel(string url, string destinationPath);
}
