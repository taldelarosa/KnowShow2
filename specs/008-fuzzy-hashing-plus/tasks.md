# Implementation Plan: Bulk Processing Extension for Episode Identification


**Branch**: `009-bulk-processing-extension` | **Date**: September 13, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/009-bulk-processing-extension/spec.md`

## Execution Flow (/plan command scope)


```

1. Load feature spec from Input path
   → Feature loaded: Bulk processing extension for video file identification




2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Project Type: single (extending existing console application)



   → Structure Decision: Extend existing EpisodeIdentifier.Core structure

3. Evaluate Constitution Check section below
   → No violations: extends existing architecture with library-based services



   → Update Progress Tracking: Initial Constitution Check - PASS

4. Execute Phase 0 → research.md
   → Research bulk processing patterns, file discovery, progress reporting




5. Execute Phase 1 → contracts, data-model.md, quickstart.md, .github/copilot-instructions.md
   → Design service contracts for bulk operations




6. Re-evaluate Constitution Check section
   → Post-design check: maintains constitutional compliance



   → Update Progress Tracking: Post-Design Constitution Check - PASS

7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
8. STOP - Ready for /tasks command
```


**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:

- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary


The feature extends the existing episode identification system with comprehensive bulk processing capabilities. Users can process individual video files or entire directories with recursive traversal, progress feedback, error handling, and performance optimization. The system maintains memory efficiency during large-scale operations and provides detailed reporting on processing outcomes.

## Technical Context


**Language/Version**: C# .NET 8.0 (extending existing project)
**Primary Dependencies**: System.IO.Abstractions, Microsoft.Extensions.Logging, System.CommandLine (existing stack)
**Storage**: Existing SQLite databases (bones.db, production_hashes.db)
**Testing**: xUnit (standard .NET testing framework)
**Target Platform**: Cross-platform (.NET 8.0)
**Project Type**: single - extending existing console application
**Performance Goals**: Handle thousands of video files efficiently, <2GB memory usage for large directories
**Constraints**: Memory-efficient streaming processing, graceful error handling, progress reporting
**Scale/Scope**: Support for media libraries with 10,000+ files, recursive directory processing

## Constitution Check


*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (extending existing EpisodeIdentifier.Core CLI application)
- Using framework directly? ✅ YES (System.CommandLine, System.IO, .NET APIs directly)
- Single data model? ✅ YES (BulkProcessingRequest/Result/Options - no DTOs, models serve all layers)
- Avoiding patterns? ✅ YES (no Repository pattern - direct service composition, no unnecessary abstraction layers)

**Architecture**:

- EVERY feature as library? ✅ YES (IBulkProcessor, IFileDiscoveryService, IProgressTracker services in EpisodeIdentifier.Core)
- Libraries listed:
    - BulkProcessorService: orchestrates bulk file processing workflows
    - FileDiscoveryService: handles file enumeration and filtering
    - ProgressTracker: manages progress reporting and statistics
- CLI per library: ✅ YES
    - `process-file --help` (single file processing)
    - `process-directory --help --recursive --max-errors` (bulk directory processing)
- Library docs: ✅ YES (comprehensive XML documentation for all public APIs, examples in quickstart.md)

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? ✅ YES (tasks.md explicitly orders contract tests → unit tests → integration tests)
- Git commits show tests before implementation? ✅ YES (Phase 1 creates all contract tests before Phase 4 implementation)
- Order: Contract→Integration→E2E→Unit strictly followed? ✅ YES (Task 4: Contract Tests → Task 21: Integration → Task 18: E2E CLI → Task 14: Unit)
- Real dependencies used? ✅ YES (integration tests use real file system, real database, real episode identification services)
- Integration tests for: ✅ YES (new IBulkProcessor service, contract changes for existing services, shared BulkProcessing* models)
- FORBIDDEN: Implementation before test ✅ ENFORCED (tasks.md shows all test tasks before implementation tasks, TDD workflow mandatory)

**Observability**:

- Structured logging included? ✅ YES (uses existing ILogger<T> infrastructure, structured progress reporting, error categorization)
- Frontend logs → backend? ✅ N/A (CLI application - console output unified with application logging)
- Error context sufficient? ✅ YES (BulkProcessingError with file paths, error types, timestamps, exception details)

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
- Each contract → contract test task [P]
- Each entity → model creation task [P]
- Each user story → integration test task
- Implementation tasks to make tests pass

**Ordering Strategy**:

- TDD order: Tests before implementation
- Dependency order: Models before services before UI
- Mark [P] for parallel execution (independent files)

**Estimated Output**: 25-30 numbered, ordered tasks in tasks.md

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

- [ ] Phase 0: Research complete (/plan command)
- [ ] Phase 1: Design complete (/plan command)
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [ ] Initial Constitution Check: PASS
- [ ] Post-Design Constitution Check: PASS
- [ ] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
