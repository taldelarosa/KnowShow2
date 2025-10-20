# Research: ML Embedding-Based Subtitle Matching

**Feature**: 013-ml-embedding-matching  
**Date**: October 19, 2025  
**Phase**: 0 (Research)

---

## Executive Summary

This document consolidates research findings for implementing ML embedding-based subtitle matching using ONNX Runtime and vectorlite SQLite extension. Key decisions include using Hugging Face's ONNX exports for all-MiniLM-L6-v2, Microsoft.ML.Tokenizers for BPE tokenization, vectorlite as runtime-loadable SQLite extension, and batch migration for existing database entries.

---

## Research Question 1: ONNX Model Selection

### Decision

**Use Hugging Face Optimum-exported all-MiniLM-L6-v2 ONNX model (quantized INT8 or FP16)**

### Rationale

1. **Hugging Face Optimum** provides officially maintained ONNX exports with full tokenizer support
2. **Model Repository**: `sentence-transformers/all-MiniLM-L6-v2` has ONNX variants via Optimum
3. **Quantization Options**:
   - **FP32**: ~90MB, best accuracy, slower inference
   - **FP16**: ~45MB, minimal accuracy loss, 2x faster
   - **INT8**: ~23MB, slight accuracy loss, 4x faster
4. **Recommendation**: Use **FP16** for balance of size (45MB) and accuracy
5. **384-dimensional output** verified in model card
6. **Cross-platform**: ONNX Runtime supports Windows, Linux, macOS

### Alternatives Considered

- **ONNX Model Zoo**: Limited sentence transformer models, less maintained
- **Custom PyTorch Conversion**: Requires Python toolchain, adds complexity
- **Rejected**: Both add unnecessary complexity vs Hugging Face's maintained exports

### Implementation Details

```csharp
// Model download URL (Hugging Face Hub)
const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_fp16.onnx";
const string TokenizerUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json";

// Model storage location (cross-platform)
// Windows: %APPDATA%\EpisodeIdentifier\models\
// Linux: ~/.config/EpisodeIdentifier/models/
string modelPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "EpisodeIdentifier", "models", "all-MiniLM-L6-v2-fp16.onnx"
);
```

### Testing Requirements

- Verify 384-dimensional output from ONNX Runtime
- Test cross-platform model loading (Windows/Linux)
- Benchmark FP16 vs FP32 accuracy on sample subtitles
- Validate model outputs match expected sentence transformer behavior

---

## Research Question 2: vectorlite Integration

### Decision

**Use vectorlite as runtime-loadable SQLite extension with platform-specific loading**

### Rationale

1. **Performance**: 3x-100x faster than brute force, 8x-80x speedup with HNSW index
2. **Cross-platform**: Pre-built binaries for Windows-x64, Linux-x64, macOS-x64, macOS-ARM
3. **SQL Interface**: Native SQLite virtual table, no additional database layer
4. **Cosine Similarity**: Built-in support via `distance_type='cosine'`
5. **HNSW Indexing**: Tunable parameters (M, ef_construction) for accuracy/speed tradeoff
6. **Metadata Filtering**: Supports rowid filtering for series/season constraints

### Extension Loading Strategy

```csharp
// Platform-specific extension paths
string extensionPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "vectorlite.dll"
    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? "vectorlite.dylib"
        : "vectorlite.so";

// Load extension using SQLitePCL.raw
using var connection = new SqliteConnection(connectionString);
connection.Open();

// Enable extension loading
connection.ExecuteNonQuery("PRAGMA trusted_schema = ON;");

// Load vectorlite extension
var loadCommand = connection.CreateCommand();
loadCommand.CommandText = $"SELECT load_extension('{extensionPath}');";
loadCommand.ExecuteScalar();

// Verify vectorlite loaded
var infoCommand = connection.CreateCommand();
infoCommand.CommandText = "SELECT vectorlite_info();";
var info = infoCommand.ExecuteScalar();
_logger.LogInformation("vectorlite loaded: {Info}", info);
```

### Virtual Table Creation

```sql
-- Create vectorlite virtual table with HNSW index
CREATE VIRTUAL TABLE IF NOT EXISTS vector_index USING vectorlite(
    embedding float32[384] cosine,  -- 384-dim, cosine distance
    hnsw(
        max_elements=10000,          -- Initial capacity
        ef_construction=200,         -- Build-time accuracy parameter
        M=16,                        -- HNSW graph connectivity
        random_seed=42,              -- Reproducible builds
        allow_replace_deleted=true   -- Reuse deleted slots
    )
);
```

### Vector Search Query

```sql
-- Find K nearest neighbors with cosine similarity
SELECT rowid, distance 
FROM vector_index 
WHERE knn_search(
    embedding, 
    knn_param(@query_vector, @k, @ef)  -- ef=10 default, higher=more accurate
)
AND rowid IN (
    -- Metadata filter: rowids from SubtitleHashes with specific format
    SELECT Id FROM SubtitleHashes WHERE SubtitleFormat = 'VobSub'
);
```

### Alternatives Considered

- **Custom Vector Search**: Would require implementing HNSW from scratch (complex)
- **sqlite-vec**: Brute force only, 3x-100x slower than vectorlite
- **External Vector DB**: Adds complexity, breaks offline requirement
- **Rejected**: All add complexity or violate performance/offline requirements

### Testing Requirements

- Verify extension loads on Windows and Linux
- Test HNSW index creation and search
- Benchmark search performance with 1000+ vectors
- Validate cosine similarity results match expected values

---

## Research Question 3: Tokenization Approach

### Decision

**Use Microsoft.ML.Tokenizers with Hugging Face tokenizer.json**

### Rationale

1. **Official .NET Library**: Maintained by Microsoft, .NET 8.0 compatible
2. **BPE Support**: Supports Byte-Pair Encoding used by sentence transformers
3. **tokenizer.json Format**: Directly loads Hugging Face tokenizer files
4. **No Python Dependency**: Pure .NET implementation
5. **Special Tokens**: Handles [CLS], [SEP], [PAD] automatically

### Implementation Details

```csharp
using Microsoft.ML.Tokenizers;

public class EmbeddingService
{
    private readonly Tokenizer _tokenizer;
    private readonly InferenceSession _onnxSession;
    
    public EmbeddingService(string tokenizerPath, string modelPath)
    {
        // Load Hugging Face tokenizer
        _tokenizer = Tokenizer.CreateTokenizer(tokenizerPath);
        
        // Load ONNX model
        _onnxSession = new InferenceSession(modelPath);
    }
    
    public float[] GenerateEmbedding(string cleanText)
    {
        // Tokenize text
        var encoding = _tokenizer.Encode(cleanText);
        var inputIds = encoding.Ids.ToArray();
        var attentionMask = encoding.AttentionMask.ToArray();
        
        // Convert to ONNX tensor format
        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
        
        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };
        
        using var results = _onnxSession.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        
        // Extract 384-dimensional embedding (mean pooling)
        return MeanPooling(outputTensor, attentionMask);
    }
    
    private float[] MeanPooling(Tensor<float> output, long[] attentionMask)
    {
        // Apply mean pooling over token embeddings
        var embedding = new float[384];
        var tokenCount = attentionMask.Sum();
        
        for (int i = 0; i < 384; i++)
        {
            float sum = 0;
            for (int j = 0; j < attentionMask.Length; j++)
            {
                if (attentionMask[j] == 1)
                {
                    sum += output[0, j, i];
                }
            }
            embedding[i] = sum / tokenCount;
        }
        
        return embedding;
    }
}
```

### Unicode Normalization

- Hugging Face tokenizers handle normalization internally (NFKC)
- No additional preprocessing required
- Preserves existing CleanText normalization benefits

### Alternatives Considered

- **BlingFire**: Limited BPE support, lacks Hugging Face compatibility
- **Python Interop**: Violates "no Python dependency" constraint
- **Custom Tokenizer**: Complex, error-prone, reinventing wheel
- **Rejected**: All add complexity or violate requirements

### Testing Requirements

- Verify tokenizer loads tokenizer.json correctly
- Test token ID generation from sample text
- Validate special token handling ([CLS], [SEP])
- Compare tokenization output with Python reference implementation

---

## Research Question 4: Migration Strategy

### Decision

**Batch processing with parallel embedding generation and progress logging**

### Rationale

1. **Parallel Processing**: Use `Parallel.ForEachAsync` with controlled concurrency
2. **Batch Size**: Process 100 records per transaction for commit efficiency
3. **Progress Reporting**: Log every 10% completion milestone
4. **Error Handling**: Log errors but continue migration (track failed IDs)
5. **Idempotent**: Skip records that already have embeddings

### Implementation Details

```csharp
public async Task MigrateExistingEntries(int maxConcurrency = 4)
{
    // Get all records without embeddings
    var records = await GetRecordsWithoutEmbeddings();
    var total = records.Count;
    var processed = 0;
    var failed = new ConcurrentBag<int>();
    
    _logger.LogInformation("Starting migration for {Count} records", total);
    
    // Process in parallel batches
    var semaphore = new SemaphoreSlim(maxConcurrency);
    var tasks = new List<Task>();
    
    foreach (var batch in records.Chunk(100))
    {
        tasks.Add(Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessBatch(batch, failed);
                
                Interlocked.Add(ref processed, batch.Length);
                
                // Progress logging every 10%
                var percentComplete = (processed * 100) / total;
                if (percentComplete % 10 == 0)
                {
                    _logger.LogInformation(
                        "Migration progress: {Percent}% ({Processed}/{Total})",
                        percentComplete, processed, total
                    );
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }
    
    await Task.WhenAll(tasks);
    
    _logger.LogInformation(
        "Migration complete: {Success} success, {Failed} failed",
        total - failed.Count, failed.Count
    );
    
    if (failed.Any())
    {
        _logger.LogWarning("Failed IDs: {FailedIds}", string.Join(", ", failed));
    }
}

private async Task ProcessBatch(SubtitleRecord[] batch, ConcurrentBag<int> failed)
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();
    
    using var transaction = connection.BeginTransaction();
    
    foreach (var record in batch)
    {
        try
        {
            // Generate embedding from CleanText
            var embedding = _embeddingService.GenerateEmbedding(record.CleanText);
            
            // Store embedding as BLOB
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE SubtitleHashes 
                SET Embedding = @embedding 
                WHERE Id = @id";
            command.Parameters.AddWithValue("@embedding", SerializeEmbedding(embedding));
            command.Parameters.AddWithValue("@id", record.Id);
            
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate record {Id}", record.Id);
            failed.Add(record.Id);
        }
    }
    
    await transaction.CommitAsync();
}

private byte[] SerializeEmbedding(float[] embedding)
{
    var bytes = new byte[embedding.Length * sizeof(float)];
    Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
    return bytes;
}
```

### Performance Targets

- **Embedding Generation**: <5 seconds per subtitle (single-threaded)
- **Batch Processing**: 4 concurrent workers = ~1 subtitle/1.25 seconds throughput
- **300 Existing Records**: ~6-7 minutes total migration time
- **Memory Usage**: <500MB (4 workers × ~100MB per ONNX session)

### Alternatives Considered

- **Sequential Processing**: Too slow (300 × 5s = 25 minutes)
- **Database-Driven Parallelism**: SQLite doesn't support concurrent writes well
- **External Queue System**: Over-engineered for one-time migration
- **Rejected**: Sequential is too slow, external systems add complexity

### Testing Requirements

- Test migration with 50 sample records
- Verify progress logging accuracy
- Test error handling for malformed CleanText
- Benchmark migration performance (4 workers)
- Validate embeddings generated correctly for all formats

---

## Research Question 5: Model Distribution

### Decision

**Auto-download on first run with local caching and SHA256 verification**

### Rationale

1. **No Bundling**: Keeps application package size minimal (<10MB vs >100MB)
2. **First-Run Download**: Downloads model to user data directory on first `--identify` or `--store`
3. **Version Pinning**: SHA256 hash verification prevents model corruption
4. **Graceful Degradation**: Falls back to fuzzy hashing if model unavailable
5. **Manual Override**: Support `--model-path` CLI argument for custom models

### Implementation Details

```csharp
public class ModelManager : IModelManager
{
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_fp16.onnx";
    private const string TokenizerUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json";
    private const string ModelSha256 = "abc123..."; // TODO: Get actual hash
    
    private readonly string _modelDir;
    private readonly ILogger<ModelManager> _logger;
    private readonly HttpClient _httpClient;
    
    public ModelManager(ILogger<ModelManager> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        
        // Cross-platform model storage
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
        );
        _modelDir = Path.Combine(appDataPath, "EpisodeIdentifier", "models");
        Directory.CreateDirectory(_modelDir);
    }
    
    public async Task<(string ModelPath, string TokenizerPath)> EnsureModelAvailable()
    {
        var modelPath = Path.Combine(_modelDir, "all-MiniLM-L6-v2-fp16.onnx");
        var tokenizerPath = Path.Combine(_modelDir, "tokenizer.json");
        
        // Check if model already cached and valid
        if (File.Exists(modelPath) && VerifySha256(modelPath, ModelSha256))
        {
            _logger.LogInformation("Using cached model: {Path}", modelPath);
            return (modelPath, tokenizerPath);
        }
        
        // Download model
        _logger.LogInformation("Downloading embedding model (45MB)...");
        await DownloadWithProgress(ModelUrl, modelPath);
        
        // Verify download
        if (!VerifySha256(modelPath, ModelSha256))
        {
            throw new InvalidOperationException("Model download failed: SHA256 mismatch");
        }
        
        // Download tokenizer
        _logger.LogInformation("Downloading tokenizer...");
        await DownloadFile(TokenizerUrl, tokenizerPath);
        
        _logger.LogInformation("Model ready: {Path}", modelPath);
        return (modelPath, tokenizerPath);
    }
    
    private async Task DownloadWithProgress(string url, string outputPath)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;
        
        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            downloadedBytes += bytesRead;
            
            // Log progress every 10%
            var percentComplete = (downloadedBytes * 100) / totalBytes;
            if (percentComplete % 10 == 0)
            {
                _logger.LogInformation("Download progress: {Percent}%", percentComplete);
            }
        }
    }
    
    private bool VerifySha256(string filePath, string expectedHash)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        return hash == expectedHash;
    }
}
```

### Configuration Override

```json
{
  "embeddingModel": {
    "autoDownload": true,
    "modelPath": null,  // null = auto-download, or "/custom/path/model.onnx"
    "tokenizerPath": null,
    "sha256": "abc123..."  // For verification
  }
}
```

### Alternatives Considered

- **Bundle with Application**: Bloats installer (10MB → 100MB), wastes bandwidth
- **Require Manual Download**: Poor UX, support burden
- **Use External API**: Violates offline requirement, privacy concerns
- **Rejected**: Bundle bloats size, manual download has poor UX, API breaks offline

### Testing Requirements

- Test model download on clean system
- Verify SHA256 validation catches corrupted downloads
- Test caching (second run uses cached model)
- Test manual model path override
- Handle network failures gracefully (retry logic)

---

## Technology Stack Summary

### Dependencies

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.16.0" />
<PackageReference Include="Microsoft.ML.Tokenizers" Version="0.21.0-preview.23511.1" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
<!-- Existing dependencies -->
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

### External Binaries

- **vectorlite**: Runtime-loaded SQLite extension
    - Windows: `vectorlite.dll`
    - Linux: `vectorlite.so`
    - macOS: `vectorlite.dylib`
- **Distributed via**: Python wheel extraction or direct GitHub release download

### Model Files

- **all-MiniLM-L6-v2-fp16.onnx**: ~45MB, auto-downloaded
- **tokenizer.json**: ~470KB, auto-downloaded
- **Storage**: `%APPDATA%\EpisodeIdentifier\models\` (Windows) or `~/.config/EpisodeIdentifier/models/` (Linux)

---

## Performance Benchmarks

### Expected Performance

| Operation | Target | Notes |
|-----------|--------|-------|
| Embedding Generation | <5s | Single subtitle, FP16 model, CPU |
| Vector Search (1000 entries) | <2s | Cosine similarity with HNSW |
| Database Migration (300 entries) | <7min | 4 parallel workers |
| Memory Usage | <500MB | 4 workers × ONNX session overhead |
| Model Download | ~1min | 45MB @ 6 Mbps connection |

### Accuracy Targets

| Metric | Target | Notes |
|--------|--------|-------|
| VobSub → Text Match | >85% confidence | Criminal Minds S06E19 validation |
| Text → Text Match | >95% confidence | No regression vs fuzzy hashing |
| PGS → Text Match | >80% confidence | OCR quality dependent |
| Embedding Similarity | >0.85 cosine | Same episode, different formats |

---

## Risk Mitigation

### Risk 1: Model Download Failures

- **Mitigation**: Retry logic with exponential backoff (3 attempts)
- **Fallback**: Option to manually download and specify `--model-path`
- **User Messaging**: Clear error messages with download links

### Risk 2: Platform-Specific Extension Loading

- **Mitigation**: Bundle vectorlite binaries for Windows/Linux/macOS in application directory
- **Testing**: CI/CD tests on all platforms before release
- **Documentation**: Platform-specific troubleshooting guide

### Risk 3: Performance Regression

- **Mitigation**: Keep fuzzy hashing as optional fallback mode
- **Benchmarking**: Performance tests in CI/CD pipeline
- **Configuration**: Allow users to disable embeddings via config

### Risk 4: Embedding Accuracy

- **Mitigation**: Test with Criminal Minds S06E19 (known VobSub/Text pair)
- **Validation**: Compare embeddings across formats for same content
- **Tuning**: Configurable similarity thresholds per format

---

## Open Questions Resolved

### Q1: Should we keep fuzzy hashing as fallback?

**A**: **Yes**, keep fuzzy hashing as fallback option via configuration flag:

- Allows A/B testing of matching accuracy
- Provides degradation path if model unavailable
- Supports comparison benchmarking

```json
{
  "matchingStrategy": "embedding",  // Options: "embedding", "fuzzy", "hybrid"
  "embeddingFallback": true  // Use fuzzy hashing if embedding fails
}
```

### Q2: Best ONNX model source?

**A**: **Hugging Face Optimum exports** (see Research Question 1)

### Q3: vectorlite extension loading approach?

**A**: **Platform-specific runtime loading** with bundled binaries (see Research Question 2)

### Q4: Async embedding generation?

**A**: **Yes**, use `Parallel.ForEachAsync` for batch processing with semaphore-controlled concurrency (see Research Question 4)

---

## Next Steps (Phase 1)

1. **Create data-model.md**: Define SubtitleEmbedding, SubtitleFormat, VectorSimilarityResult entities
2. **Generate contracts**: IEmbeddingService, IVectorSearchService, IModelManager interfaces with JSON schemas
3. **Write contract tests**: Failing tests for all three services
4. **Create quickstart.md**: Manual validation steps for Criminal Minds S06E19
5. **Update agent context**: Add ML embedding technologies to `.github/copilot-instructions.md`

---

**Research Complete**: All NEEDS CLARIFICATION items resolved. Ready for Phase 1 (Design & Contracts).
