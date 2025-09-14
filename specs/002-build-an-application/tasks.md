# Tasks: Identify Season and Episode from AV1 Video via PGS Subtitle Comparison (CLI, JSON Output)








**Input**: Design documents from `/specs/002-build-an-application/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)








```

1. Load plan.md from feature directory
2. Load optional design documents: data-model.md, contracts/, research.md, quickstart.md
3. Generate tasks by category: setup, tests, core, integration, polish
4. Apply task rules: parallelize where possible, tests before implementation
5. Number tasks sequentially (T001, T002...)
6. Validate task completeness
7. Return: SUCCESS (tasks ready for execution)
```








## Phase 3.1: Setup








- [x] T001 Create project structure in `src/` and `tests/` per plan.md
- [x] T002 Initialize C# project with .NET CLI in `src/`
- [x] T003 [P] Add dependencies: ffmpeg, mkvextract, sqlite3, fuzzy hash tool (e.g., ssdeep) to project and document in README
- [ ] T004 [P] Configure linting and formatting tools for C# (.editorconfig, dotnet-format)

## Phase 3.2: Tests First (TDD)








- [x] T005 [P] Write contract test for CLI output in `tests/contract/test_cli_output.cs` (see contracts/cli-contract-test.md)
- [x] T006 [P] Write contract test for fuzzy hash DB in `tests/contract/test_fuzzy_hash_db.cs` (see contracts/fuzzy-hash-db-contract-test.md)
- [x] T007 [P] Write integration test for end-to-end identification in `tests/integration/test_identification.cs` (see quickstart.md)
- [ ] T008 [P] Write integration test for ambiguous/partial match in `tests/integration/test_ambiguity.cs`
- [ ] T009 [P] Write integration test for language mismatch in `tests/integration/test_language_mismatch.cs`

## Phase 3.3: Core Implementation








- [x] T010 [P] Implement VideoFile and PGSSubtitle models in `src/models/`
- [x] T011 [P] Implement LabelledSubtitle and IdentificationResult models in `src/models/`
- [x] T012 Implement CLI entrypoint in `src/cli/IdentifyEpisode.cs` (parse args, enforce JSON output)
- [x] T013 Implement subtitle extraction logic using ffmpeg/mkvextract in `src/services/SubtitleExtractor.cs`
- [x] T014 Implement fuzzy hash computation and DB logic in `src/services/FuzzyHashService.cs`
- [x] T015 Implement subtitle comparison and matching logic in `src/services/SubtitleMatcher.cs`
- [x] T016 Integrate all services in CLI entrypoint, ensure contract compliance

## Phase 3.4: Integration & Logging








- [ ] T017 Implement logging of identification attempts in `src/services/LoggingService.cs`
- [ ] T018 Integrate logging into CLI and services
- [x] T019 [P] Implement error handling and structured error output in all services

## Phase 3.5: Polish & Documentation








- [x] T020 [P] Write unit tests for all models and services in `tests/unit/`
- [ ] T021 [P] Write performance test for batch processing in `tests/performance/test_batch.cs`
- [x] T022 [P] Write/Update quickstart and usage docs in `specs/002-build-an-application/quickstart.md`
- [x] T023 [P] Review and update README with setup and usage instructions

## Parallel Execution Guidance








- Tasks marked [P] can be executed in parallel (different files, no dependencies)
- Example: T003, T004, T005, T006, T007, T008, T009, T010, T011, T020, T021, T022, T023 can be run in parallel after setup

---
