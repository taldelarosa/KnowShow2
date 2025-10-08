# Tasks: Hash Performance Improvements with Series/Season Filtering

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow2-charlie/specs/010-hash-perf-improvements/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Overview

This feature adds optional `--series` and `--season` parameters to the identify command for performance optimization through database query filtering. All changes are backwards compatible and follow strict TDD principles.

**Tech Stack**: C# / .NET 8.0, SQLite, System.CommandLine, xUnit
**Key Files**:
- `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
- `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Commands/IdentifyCommands.cs` (to be created/modified)
- `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/FilteredHashSearchTests.cs` (to be created)
- `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration/SeriesSeasonFilteringTests.cs` (to be created)

## Phase 3.1: Setup

- [ ] **T001** Verify existing test infrastructure supports multi-series databases
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/TestDatabaseConfig.cs`
  - Verify: Existing TestDatabaseConfig can handle multiple series
  - Action: Add helper methods if needed for multi-series test data

- [ ] **T002** Create test data fixtures for multi-series scenarios
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/data/multi-series/`
  - Create: Sample subtitle files for Bones, Breaking Bad, The Office (3+ episodes each)
  - Format: Follow existing subtitle file structure

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests (Parallel - Different Files)

- [ ] **T003** [P] Contract test: FindMatches accepts optional seriesFilter parameter
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/FilteredHashSearchTests.cs`
  - Test: Call `FindMatches(text, 0.8, "Bones", null)` compiles and runs
  - Assert: Method signature accepts seriesFilter parameter
  - Expected: FAILS (parameter doesn't exist yet)

- [ ] **T004** [P] Contract test: FindMatches accepts optional seasonFilter parameter
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/FilteredHashSearchTests.cs`
  - Test: Call `FindMatches(text, 0.8, "Bones", 2)` compiles and runs
  - Assert: Method signature accepts seasonFilter parameter
  - Expected: FAILS (parameter doesn't exist yet)

- [ ] **T005** [P] Contract test: Season without series throws ArgumentException
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/FilteredHashSearchTests.cs`
  - Test: Call `FindMatches(text, 0.8, null, 2)`
  - Assert: Throws ArgumentException with message about requiring series
  - Expected: FAILS (validation doesn't exist yet)

- [ ] **T006** [P] Contract test: Case-insensitive series matching
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/FilteredHashSearchTests.cs`
  - Test: Search with "BONES", "bones", "Bones" all return same results
  - Assert: All three queries return identical matches
  - Expected: FAILS (case-insensitive logic doesn't exist yet)

- [ ] **T007** [P] Contract test: CLI --series option exists and parses
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/CLIFilteringTests.cs`
  - Test: Parse command with `--series "Bones"`
  - Assert: Series option accepted without error
  - Expected: FAILS (CLI option doesn't exist yet)

- [ ] **T008** [P] Contract test: CLI --season option exists and requires --series
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract/CLIFilteringTests.cs`
  - Test: Parse command with `--season 2` (no --series)
  - Assert: Validation error returned
  - Expected: FAILS (CLI option and validation don't exist yet)

### Integration Tests (Parallel - Different Files)

- [ ] **T009** [P] Integration test: Multi-series database filtering
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration/SeriesSeasonFilteringTests.cs`
  - Setup: Database with Bones (246 eps), Breaking Bad (62 eps), The Office (201 eps)
  - Test: Search with --series "Bones" returns only Bones episodes
  - Assert: No Breaking Bad or The Office episodes in results
  - Expected: FAILS (filtering doesn't exist yet)

- [ ] **T010** [P] Integration test: Series + Season filtering reduces record count
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration/SeriesSeasonFilteringTests.cs`
  - Setup: Same multi-series database
  - Test: Search with --series "Bones" --season 2
  - Assert: Scanned record count ≤ 25 (approximately one season)
  - Expected: FAILS (filtering doesn't exist yet)

- [ ] **T011** [P] Integration test: Empty results for non-existent series
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration/SeriesSeasonFilteringTests.cs`
  - Test: Search with --series "NonExistentShow"
  - Assert: Returns empty list (not exception), exit code 0
  - Expected: FAILS (graceful handling doesn't exist yet)

- [ ] **T012** [P] Integration test: Backwards compatibility validation
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration/BackwardsCompatibilityTests.cs`
  - Test: Call existing `FindMatches(text)` and `FindMatches(text, 0.8)`
  - Assert: Works exactly as before, all existing tests pass
  - Expected: SHOULD PASS (no breaking changes), re-run after implementation

### Performance Tests

- [ ] **T013** [P] Performance test: Measure search time improvements
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/tests/performance/FilteringPerformanceTests.cs`
  - Test: Compare execution times for no filter vs series vs series+season
  - Assert: Filtered queries measurably faster (any improvement acceptable)
  - Expected: FAILS (filtering doesn't exist yet)

**🔴 RED PHASE CHECKPOINT**: Commit all tests, verify they fail, then proceed to implementation

## Phase 3.3: Core Implementation (ONLY after tests are failing)

### FuzzyHashService Method Extension

- [ ] **T014** Extend FindMatches method signature with optional parameters
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Add `string? seriesFilter = null, int? seasonFilter = null` parameters
  - Location: Line ~352 (existing FindMatches method)
  - Verify: All existing calls compile without changes

- [ ] **T015** Implement parameter validation logic
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Add validation at start of FindMatches method
  - Logic: If seasonFilter.HasValue && string.IsNullOrWhiteSpace(seriesFilter) → throw ArgumentException
  - Message: "Season filter requires series filter to be specified"

- [ ] **T016** Implement dynamic SQL WHERE clause building
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Build SQL query dynamically based on filter parameters
  - Logic: Add WHERE clauses for LOWER(Series) = LOWER(@series) and/or Season = @season
  - Location: Line ~376 (existing query construction)

- [ ] **T017** Implement parameterized query execution
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Add SqlParameter for @series and @season when filters provided
  - Format: Season as zero-padded string ("02") for database matching

- [ ] **T018** Add performance logging for filtered queries
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Log filter parameters and record counts
  - Message: "Search filter applied: Series='{Series}', Season={Season}"
  - Metric: "scanned {Count} records" in completion message

### CLI Command Extension

- [ ] **T019** Add --series option to identify command
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Commands/IdentifyCommands.cs` (or create if doesn't exist)
  - Action: Add Option<string?> for --series with alias -s
  - Description: "Filter search to specific TV series (case-insensitive)"
  - Default: null

- [ ] **T020** Add --season option to identify command
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Commands/IdentifyCommands.cs`
  - Action: Add Option<int?> for --season with alias -n
  - Description: "Filter search to specific season (requires --series)"
  - Default: null

- [ ] **T021** Wire CLI options to FuzzyHashService.FindMatches call
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Commands/IdentifyCommands.cs`
  - Action: Pass series and season parameters from command handler to FindMatches
  - Validation: CLI-level validation for season-without-series (before service call)

- [ ] **T022** Add error handling for invalid parameter combinations
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Commands/IdentifyCommands.cs`
  - Action: Catch ArgumentException from FindMatches
  - Output: Clear error message to stderr, exit code 1

**🟢 GREEN PHASE CHECKPOINT**: All tests should now pass

## Phase 3.4: Integration & Refinement

- [ ] **T023** Verify existing tests still pass (backwards compatibility)
  - Command: `cd /mnt/c/Users/Ragma/KnowShow2-charlie && dotnet test`
  - Assert: All existing tests pass without modification
  - Action: Fix any regressions immediately

- [ ] **T024** Test with real production database (production_hashes.db)
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/production_hashes.db`
  - Test: Run identify command with Bones Season 2 filter
  - Verify: Matches correctly, performance improvement observed

- [ ] **T025** Measure and document actual performance improvements
  - Script: Create simple benchmark script
  - Test: Compare no filter vs series vs series+season on production DB
  - Document: Record timings in quickstart.md or new PERFORMANCE.md

## Phase 3.5: Polish & Documentation

- [ ] **T026** [P] Add XML documentation comments to new parameters
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core/Services/FuzzyHashService.cs`
  - Action: Add /// <param> comments for seriesFilter and seasonFilter
  - Content: Describe purpose, nullability, case-insensitivity

- [ ] **T027** [P] Update README.md with new CLI options
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/README.md`
  - Action: Add examples showing --series and --season usage
  - Include: Performance benefits explanation

- [ ] **T028** [P] Run quickstart.md validation steps
  - Path: `/mnt/c/Users/Ragma/KnowShow2-charlie/specs/010-hash-perf-improvements/quickstart.md`
  - Action: Execute all validation steps manually
  - Verify: All success criteria met

- [ ] **T029** Code review and cleanup
  - Review: Check for code duplication, magic numbers, unclear variable names
  - Refactor: Extract constants for error messages if needed
  - Verify: Follows existing code style conventions

- [ ] **T030** Final test run and commit
  - Command: `dotnet test --verbosity normal`
  - Verify: All tests pass, including new and existing
  - Commit: "Feature 010: Add series/season filtering for performance optimization"

## Dependencies

```
Setup (T001-T002)
    ↓
Contract Tests (T003-T008) [Parallel]
    ↓
Integration Tests (T009-T013) [Parallel]
    ↓
🔴 RED CHECKPOINT (verify failures)
    ↓
Core Implementation (T014-T022) [Sequential - same file]
    ↓
🟢 GREEN CHECKPOINT (verify passes)
    ↓
Integration & Refinement (T023-T025) [Sequential]
    ↓
Polish & Documentation (T026-T030) [Parallel possible]
```

## Parallel Execution Examples

### Parallel Block 1: Contract Tests (After Setup)

```bash
# Can run simultaneously - different test files
dotnet test --filter "FullyQualifiedName~FilteredHashSearchTests.AcceptsSeriesFilter"
dotnet test --filter "FullyQualifiedName~FilteredHashSearchTests.AcceptsSeasonFilter"
dotnet test --filter "FullyQualifiedName~FilteredHashSearchTests.SeasonWithoutSeriesThrows"
dotnet test --filter "FullyQualifiedName~FilteredHashSearchTests.CaseInsensitiveMatching"
dotnet test --filter "FullyQualifiedName~CLIFilteringTests.SeriesOptionExists"
dotnet test --filter "FullyQualifiedName~CLIFilteringTests.SeasonOptionRequiresSeries"
```

### Parallel Block 2: Integration Tests (After Contract Tests)

```bash
# Can run simultaneously - different test files
dotnet test --filter "FullyQualifiedName~SeriesSeasonFilteringTests"
dotnet test --filter "FullyQualifiedName~BackwardsCompatibilityTests"
dotnet test --filter "FullyQualifiedName~FilteringPerformanceTests"
```

### Parallel Block 3: Documentation (After Implementation)

```bash
# Can work on simultaneously - different files
# Task: Update README.md
# Task: Add XML docs to FuzzyHashService.cs
# Task: Run quickstart.md validation
```

## Success Criteria

✅ All contract tests pass
✅ All integration tests pass
✅ All existing tests still pass (backwards compatibility)
✅ Performance improvement measurable and documented
✅ CLI help text includes new options
✅ Error messages clear and helpful
✅ Code follows existing style conventions
✅ Zero breaking changes to API or CLI

## Notes

- **TDD Enforcement**: Do NOT skip the RED phase. Tests must fail before implementation.
- **Backwards Compatibility**: Critical requirement. All existing code must work unchanged.
- **Performance**: Any measurable improvement is acceptable per specification.
- **Case Sensitivity**: Series matching must be case-insensitive per FR-006.
- **Database**: No schema changes required - uses existing Series/Season columns.

---

*Tasks ready for execution. Follow TDD cycle strictly: RED → GREEN → REFACTOR*
