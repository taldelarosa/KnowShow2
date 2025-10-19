# KnowShow_Specd Development Guidelines


Auto-generated from all feature plans. Last updated: 2025-10-19

## Active Technologies


- C# .NET 8.0, System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
- SQLite database for hash storage, JSON configuration with hot-reload
- xUnit testing framework with TDD approach
- (008-fuzzy-hashing-plus) CTPH fuzzy hashing for subtitle matching
- (010-async-processing-where) Configurable concurrent episode identification
- (012-process-dvd-subtitle) DVD subtitle (VobSub) OCR support with mkvextract + Tesseract
  - VobSubExtractor: Extracts .idx/.sub files from MKV containers
  - VobSubOcrService: Performs OCR on VobSub subtitle images
  - Subtitle priority: Text > PGS > DVD
- (013-ml-embedding-matching) ML embedding-based semantic similarity matching
  - Microsoft.ML.OnnxRuntime: all-MiniLM-L6-v2 model for 384-dimensional embeddings
  - Microsoft.ML.Tokenizers: BPE tokenization for sentence transformers
  - vectorlite SQLite extension: Fast cosine similarity search with HNSW indexing
  - Solves OCR subtitle matching (VobSub/PGS → Text) with >85% confidence
  - EmbeddingService, VectorSearchService, ModelManager for inference and vector search

## Project Structure


```
src/
  EpisodeIdentifier.Core/
    Models/
      BulkProcessingOptions.cs          # Contains MaxConcurrency property
      VobSubExtractionResult.cs         # DVD subtitle extraction results
      VobSubOcrResult.cs                # DVD subtitle OCR results
      SubtitleEmbedding.cs              # 384-dim embedding vector model
      SubtitleFormat.cs                 # Enum: Text/PGS/VobSub
      VectorSimilarityResult.cs         # Vector search result with metadata
      ModelInfo.cs                      # ONNX model metadata
      Configuration/
        EmbeddingMatchThresholds.cs     # Per-format embedding thresholds
    Services/
      VobSubExtractor.cs                # DVD subtitle extraction service
      VobSubOcrService.cs               # DVD subtitle OCR service
      EmbeddingService.cs               # Generate embeddings via ONNX Runtime
      VectorSearchService.cs            # Vector search via vectorlite extension
      ModelManager.cs                   # Model download and caching
    Interfaces/
      IVobSubExtractor.cs               # DVD subtitle extraction contract
      IVobSubOcrService.cs              # DVD subtitle OCR contract
      IEmbeddingService.cs              # Embedding generation contract
      IVectorSearchService.cs           # Vector search contract
      IModelManager.cs                  # Model management contract
    Program.cs                          # CLI entry point
tests/
  unit/
  integration/
  contract/
    VobSubExtractorContractTests.cs     # VobSub extractor tests
    VobSubOcrServiceContractTests.cs    # VobSub OCR tests
    EmbeddingServiceContractTests.cs    # Embedding generation tests
    VectorSearchServiceContractTests.cs # Vector search tests
    ModelManagerContractTests.cs        # Model download tests
specs/
  010-async-processing-where/
  012-process-dvd-subtitle/
  013-ml-embedding-matching/   # Current feature: ML embedding matching
    spec.md                     # Feature specification
    plan.md                     # Implementation plan
    research.md                 # Phase 0: ONNX model, vectorlite, tokenization
    data-model.md               # Phase 1: SubtitleEmbedding, VectorSimilarityResult
    quickstart.md               # Criminal Minds S06E19 validation
    contracts/
      embedding-service.json    # IEmbeddingService contract
      vector-search-service.json # IVectorSearchService contract
      model-manager.json        # IModelManager contract
```


## Commands


### Bulk Processing


- `--bulk-identify <directory>` - Process multiple files with configurable concurrency
- Reads maxConcurrency from episodeidentifier.config.json (default: 1, range: 1-100)
- Supports hot-reload of configuration during processing

### Subtitle Processing


- Text subtitles (SRT, ASS, WebVTT): Processed directly (Priority 1)
- PGS subtitles: OCR using pgsrip + Tesseract (Priority 2)
- DVD subtitles (VobSub): Conversion using vobsub2srt tool (Priority 3)
- Requires: mkvextract (mkvtoolnix), vobsub2srt, tesseract-ocr for DVD subtitle support

### Configuration


- **MatchingStrategy**: Choose between "embedding", "fuzzy", or "hybrid" matching
  - `embedding`: Use ML embeddings with cosine similarity (default for 013+)
  - `fuzzy`: Use CTPH fuzzy hashing (legacy, fallback)
  - `hybrid`: Try embedding first, fallback to fuzzy if confidence low
  
- **EmbeddingThresholds**: Per-format thresholds for embedding-based matching
  - Each subtitle type (textBased, pgs, vobSub) has:
    - `embedSimilarity` (0.0-1.0): Minimum cosine similarity for match candidate
    - `matchConfidence` (0.0-1.0): Minimum confidence to report match
    - `renameConfidence` (0.0-1.0): Minimum confidence for auto-renaming
  - Higher thresholds for text (0.85), lower for VobSub (0.75) due to OCR errors
  - Example config:
    ```json
    "matchingStrategy": "embedding",
    "embeddingThresholds": {
      "textBased": { "embedSimilarity": 0.85, "matchConfidence": 0.70, "renameConfidence": 0.80 },
      "pgs": { "embedSimilarity": 0.80, "matchConfidence": 0.60, "renameConfidence": 0.70 },
      "vobSub": { "embedSimilarity": 0.75, "matchConfidence": 0.50, "renameConfidence": 0.60 }
    }
    ```

- **MatchingThresholds**: Legacy fuzzy hash thresholds (still supported for fallback)
  - Each subtitle type has:
    - `matchConfidence` (0.0-1.0): Minimum confidence to report a match
    - `renameConfidence` (0.0-1.0): Minimum confidence for auto-renaming
    - `fuzzyHashSimilarity` (0-100): CTPH hash similarity threshold

## Code Style


: Follow standard conventions

## Recent Changes


- 010-async-processing-where: Implemented configurable concurrent episode identification
- 012-process-dvd-subtitle: Added DVD subtitle (VobSub) OCR support with VobSubExtractor and VobSubOcrService
- 013-ml-embedding-matching: Added ML embedding-based semantic similarity matching
  - Replaces CTPH fuzzy hashing with 384-dimensional embeddings from all-MiniLM-L6-v2
  - Solves VobSub OCR matching problem (0% fuzzy hash → >85% embedding similarity)
  - Uses vectorlite SQLite extension for fast cosine similarity search with HNSW indexing
  - Automatic model download on first run (45MB), cross-platform support
  - Database migration: generates embeddings for existing CleanText entries
  - Per-format thresholds in embeddingThresholds configuration section
  - New services: EmbeddingService, VectorSearchService, ModelManager

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
