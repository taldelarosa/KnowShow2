# Tasks: NonPGS Subtitle Workflow

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/006-adding-nonpgs-workflow/`
**Prerequisites**: plan.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓)

## Execution Flow (main)
```
1. Load plan.md from feature directory ✓
   → Extracted: C# .NET 8.0, FFmpeg, xUnit, CLI application structure
2. Load optional design documents ✓:
   → data-model.md: 5 entities + 2 enums → 7 model tasks
   → contracts/: 2 files → 2 contract test tasks
   → research.md: Format decisions → setup tasks
3. Generate tasks by category ✓:
   → Setup: dependencies, test data
   → Tests: contract tests, integration tests
   → Core: models, format handlers, service extension
   → Integration: CLI flags, workflow integration
   → Polish: unit tests, performance, docs
4. Apply task rules ✓:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...) ✓
6. Generate dependency graph ✓
7. Create parallel execution examples ✓
8. Validate task completeness ✓:
   → All contracts have tests: ✓
   → All entities have models: ✓
   → All format handlers implemented: ✓
9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **Single project**: `src/EpisodeIdentifier.Core/`, `tests/` at repository root
- Paths based on existing project structure

## Phase 3.1: Setup & Prerequisites
- [ ] T001 Create test data directory structure at `tests/data/nonpgs-workflow/`
- [ ] T002 Add FFmpeg text subtitle extraction utilities to existing project dependencies
- [ ] T003 [P] Configure test video files with SRT, ASS, and VTT subtitle tracks in `tests/data/nonpgs-workflow/`

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**
- [ ] T004 [P] Contract test ITextSubtitleExtractor.DetectTextSubtitleTracksAsync in `tests/contract/TextSubtitleExtractorContractTests.cs`
- [ ] T005 [P] Contract test ITextSubtitleExtractor.ExtractTextSubtitleContentAsync in `tests/contract/TextSubtitleExtractorContractTests.cs`
- [ ] T006 [P] Contract test ITextSubtitleExtractor.TryExtractAllTextSubtitlesAsync in `tests/contract/TextSubtitleExtractorContractTests.cs`
- [ ] T007 [P] Contract test ISubtitleFormatHandler.CanHandle in `tests/contract/SubtitleFormatHandlerContractTests.cs`
- [ ] T008 [P] Contract test ISubtitleFormatHandler.ParseSubtitleTextAsync in `tests/contract/SubtitleFormatHandlerContractTests.cs`
- [ ] T009 [P] Integration test SRT format processing workflow in `tests/integration/SrtWorkflowTests.cs`
- [ ] T010 [P] Integration test ASS format processing workflow in `tests/integration/AssWorkflowTests.cs`
- [ ] T011 [P] Integration test VTT format processing workflow in `tests/integration/VttWorkflowTests.cs`
- [ ] T012 [P] Integration test multi-track sequential processing in `tests/integration/MultiTrackProcessingTests.cs`
- [ ] T013 [P] Integration test PGS priority preservation in `tests/integration/PgsPriorityTests.cs`

## Phase 3.3: Core Models (ONLY after tests are failing)
- [ ] T014 [P] SubtitleFormat enum in `src/EpisodeIdentifier.Core/Models/SubtitleFormat.cs`
- [ ] T015 [P] SubtitleSourceType enum in `src/EpisodeIdentifier.Core/Models/SubtitleSourceType.cs`
- [ ] T016 [P] TextSubtitleTrack model in `src/EpisodeIdentifier.Core/Models/TextSubtitleTrack.cs`
- [ ] T017 [P] TextSubtitleContent model in `src/EpisodeIdentifier.Core/Models/TextSubtitleContent.cs`
- [ ] T018 [P] SubtitleProcessingResult model in `src/EpisodeIdentifier.Core/Models/SubtitleProcessingResult.cs`
- [ ] T019 Enhance IdentificationResult model with SubtitleSource and SubtitleMetadata in `src/EpisodeIdentifier.Core/Models/IdentificationResult.cs`
- [ ] T020 Enhance LabelledSubtitle model with SourceFormat and SourceTrackIndex in `src/EpisodeIdentifier.Core/Models/LabelledSubtitle.cs`

## Phase 3.4: Format Handlers (ONLY after models complete)
- [ ] T021 [P] ISubtitleFormatHandler interface in `src/EpisodeIdentifier.Core/Services/ISubtitleFormatHandler.cs`
- [ ] T022 [P] SrtFormatHandler implementation in `src/EpisodeIdentifier.Core/Services/SrtFormatHandler.cs`
- [ ] T023 [P] AssFormatHandler implementation in `src/EpisodeIdentifier.Core/Services/AssFormatHandler.cs`
- [ ] T024 [P] VttFormatHandler implementation in `src/EpisodeIdentifier.Core/Services/VttFormatHandler.cs`

## Phase 3.5: Text Subtitle Extractor (ONLY after format handlers complete)
- [ ] T025 ITextSubtitleExtractor interface in `src/EpisodeIdentifier.Core/Services/ITextSubtitleExtractor.cs`
- [ ] T026 TextSubtitleExtractor implementation (DetectTextSubtitleTracksAsync) in `src/EpisodeIdentifier.Core/Services/TextSubtitleExtractor.cs`
- [ ] T027 TextSubtitleExtractor implementation (ExtractTextSubtitleContentAsync) in `src/EpisodeIdentifier.Core/Services/TextSubtitleExtractor.cs`
- [ ] T028 TextSubtitleExtractor implementation (TryExtractAllTextSubtitlesAsync) in `src/EpisodeIdentifier.Core/Services/TextSubtitleExtractor.cs`

## Phase 3.6: Service Integration
- [ ] T029 Extend SubtitleExtractor service with text subtitle fallback logic in `src/EpisodeIdentifier.Core/Services/SubtitleExtractor.cs`
- [ ] T030 Update SubtitleMatcher service to handle TextSubtitleContent in `src/EpisodeIdentifier.Core/Services/SubtitleMatcher.cs`
- [ ] T031 Add structured logging for text subtitle processing in existing services

## Phase 3.7: CLI Integration
- [ ] T032 Add --enable-text-subtitles CLI flag to Program.cs in `src/EpisodeIdentifier.Core/Program.cs`
- [ ] T033 Add --detect-subtitles CLI command for debugging in `src/EpisodeIdentifier.Core/Program.cs`
- [ ] T034 Add --extract-text-subtitles CLI command for debugging in `src/EpisodeIdentifier.Core/Program.cs`
- [ ] T035 Update CLI help text and argument parsing for new text subtitle options

## Phase 3.8: Polish & Validation
- [ ] T036 [P] Unit tests for SrtFormatHandler in `tests/unit/SrtFormatHandlerTests.cs`
- [ ] T037 [P] Unit tests for AssFormatHandler in `tests/unit/AssFormatHandlerTests.cs`
- [ ] T038 [P] Unit tests for VttFormatHandler in `tests/unit/VttFormatHandlerTests.cs`
- [ ] T039 [P] Unit tests for TextSubtitleExtractor in `tests/unit/TextSubtitleExtractorTests.cs`
- [ ] T040 [P] Performance tests for large subtitle file processing (<10 seconds per track)
- [ ] T041 [P] Update README.md with text subtitle workflow documentation
- [ ] T042 [P] Update API documentation with new models and interfaces
- [ ] T043 Validate quickstart.md scenarios work end-to-end
- [ ] T044 Remove code duplication and refactor common subtitle processing logic

## Dependencies
```
Setup Phase (T001-T003) → Tests Phase (T004-T013)
Tests Phase (T004-T013) → Models Phase (T014-T020)
Models Phase (T014-T020) → Format Handlers (T021-T024)
Format Handlers (T021-T024) → Text Extractor (T025-T028)
Text Extractor (T025-T028) → Service Integration (T029-T031)
Service Integration (T029-T031) → CLI Integration (T032-T035)
CLI Integration (T032-T035) → Polish Phase (T036-T044)

Sequential dependencies within phases:
- T019-T020 (modify existing models) must be sequential
- T025-T028 (same file TextSubtitleExtractor.cs) must be sequential
- T032-T035 (same file Program.cs) must be sequential
```

## Parallel Execution Examples

### Phase 3.2: Contract & Integration Tests
```bash
# Launch contract tests together (T004-T008):
Task: "Contract test ITextSubtitleExtractor.DetectTextSubtitleTracksAsync in tests/contract/TextSubtitleExtractorContractTests.cs"
Task: "Contract test ITextSubtitleExtractor.ExtractTextSubtitleContentAsync in tests/contract/TextSubtitleExtractorContractTests.cs"
Task: "Contract test ITextSubtitleExtractor.TryExtractAllTextSubtitlesAsync in tests/contract/TextSubtitleExtractorContractTests.cs"
Task: "Contract test ISubtitleFormatHandler.CanHandle in tests/contract/SubtitleFormatHandlerContractTests.cs"
Task: "Contract test ISubtitleFormatHandler.ParseSubtitleTextAsync in tests/contract/SubtitleFormatHandlerContractTests.cs"

# Launch integration tests together (T009-T013):
Task: "Integration test SRT format processing workflow in tests/integration/SrtWorkflowTests.cs"
Task: "Integration test ASS format processing workflow in tests/integration/AssWorkflowTests.cs"
Task: "Integration test VTT format processing workflow in tests/integration/VttWorkflowTests.cs"
Task: "Integration test multi-track sequential processing in tests/integration/MultiTrackProcessingTests.cs"
Task: "Integration test PGS priority preservation in tests/integration/PgsPriorityTests.cs"
```

### Phase 3.3: Core Models
```bash
# Launch model creation together (T014-T018):
Task: "SubtitleFormat enum in src/EpisodeIdentifier.Core/Models/SubtitleFormat.cs"
Task: "SubtitleSourceType enum in src/EpisodeIdentifier.Core/Models/SubtitleSourceType.cs"
Task: "TextSubtitleTrack model in src/EpisodeIdentifier.Core/Models/TextSubtitleTrack.cs"
Task: "TextSubtitleContent model in src/EpisodeIdentifier.Core/Models/TextSubtitleContent.cs"
Task: "SubtitleProcessingResult model in src/EpisodeIdentifier.Core/Models/SubtitleProcessingResult.cs"
```

### Phase 3.4: Format Handlers
```bash
# Launch format handlers together (T022-T024):
Task: "SrtFormatHandler implementation in src/EpisodeIdentifier.Core/Services/SrtFormatHandler.cs"
Task: "AssFormatHandler implementation in src/EpisodeIdentifier.Core/Services/AssFormatHandler.cs"  
Task: "VttFormatHandler implementation in src/EpisodeIdentifier.Core/Services/VttFormatHandler.cs"
```

### Phase 3.8: Unit Tests & Documentation
```bash
# Launch unit tests together (T036-T039):
Task: "Unit tests for SrtFormatHandler in tests/unit/SrtFormatHandlerTests.cs"
Task: "Unit tests for AssFormatHandler in tests/unit/AssFormatHandlerTests.cs"
Task: "Unit tests for VttFormatHandler in tests/unit/VttFormatHandlerTests.cs"
Task: "Unit tests for TextSubtitleExtractor in tests/unit/TextSubtitleExtractorTests.cs"

# Launch documentation together (T041-T042):
Task: "Update README.md with text subtitle workflow documentation"
Task: "Update API documentation with new models and interfaces"
```

## Notes
- [P] tasks = different files, no dependencies between them
- Verify all tests fail before implementing (TDD requirement)
- Commit after each task completion
- Sequential tasks within same file must complete in order
- Integration tests require test video files from T003

## Validation Checklist
*GATE: Checked before execution*

- [x] All contracts have corresponding tests (T004-T008)
- [x] All entities have model tasks (T014-T020)
- [x] All tests come before implementation (T004-T013 before T014+)
- [x] Parallel tasks truly independent (different files)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] TDD workflow preserved (tests fail first, then implement)
- [x] Dependencies clearly mapped
- [x] CLI integration preserves existing functionality
