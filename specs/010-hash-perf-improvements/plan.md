# Implementation Plan: Hash Performance Improvements with Series/Season Filtering


**Branch**: `010-hash-perf-improvements` | **Date**: October 7, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow2-charlie/specs/010-hash-perf-improvements/spec.md`

## Execution Flow (/plan command scope)


```

1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"









2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)








   → Set Structure Decision based on project type

3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking








   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check

4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"









5. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, or `GEMINI.md` for Gemini CLI).
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


This feature adds optional series name and season number parameters to hash-based episode identification searches, enabling dramatic performance improvements when users have context about the content they're identifying. By filtering database queries to specific series/seasons instead of searching the entire database, search operations can complete significantly faster. The system maintains full backwards compatibility for users without series/season knowledge.

## Technical Context


**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: Microsoft.Data.Sqlite 8.0.0, ssdeep.NET 1.0.0, System.CommandLine 2.0.0-beta4.22272.1
**Storage**: SQLite database with SubtitleHashes table (Series, Season, Episode, hash columns)
**Testing**: xUnit (existing test infrastructure)
**Target Platform**: Cross-platform CLI application (Linux, Windows, macOS)
**Project Type**: Single project (src/EpisodeIdentifier.Core)
**Performance Goals**: Measurable search speed improvement with series/season filtering vs. full database search
**Constraints**: No memory usage increase compared to current implementation; maintain backwards compatibility
**Scale/Scope**: Current database has 246 episodes (Bones complete series); system designed for thousands of episodes across multiple series

## Constitution Check


*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (EpisodeIdentifier.Core only)
- Using framework directly? YES (System.CommandLine, Microsoft.Data.Sqlite directly)
- Single data model? YES (existing SubtitleHashes table, no DTOs needed)
- Avoiding patterns? YES (direct database queries, no Repository/UoW pattern)

**Architecture**:

- EVERY feature as library? YES (FuzzyHashService is library with CLI interface)
- Libraries listed:
  - FuzzyHashService: Hash generation, storage, and filtered searching
  - SubtitleNormalizationService: Text normalization (existing)
  - ConfigurationService: Configuration management (existing)
- CLI per library: YES (existing `config` command structure, will extend for filtering params)
- Library docs: llms.txt format used in documentation

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? YES (will write failing tests first)
- Git commits show tests before implementation? YES (will commit tests, verify failure, then implement)
- Order: Contract→Integration→E2E→Unit strictly followed? YES
- Real dependencies used? YES (actual SQLite database, not mocks)
- Integration tests for: new libraries, contract changes, shared schemas? YES (database query filtering is contract change)
- FORBIDDEN: Implementation before test, skipping RED phase - **ACKNOWLEDGED**

**Observability**:

- Structured logging included? YES (using Microsoft.Extensions.Logging, already in place)
- Frontend logs → backend? N/A (CLI application only)
- Error context sufficient? YES (will log filter parameters, query performance, match counts)

**Versioning**:

- Version number assigned? 0.10.0 (MINOR increment - new optional functionality)
- BUILD increments on every change? YES (following existing pattern)
- Breaking changes handled? NO breaking changes (optional parameters only)

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


**Structure Decision**: Option 1 (Single project - EpisodeIdentifier.Core)

## Phase 0: Outline & Research ✅ COMPLETE


**Output**: `research.md` with all technical decisions documented

Key research findings:

- Database query filtering using WHERE clause with optional parameters
- Leverage existing idx_series_season index for performance
- Optional nullable parameters for backwards compatibility
- ArgumentException for invalid parameter combinations (season without series)
- Performance measurement using Stopwatch and ILogger

## Phase 1: Design & Contracts ✅ COMPLETE


**Outputs**:

- `data-model.md`: Search filter parameters, method signatures, database query construction
- `contracts/FindMatches-API.md`: Service method contract with filtering parameters
- `contracts/CLI-identify-command.md`: CLI command contract with --series and --season options
- `quickstart.md`: Feature validation steps and testing guide

Key design artifacts:

- Method signature extension with optional seriesFilter and seasonFilter parameters
- Dynamic SQL WHERE clause building for conditional filtering
- CLI option definitions for System.CommandLine framework
- Validation rules and error handling specifications
- Performance measurement logging contract

## Phase 2: Task Planning Approach


*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:

The /tasks command will:

1. Load `/templates/tasks-template.md` as base structure
2. Generate tasks from Phase 1 design artifacts:
   - Contract tests for FuzzyHashService.FindMatches method extension
   - Contract tests for CLI identify command new options
   - Unit tests for parameter validation logic
   - Integration tests for filtered database queries
   - Integration tests for performance measurement
   - Implementation tasks to satisfy contracts

**Task Categories**:

1. **Contract Tests** (TDD - RED phase):
   - T001: Contract test - FindMatches accepts optional series filter [P]
   - T002: Contract test - FindMatches accepts optional season filter [P]
   - T003: Contract test - Season without series throws ArgumentException [P]
   - T004: Contract test - Case-insensitive series matching [P]
   - T005: Contract test - CLI --series option parsing [P]
   - T006: Contract test - CLI --season option parsing [P]

2. **Implementation Tasks** (TDD - GREEN phase):
   - T007: Implement FindMatches parameter validation
   - T008: Implement dynamic SQL WHERE clause building
   - T009: Implement CLI --series option
   - T010: Implement CLI --season option and validation
   - T011: Implement performance logging

3. **Integration Tests** (Real database testing):
   - T012: Integration test - Multi-series database filtering
   - T013: Integration test - Performance measurement comparison
   - T014: Integration test - Backwards compatibility validation

4. **Documentation and Cleanup**:
   - T015: Update README with new CLI options
   - T016: Run performance benchmarks and document results

**Ordering Strategy**:

- **Constitutional TDD order**: Contract tests (RED) → Implementation (GREEN) → Integration tests
- **Dependency order**: Parameter validation → Query building → CLI integration
- **Parallel execution**: All contract tests marked [P] (independent files)
- **Sequential implementation**: Implementation tasks depend on contract tests passing

**Estimated Task Count**: 16-20 tasks total

**Test-First Commitment**:

- ALL contract tests written before ANY implementation
- Git commit after contract tests (verifying RED phase)
- Implementation only proceeds after test failures verified
- Integration tests validate real database performance

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation


*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking


*Fill ONLY if Constitution Check has violations that must be justified*

**No violations found** - All constitutional requirements satisfied:

- Single project architecture maintained
- Library-first approach with CLI interface
- TDD workflow enforced
- No unnecessary patterns or abstractions
- Backwards compatible extension

## Progress Tracking


*This checklist is updated during execution flow*

**Phase Status**:

- [x] Phase 0: Research complete (/plan command) ✅
- [x] Phase 1: Design complete (/plan command) ✅
- [x] Phase 2: Task planning complete (/plan command - describe approach only) ✅
- [x] Phase 3: Tasks generated (/tasks command) ✅
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS ✅
- [x] Post-Design Constitution Check: PASS ✅
- [x] All NEEDS CLARIFICATION resolved ✅
- [x] Complexity deviations documented: N/A (no deviations) ✅

**Artifacts Generated**:

- [x] `/specs/010-hash-perf-improvements/spec.md` ✅
- [x] `/specs/010-hash-perf-improvements/research.md` ✅
- [x] `/specs/010-hash-perf-improvements/data-model.md` ✅
- [x] `/specs/010-hash-perf-improvements/contracts/FindMatches-API.md` ✅
- [x] `/specs/010-hash-perf-improvements/contracts/CLI-identify-command.md` ✅
- [x] `/specs/010-hash-perf-improvements/quickstart.md` ✅
- [x] `/.github/copilot-instructions.md` (updated) ✅
- [x] `/specs/010-hash-perf-improvements/tasks.md` ✅
- [ ] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
