# Implementation Plan: TextRank-Based Semantic Subtitle Matching

**Branch**: `014-use-textrank-to` | **Date**: 2025-10-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/014-use-textrank-to/spec.md`

## Execution Flow (/plan command scope)

```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
   ✓ Loaded spec.md successfully

2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Single project (extending EpisodeIdentifier.Core)
   → Set Structure Decision based on project type
   ✓ Technical Context complete

3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
   ✓ Constitution check passed

4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
   ✓ Research.md generated

5. Execute Phase 1 → contracts, data-model.md, quickstart.md, .github/copilot-instructions.md
   ✓ Phase 1 artifacts generated

6. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
   ✓ Post-design check passed

7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
   ✓ Task planning strategy documented

8. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:

- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary

Enhance episode identification accuracy by applying TextRank graph-based ranking to extract plot-relevant sentences from subtitle files before embedding generation. System scores each sentence by semantic importance, selects top 25% (configurable 10-50%), generates embeddings only from high-scoring content, and uses these filtered embeddings for matching. This filters out conversational filler, character banter, and repetitive phrases that don't contribute to episode identification, improving accuracy especially for verbose translations or OCR subtitles with noise.

## Technical Context

**Language/Version**: C# .NET 8.0
**Primary Dependencies**: Existing Microsoft.ML.OnnxRuntime, Microsoft.ML.Tokenizers, vectorlite SQLite extension, System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
**Storage**: SQLite database (extends existing Feature 013 schema), no new columns required (embeddings stored in existing Embedding BLOB column), JSON configuration with hot-reload
**Testing**: xUnit with existing unit/integration/contract test structure, TDD approach with RED-GREEN-Refactor
**Target Platform**: Cross-platform CLI application (.NET 8.0, Windows/Linux)
**Project Type**: single - extends existing EpisodeIdentifier.Core library
**Performance Goals**: <1 second for TextRank sentence scoring per subtitle (500 sentences), <5 seconds total for extraction + embedding generation, maintain <500MB memory usage during batch processing
**Constraints**: Fully offline operation (no external APIs), cross-platform compatibility, no new external dependencies (implement TextRank from scratch using existing .NET libraries), maintain existing CLI interface, pass all existing tests, backward compatible with Feature 013 full-text embeddings
**Scale/Scope**: Support existing database scale (tested with 316+ episodes), enhance matching accuracy for verbose/translated subtitles without regressing performance on concise text subtitles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (extending existing EpisodeIdentifier.Core - max 3 ✓)
- Using framework directly? Yes (existing ONNX Runtime, vectorlite, System.CommandLine - no new wrappers ✓)
- Single data model? Yes (reusing existing SubtitleHashes table and embedding infrastructure ✓)
- Avoiding patterns? Yes (no Repository/UoW - using direct services, TextRank implemented as simple service ✓)

**Architecture**:

- EVERY feature as library? Yes (functionality in EpisodeIdentifier.Core library ✓)
- Libraries listed: EpisodeIdentifier.Core + purpose: Episode identification with TextRank-filtered embedding matching
- CLI per library: Existing CLI with --identify, --store, --bulk-identify commands unchanged; no new commands required
- Library docs: Will update existing .github/copilot-instructions.md with TextRank feature

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? YES (tests written first, must fail, then implement ✓)
- Git commits show tests before implementation? YES (will follow TDD ✓)
- Order: Contract→Integration→E2E→Unit strictly followed? YES ✓
- Real dependencies used? YES (actual subtitle files, SQLite DB with vectorlite, existing ONNX models ✓)
- Integration tests for: TextRank extraction, sentence selection, fallback behavior, filtered embedding generation ✓
- FORBIDDEN: Implementation before test, skipping RED phase ✓

**Observability**:

- Structured logging included? YES (log TextRank statistics: total sentences, selected sentences, average scores ✓)
- Frontend logs → backend? N/A (CLI application ✓)
- Error context sufficient? YES (log fallback triggers, threshold violations ✓)

**Versioning**:

- Version number assigned? BUILD increment (existing MAJOR.MINOR, no API changes ✓)
- BUILD increments on every change? YES ✓
- Breaking changes handled? NO breaking changes - backward compatible with Feature 013 full-text embeddings ✓

## Project Structure

### Documentation (this feature)

```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```

# Option 1: Single project (DEFAULT)







src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Option 2: Web application (when "frontend" + "backend" detected)







backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# Option 3: Mobile + API (when "iOS/Android" detected)







api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure]
```

**Structure Decision**: Option 1 (Single project) - extends existing EpisodeIdentifier.Core structure

## Phase 0: Outline & Research

**Research Tasks Completed**:

1. **TextRank Algorithm Implementation**: Research graph-based ranking for sentence extraction
   - Core PageRank algorithm adapted for sentence graphs
   - Sentence similarity calculation (cosine similarity on word vectors)
   - Convergence criteria and damping factor tuning

2. **Sentence Segmentation**: Research .NET sentence boundary detection
   - Regex-based segmentation with period/question/exclamation detection
   - Handling edge cases: abbreviations, ellipsis, quotations
   - Subtitle-specific considerations (timestamps, speaker tags)

3. **Configuration Integration**: Extend existing configuration system
   - Add textRankSentencePercentage (10-50% range, default 25%)
   - Add enableTextRankFiltering boolean toggle
   - Add minSentencesThreshold and minPercentageThreshold for fallback

4. **Performance Considerations**: Graph construction and ranking optimization
   - Sparse matrix representation for sentence similarity graph
   - Iterative convergence with early stopping
   - Memory-efficient processing for large subtitle files

**Output**: research.md with implementation decisions and technical approach

## Phase 1: Design & Contracts

**Artifacts Generated**:

1. **data-model.md**: Entity definitions
   - TextRankExtractionResult (extraction output with statistics)
   - SentenceScore (sentence + TextRank score for debugging)
   - TextRankConfiguration (configuration settings)
   - SentenceGraph (internal graph representation)
   - No database schema changes (reuses existing Embedding column)

2. **contracts/textrank-service.json**: ITextRankService contract
   - ExtractPlotRelevantSentences(subtitleText, sentencePercentage, ...) → TextRankExtractionResult
   - CalculateTextRankScores(sentences) → Dictionary<int, double>
   - Contract tests: 5 scenarios (verbose subtitle, fallback triggers, single sentence, score calculation, large file)

3. **quickstart.md**: Criminal Minds S06E19 validation scenario
   - Baseline match without TextRank (confidence: 0.68)
   - TextRank match with filtering (confidence: 0.79, +16% improvement)
   - Fallback validation for short subtitles
   - Performance validation for large files (< 500ms)
   - Configuration hot-reload verification

4. **.github/copilot-instructions.md**: Updated with Feature 014
   - Added TextRank filtering to active technologies
   - Updated project structure (Models, Services, Interfaces)
   - Added configuration section for textRankFiltering
   - Updated recent changes log

**Output**: All Phase 1 artifacts complete, ready for task generation

## Phase 2: Task Planning Approach

**Task Generation Strategy**:

1. **Load Base Template**: Use `/templates/tasks-template.md` structure
2. **Generate from Phase 1 Artifacts**:
   - Data model → model creation tasks
   - Contracts → contract test tasks (RED phase)
   - Services → implementation tasks (GREEN phase)
   - Integration → integration test tasks
   - Configuration → config extension tasks

3. **Task Categories**:

   **Phase 3.1: Models & Configuration** [P = Parallel]
   - T001: Create TextRankExtractionResult model [P]
   - T002: Create SentenceScore model [P]
   - T003: Create TextRankConfiguration model [P]
   - T004: Extend AppConfig with TextRankConfiguration [P]
   - T005: Add configuration validation

   **Phase 3.2: Service Interfaces**
   - T006: Create ITextRankService interface [P]
   - T007: Update IConfigurationService for TextRank config

   **Phase 3.3: Contract Tests (RED phase)**
   - T008: Contract test - Extract verbose subtitle sentences
   - T009: Contract test - Trigger fallback (insufficient sentences)
   - T010: Contract test - Trigger fallback (low percentage)
   - T011: Contract test - Handle single-sentence subtitle
   - T012: Contract test - Calculate TextRank scores correctly

   **Phase 3.4: Service Implementation (GREEN phase)**
   - T013: Implement SentenceSegmenter helper class
   - T014: Implement sentence similarity calculation (bag-of-words)
   - T015: Implement PageRank iteration algorithm
   - T016: Implement TextRankService.ExtractPlotRelevantSentences
   - T017: Implement TextRankService.CalculateTextRankScores
   - T018: Implement fallback decision logic
   - T019: Add structured logging for TextRank statistics

   **Phase 3.5: Integration with Feature 013**
   - T020: Integrate TextRank into EpisodeIdentificationService
   - T021: Update dependency injection (ServiceCollectionExtensions)
   - T022: Update configuration loading (ConfigurationService)

   **Phase 3.6: Integration Tests**
   - T023: Integration test - Verbose subtitle matching improvement
   - T024: Integration test - Fallback behavior validation
   - T025: Integration test - Configuration hot-reload
   - T026: Integration test - Performance validation (large files)
   - T027: Integration test - Backward compatibility (Feature 013 tests)

   **Phase 3.7: Quickstart Validation**
   - T028: Execute quickstart scenario (Criminal Minds S06E19)
   - T029: Verify confidence improvement (+10-15%)
   - T030: Validate performance metrics (< 500ms overhead)

4. **Ordering Strategy**:
   - TDD order: Contract tests (RED) before implementation (GREEN)
   - Dependency order: Models → Interfaces → Tests → Implementation → Integration
   - Parallel tasks marked [P] for independent execution
   - Sequential integration tasks to avoid conflicts

5. **Estimated Task Count**: ~30 tasks across 7 phases

**IMPORTANT**: This section describes the task generation approach. The /tasks command will create tasks.md with full task details.

## Phase 3+: Future Implementation

*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional TDD principles)
**Phase 5**: Validation (run quickstart.md, verify all tests pass, performance profiling)

## Complexity Tracking

*No constitutional violations* - all constitutional principles satisfied:

- Single project (EpisodeIdentifier.Core)
- Using frameworks directly (no wrappers)
- Single data model (reusing existing schema)
- No unnecessary patterns (direct service usage)
- Library-first architecture maintained
- TDD enforced with RED-GREEN-Refactor
- Real dependencies in tests
- Structured logging included
- Backward compatible (no versioning impact)

## Progress Tracking

*This checklist is updated during execution flow*

**Phase Status**:

- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (none)
- [x] Complexity deviations documented (none - no violations)

**Artifacts Generated**:

- [x] research.md - TextRank algorithm research and decisions
- [x] data-model.md - Entity definitions and configuration schema
- [x] contracts/textrank-service.json - ITextRankService contract
- [x] quickstart.md - Criminal Minds S06E19 validation scenario
- [x] .github/copilot-instructions.md - Updated with Feature 014
- [ ] tasks.md - Awaiting /tasks command

---
**Status**: ✅ PLAN COMPLETE - Ready for /tasks command

*Based on Constitution v2.1.1 (template) - See `/memory/constitution.md`*
