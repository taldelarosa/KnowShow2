using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for generating semantic embeddings using ONNX Runtime with all-MiniLM-L6-v2 model.
/// Generates 384-dimensional embeddings from subtitle text.
/// </summary>
public class EmbeddingService : IEmbeddingService, IDisposable
{
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IModelManager _modelManager;
    private InferenceSession? _session;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public EmbeddingService(ILogger<EmbeddingService> logger, IModelManager modelManager)
    {
        _logger = logger;
        _modelManager = modelManager;
    }

    /// <inheritdoc/>
    public float[] GenerateEmbedding(string cleanText)
    {
        if (cleanText == null)
        {
            throw new ArgumentNullException(nameof(cleanText));
        }

        if (string.IsNullOrWhiteSpace(cleanText))
        {
            throw new ArgumentException("Text cannot be empty or whitespace", nameof(cleanText));
        }

        EnsureModelInitialized();

        _logger.LogDebug("Generating embedding for text ({Length} chars)", cleanText.Length);

        try
        {
            // Tokenize the text
            var tokenIds = TokenizeText(cleanText);
            var attentionMask = CreateAttentionMask(tokenIds.Length);

            // Create input tensors
            var inputIds = new DenseTensor<long>(tokenIds, new[] { 1, tokenIds.Length });
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var results = _session!.Run(inputs);
            
            // Extract embedding from output (typically "last_hidden_state" or "sentence_embedding")
            // For sentence transformers, we need to pool the token embeddings
            var output = results.First().AsEnumerable<float>().ToArray();
            
            // Apply mean pooling to get sentence embedding
            var embedding = ApplyMeanPooling(output, tokenIds.Length);

            _logger.LogDebug("Generated embedding: {Dimensions} dimensions", embedding.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding: {Error}", ex.Message);
            throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public List<float[]> BatchGenerateEmbeddings(List<string> cleanTexts)
    {
        if (cleanTexts == null)
        {
            throw new ArgumentNullException(nameof(cleanTexts));
        }

        if (cleanTexts.Count == 0)
        {
            throw new ArgumentException("Text list cannot be empty", nameof(cleanTexts));
        }

        if (cleanTexts.Any(text => string.IsNullOrWhiteSpace(text)))
        {
            throw new ArgumentException("Text list contains null or empty entries", nameof(cleanTexts));
        }

        _logger.LogInformation("Batch generating embeddings for {Count} texts", cleanTexts.Count);

        var embeddings = new List<float[]>();
        
        // For now, process sequentially
        // TODO: Implement true batch processing with dynamic batching for better performance
        foreach (var text in cleanTexts)
        {
            embeddings.Add(GenerateEmbedding(text));
        }

        return embeddings;
    }

    /// <inheritdoc/>
    public bool IsModelLoaded()
    {
        return _isInitialized && _session != null;
    }

    /// <inheritdoc/>
    public ModelInfo? GetModelInfo()
    {
        return _modelManager.GetModelInfo();
    }

    private void EnsureModelInitialized()
    {
        if (_isInitialized) return;

        _initLock.Wait();
        try
        {
            if (_isInitialized) return;

            _logger.LogInformation("Initializing ONNX Runtime session...");

            // Ensure model is downloaded and available
            _modelManager.EnsureModelAvailable().Wait();

            var modelInfo = _modelManager.GetModelInfo();
            if (modelInfo == null)
            {
                throw new InvalidOperationException("Model info not available after initialization");
            }

            // Create ONNX Runtime session
            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            _session = new InferenceSession(modelInfo.ModelPath, sessionOptions);

            _isInitialized = true;
            _logger.LogInformation("ONNX Runtime session initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX Runtime session: {Error}", ex.Message);
            throw new InvalidOperationException($"Failed to initialize embedding model: {ex.Message}", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private long[] TokenizeText(string text)
    {
        // Simple whitespace tokenization for now
        // TODO: Replace with actual BPE tokenizer using Microsoft.ML.Tokenizers
        // For proof-of-concept, this creates simple token IDs
        
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        var tokenIds = new List<long> { 101 }; // [CLS] token
        
        foreach (var word in words.Take(510)) // Reserve space for [CLS] and [SEP]
        {
            // Simple hash-based token ID generation (placeholder)
            var tokenId = Math.Abs(word.GetHashCode() % 30000) + 1000;
            tokenIds.Add(tokenId);
        }
        
        tokenIds.Add(102); // [SEP] token

        return tokenIds.ToArray();
    }

    private long[] CreateAttentionMask(int sequenceLength)
    {
        // All ones for attention mask (attend to all tokens)
        return Enumerable.Repeat(1L, sequenceLength).ToArray();
    }

    private float[] ApplyMeanPooling(float[] tokenEmbeddings, int sequenceLength)
    {
        // Extract 384-dimensional embedding per token and apply mean pooling
        const int embeddingDim = 384;
        var embedding = new float[embeddingDim];

        // Assuming token embeddings are flattened: [sequence_length, embedding_dim]
        for (int i = 0; i < sequenceLength && i * embeddingDim < tokenEmbeddings.Length; i++)
        {
            for (int j = 0; j < embeddingDim; j++)
            {
                var index = i * embeddingDim + j;
                if (index < tokenEmbeddings.Length)
                {
                    embedding[j] += tokenEmbeddings[index];
                }
            }
        }

        // Average
        for (int j = 0; j < embeddingDim; j++)
        {
            embedding[j] /= sequenceLength;
        }

        // Normalize to unit length (L2 normalization)
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < embeddingDim; i++)
            {
                embedding[i] /= (float)magnitude;
            }
        }

        return embedding;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock?.Dispose();
    }
}
