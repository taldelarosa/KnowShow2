# Tasks: Fuzzy Hashing Plus Configuration System

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/008-fuzzy-hashing-plus/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)

```
1. Load plan.md from feature directory
   → Tech stack: C# .NET 8.0, NUnit testing, System.CommandLine
   → Libraries: ssdeep-dotnet, FluentValidation, System.IO.Abstractions
2. Load design documents:
   → data-model.md: Configuration, FilenamePatterns, FuzzyHashResult entities
   → contracts/: ConfigurationService, CTPhHashingService contracts
   → research.md: CTPH library selection, hot-reload patterns
   → quickstart.md: Config validation, hash testing, hot-reload scenarios
3. Generate tasks by category:
   → Setup: NuGet packages, project structure
   → Tests: 2 contract tests, 3 integration tests
   → Core: 3 models, 2 services, CLI commands
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
