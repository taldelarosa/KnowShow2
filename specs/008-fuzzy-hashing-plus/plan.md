# Implementation Plan: Fuzzy Hashing Plus Configuration System

**Branch**: `008-fuzzy-hashing-plus` | **Date**: September 12, 2025 | **Spec**: [spec.md](./spec.md)
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

Primary requirement: Implement configurable fuzzy hashing system using Context-triggered piecewise hashing (CTPH) instead of SHA1/MD5, with JSON configuration support for match thresholds, name confidence thresholds, and filename templates that reload per file processing.

## Technical Context

**Language/Version**: C# .NET 8.0  
**Primary Dependencies**: FuzzySharp 2.0.2, System.CommandLine 2.0.0-beta4, Microsoft.Extensions.Logging 8.0.0, CTPH library (TBD)  
**Storage**: SQLite (existing), JSON configuration files  
**Testing**: NUnit (from existing test structure)  
**Target Platform**: Cross-platform (.NET 8.0)
**Project Type**: single - console application with library structure  
**Performance Goals**: Per-file config reload (<10ms), fuzzy hash comparison <100ms per file pair  
**Constraints**: Maintain backward compatibility with existing workflows, config reload on every file processing  
**Scale/Scope**: Episode identification for media libraries (hundreds to thousands of files)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (single console application with library structure)
- Using framework directly? Yes (System.CommandLine, FuzzySharp, direct .NET APIs)
- Single data model? Yes (Configuration model with threshold and template entities)
- Avoiding patterns? Yes (no Repository/UoW - direct service layer)

**Architecture**:

- EVERY feature as library? Yes (Configuration library, CTPH hashing library)
- Libraries listed:
  - EpisodeIdentifier.Configuration: Config loading, validation, hot-reload
  - EpisodeIdentifier.Hashing: CTPH implementation and fuzzy matching
- CLI per library: Yes (--config-validate, --hash-test, --help, --version, --format)
- Library docs: Yes, llms.txt format planned

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? Yes (contract tests first, then integration, then unit)
- Git commits show tests before implementation? Yes (will ensure in tasks)
- Order: Contract→Integration→E2E→Unit strictly followed? Yes
- Real dependencies used? Yes (actual JSON files, real CTPH libraries)
- Integration tests for: Yes (new config library, CTPH hashing library, schema changes)
- FORBIDDEN: Implementation before test, skipping RED phase

**Observability**:

- Structured logging included? Yes (Microsoft.Extensions.Logging with JSON output)
- Frontend logs → backend? N/A (console application)
- Error context sufficient? Yes (config validation errors, hash comparison failures)

**Versioning**:

- Version number assigned? 0.8.0 (feature 008)
- BUILD increments on every change? Yes
- Breaking changes handled? Yes (parallel config structure, backward compatibility tests)

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
- Configuration Service contract → contract test task [P]
- CTPH Hashing Service contract → contract test task [P]  
- Configuration entity → model creation task [P]
- FilenamePatterns entity → model creation task [P]
- FuzzyHashResult entity → model creation task [P]
- Each quickstart step → integration test task
- Implementation tasks to make contract tests pass

**Specific Task Categories**:

1. **Contract Tests** (5 tasks):
   - ConfigurationService contract test
   - CTPhHashingService contract test  
   - Configuration validation test
   - File reload detection test
   - Backward compatibility test

2. **Model Implementation** (3 tasks):
   - Configuration entity with validation
   - FilenamePatterns entity
   - FuzzyHashResult entity with performance timing

3. **Service Implementation** (4 tasks):
   - ConfigurationService with hot-reload
   - CTPhHashingService with ssdeep integration
   - Configuration validation service
   - File system watcher service

4. **Integration Tests** (3 tasks):
   - End-to-end config loading and validation
   - Fuzzy hash comparison workflow
   - Hot-reload during file processing

**Ordering Strategy**:

- TDD order: Contract tests → Models → Services → Integration tests
- Dependency order: Configuration entities → Hashing services → Integration
- Mark [P] for parallel execution (independent libraries)

**Estimated Output**: 15-18 numbered, ordered tasks in tasks.md

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
- [x] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
