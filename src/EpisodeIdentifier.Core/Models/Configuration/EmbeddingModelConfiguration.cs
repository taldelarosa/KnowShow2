using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Configuration for the ML embedding model (sentence transformer).
/// Defines model download URLs, verification hashes, and model properties.
/// </summary>
public class EmbeddingModelConfiguration
{
    /// <summary>
    /// Name of the embedding model (e.g., "all-MiniLM-L6-v2").
    /// Used for display and logging purposes.
    /// </summary>
    [Required]
    public string Name { get; set; } = "all-MiniLM-L6-v2";

    /// <summary>
    /// URL to download the ONNX model file.
    /// Should point to a Hugging Face model repository or similar trusted source.
    /// </summary>
    [Required]
    [Url]
    public string ModelUrl { get; set; } = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";

    /// <summary>
    /// URL to download the tokenizer configuration file.
    /// Must be compatible with the model (typically from same repository).
    /// </summary>
    [Required]
    [Url]
    public string TokenizerUrl { get; set; } = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json";

    /// <summary>
    /// SHA256 hash of the model file for integrity verification.
    /// Set to "SKIP" to bypass verification (not recommended for production).
    /// </summary>
    [Required]
    public string ModelSha256 { get; set; } = "PLACEHOLDER_UPDATE_AFTER_DOWNLOAD";

    /// <summary>
    /// SHA256 hash of the tokenizer file for integrity verification.
    /// Set to "SKIP" to bypass verification (not recommended for production).
    /// </summary>
    [Required]
    public string TokenizerSha256 { get; set; } = "PLACEHOLDER_UPDATE_AFTER_DOWNLOAD";

    /// <summary>
    /// Number of dimensions in the embedding vectors produced by this model.
    /// Must match the model's actual output dimension (typically 384 for MiniLM-L6).
    /// </summary>
    [Range(1, 4096)]
    public int Dimensions { get; set; } = 384;

    /// <summary>
    /// Maximum number of tokens the model can process in a single input.
    /// Longer texts will be truncated. Typical value for BERT models is 512.
    /// </summary>
    [Range(1, 8192)]
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Gets the default embedding model configuration.
    /// Used as fallback when configuration is not provided.
    /// </summary>
    public static EmbeddingModelConfiguration Default => new()
    {
        Name = "all-MiniLM-L6-v2",
        ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx",
        TokenizerUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json",
        ModelSha256 = "SKIP", // Skip verification for default config
        TokenizerSha256 = "SKIP",
        Dimensions = 384,
        MaxTokens = 512
    };
}
