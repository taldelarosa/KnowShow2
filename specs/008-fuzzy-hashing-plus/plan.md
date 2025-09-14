# Implementation Plan: Fuzzy Hashing Plus Configuration System








**Branch**: `008-fuzzy-hashing-plus` | **Date**: September 13, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/008-fuzzy-hashing-plus/spec.md`

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








The feature implements a comprehensive configuration system for episode identification with fuzzy hashing capabilities and bulk processing. The system will read JSON configuration files for match thresholds, name confidence thresholds, and filename templates while replacing SHA1/MD5 hashing with Context-triggered piecewise hashing (CTPH). Additionally, it will support both individual video file processing and bulk directory processing with recursive discovery.

## Technical Context








**Language/Version**: C# .NET 8.0
**Primary Dependencies**: Microsoft.Data.Sqlite, System.CommandLine, FuzzySharp, ssdeep.NET, FluentValidation, System.IO.Abstractions
**Storage**: SQLite database (bones.db, production_hashes.db)
**Testing**: xUnit (standard .NET testing framework)
**Target Platform**: Cross-platform (.NET 8.0)
**Project Type**: single - console application with CLI interface
**Performance Goals**: Process large directories efficiently, handle thousands of video files
**Constraints**: CTPH algorithm requirement, JSON configuration validation, recursive directory traversal
**Scale/Scope**: Support for large media libraries, bulk processing operations

## Constitution Check








*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (EpisodeIdentifier.Core - CLI application)
- Using framework directly? Yes (direct .NET libraries, System.CommandLine for CLI)
- Single data model? Yes (Configuration, Episode, ProcessingResult models)
- Avoiding patterns? Yes (direct service usage, no unnecessary abstractions)

**Architecture**:

- EVERY feature as library? Yes - Configuration, Hashing, Processing as separate service libraries
- Libraries listed:
  - ConfigurationService (JSON config management)
  - HashingService (CTPH fuzzy hashing)
  - InputProcessorService (bulk file/directory processing)
  - FileIdentificationService (episode identification logic)
- CLI per library: Single CLI with subcommands (process-file, process-directory, configure)
- Library docs: llms.txt format planned - Yes

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? Yes (tests written first, must fail, then implement)
- Git commits show tests before implementation? Yes (contract tests → implementation)
- Order: Contract→Integration→E2E→Unit strictly followed? Yes
- Real dependencies used? Yes (actual file system, real SQLite database)
- Integration tests for: Yes - new services, contract changes, configuration schemas
- FORBIDDEN: Implementation before test, skipping RED phase - Strictly enforced

**Observability**:

- Structured logging included? Yes (Microsoft.Extensions.Logging with JSON formatting)
- Frontend logs → backend? N/A (single console application)
- Error context sufficient? Yes (detailed error reporting with context)

**Versioning**:

- Version number assigned? 0.9.0 (MAJOR.MINOR.BUILD)
- BUILD increments on every change? Yes
- Breaking changes handled? Yes (configuration migration, backward compatibility)

## Project Structure








### Documentation (this feature)








```
specs/008-fuzzy-hashing-plus/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```








### Source Code (repository root)








```

# Single project structure (current architecture)







src/EpisodeIdentifier.Core/
├── Models/
│   ├── Configuration/
│   ├── Identification/
│   └── Processing/
├── Services/
│   ├── Configuration/
│   ├── Hashing/
│   ├── Processing/
│   └── Identification/
├── Interfaces/
├── Commands/
└── Program.cs

tests/
├── contract/
├── integration/
└── unit/
```








**Structure Decision**: Option 1 (Single project) - matches existing EpisodeIdentifier.Core structure

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

Based on the completed Phase 1 design, the /tasks command will generate tasks from:

- Configuration service contract → configuration loading and validation tasks
- CTPH hashing service contract → fuzzy hashing implementation tasks
- Data model entities → model classes and validation tasks
- Bulk processing requirements → input processor and file discovery tasks
- CLI command structure → command handler implementation tasks

**Ordering Strategy**:

Following TDD principles and dependency order:

1. Contract tests for all services (parallel execution [P])
2. Model classes (Configuration, ProcessingResult, ValidationResult) [P]
3. Service interfaces and validation logic [P]
4. Core service implementations (ConfigurationService, HashingService)
5. Input processing and bulk operations (InputProcessor, file discovery)
6. CLI command integration and progress reporting
7. Integration tests for end-to-end workflows
8. Performance and error handling tests

**Estimated Output**: 28-32 numbered, ordered tasks in tasks.md

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

## Complexity Tracking








*Fill ONLY if Constitution Check has violations that must be justified*

No constitutional violations identified. The design follows all principles:

- Single project structure (simplicity)
- Library-based architecture with clear separation of concerns
- Direct framework usage without unnecessary abstractions
- Test-first development approach
- Proper observability and versioning

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
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
