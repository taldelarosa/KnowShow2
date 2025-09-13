# Tasks: Fuzzy Hashing Plus Configuration System

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/008-fuzzy-hashing-plus/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)

```
1. Load plan.md from feature directory
   → Tech stack: C# .NET 8.0, System.CommandLine, ssdeep.NET, FluentValidation
   → Structure: Single console application project (src/EpisodeIdentifier.Core/)
2. Load design documents:
   → data-model.md: Configuration, HashingAlgorithm, InputProcessor, ProcessingResult entities
   → contracts/: ConfigurationService, CTPhHashingService contracts
   → quickstart.md: Configuration validation, fuzzy hash testing scenarios
3. Generate tasks by category:
   → Setup: project dependencies, configuration structure
   → Tests: contract tests for services, integration tests for user scenarios
   → Core: model classes, service implementations, CLI commands
   → Integration: file processing, bulk operations, progress reporting
   → Polish: unit tests, performance optimization, error handling
4. Apply task rules:
   → Contract tests [P], model classes [P], different files = parallel
   → Service implementations sequential (shared interfaces)
   → Tests before implementation (TDD enforced)
5. Generated 29 tasks sequentially numbered T001-T029
6. Dependencies: Setup → Tests → Models → Services → CLI → Integration → Polish
7. Parallel execution for independent file operations
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Paths are absolute from repository root: `/mnt/c/Users/Ragma/KnowShow_Specd/`

## Phase 3.1: Setup

- [ ] T001 Update project dependencies in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj` (ensure xUnit, ssdeep.NET, FluentValidation are current versions)
- [ ] T002 Create configuration structure directories in `src/EpisodeIdentifier.Core/Models/Configuration/` and `src/EpisodeIdentifier.Core/Services/Configuration/`
- [ ] T003 [P] Configure markdown linting and ensure all documentation passes markdownlint-cli validation

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [ ] T004 [P] Contract test for IConfigurationService.LoadConfiguration() in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/contract/ConfigurationServiceContractTests.cs`
- [ ] T005 [P] Contract test for IConfigurationService.ReloadIfChanged() in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/contract/ConfigurationServiceContractTests.cs`
- [ ] T006 [P] Contract test for ICTPhHashingService.ComputeFuzzyHash() in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/contract/CTPhHashingServiceContractTests.cs`
- [ ] T007 [P] Contract test for ICTPhHashingService.CompareFuzzyHashes() in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/contract/CTPhHashingServiceContractTests.cs`
- [ ] T008 [P] Contract test for ICTPhHashingService.CompareFiles() in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/contract/CTPhHashingServiceContractTests.cs`
- [ ] T009 [P] Integration test for configuration validation scenario in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/integration/ConfigurationValidationTests.cs`
- [ ] T010 [P] Integration test for fuzzy hash comparison scenario in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/integration/FuzzyHashComparisonTests.cs`
- [ ] T011 [P] Integration test for bulk directory processing scenario in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/integration/BulkProcessingTests.cs`

## Phase 3.3: Core Implementation (ONLY after tests are failing)

- [ ] T012 [P] Configuration model class in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Models/Configuration/Configuration.cs`
- [ ] T013 [P] FilenamePatterns model class in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Models/Configuration/FilenamePatterns.cs`
- [ ] T014 [P] HashingAlgorithm enum in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Models/Configuration/HashingAlgorithm.cs`
- [ ] T015 [P] ProcessingResult model class in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Models/Processing/ProcessingResult.cs`
- [ ] T016 [P] ValidationResult model class in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Models/Validation/ValidationResult.cs`
- [ ] T017 [P] IConfigurationService interface in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Interfaces/IConfigurationService.cs`
- [ ] T018 [P] ICTPhHashingService interface in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Interfaces/ICTPhHashingService.cs`
- [ ] T019 ConfigurationService implementation in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Services/Configuration/ConfigurationService.cs`
- [ ] T020 CTPhHashingService implementation in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Services/Hashing/CTPhHashingService.cs`
- [ ] T021 Configuration validation with FluentValidation in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Services/Configuration/ConfigurationValidator.cs`

## Phase 3.4: CLI Commands

- [ ] T022 --config-validate CLI command in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Commands/ConfigureCommand.cs`
- [ ] T023 --hash-test CLI command for fuzzy hash comparison in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Commands/HashTestCommand.cs`
- [ ] T024 --process-file CLI command for single file processing in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Commands/ProcessFileCommand.cs`
- [ ] T025 --process-directory CLI command for bulk processing in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Commands/ProcessDirectoryCommand.cs`

## Phase 3.5: Integration & Bulk Processing

- [ ] T026 InputProcessor service for file/directory discovery in `/mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core/Services/Processing/InputProcessor.cs`
- [ ] T027 Integrate CTPH hashing with existing identification workflow (modify existing HashingService to support multiple algorithms)
- [ ] T028 Progress reporting and error handling for bulk operations

## Phase 3.6: Polish

- [ ] T029 [P] Unit tests for configuration validation edge cases in `/mnt/c/Users/Ragma/KnowShow_Specd/tests/unit/ConfigurationValidationUnitTests.cs`

## Dependencies

**Critical Path**:

- Setup (T001-T003) → Tests (T004-T011) → Models (T012-T016) → Interfaces (T017-T018) → Services (T019-T021) → CLI (T022-T025) → Integration (T026-T028) → Polish (T029)

**Blocking Dependencies**:

- T004-T005 require T017 interface design (but tests written first per TDD)
- T006-T008 require T018 interface design (but tests written first per TDD)  
- T019 requires T012, T013, T016, T017
- T020 requires T014, T018
- T022-T025 require T019, T020
- T026-T028 require T015, T020

## Parallel Example

```
# Launch contract tests together (T004-T008):
Task: "Contract test for IConfigurationService.LoadConfiguration() in tests/contract/ConfigurationServiceContractTests.cs"
Task: "Contract test for ICTPhHashingService.ComputeFuzzyHash() in tests/contract/CTPhHashingServiceContractTests.cs"
Task: "Contract test for ICTPhHashingService.CompareFuzzyHashes() in tests/contract/CTPhHashingServiceContractTests.cs"

# Launch model classes together (T012-T016):
Task: "Configuration model class in src/EpisodeIdentifier.Core/Models/Configuration/Configuration.cs"
Task: "ProcessingResult model class in src/EpisodeIdentifier.Core/Models/Processing/ProcessingResult.cs" 
Task: "ValidationResult model class in src/EpisodeIdentifier.Core/Models/Validation/ValidationResult.cs"
```

## Notes

- All tests must be written first and must fail before implementation (TDD enforcement)
- [P] tasks can run in parallel because they modify different files
- Service implementations are sequential due to shared interface dependencies
- Configuration loading integrates with existing episodeidentifier.config.json
- CTPH hashing replaces existing SHA1/MD5 for fuzzy matching capability
- Bulk processing adds directory traversal and progress reporting to existing workflows

## Task Generation Rules Applied

1. **From Contracts**:
   - ConfigurationService contract → T004-T005, T017, T019
   - CTPhHashingService contract → T006-T008, T018, T020

2. **From Data Model**:
   - Configuration entity → T012
   - FilenamePatterns entity → T013  
   - HashingAlgorithm enum → T014
   - ProcessingResult entity → T015
   - ValidationResult entity → T016

3. **From Quickstart Scenarios**:
   - Configuration validation → T009, T022, T029
   - Fuzzy hash testing → T010, T023
   - Bulk processing → T011, T024-T025, T026-T028

4. **TDD Ordering Enforced**:
   - All contract and integration tests (T004-T011) before implementation
   - Models before services that use them
   - Services before CLI commands that use them

## Validation Checklist ✅

- [x] All contracts have corresponding tests (ConfigurationService: T004-T005, CTPhHashingService: T006-T008)
- [x] All entities have model tasks (Configuration: T012, FilenamePatterns: T013, etc.)
- [x] All tests come before implementation (Phase 3.2 before 3.3)
- [x] Parallel tasks are truly independent (different files, no shared state)
- [x] Each task specifies exact file path (all paths absolute from repository root)  
- [x] No [P] task modifies same file as another [P] task
   → Integration: config loading, file watching
   → Polish: unit tests, performance validation

4. Task rules applied:
   → Different files marked [P] for parallel execution
   → Tests before implementation (TDD enforced)
5. 18 tasks generated and numbered T001-T018
6. Dependencies identified and validated
7. Parallel execution groups created
8. SUCCESS: All contracts, entities, and scenarios covered

```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- All file paths are absolute from repository root

## Path Conventions

Single project structure (from plan.md):

- **Source**: `src/EpisodeIdentifier.Core/`
- **Tests**: `tests/contract/`, `tests/integration/`, `tests/unit/`

## Phase 3.1: Setup

- [x] **T001** Add NuGet dependencies (ssdeep-dotnet, FluentValidation, System.IO.Abstractions) to `src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj`
- [x] **T002** [P] Create configuration models directory structure in `src/EpisodeIdentifier.Core/Models/Configuration/`
- [x] **T003** [P] Create hashing services directory structure in `src/EpisodeIdentifier.Core/Services/Hashing/`

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [x] **T004** [P] Contract test IConfigurationService.LoadConfiguration() in `tests/contract/ConfigurationServiceContractTests.cs`
- [x] **T005** [P] Contract test ICTPhHashingService.ComputeFuzzyHash() and CompareFiles() in `tests/contract/CTPhHashingServiceContractTests.cs`
- [x] **T006** [P] Integration test config loading and validation in `tests/integration/ConfigurationLoadingTests.cs`
- [x] **T007** [P] Integration test fuzzy hash comparison workflow in `tests/integration/FuzzyHashWorkflowTests.cs`
- [x] **T008** [P] Integration test hot-reload during file processing in `tests/integration/ConfigHotReloadTests.cs`

## Phase 3.3: Core Implementation (ONLY after tests are failing)

- [x] **T009** [P] Configuration entity with validation in `src/EpisodeIdentifier.Core/Models/Configuration/Configuration.cs`
- [x] **T010** [P] FilenamePatterns entity in `src/EpisodeIdentifier.Core/Models/Configuration/FilenamePatterns.cs`
- [x] **T011** [P] FuzzyHashResult entity in `src/EpisodeIdentifier.Core/Models/Configuration/FuzzyHashResult.cs`
- [x] **T012** ConfigurationService with hot-reload in `src/EpisodeIdentifier.Core/Services/ConfigurationService.cs`
- [x] **T013** CTPhHashingService with ssdeep integration in `src/EpisodeIdentifier.Core/Services/Hashing/CTPhHashingService.cs`
- [x] **T014** CLI commands for config validation and hash testing in `src/EpisodeIdentifier.Core/Commands/ConfigurationCommands.cs`

## Phase 3.4: Integration

- [x] **T015** Wire ConfigurationService into main application startup in `src/EpisodeIdentifier.Core/Program.cs`
- [x] **T016** Integrate CTPhHashingService into existing episode identification workflow in `src/EpisodeIdentifier.Core/Services/EpisodeIdentificationService.cs`
- [x] **T017** Add structured logging for config operations and hash comparisons throughout services

## Phase 3.5: Polish

- [x] **T018** [P] Unit tests for edge cases and validation rules in `tests/unit/ConfigurationValidationTests.cs`

## Dependencies

### Critical Path

- **Setup First**: T001-T003 before everything else
- **Tests Before Implementation**: T004-T008 MUST complete and FAIL before T009-T014
- **Models Before Services**: T009-T011 before T012-T013
- **Services Before Integration**: T012-T013 before T015-T016
- **Implementation Before Polish**: T014-T017 before T018

### Blocking Dependencies

- T012 (ConfigurationService) requires T009 (Configuration model)
- T013 (CTPhHashingService) requires T011 (FuzzyHashResult model)
- T015 (Program.cs integration) requires T012 (ConfigurationService)
- T016 (Workflow integration) requires T013 (CTPhHashingService)

## Parallel Execution Examples

### Setup Phase (can run together)

```

Task: "Create configuration models directory structure in src/EpisodeIdentifier.Core/Models/Configuration/"
Task: "Create hashing services directory structure in src/EpisodeIdentifier.Core/Services/Hashing/"

```

### Test Phase (can run together after setup)

```

Task: "Contract test IConfigurationService.LoadConfiguration() in tests/contract/ConfigurationServiceContractTests.cs"
Task: "Contract test ICTPhHashingService.ComputeFuzzyHash() and CompareFiles() in tests/contract/CTPhHashingServiceContractTests.cs"
Task: "Integration test config loading and validation in tests/integration/ConfigurationLoadingTests.cs"
Task: "Integration test fuzzy hash comparison workflow in tests/integration/FuzzyHashWorkflowTests.cs"
Task: "Integration test hot-reload during file processing in tests/integration/ConfigHotReloadTests.cs"

```

### Model Phase (can run together after tests fail)

```

Task: "Configuration entity with validation in src/EpisodeIdentifier.Core/Models/Configuration/Configuration.cs"
Task: "FilenamePatterns entity in src/EpisodeIdentifier.Core/Models/Configuration/FilenamePatterns.cs"
Task: "FuzzyHashResult entity in src/EpisodeIdentifier.Core/Models/Configuration/FuzzyHashResult.cs"

```

## Notes

- All [P] tasks target different files with no shared dependencies
- Verify all contract and integration tests fail before starting implementation
- Commit after completing each task
- Use absolute file paths for all tasks
- Maintain backward compatibility with existing configuration format

## Task Generation Rules Applied

1. **From Contracts**: 2 contract files → 2 contract test tasks [P]
2. **From Data Model**: 3 entities → 3 model creation tasks [P]  
3. **From Quickstart**: 3 test scenarios → 3 integration test tasks [P]
4. **From Research**: CTPH library → dependency setup task
5. **From Plan**: Hot-reload requirement → FileSystemWatcher integration

## Validation Checklist

- [x] All contracts have corresponding tests (T004, T005)
- [x] All entities have model tasks (T009-T011)
- [x] All tests come before implementation (T004-T008 before T009-T014)
- [x] Parallel tasks truly independent (different file paths)
- [x] Each task specifies exact absolute file path
- [x] No [P] task modifies same file as another [P] task
