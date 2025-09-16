# Tasks: Async Processing with Configurable Concurrency

**Input**: Design documents from `/specs/010-async-processing-where/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)

```
1. Load plan.md from feature directory
   → Found: C# .NET 8.0, System.CommandLine, Microsoft.Extensions.Logging
   → Tech stack: Existing EpisodeIdentifier.Core CLI application extension
   → Structure: Single project with src/EpisodeIdentifier.Core

2. Load optional design documents:
   → data-model.md: Extract entities - ConcurrencyConfiguration extension, BulkProcessingOptions modification
   → contracts/: api-contracts.md - Configuration schema, CLI enhancements, service contracts
   → research.md: Minimal changes approach leveraging existing infrastructure

3. Generate tasks by category:
   → Setup: Configuration extension, validation setup
   → Tests: Contract tests for config and processing, integration tests for concurrency scenarios
   → Core: Configuration service extension, BulkProcessingOptions modification
   → Integration: Hot-reload integration, progress reporting enhancements
   → Polish: Unit tests, performance validation, documentation

4. Apply task rules:
   → Different files = mark [P] for parallel (config models, test files)
   → Same file = sequential (BulkProcessingOptions modifications)
   → Tests before implementation (TDD)

5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → All contracts have tests? YES - configuration and processing contracts
   → All entities have models? YES - configuration extension
   → All functionality implemented? YES - config reading, hot-reload, concurrent processing

9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/EpisodeIdentifier.Core/`, `tests/` at repository root
- Tests organized as: `tests/contract/`, `tests/integration/`, `tests/unit/`

## Phase 3.1: Setup

- [ ] T001 Add maxConcurrency property to existing configuration schema in src/EpisodeIdentifier.Core/Models/Configuration/AppConfiguration.cs
- [ ] T002 [P] Set up configuration validation framework for concurrency range (1-100) in src/EpisodeIdentifier.Core/Services/ConfigurationValidationService.cs
- [ ] T003 [P] Prepare test infrastructure with sample config files in tests/data/sample-configs/

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [ ] T004 [P] Contract test for maxConcurrency configuration loading in tests/contract/ConfigurationLoadingContractTests.cs
- [ ] T005 [P] Contract test for configuration validation (valid range 1-100) in tests/contract/ConfigurationValidationContractTests.cs
- [ ] T006 [P] Contract test for hot-reload behavior with maxConcurrency changes in tests/contract/ConfigurationHotReloadContractTests.cs
- [ ] T007 [P] Contract test for BulkProcessingOptions using config-based concurrency in tests/contract/BulkProcessingOptionsContractTests.cs
- [ ] T008 [P] Integration test for single file processing (maxConcurrency=1) in tests/integration/SingleFileProcessingIntegrationTests.cs
- [ ] T009 [P] Integration test for concurrent processing (maxConcurrency=3) in tests/integration/ConcurrentProcessingIntegrationTests.cs
- [ ] T010 [P] Integration test for hot-reload during active processing in tests/integration/HotReloadIntegrationTests.cs
- [ ] T011 [P] Integration test for error handling with concurrent operations in tests/integration/ConcurrentErrorHandlingIntegrationTests.cs
- [ ] T012 [P] Integration test for configuration validation fallback behavior in tests/integration/ConfigurationFallbackIntegrationTests.cs

## Phase 3.3: Core Implementation (ONLY after tests are failing)

- [ ] T013 Extend IAppConfigService interface with MaxConcurrency property in src/EpisodeIdentifier.Core/Interfaces/IAppConfigService.cs
- [ ] T014 Implement MaxConcurrency property in AppConfigService in src/EpisodeIdentifier.Core/Services/AppConfigService.cs
- [ ] T015 Add maxConcurrency validation logic in AppConfigService.LoadConfiguration method in src/EpisodeIdentifier.Core/Services/AppConfigService.cs
- [ ] T016 Modify BulkProcessingOptions initialization to read from IAppConfigService instead of Environment.ProcessorCount in src/EpisodeIdentifier.Core/Models/BulkProcessingOptions.cs
- [ ] T017 Update bulk processing command initialization to use config-based concurrency in src/EpisodeIdentifier.Core/Program.cs
- [ ] T018 [P] Add configuration validation error logging in src/EpisodeIdentifier.Core/Services/AppConfigService.cs
- [ ] T019 [P] Enhance progress reporting to include concurrent operation details in src/EpisodeIdentifier.Core/Models/BulkProcessingProgress.cs

## Phase 3.4: Integration

- [ ] T020 Integrate hot-reload detection for maxConcurrency changes in existing configuration service hot-reload mechanism
- [ ] T021 Add graceful handling of invalid configurations with fallback to default (1) 
- [ ] T022 Update JSON result aggregation to properly handle concurrent operation results
- [ ] T023 Ensure database connection pooling works efficiently with concurrent operations

## Phase 3.5: Polish

- [ ] T024 [P] Unit tests for configuration validation edge cases in tests/unit/ConfigurationValidationUnitTests.cs
- [ ] T025 [P] Unit tests for BulkProcessingOptions initialization logic in tests/unit/BulkProcessingOptionsUnitTests.cs
- [ ] T026 [P] Performance tests comparing different concurrency levels (1, 3, 5, 10) in tests/performance/ConcurrencyPerformanceTests.cs
- [ ] T027 [P] Update episodeidentifier.config.json example with maxConcurrency field and documentation
- [ ] T028 [P] Update README.md with concurrency configuration instructions
- [ ] T029 Run quickstart validation scenarios from specs/010-async-processing-where/quickstart.md
- [ ] T030 Code review and refactoring for maintainability

## Dependencies

### Critical Path
- Tests (T004-T012) before implementation (T013-T023)
- T013 (interface extension) blocks T014-T017 (implementations)
- T014 (AppConfigService) blocks T015-T018 (service modifications)
- T016 (BulkProcessingOptions) blocks T017 (Program.cs usage)
- Implementation (T013-T023) before polish (T024-T030)

### Configuration Dependencies
- T001 (schema) blocks T004-T007 (contract tests)
- T002 (validation framework) blocks T005 (validation tests)
- T014-T015 (config service) blocks T020-T021 (integration)

### Processing Dependencies
- T007 (BulkProcessingOptions contract) blocks T016 (implementation)
- T016 (options implementation) blocks T017 (Program.cs integration)
- T019 (progress reporting) can run parallel after T016 completes

## Parallel Example

```
# Phase 3.2 - Launch contract tests together (all different files):
Task: "Contract test for maxConcurrency configuration loading in tests/contract/ConfigurationLoadingContractTests.cs"
Task: "Contract test for configuration validation in tests/contract/ConfigurationValidationContractTests.cs"
Task: "Contract test for hot-reload behavior in tests/contract/ConfigurationHotReloadContractTests.cs"
Task: "Integration test for concurrent processing in tests/integration/ConcurrentProcessingIntegrationTests.cs"

# Phase 3.3 - After interface extension (T013), parallel implementation:
Task: "Add maxConcurrency validation logic in AppConfigService.LoadConfiguration"
Task: "Add configuration validation error logging in AppConfigService"
Task: "Enhance progress reporting to include concurrent operation details"

# Phase 3.5 - Polish tasks in parallel (different files):
Task: "Unit tests for configuration validation edge cases in tests/unit/ConfigurationValidationUnitTests.cs"
Task: "Performance tests comparing concurrency levels in tests/performance/ConcurrencyPerformanceTests.cs"
Task: "Update README.md with concurrency configuration instructions"
```

## Implementation Strategy

### Minimal Change Approach
- Extend existing configuration infrastructure rather than creating new systems
- Leverage existing BulkProcessingOptions.MaxConcurrency property
- Use existing hot-reload capabilities
- Maintain backward compatibility with default maxConcurrency = 1

### Test-Driven Development
- All tests must be written first and MUST FAIL
- Contract tests validate interface specifications
- Integration tests validate end-to-end behavior  
- Unit tests validate edge cases and error conditions
- Performance tests validate concurrent processing efficiency

### Key Integration Points
1. **Configuration Extension**: Add maxConcurrency to existing JSON schema
2. **Service Integration**: Extend IAppConfigService with new property
3. **Processing Integration**: Modify BulkProcessingOptions initialization
4. **Hot-Reload Integration**: Use existing configuration change detection
5. **Progress Integration**: Enhance existing progress reporting

## Validation Checklist

*GATE: Checked before task execution*

- [x] All contracts have corresponding tests (T004-T007 cover configuration contracts)
- [x] All entities have model tasks (T001 covers configuration schema extension)
- [x] All tests come before implementation (T004-T012 before T013-T023)
- [x] Parallel tasks truly independent (different files marked [P])
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task (verified file paths)
- [x] Backward compatibility maintained (default maxConcurrency = 1)
- [x] Constitutional compliance (TDD, library-first, minimal complexity)

## Notes

- Focus on extending existing infrastructure rather than building new systems
- Default maxConcurrency = 1 ensures no breaking changes
- Configuration validation provides graceful degradation
- Hot-reload enables runtime adjustment without restart
- JSON output format remains unchanged
- All concurrent operations report to existing result aggregation system