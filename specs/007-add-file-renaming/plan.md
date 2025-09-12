# Implementation Plan: Add File Renaming Recommendations

**Branch**: `007-add-file-renaming` | **Date**: September 10, 2025 | **Spec**: [spec.md](/mnt/c/Users/Ragma/KnowShow_Specd/specs/007-add-file-renaming/spec.md)
**Input**: Feature specification from `/specs/007-add-file-renaming/spec.md`

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

The file renaming recommendations feature enhances the episode identification system to include suggested filenames in JSON responses for high-confidence episode identifications. The feature adds a new "suggestedFilename" field with standardized naming (SeriesName - S##E## - EpisodeName.mkv) and includes an optional rename flag for automatic file renaming.

## Technical Context

**Language/Version**: C# 8.0 with .NET 8.0 SDK  
**Primary Dependencies**: Microsoft.Data.Sqlite, Microsoft.Extensions.Logging, System.CommandLine, FuzzySharp  
**Storage**: SQLite database for fuzzy hash storage and subtitle metadata  
**Testing**: NUnit with contract, integration, and unit test layers  
**Target Platform**: Linux (primary), Windows filesystem compatibility required  
**Project Type**: Single project (console application with library structure)  
**Performance Goals**: Filename generation <10ms, Windows filesystem compliance  
**Constraints**: 260-character Windows filename limit, Windows-disallowed character replacement  
**Scale/Scope**: Per-request filename generation, database schema migration, CLI parameter addition

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (EpisodeIdentifier.Core - within max 3)
- Using framework directly? (Yes - System.CommandLine, Microsoft.Data.Sqlite)
- Single data model? (Yes - extends existing IdentificationResult)
- Avoiding patterns? (Yes - direct SQLite access, no Repository pattern)

**Architecture**:

- EVERY feature as library? (Yes - FilenameService as library component)
- Libraries listed: FilenameService (filename generation and sanitization)
- CLI per library: (Integrated into existing --rename flag)
- Library docs: llms.txt format planned? (Yes - follows existing pattern)

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? (Yes - contract tests first)
- Git commits show tests before implementation? (Yes - TDD approach)
- Order: Contract→Integration→E2E→Unit strictly followed? (Yes)
- Real dependencies used? (Yes - actual SQLite database)
- Integration tests for: contract changes (IdentificationResult), shared schemas (SQLite)
- FORBIDDEN: Implementation before test, skipping RED phase

**Observability**:

- Structured logging included? (Yes - uses existing Microsoft.Extensions.Logging)
- Frontend logs → backend? (N/A - console application)
- Error context sufficient? (Yes - file operation errors, validation failures)

**Versioning**:

- Version number assigned? (007.1.0 following existing pattern)
- BUILD increments on every change? (Yes)
- Breaking changes handled? (No breaking changes - additive JSON field)

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

1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:

   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts

*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `/scripts/update-agent-context.sh [claude|gemini|copilot]` for your AI assistant
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach

*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:

- Load `/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- Each contract → contract test task [P] (FilenameService, FileRenameService, CLI)
- Each entity → model creation task [P] (FilenameGenerationRequest/Result, FileRenameRequest/Result)
- Each user story → integration test task (filename suggestion, automatic rename, error scenarios)
- Implementation tasks to make tests pass (services, CLI integration, database migration)

**Ordering Strategy**:

- TDD order: Tests before implementation (contract tests first, then integration tests)
- Dependency order: Models → Services → CLI integration → Database migration
- Mark [P] for parallel execution (independent files: models, separate service implementations)
- Sequential: Database migration must happen before service integration

**Estimated Output**: 20-25 numbered, ordered tasks in tasks.md

**Task Categories**:

1. **Phase 3.1**: Setup and model creation (5 tasks) - All [P]
2. **Phase 3.2**: Contract tests for services (6 tasks) - FilenameService [P], FileRenameService [P], CLI tests
3. **Phase 3.3**: Service implementations (4 tasks) - Make contract tests pass
4. **Phase 3.4**: Integration (6 tasks) - Database migration, CLI integration, end-to-end tests
5. **Phase 3.5**: Polish (4 tasks) - Error handling, logging, documentation

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

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
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (None required)

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
