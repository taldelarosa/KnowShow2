# Tasks: Add File Renaming Recommendations

**Input**: Design documents from `/specs/007-add-file-renaming/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)

```
1. Load plan.md from feature directory
   → If not found: ERROR "No implementation plan found"
   → Extract: tech stack, libraries, structure
2. Load optional design documents:
   → data-model.md: Extract entities → model tasks
   → contracts/: Each file → contract test task
   → research.md: Extract decisions → setup tasks
3. Generate tasks by category:
   → Setup: project init, dependencies, linting
   → Tests: contract tests, integration tests
   → Core: models, services, CLI commands
   → Integration: DB, middleware, logging
   → Polish: unit tests, performance, docs
4. Apply task rules:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → All contracts have tests?
   → All entities have models?
   → All endpoints implemented?
9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- Paths shown below assume single project structure per plan.md

## Phase 3.1: Setup

- [x] T001 Add --rename flag to CLI in src/EpisodeIdentifier.Core/Program.cs
- [x] T002 [P] Create FilenameGenerationRequest model in src/EpisodeIdentifier.Core/Models/FilenameGenerationRequest.cs
- [x] T003 [P] Create FilenameGenerationResult model in src/EpisodeIdentifier.Core/Models/FilenameGenerationResult.cs
- [x] T004 [P] Create FileRenameRequest model in src/EpisodeIdentifier.Core/Models/FileRenameRequest.cs
- [x] T005 [P] Create FileRenameResult model in src/EpisodeIdentifier.Core/Models/FileRenameResult.cs
- [x] T006 [P] Create FileRenameError enum in src/EpisodeIdentifier.Core/Models/FileRenameError.cs

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [x] T007 [P] Contract test for IFilenameService.GenerateFilename in tests/contract/FilenameServiceContractTests.cs
- [x] T008 [P] Contract test for IFilenameService.SanitizeForWindows in tests/contract/FilenameServiceContractTests.cs
- [x] T009 [P] Contract test for IFileRenameService.RenameFileAsync in tests/contract/FileRenameServiceContractTests.cs
- [x] T010 [P] Contract test for IFileRenameService.CanRenameFile in tests/contract/FileRenameServiceContractTests.cs
- [x] T011 [P] Contract test for enhanced IdentificationResult JSON in tests/contract/IdentificationResultContractTests.cs
- [x] T012 [P] Integration test for filename suggestion with high confidence in tests/integration/FilenameRecommendationTests.cs
- [x] T013 [P] Integration test for automatic file rename with --rename flag in tests/integration/FileRenameIntegrationTests.cs
- [x] T014 [P] Integration test for low confidence (no filename suggestion) in tests/integration/FilenameRecommendationTests.cs
- [x] T015 [P] Integration test for Windows character sanitization in tests/integration/FilenameRecommendationTests.cs
- [x] T016 [P] Integration test for filename length truncation in tests/integration/FilenameRecommendationTests.cs

## Phase 3.3: Core Implementation (ONLY after tests are failing)

- [x] T017 Add SuggestedFilename property to IdentificationResult in src/EpisodeIdentifier.Core/Models/IdentificationResult.cs
- [x] T018 Add FileRenamed and OriginalFilename properties to IdentificationResult in src/EpisodeIdentifier.Core/Models/IdentificationResult.cs
- [x] T019 [P] Create IFilenameService interface in src/EpisodeIdentifier.Core/Interfaces/IFilenameService.cs
- [x] T020 [P] Create IFileRenameService interface in src/EpisodeIdentifier.Core/Interfaces/IFileRenameService.cs
- [x] T021 [P] Implement FilenameService class in src/EpisodeIdentifier.Core/Services/FilenameService.cs
- [x] T022 [P] Implement FileRenameService class in src/EpisodeIdentifier.Core/Services/FileRenameService.cs
- [x] T023 Add EpisodeName column to SubtitleHashes table in src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs
- [x] T024 Update FuzzyHashService to store/retrieve episode names in src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs

## Phase 3.4: Integration

- [x] T025 Integrate FilenameService into episode identification workflow in src/EpisodeIdentifier.Core/Program.cs
- [x] T026 Integrate FileRenameService for --rename flag handling in src/EpisodeIdentifier.Core/Program.cs
- [ ] T027 Update JSON serialization to include new IdentificationResult fields in src/EpisodeIdentifier.Core/Program.cs
- [ ] T028 Add error handling for file rename failures in src/EpisodeIdentifier.Core/Program.cs
- [ ] T029 Update CLI help text to document --rename flag in src/EpisodeIdentifier.Core/Program.cs

## Phase 3.5: Polish

- [ ] T030 [P] Unit tests for filename sanitization edge cases in tests/unit/FilenameServiceTests.cs
- [ ] T031 [P] Unit tests for file rename error scenarios in tests/unit/FileRenameServiceTests.cs
- [ ] T032 [P] Unit tests for Windows path length validation in tests/unit/FilenameServiceTests.cs
- [ ] T033 [P] Performance test for filename generation (<10ms) in tests/performance/FilenamePerformanceTests.cs
- [ ] T034 [P] Update quickstart.md with actual test results
- [ ] T035 Update README.md with --rename flag documentation
- [ ] T036 Run end-to-end validation per quickstart.md scenarios

## Dependencies

- Models (T002-T006) can run in parallel (different files)
- Contract tests (T007-T011) before implementations (T017-T024)
- Interface creation (T019-T020) before service implementations (T021-T022)
- Database migration (T023) before service integration (T024)
- Core implementation (T017-T024) before integration (T025-T029)
- Integration complete before polish (T030-T036)

## Parallel Example

```bash
# Phase 3.1: Launch model creation tasks together
Task: "Create FilenameGenerationRequest model in src/EpisodeIdentifier.Core/Models/FilenameGenerationRequest.cs"
Task: "Create FilenameGenerationResult model in src/EpisodeIdentifier.Core/Models/FilenameGenerationResult.cs"  
Task: "Create FileRenameRequest model in src/EpisodeIdentifier.Core/Models/FileRenameRequest.cs"
Task: "Create FileRenameResult model in src/EpisodeIdentifier.Core/Models/FileRenameResult.cs"
Task: "Create FileRenameError enum in src/EpisodeIdentifier.Core/Models/FileRenameError.cs"

# Phase 3.2: Launch contract tests together
Task: "Contract test for IFilenameService.GenerateFilename in tests/contract/FilenameServiceContractTests.cs"
Task: "Contract test for IFileRenameService.RenameFileAsync in tests/contract/FileRenameServiceContractTests.cs"
Task: "Integration test for filename suggestion in tests/integration/FilenameRecommendationTests.cs"

# Phase 3.3: Launch service implementations together  
Task: "Implement FilenameService class in src/EpisodeIdentifier.Core/Services/FilenameService.cs"
Task: "Implement FileRenameService class in src/EpisodeIdentifier.Core/Services/FileRenameService.cs"

# Phase 3.5: Launch polish tasks together
Task: "Unit tests for filename sanitization in tests/unit/FilenameServiceTests.cs"
Task: "Unit tests for file rename errors in tests/unit/FileRenameServiceTests.cs"
Task: "Performance test for filename generation in tests/performance/FilenamePerformanceTests.cs"
```

## Notes

### TDD Enforcement
- All contract and integration tests (T007-T016) MUST be completed and failing before ANY implementation tasks (T017-T024)
- This ensures proper red-green-refactor cycle

### Database Migration Strategy
- T023 adds EpisodeName column with backward compatibility (nullable)
- T024 updates existing service to use new column
- Migration runs automatically on application start

### Error Handling Integration
- T028 integrates with existing error patterns in Program.cs
- File rename errors included in JSON response with proper error codes
- Maintains backward compatibility for existing error scenarios

### Performance Requirements
- T021 must implement filename generation in <10ms (per plan.md)
- T033 validates performance requirement
- T022 must handle file operations efficiently

### Windows Compatibility
- T021 implements character sanitization per contracts/filename-service-contract.md
- T015 validates actual Windows compatibility with test scenarios
- T032 ensures path length compliance

## Task Generation Rules Applied

✅ Each contract file → contract test task marked [P]
✅ Each entity in data-model → model creation task marked [P]  
✅ Each user story → integration test marked [P]
✅ Different files = can be parallel [P]
✅ Same file = sequential (no [P])
✅ Setup before everything
✅ Tests before implementation (TDD)
✅ Models before services
✅ Core before integration
✅ Everything before polish

## Validation Checklist

✅ All contracts have tests (FilenameService, FileRenameService, CLI)
✅ All entities have models (FilenameGeneration*, FileRename*, Enhanced IdentificationResult)
✅ All user stories have integration tests (filename suggestion, auto rename, edge cases)
✅ TDD order enforced (tests T007-T016 before implementation T017-T024)
✅ Dependencies clearly defined
✅ Parallel execution opportunities identified
✅ File paths are specific and actionable
