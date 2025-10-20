# Implementation Tasks: ML Embedding-Based Subtitle Matching

**Feature**: 013-ml-embedding-matching  
**Generated**: October 19, 2025  
**Status**: Ready for execution

---

## Task Overview

Total tasks: 32  
Estimated effort: 3-4 days  
TDD approach: Tests before implementation (RED-GREEN-Refactor)

---

## Phase 3.1: Setup & Preparation

### T001: Create feature branch [P]

**Files**: git
**Type**: Setup
**Parallel**: Yes (can be done independently)
**Description**: Create feature branch `013-ml-embedding-matching` from current branch
**Commands**:

```bash
git checkout -b 013-ml-embedding-matching
git push -u origin 013-ml-embedding-matching
```

**Validation**: Branch exists and is checked out
**Dependencies**: None

### T002: Add NuGet dependencies [P]

**Files**: `src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj`
**Type**: Setup
**Parallel**: Yes (independent of T001)
**Description**: Add required NuGet packages for ML embedding support
**Changes**:

- Add `Microsoft.ML.OnnxRuntime` (latest stable)
- Add `Microsoft.ML.Tokenizers` (latest stable)
**Validation**: `dotnet restore` succeeds, packages downloaded
**Dependencies**: None

### T003: Download vectorlite extension [P]

**Files**: `external/vectorlite/` (new directory)
**Type**: Setup
**Parallel**: Yes (independent of T001-T002)
**Description**: Download platform-specific vectorlite SQLite extensions
**Commands**:

```bash
mkdir -p external/vectorlite
cd external/vectorlite
# Download vectorlite binaries for Windows/Linux
wget https://github.com/1yefuwang1/vectorlite/releases/latest/download/vectorlite-linux-x64.so
wget https://github.com/1yefuwang1/vectorlite/releases/latest/download/vectorlite-win-x64.dll
```

**Validation**: Extension files exist in `external/vectorlite/`
**Dependencies**: None

---

## Phase 3.2: Contract Tests (TDD - RED Phase Setup)

### T004: Create IEmbeddingService contract test [P]

**Files**: `tests/contract/EmbeddingServiceContractTests.cs` (new)
**Type**: Test - Contract
**Parallel**: Yes (different file from T005-T006)
**Description**: Create contract test for IEmbeddingService based on `contracts/embedding-service.json`
**Test Methods**:

- `GenerateEmbedding_ValidText_Returns384Dimensions`
- `GenerateEmbedding_NullText_ThrowsArgumentNullException`
- `GenerateEmbedding_EmptyText_ThrowsArgumentException`
- `BatchGenerateEmbeddings_ValidTexts_ReturnsCorrectOrder`
- `IsModelLoaded_WhenModelNotLoaded_ReturnsFalse`
- `GetModelInfo_WhenModelLoaded_ReturnsMetadata`
**Validation**: File compiles (will have red squiggles for missing interface)
**Dependencies**: T002 (NuGet packages)

### T005: Create IVectorSearchService contract test [P]

**Files**: `tests/contract/VectorSearchServiceContractTests.cs` (new)
**Type**: Test - Contract
**Parallel**: Yes (different file from T004, T006)
**Description**: Create contract test for IVectorSearchService based on `contracts/vector-search-service.json`
**Test Methods**:

- `SearchBySimilarity_ValidEmbedding_ReturnsTopResults`
- `SearchBySimilarity_EmptyDatabase_ReturnsEmptyList`
- `IsVectorliteLoaded_WhenExtensionLoaded_ReturnsTrue`
- `GetIndexStats_ReturnsValidStatistics`
- `RebuildIndex_RegeneratesVectorIndex`
**Validation**: File compiles (will have red squiggles for missing interface)
**Dependencies**: T002 (NuGet packages)

### T006: Create IModelManager contract test [P]

**Files**: `tests/contract/ModelManagerContractTests.cs` (new)
**Type**: Test - Contract
**Parallel**: Yes (different file from T004-T005)
**Description**: Create contract test for IModelManager based on `contracts/model-manager.json`
**Test Methods**:

- `EnsureModelAvailable_FirstRun_DownloadsModel`
- `EnsureModelAvailable_ModelExists_SkipsDownload`
- `VerifyModel_ValidFile_ReturnsTrue`
- `VerifyModel_CorruptedFile_ReturnsFalse`
- `DownloadModel_ValidUrl_SavesFile`
- `GetModelInfo_ValidModel_ReturnsMetadata`
**Validation**: File compiles (will have red squiggles for missing interface)
**Dependencies**: T002 (NuGet packages)

---

## Phase 3.3: Data Models (Library-First)

### T007: Create SubtitleEmbedding model [P]

**Files**: `src/EpisodeIdentifier.Core/Models/SubtitleEmbedding.cs` (new)
**Type**: Implementation - Model
**Parallel**: Yes (different file from T008-T011)
**Description**: Implement SubtitleEmbedding record with 384-dimensional vector
**Implementation**:

- `float[] Vector { get; init; }` (384 elements)
- `string SourceText { get; init; }`
- `double? Similarity { get; init; }`
- `byte[] ToBytes()` - Serialize to BLOB
- `static float[] FromBytes(byte[] bytes)` - Deserialize from BLOB
- `static double CosineSimilarity(float[] a, float[] b)` - Calculate similarity
**Validation**:
- Constructor validates 384 dimensions
- ToBytes/FromBytes round-trip test
- CosineSimilarity test with known vectors
**Dependencies**: None

### T008: Create SubtitleFormat enum [P]

**Files**: `src/EpisodeIdentifier.Core/Models/SubtitleFormat.cs` (new)
**Type**: Implementation - Model
**Parallel**: Yes (different file from T007, T009-T011)
**Description**: Implement SubtitleFormat enum and extension methods
**Implementation**:

- Enum: `Text`, `PGS`, `VobSub`
- `ToDbString()` extension
- `FromDbString(string)` extension
- `GetDefaultMatchConfidence()` extension (Text=0.85, PGS=0.80, VobSub=0.75)
**Validation**: Unit test for each extension method
**Dependencies**: None

### T009: Create VectorSimilarityResult model [P]

**Files**: `src/EpisodeIdentifier.Core/Models/VectorSimilarityResult.cs` (new)
**Type**: Implementation - Model
**Parallel**: Yes (different file from T007-T008, T010-T011)
**Description**: Implement VectorSimilarityResult with search result metadata
**Implementation**:

- Properties: `Id`, `Series`, `Season`, `Episode`, `EpisodeName`, `Format`, `Similarity`, `Confidence`, `Distance`, `Rank`
- `ToLabelledSubtitle()` method for compatibility
**Validation**: Constructor validation test, ToLabelledSubtitle conversion test
**Dependencies**: T008 (SubtitleFormat)

### T010: Create ModelInfo model [P]

**Files**: `src/EpisodeIdentifier.Core/Models/ModelInfo.cs` (new)
**Type**: Implementation - Model
**Parallel**: Yes (different file from T007-T009, T011)
**Description**: Implement ModelInfo record with ONNX model metadata
**Implementation**:

- Properties: `ModelName`, `Variant`, `Dimension`, `ModelPath`, `TokenizerPath`, `Sha256Hash`, `ModelSizeBytes`, `LastVerified`
**Validation**: Constructor test with sample values
**Dependencies**: None

### T011: Create EmbeddingMatchThresholds model [P]

**Files**: `src/EpisodeIdentifier.Core/Models/Configuration/EmbeddingMatchThresholds.cs` (new)
**Type**: Implementation - Model
**Parallel**: Yes (different file from T007-T010)
**Description**: Implement EmbeddingMatchThresholds for per-format configuration
**Implementation**:

- Nested classes: `FormatThreshold` (EmbedSimilarity, MatchConfidence, RenameConfidence)
- Properties: `TextBased`, `Pgs`, `VobSub` (each is FormatThreshold)
- `GetThreshold(SubtitleFormat)` method
**Validation**:
- Default values test (Text=0.85/0.70/0.80, PGS=0.80/0.60/0.70, VobSub=0.75/0.50/0.60)
- GetThreshold test for each format
**Dependencies**: T008 (SubtitleFormat)

---

## Phase 3.4: Database Schema Migration

### T012: Create database migration script

**Files**: `src/EpisodeIdentifier.Core/Data/Migrations/013_add_embedding_columns.sql` (new)
**Type**: Implementation - Database
**Parallel**: No
**Description**: Create SQL migration to add Embedding and SubtitleFormat columns
**Implementation**:

```sql
-- Add embedding storage column (1536 bytes = 384 floats × 4)
ALTER TABLE SubtitleHashes ADD COLUMN Embedding BLOB NULL;

-- Add subtitle format tracking column
ALTER TABLE SubtitleHashes ADD COLUMN SubtitleFormat TEXT NOT NULL DEFAULT 'Text';

-- Create vectorlite virtual table for fast similarity search
CREATE VIRTUAL TABLE IF NOT EXISTS vector_index using vectorlite(
    embedding float32[384],
    hnsw(max_elements=10000, ef_construction=200, M=48)
);
```

**Validation**:

- SQL syntax valid
- Execute migration on test database
- Verify columns exist: `PRAGMA table_info(SubtitleHashes);`
**Dependencies**: None

---

## Phase 3.5: Service Interfaces (Library-First)

### T013: Create IEmbeddingService interface [P]

**Files**: `src/EpisodeIdentifier.Core/Interfaces/IEmbeddingService.cs` (new)
**Type**: Implementation - Interface
**Parallel**: Yes (different file from T014-T015)
**Description**: Define IEmbeddingService interface based on embedding-service.json contract
**Implementation**:

- `float[] GenerateEmbedding(string cleanText)`
- `List<float[]> BatchGenerateEmbeddings(List<string> cleanTexts)`
- `bool IsModelLoaded()`
- `ModelInfo? GetModelInfo()`
**Validation**: Interface compiles, matches contract JSON structure
**Dependencies**: T007 (SubtitleEmbedding), T010 (ModelInfo)

### T014: Create IVectorSearchService interface [P]

**Files**: `src/EpisodeIdentifier.Core/Interfaces/IVectorSearchService.cs` (new)
**Type**: Implementation - Interface
**Parallel**: Yes (different file from T013, T015)
**Description**: Define IVectorSearchService interface based on vector-search-service.json contract
**Implementation**:

- `List<VectorSimilarityResult> SearchBySimilarity(float[] queryEmbedding, int topK = 10)`
- `bool IsVectorliteLoaded()`
- `VectorIndexStats GetIndexStats()`
- `void RebuildIndex()`
**Validation**: Interface compiles, matches contract JSON structure
**Dependencies**: T007 (SubtitleEmbedding), T009 (VectorSimilarityResult)

### T015: Create IModelManager interface [P]

**Files**: `src/EpisodeIdentifier.Core/Interfaces/IModelManager.cs` (new)
**Type**: Implementation - Interface
**Parallel**: Yes (different file from T013-T014)
**Description**: Define IModelManager interface based on model-manager.json contract
**Implementation**:

- `Task EnsureModelAvailable()`
- `Task<ModelInfo> LoadModel()`
- `ModelInfo? GetModelInfo()`
- `Task DeleteCachedModel()`
- `Task<bool> VerifyModel(string modelPath)`
- `Task DownloadModel(string url, string destinationPath)`
**Validation**: Interface compiles, matches contract JSON structure
**Dependencies**: T010 (ModelInfo)

---

## Phase 3.6: TDD RED Phase - Make Tests Fail

### T016: Run EmbeddingServiceContractTests (should FAIL)

**Files**: Tests from T004
**Type**: Test - RED Phase
**Parallel**: No
**Description**: Run EmbeddingServiceContractTests to verify tests fail (no implementation yet)
**Commands**:

```bash
dotnet test --filter "FullyQualifiedName~EmbeddingServiceContractTests"
```

**Expected Result**: ALL TESTS FAIL (no implementation exists)
**Validation**: Test output shows failures with "not implemented" or similar errors
**Dependencies**: T004 (contract tests), T013 (interface)

### T017: Run VectorSearchServiceContractTests (should FAIL)

**Files**: Tests from T005
**Type**: Test - RED Phase
**Parallel**: No
**Description**: Run VectorSearchServiceContractTests to verify tests fail
**Commands**:

```bash
dotnet test --filter "FullyQualifiedName~VectorSearchServiceContractTests"
```

**Expected Result**: ALL TESTS FAIL (no implementation exists)
**Validation**: Test output shows failures
**Dependencies**: T005 (contract tests), T014 (interface)

### T018: Run ModelManagerContractTests (should FAIL)

**Files**: Tests from T006
**Type**: Test - RED Phase
**Parallel**: No
**Description**: Run ModelManagerContractTests to verify tests fail
**Commands**:

```bash
dotnet test --filter "FullyQualifiedName~ModelManagerContractTests"
```

**Expected Result**: ALL TESTS FAIL (no implementation exists)
**Validation**: Test output shows failures
**Dependencies**: T006 (contract tests), T015 (interface)

---

## Phase 3.7: Service Implementations (TDD GREEN Phase)

### T019: Implement ModelManager service

**Files**: `src/EpisodeIdentifier.Core/Services/ModelManager.cs` (new)
**Type**: Implementation - Service
**Parallel**: No
**Description**: Implement IModelManager to download and verify ONNX model
**Implementation**:

- `EnsureModelAvailable()`: Check cache, download if missing from Hugging Face
- `LoadModel()`: Load model metadata from disk
- `VerifyModel()`: Check file size and SHA256 hash
- `DownloadModel()`: HTTP download with progress reporting
- Model URL: `https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_fp16.onnx`
- Cache location: `~/.episodeidentifier/models/all-MiniLM-L6-v2/`
**Validation**:
- Run T018 tests - should now PASS
- Verify model downloads on first run
**Dependencies**: T015 (interface), T018 (RED phase)

### T020: Implement EmbeddingService

**Files**: `src/EpisodeIdentifier.Core/Services/EmbeddingService.cs` (new)
**Type**: Implementation - Service
**Parallel**: No
**Description**: Implement IEmbeddingService to generate embeddings via ONNX Runtime
**Implementation**:

- Constructor: Inject IModelManager, initialize ONNX InferenceSession
- `GenerateEmbedding()`: Tokenize text → Run ONNX inference → Return 384-dim vector
- `BatchGenerateEmbeddings()`: Process multiple texts in batch
- `IsModelLoaded()`: Check InferenceSession is not null
- Use `Microsoft.ML.Tokenizers` for BPE tokenization (no Python dependency)
**Validation**:
- Run T016 tests - should now PASS
- Test with sample text: "Hello world" → 384-dim output
**Dependencies**: T013 (interface), T019 (ModelManager), T016 (RED phase)

### T021: Implement VectorSearchService

**Files**: `src/EpisodeIdentifier.Core/Services/VectorSearchService.cs` (new)
**Type**: Implementation - Service
**Parallel**: No
**Description**: Implement IVectorSearchService to search embeddings using vectorlite
**Implementation**:

- Constructor: Inject database connection, load vectorlite extension
- `SearchBySimilarity()`: Query vector_index virtual table for top K matches
- `IsVectorliteLoaded()`: Test if extension loaded successfully
- `GetIndexStats()`: Query index metadata (count, dimension)
- `RebuildIndex()`: Drop and recreate vector_index from SubtitleHashes.Embedding
- Platform-specific extension loading (Windows `.dll` vs Linux `.so`)
**Validation**:
- Run T017 tests - should now PASS
- Test search with known embedding → Returns results
**Dependencies**: T014 (interface), T003 (vectorlite extension), T017 (RED phase)

---

## Phase 3.8: Integration Tests

### T022: Create VobSub OCR matching integration test [P]

**Files**: `tests/integration/EmbeddingMatchingIntegrationTests.cs` (new or extend existing)
**Type**: Test - Integration
**Parallel**: Yes (different test scenario from T023-T025)
**Description**: Test US-1: VobSub OCR matches text subtitle with >85% confidence
**Test Scenario**:

- Store Criminal Minds S06E19 text subtitle (reference)
- Extract VobSub from MKV (simulated or test fixture)
- Run OCR on VobSub
- Generate embedding from OCR text
- Search vector index → Should match S06E19 with >85% confidence
**Validation**: Test passes, confidence >85%
**Dependencies**: T020 (EmbeddingService), T021 (VectorSearchService)

### T023: Create database migration integration test [P]

**Files**: `tests/integration/DatabaseMigrationTests.cs` (new or extend existing)
**Type**: Test - Integration
**Parallel**: Yes (different test scenario from T022, T024-T025)
**Description**: Test US-2: Migrate existing database with embedding generation
**Test Scenario**:

- Create test database with 5 subtitle entries (no embeddings)
- Run migration service to generate embeddings
- Verify all 5 entries now have Embedding BLOB (1536 bytes each)
- Verify migration completes <30 seconds (5 entries)
**Validation**: Test passes, all entries have embeddings
**Dependencies**: T020 (EmbeddingService), T012 (migration script)

### T024: Create threshold configuration integration test [P]

**Files**: `tests/integration/ThresholdConfigurationTests.cs` (new)
**Type**: Test - Integration
**Parallel**: Yes (different test scenario from T022-T023, T025)
**Description**: Test US-3: Per-format thresholds applied correctly
**Test Scenario**:

- Configure different thresholds for Text (0.85), PGS (0.80), VobSub (0.75)
- Test matching with each format type
- Verify correct threshold applied (e.g., VobSub match at 0.76 similarity → passes)
**Validation**: Test passes, thresholds enforced per format
**Dependencies**: T011 (EmbeddingMatchThresholds), T021 (VectorSearchService)

### T025: Create CLI compatibility integration test [P]

**Files**: `tests/integration/CliCompatibilityTests.cs` (new or extend existing)
**Type**: Test - Integration
**Parallel**: Yes (different test scenario from T022-T024)
**Description**: Test US-4: CLI commands work with embedding-based matching
**Test Scenario**:

- Run `--store` command → Embedding generated automatically
- Run `--identify` command → Uses embedding matching (if configured)
- Run `--bulk-identify` command → Concurrent embedding generation
- Verify all existing tests still pass (no regressions)
**Validation**: Test passes, CLI commands functional
**Dependencies**: T020 (EmbeddingService), T021 (VectorSearchService)

---

## Phase 3.9: Configuration & Wiring

### T026: Update configuration schema

**Files**:

- `episodeidentifier.config.template.json`
- `episodeidentifier.config.example.json`
- `src/EpisodeIdentifier.Core/Models/Configuration/AppConfiguration.cs` (extend existing)
**Type**: Implementation - Configuration
**Parallel**: No
**Description**: Add embedding configuration to JSON config files
**Changes**:

```json
{
  "matchingStrategy": "embedding",  // NEW: "embedding", "fuzzy", or "hybrid"
  "embeddingThresholds": {          // NEW: Per-format thresholds
    "textBased": {
      "embedSimilarity": 0.85,
      "matchConfidence": 0.70,
      "renameConfidence": 0.80
    },
    "pgs": {
      "embedSimilarity": 0.80,
      "matchConfidence": 0.60,
      "renameConfidence": 0.70
    },
    "vobSub": {
      "embedSimilarity": 0.75,
      "matchConfidence": 0.50,
      "renameConfidence": 0.60
    }
  }
}
```

**Validation**:

- JSON schema valid
- Config hot-reload test
- AppConfiguration deserializes correctly
**Dependencies**: T011 (EmbeddingMatchThresholds)

### T027: Wire up dependency injection

**Files**: `src/EpisodeIdentifier.Core/Program.cs` (extend existing)
**Type**: Implementation - DI Wiring
**Parallel**: No
**Description**: Register embedding services in DI container
**Changes**:

- Register `IModelManager` → `ModelManager` (singleton)
- Register `IEmbeddingService` → `EmbeddingService` (singleton)
- Register `IVectorSearchService` → `VectorSearchService` (singleton)
- Inject into `EpisodeIdentificationService`
**Validation**:
- Application starts without DI errors
- Services resolve correctly
**Dependencies**: T019-T021 (service implementations)

---

## Phase 3.10: Integration & Matching Logic

### T028: Implement embedding-based matching in EpisodeIdentificationService

**Files**: `src/EpisodeIdentifier.Core/Services/EpisodeIdentificationService.cs` (extend existing)
**Type**: Implementation - Core Logic
**Parallel**: No
**Description**: Add embedding matching path to identification logic
**Implementation**:

- Check `matchingStrategy` config (embedding/fuzzy/hybrid)
- If embedding: Generate embedding → Search vector index → Apply format-specific threshold
- If hybrid: Try embedding first, fallback to fuzzy if confidence too low
- Store embedding when using `--store` command
- Update matching confidence calculation to use embedding similarity
**Validation**:
- Existing fuzzy hash tests still pass (backward compatibility)
- New embedding tests pass
**Dependencies**: T020 (EmbeddingService), T021 (VectorSearchService), T026 (config)

### T029: Implement database migration service

**Files**: `src/EpisodeIdentifier.Core/Services/DatabaseMigrationService.cs` (new or extend existing)
**Type**: Implementation - Migration
**Parallel**: No
**Description**: Service to generate embeddings for existing database entries
**Implementation**:

- Query SubtitleHashes WHERE Embedding IS NULL
- Batch process (4 workers) to generate embeddings from CleanText
- Update SubtitleHashes.Embedding column
- Rebuild vector_index after migration
- Progress reporting (console output)
**Validation**:
- T023 integration test passes
- Benchmark with 300 entries <7 minutes
**Dependencies**: T020 (EmbeddingService), T012 (migration script)

### T030: Add migration CLI command

**Files**: `src/EpisodeIdentifier.Core/Program.cs` (extend existing)
**Type**: Implementation - CLI
**Parallel**: No
**Description**: Add `--migrate-embeddings` command to generate embeddings for existing entries
**Implementation**:

```bash
dotnet run -- --migrate-embeddings --hash-db "bones.db"
# Output: Migrated 316 entries in 6m 42s
```

**Validation**: Command runs successfully, embeddings generated
**Dependencies**: T029 (migration service)

---

## Phase 3.11: Documentation & Polish

### T031: Update CONFIGURATION_GUIDE.md [P]

**Files**: `CONFIGURATION_GUIDE.md` (extend existing)
**Type**: Documentation
**Parallel**: Yes (different file from T032)
**Description**: Document new embedding configuration options
**Sections to Add**:

- Matching Strategy (embedding vs fuzzy vs hybrid)
- Embedding Thresholds (per-format configuration)
- Model Download (automatic on first run, cache location)
- Migration Guide (existing database to embeddings)
**Validation**: Documentation reviewed, examples accurate
**Dependencies**: T026 (configuration)

### T032: Update .github/copilot-instructions.md [P]

**Files**: `.github/copilot-instructions.md`
**Type**: Documentation
**Parallel**: Yes (different file from T031)
**Description**: Update AI agent context with embedding feature
**Sections to Update**:

- Active Technologies: Add ONNX Runtime, vectorlite, tokenizers
- Commands: Add `--migrate-embeddings`
- Configuration: Add matchingStrategy and embeddingThresholds
- Recent Changes: Add feature 013 summary
**Validation**: File updated, consistent with feature implementation
**Dependencies**: T026-T030 (all implementation complete)

---

## Phase 3.12: End-to-End Validation

### T033: Run all tests

**Files**: All test files
**Type**: Validation
**Parallel**: No
**Description**: Execute full test suite (unit + integration + contract)
**Commands**:

```bash
cd src/EpisodeIdentifier.Core
dotnet test --verbosity normal
```

**Expected Result**: ALL TESTS PASS (including existing tests for backward compatibility)
**Validation**: Zero test failures, no regressions
**Dependencies**: T001-T032 (all implementation complete)

### T034: Manual quickstart validation

**Files**: `specs/013-ml-embedding-matching/quickstart.md`
**Type**: Validation - Manual
**Parallel**: No
**Description**: Execute all 4 user story validations from quickstart.md
**Scenarios**:

1. US-1: Match VobSub OCR to text database (>85% confidence)
2. US-2: Migrate existing database (5 entries <30s)
3. US-3: Threshold configuration (per-format thresholds)
4. US-4: CLI compatibility (all commands work)
**Validation**:

- All 4 scenarios pass
- Screenshots or logs captured
**Dependencies**: T033 (all tests pass)

---

## Completion Checklist

### Code Quality

- [ ] All 34 tasks completed
- [ ] All tests pass (unit + integration + contract)
- [ ] No compiler warnings
- [ ] Code follows C# conventions
- [ ] XML documentation comments on public APIs

### Testing

- [ ] Contract tests pass (3 services)
- [ ] Unit tests pass (models, extensions)
- [ ] Integration tests pass (4 scenarios)
- [ ] Manual quickstart validation complete
- [ ] No test regressions (existing tests still pass)

### Documentation

- [ ] CONFIGURATION_GUIDE.md updated
- [ ] .github/copilot-instructions.md updated
- [ ] XML docs on all public interfaces/classes
- [ ] quickstart.md validated

### Performance

- [ ] Embedding generation <5s per subtitle
- [ ] Vector search <2s for 1000 entries
- [ ] Migration <7min for 300 entries (4 workers)
- [ ] Memory usage <500MB during batch processing

### Validation

- [ ] Criminal Minds S06E19 VobSub → Text match >85% confidence
- [ ] Existing fuzzy hash tests still pass (backward compatibility)
- [ ] All CLI commands functional (--store, --identify, --bulk-identify)
- [ ] Configuration hot-reload works

---

## Task Execution Notes

**TDD Enforcement**:

- Tasks T004-T006 create contract tests FIRST (RED phase setup)
- Tasks T016-T018 verify tests FAIL before implementation (RED phase)
- Tasks T019-T021 implement services to pass tests (GREEN phase)
- Integration tests (T022-T025) verify end-to-end scenarios

**Parallel Execution**:

- Tasks marked [P] can be executed concurrently (different files)
- Example: T004, T005, T006 can run in parallel (independent contract tests)
- Example: T007-T011 can run in parallel (independent model files)

**Dependencies**:

- Each task lists explicit dependencies (must complete before starting)
- Some tasks have no dependencies (can start immediately)
- Respect dependency order to avoid compilation errors

**Validation**:

- Each task has clear validation criteria
- Integration tests validate user stories from spec.md
- Final validation (T033-T034) ensures feature completeness

---

*Generated from plan.md, data-model.md, contracts/, and quickstart.md*
