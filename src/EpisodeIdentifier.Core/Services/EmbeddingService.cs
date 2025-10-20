using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

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
    private Tokenizer? _tokenizer;
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

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Generating embedding for text ({Length} chars)", cleanText.Length);

        try
        {
            // Tokenize the text using BERT tokenizer
            if (_tokenizer == null)
            {
                throw new InvalidOperationException("Tokenizer not initialized");
            }

            // Use EncodeToIds to tokenize the text
            var result = _tokenizer.EncodeToIds(cleanText);
            
            // Convert to long arrays for ONNX
            const int maxSeqLength = 512;
            var tokenIds = result.Take(maxSeqLength).Select(id => (long)id).ToArray();
            var attentionMask = Enumerable.Repeat(1L, tokenIds.Length).ToArray();
            
            if (result.Count > maxSeqLength) _logger.LogDebug("Truncated input from {Original} to {Max} tokens", result.Count, maxSeqLength);
            // Create token_type_ids (all zeros for single sentence)
            var tokenTypeIds = new long[tokenIds.Length];

            // Create input tensors
            var inputIdsTensor = new DenseTensor<long>(tokenIds, new[] { 1, tokenIds.Length });
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

            // Run inference with all three required inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
            };

            using var results = _session!.Run(inputs);
            
            // Extract embedding from output (typically "last_hidden_state" or "sentence_embedding")
            // For sentence transformers, we need to pool the token embeddings
            var output = results.First().AsEnumerable<float>().ToArray();
            
            // Apply mean pooling to get sentence embedding
            var embedding = ApplyMeanPooling(output, tokenIds.Length);

            stopwatch.Stop();
            _logger.LogInformation(
                "Generated {Dimensions}-dim embedding in {ElapsedMs}ms (throughput: {CharsPerSec:F1} chars/sec)",
                embedding.Length, stopwatch.ElapsedMilliseconds, cleanText.Length * 1000.0 / Math.Max(1, stopwatch.ElapsedMilliseconds));

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

        var batchStopwatch = Stopwatch.StartNew();
        var embeddings = new List<float[]>();
        
        // For now, process sequentially
        // TODO: Implement true batch processing with dynamic batching for better performance
        foreach (var text in cleanTexts)
        {
            embeddings.Add(GenerateEmbedding(text));
        }

        batchStopwatch.Stop();
        _logger.LogInformation(
            "Batch generated {Count} embeddings in {ElapsedMs}ms (avg: {AvgMs:F1}ms per embedding)",
            embeddings.Count, batchStopwatch.ElapsedMilliseconds, batchStopwatch.ElapsedMilliseconds / (double)Math.Max(1, embeddings.Count));

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
            // Load tokenizer from vocab.txt with explicit special tokens
            _logger.LogInformation("Loading BERT tokenizer from {TokenizerPath}", modelInfo.TokenizerPath);
            
            // all-MiniLM-L6-v2 uses WordPiece tokenization with standard BERT special tokens
            var options = new BertOptions
            {
                UnknownToken = "[UNK]",
                SeparatorToken = "[SEP]",
                ClassificationToken = "[CLS]"
            };
            
            _tokenizer = BertTokenizer.CreateAsync(modelInfo.TokenizerPath, options).GetAwaiter().GetResult();

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
