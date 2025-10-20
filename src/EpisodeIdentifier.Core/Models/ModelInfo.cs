namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Information about the loaded embedding model and tokenizer.
/// Returned by IModelManager and IEmbeddingService.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Model name (e.g., "all-MiniLM-L6-v2").
    /// </summary>
    public string ModelName { get; init; }

    /// <summary>
    /// Model version or variant (e.g., "fp16", "int8", "full").
    /// </summary>
    public string Variant { get; init; }

    /// <summary>
    /// Embedding dimension (should be 384 for all-MiniLM-L6-v2).
    /// </summary>
    public int Dimension { get; init; }

    /// <summary>
    /// File path to ONNX model file.
    /// </summary>
    public string ModelPath { get; init; }

    /// <summary>
    /// File path to tokenizer JSON file.
    /// </summary>
    public string TokenizerPath { get; init; }

    /// <summary>
    /// SHA256 hash of model file for verification.
    /// Used to detect corruption or tampering.
    /// </summary>
    public string? Sha256Hash { get; init; }

    /// <summary>
    /// Model file size in bytes.
    /// </summary>
    public long ModelSizeBytes { get; init; }

    /// <summary>
    /// When model was downloaded or last verified.
    /// </summary>
    public DateTime LastVerified { get; init; }

    public ModelInfo(
        string modelName,
        string variant,
        int dimension,
        string modelPath,
        string tokenizerPath,
        string? sha256Hash,
        long modelSizeBytes,
        DateTime lastVerified)
    {
        ModelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        Variant = variant ?? throw new ArgumentNullException(nameof(variant));
        Dimension = dimension;
        ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        TokenizerPath = tokenizerPath ?? throw new ArgumentNullException(nameof(tokenizerPath));
        Sha256Hash = sha256Hash;
        ModelSizeBytes = modelSizeBytes;
        LastVerified = lastVerified;
    }

    /// <summary>
    /// Get a human-readable description of the model.
    /// </summary>
    public string GetDescription()
    {
        return $"{ModelName} ({Variant}) - {Dimension}D embeddings - {ModelSizeBytes / 1024 / 1024}MB";
    }
}
