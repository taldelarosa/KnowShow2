# Implementation Plan: ML Embedding-Based Subtitle Matching

**Branch**: `013-ml-embedding-matching` | **Date**: October 19, 2025 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/013-ml-embedding-matching/spec.md`

## Execution Flow (/plan command scope)

```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
   ✓ Loaded spec.md successfully

2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Single project (extending EpisodeIdentifier.Core)
   → Set Structure Decision based on project type

3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check

4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"

5. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file
6. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check

7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
8. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:

- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary

Replace CTPH fuzzy hashing with ML embedding-based semantic similarity matching for subtitle identification. System generates 384-dimensional embeddings from CleanText using all-MiniLM-L6-v2 ONNX model, stores embeddings in SQLite with vectorlite extension, and uses cosine similarity for matching. Solves OCR subtitle matching problem where VobSub/PGS subtitles show 0% fuzzy hash similarity to text subtitles despite 99.3% content similarity. Maintains CLI compatibility, supports configurable thresholds per format (Text/PGS/VobSub), and provides automatic database migration for existing entries.

## Technical Context

**Language/Version**: C# .NET 8.0
**Primary Dependencies**: Microsoft.ML.OnnxRuntime, Microsoft.ML.Tokenizers, vectorlite SQLite extension, existing System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
**Storage**: SQLite database with vectorlite extension for vector similarity search, BLOB column for 384-dimensional embeddings, JSON configuration with hot-reload
**Testing**: xUnit with existing unit/integration test structure, contract tests for embedding services
**Target Platform**: Cross-platform CLI application (.NET 8.0, Windows/Linux)
**Project Type**: single - extends existing EpisodeIdentifier.Core library
**Performance Goals**: <5 seconds for embedding generation per subtitle, <2 seconds for vector search across 1000 entries, <500MB memory usage during batch processing
**Constraints**: Fully offline operation (no external APIs), cross-platform compatibility, model <100MB, maintain existing CLI interface, pass all existing tests
**Scale/Scope**: Support same database scale as existing system (tested with 316+ episodes), enhance matching accuracy for OCR-based subtitles without regressing text subtitle performance

**User-Provided Implementation Details**:
- Replace CTPH fuzzy hashing with semantic embeddings using sentence transformers
- Generate 384-dimensional embeddings from CleanText using all-MiniLM-L6-v2 or similar lightweight model
- Store embeddings in SQLite database (new Embedding BLOB column)
- Implement cosine similarity matching with configurable thresholds per format
- Support all three subtitle formats (Text, PGS, VobSub) with same embedding approach
- Add SubtitleFormat column to track format type for analytics
- Maintain backward compatibility with automatic migration for existing entries
- Use vectorlite extension: https://github.com/1yefuwang1/vectorlite
- Dependencies: Microsoft.ML.OnnxRuntime, lightweight sentence transformer model (<100MB)
- Testing: Verify VobSub OCR matches text subtitles for Criminal Minds S06E19

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:
- Projects: 1 (extending existing EpisodeIdentifier.Core - max 3 ✓)
- Using framework directly? Yes (ONNX Runtime, vectorlite, existing System.CommandLine ✓)
- Single data model? Yes (extending existing SubtitleHashes table ✓)
- Avoiding patterns? Yes (no Repository/UoW - using direct services ✓)

**Architecture**:
- EVERY feature as library? Yes (functionality in EpisodeIdentifier.Core library ✓)
- Libraries listed: EpisodeIdentifier.Core + purpose: Episode identification with ML embedding-based matching
- CLI per library: Existing CLI with --identify, --store, --bulk-identify commands unchanged
- Library docs: Will update existing llms.txt format

**Testing (NON-NEGOTIABLE)**:
- RED-GREEN-Refactor cycle enforced? YES (tests written first, must fail, then implement ✓)
- Git commits show tests before implementation? YES (will follow TDD ✓)
- Order: Contract→Integration→E2E→Unit strictly followed? YES ✓
- Real dependencies used? YES (actual ONNX models, SQLite DB with vectorlite ✓)
- Integration tests for: embedding generation, vector search, database migration, format-specific matching ✓
- FORBIDDEN: Implementation before test, skipping RED phase ✓

**Observability**:
- Structured logging included? YES (Microsoft.Extensions.Logging already used, will add embedding timing metrics ✓)
- Frontend logs → backend? N/A (CLI application)
- Error context sufficient? YES (existing error handling with JSON output, will add model loading errors ✓)

**Versioning**:
- Version number assigned? Will increment BUILD version (existing pattern ✓)
- BUILD increments on every change? YES (following existing practice ✓)
- Breaking changes handled? NO breaking changes - backward compatible enhancement with migration ✓

## Project Structure

### Documentation (this feature)

```
specs/013-ml-embedding-matching/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
│   ├── embedding-service.json
│   ├── vector-search-service.json
│   └── model-manager.json
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
# Option 1: Single project (DEFAULT - SELECTED)

src/EpisodeIdentifier.Core/
├── Models/
│   ├── SubtitleEmbedding.cs
│   ├── SubtitleFormat.cs (enum)
│   ├── VectorSimilarityResult.cs
│   └── Configuration/
│       └── EmbeddingMatchThresholds.cs
├── Services/
│   ├── EmbeddingService.cs
│   ├── VectorSearchService.cs
│   ├── ModelManager.cs
│   └── FuzzyHashService.cs (extend with embedding support)
└── Interfaces/
    ├── IEmbeddingService.cs
    ├── IVectorSearchService.cs
    └── IModelManager.cs

tests/
├── contract/
│   ├── EmbeddingServiceContractTests.cs
│   ├── VectorSearchServiceContractTests.cs
│   └── ModelManagerContractTests.cs
├── integration/
│   ├── EmbeddingDatabaseMigrationTests.cs
│   ├── VectorSimilaritySearchTests.cs
│   └── CriminalMindsS06E19ValidationTests.cs
└── unit/
    └── EmbeddingConfigurationTests.cs

models/ (new directory)
└── all-MiniLM-L6-v2.onnx (downloaded on first run)
```

**Structure Decision**: Option 1 (Single project) - Extending existing EpisodeIdentifier.Core library with embedding support

## Phase 0: Outline & Research

### Research Questions

1. **ONNX Model Selection**: Which all-MiniLM-L6-v2 ONNX model source is best?
   - Hugging Face Optimum exports
   - Pre-converted ONNX Model Zoo
   - Custom conversion from PyTorch
   - Decision criteria: file size, cross-platform compatibility, quantization options

2. **vectorlite Integration**: How to load vectorlite extension in SQLite with .NET?
   - SQLitePCLRaw provider approach
   - Platform-specific extension loading (Windows .dll vs Linux .so)
   - Extension initialization and index creation
   - Performance characteristics with 1000+ entries

3. **Tokenization Approach**: How to implement BPE tokenization in .NET?
   - Microsoft.ML.Tokenizers library capabilities
   - Tokenizer model format and loading
   - Token ID to embedding pipeline
   - Unicode normalization requirements

4. **Migration Strategy**: How to generate embeddings for existing database entries?
   - Batch processing approach for 300+ existing entries
   - Progress reporting during migration
   - Error handling for malformed CleanText
   - Performance optimization (parallel processing)

5. **Model Distribution**: How to handle model download and caching?
   - Bundle with application vs download on first run
   - Model storage location (cross-platform paths)
   - Version management and updates
   - Fallback behavior if download fails

### Research Tasks

```
Task 1: Research all-MiniLM-L6-v2 ONNX model sources and formats
  - Compare Hugging Face, ONNX Model Zoo, custom conversion
  - Evaluate quantization options (INT8, FP16, FP32)
  - Verify 384-dimensional output
  - Test cross-platform compatibility (Windows/Linux)

Task 2: Research vectorlite SQLite extension integration with .NET
  - Document SQLitePCLRaw provider configuration
  - Test platform-specific extension loading
  - Benchmark cosine similarity performance
  - Verify BLOB storage and retrieval

Task 3: Research Microsoft.ML.Tokenizers for BPE tokenization
  - Test tokenizer model loading
  - Verify token ID generation from text
  - Document special token handling ([CLS], [SEP], [PAD])
  - Test with sample subtitle text

Task 4: Research database migration patterns for embedding generation
  - Design batch processing strategy
  - Document progress reporting approach
  - Test error handling for edge cases
  - Benchmark migration performance

Task 5: Research model download and caching strategies
  - Evaluate HttpClient download approach
  - Document cross-platform model storage paths
  - Design version checking mechanism
  - Test network failure handling
```

**Output**: research.md with all decisions documented

## Phase 1: Design & Contracts

*Prerequisites: research.md complete*

### Design Tasks

1. **Extract entities from feature spec** → `data-model.md`:
   - SubtitleEmbedding (BLOB field, 384 dimensions, float32 array)
   - SubtitleFormat (enum: Text, PGS, VobSub)
   - VectorSimilarityResult (subtitle metadata + cosine similarity + format)
   - EmbeddingMatchThresholds (per-format thresholds)
   - ModelInfo (version, path, download URL)

2. **Generate API contracts** → `/contracts/`:
   - embedding-service.json: IEmbeddingService contract
     - GenerateEmbedding(string cleanText) → float[] (384 dims)
     - BatchGenerateEmbeddings(List<string> cleanTexts) → List<float[]>
   - vector-search-service.json: IVectorSearchService contract
     - SearchBySimilarity(float[] embedding, double threshold, SubtitleFormat? format) → List<VectorSimilarityResult>
   - model-manager.json: IModelManager contract
     - LoadModel() → ModelSession
     - EnsureModelAvailable() → Task<bool>
     - GetModelInfo() → ModelInfo

3. **Generate contract tests** from contracts:
   - EmbeddingServiceContractTests.cs (test MUST fail initially)
   - VectorSearchServiceContractTests.cs (test MUST fail initially)
   - ModelManagerContractTests.cs (test MUST fail initially)

4. **Extract test scenarios** from user stories → `quickstart.md`:
   - US-1: Match VobSub OCR to text database (Criminal Minds S06E19)
   - US-2: Migrate existing database with 300+ entries
   - US-3: Configure thresholds and verify matching behavior
   - US-4: CLI commands work unchanged (identify, store, bulk-identify)

5. **Update agent file** (.github/copilot-instructions.md):
   - Add ML embedding technologies to Active Technologies section
   - Add new services (EmbeddingService, VectorSearchService, ModelManager) to Project Structure
   - Update Commands section with embedding-specific configuration
   - Add embedding match thresholds to Configuration section
   - Update Recent Changes with 013-ml-embedding-matching

**Output**: data-model.md, contracts/*.json, failing contract tests, quickstart.md, updated .github/copilot-instructions.md

## Phase 2: Task Planning Approach

*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:

- Load `/templates/tasks-template.md` as base structure
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- Contract tests (3 files) → 3 contract test tasks [P] (parallel)
- Entities (5 models) → 5 model creation tasks [P]
- Database migration → 1 schema update task + 1 migration service task
- Services (3 interfaces + implementations) → 6 tasks (interface + impl for each)
- Integration tests (4 scenarios) → 4 integration test tasks
- Configuration (2 changes) → 2 configuration tasks
- Quickstart validation → 1 end-to-end validation task
- Documentation updates → 2 tasks (CONFIGURATION_GUIDE.md, agent file)

**Ordering Strategy**:

1. TDD order enforced: Tests before implementation
   - Contract tests first (T001-T003) [P]
   - Model definitions (T004-T008) [P]
   - Database schema update (T009)
   - Interface definitions (T010-T012) [P]
   - Contract test implementation to fail RED phase (T013-T015)
   - Service implementations to pass tests GREEN phase (T016-T018)
   - Integration tests (T019-T022)
   - Migration service (T023)
   - Configuration updates (T024-T025)
   - Quickstart validation (T026)
   - Documentation (T027-T028)

2. Dependency order:
   - Models before services
   - Database schema before migration service
   - Contract tests before implementations
   - Services before integration tests

3. Parallel execution markers [P]:
   - Contract test files (independent)
   - Model definition files (independent)
   - Interface definition files (independent)

**Estimated Output**: ~28 numbered, ordered tasks in tasks.md following TDD RED-GREEN-Refactor cycle

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation

*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md with 28 ordered tasks)
**Phase 4**: Implementation (execute tasks.md following TDD cycle, tests must fail before implementation)
**Phase 5**: Validation (run all tests, execute quickstart.md, performance benchmarking with Criminal Minds S06E19)

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

No complexity violations detected. All constitutional requirements satisfied:
- Single project extension
- Library-first architecture maintained
- TDD enforced
- No unnecessary patterns
- Backward compatible

## Progress Tracking

*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command) - research.md generated
- [x] Phase 1: Design complete (/plan command) - data-model.md, contracts/, quickstart.md, agent file updated
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (research.md complete)
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
