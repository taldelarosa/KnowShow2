
# Implementation Plan: Identify Season and Episode from AV1 Video via PGS Subtitle Comparison (CLI, JSON Output)

**Branch**: `002-build-an-application` | **Date**: September 7, 2025 | **Spec**: [/mnt/c/Users/Ragma/KnowShow_Specd/specs/002-build-an-application/spec.md]
**Input**: Feature specification from `/specs/002-build-an-application/spec.md`

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
This feature provides a CLI-only tool to identify the Season and Episode of an AV1 video file by extracting PGS subtitles and comparing them to known, labelled subtitles stored as text files in a Subtitles=>Series=>Season folder structure. The tool uses standalone utilities for video/subtitle extraction, fuzzy hashing for comparison, and a local SQLite database for hash storage. All output is in JSON format for automation.


## Technical Context
**Language/Version**: C# (latest stable)  
**Primary Dependencies**: ffmpeg, mkvextract, sqlite3, fuzzy hash tool (e.g., ssdeep), .NET CLI  
**Storage**: Text files (subtitles), SQLite (hashes)  
**Testing**: .NET test, CLI contract tests, integration tests  
**Target Platform**: Linux server  
**Project Type**: single (CLI tool, supporting libraries)  
**Performance Goals**: Fast enough for automation; <5s per file typical  
**Constraints**: CLI-only, JSON output, no interactive prompts, must run in automated workflows  
**Scale/Scope**: Single-user, batch/automation use, local or mounted file shares

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:
- Projects: 1 (cli + supporting libraries)
- Using framework directly: Yes (C#/.NET CLI)
- Single data model: Yes (see data-model.md)
- Avoiding patterns: Yes (no unnecessary abstractions)

**Architecture**:
- Feature as library: Yes (core logic in library, CLI as entrypoint)
- Libraries listed: core (identification logic), cli (entrypoint)
- CLI per library: Yes (see contracts/cli-contract.md)
- Library docs: Planned in llms.txt format

**Testing (NON-NEGOTIABLE)**:
- RED-GREEN-Refactor cycle enforced? (test MUST fail first)
- Git commits show tests before implementation?
- Order: Contract→Integration→E2E→Unit strictly followed?
- Real dependencies used? (actual DBs, not mocks)
- Integration tests for: new libraries, contract changes, shared schemas?
- FORBIDDEN: Implementation before test, skipping RED phase

**Observability**:
- Structured logging included?
- Frontend logs → backend? (unified stream)
- Error context sufficient?

**Versioning**:
- Version number assigned? (MAJOR.MINOR.BUILD)
- BUILD increments on every change?
- Breaking changes handled? (parallel tests, migration plan)

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

**Structure Decision**: [DEFAULT to Option 1 unless Technical Context indicates web/mobile app]


## Phase 0: Outline & Research
See research.md for open questions, technology choices, best practices, and decisions. All clarifications and research tasks are tracked there.


## Phase 1: Design & Contracts
See data-model.md for entities, contracts/ for CLI and DB contracts, quickstart.md for usage and test scenarios. Contract tests are defined for CLI and fuzzy hash DB. All outputs are in the specs/002-build-an-application directory.


## Phase 2: Task Planning Approach
The /tasks command will generate tasks based on contracts, data model, and quickstart. Tasks will include contract tests, model creation, integration tests, and implementation steps, ordered for TDD and parallelizable where possible. See tasks-template.md for base structure. Estimated 25-30 tasks.

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |



## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [x] Phase 3: Tasks generated (/tasks command)
- [x] Phase 4: Implementation complete
- [x] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*