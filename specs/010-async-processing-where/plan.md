# Implementation Plan: Async Processing with Configurable Concurrency


**Branch**: `010-async-processing-where` | **Date**: September 15, 2025 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-async-processing-where/spec.md`

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


Implementation of configurable concurrent processing for episode identification workflow. Users can set concurrency level via `episodeidentifier.config.json` (default: 1) to process multiple video files simultaneously through subtitle ripping, hashing, database checking, and renaming stages. System maintains processing queue, handles failures gracefully, and outputs comprehensive JSON results at completion. Hot-reload enables runtime configuration changes without restart.

## Technical Context


**Language/Version**: C# .NET 8.0
**Primary Dependencies**: System.CommandLine, Microsoft.Extensions.Logging, System.IO.Abstractions, System.Text.Json
**Storage**: SQLite database for hash storage, JSON configuration file for settings
**Testing**: xUnit with existing unit/integration test structure
**Target Platform**: Cross-platform CLI application (.NET 8.0)
**Project Type**: single - extends existing EpisodeIdentifier.Core CLI application
**Performance Goals**: Support configurable concurrent processing (1-100 concurrent operations) with efficient resource utilization
**Constraints**: Must integrate with existing hot-reload configuration system, preserve existing JSON output format, maintain backward compatibility
**Scale/Scope**: Enhance existing bulk processing to read concurrency from config file instead of hardcoded Environment.ProcessorCount

**Existing Infrastructure**:

- BulkProcessingOptions already exists with MaxConcurrency property (currently defaults to Environment.ProcessorCount)
- Configuration system exists with episodeidentifier.config.json and hot-reload capability
- Progress reporting and error handling already implemented
- Async processing infrastructure already in place

## Constitution Check


*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (extending existing EpisodeIdentifier.Core - max 3 ✓)
- Using framework directly? Yes (System.CommandLine, System.Text.Json ✓)
- Single data model? Yes (extending existing configuration model ✓)
- Avoiding patterns? Yes (no Repository/UoW - using direct services ✓)

**Architecture**:

- EVERY feature as library? Yes (functionality in EpisodeIdentifier.Core library ✓)
- Libraries listed: EpisodeIdentifier.Core + purpose: Episode identification with configurable concurrency
- CLI per library: Existing CLI with --bulk-identify command enhanced with config-based concurrency
- Library docs: Will update existing llms.txt format

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? YES (tests written first, must fail, then implement ✓)
- Git commits show tests before implementation? YES (will follow TDD ✓)
- Order: Contract→Integration→E2E→Unit strictly followed? YES ✓
- Real dependencies used? YES (actual config files, SQLite DB ✓)
- Integration tests for: config changes, concurrent processing, hot-reload behavior ✓
- FORBIDDEN: Implementation before test, skipping RED phase ✓

**Observability**:

- Structured logging included? YES (Microsoft.Extensions.Logging already used ✓)
- Frontend logs → backend? N/A (CLI application)
- Error context sufficient? YES (existing error handling with JSON output ✓)

**Versioning**:

- Version number assigned? Will increment BUILD version (existing pattern ✓)
- BUILD increments on every change? YES (following existing practice ✓)
- Breaking changes handled? NO breaking changes - backward compatible enhancement ✓

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

Based on the research and design artifacts, the /tasks command will generate implementation tasks in strict TDD order:

1. **Configuration Extension Tasks**:
   - Add maxConcurrency property to IAppConfigService interface [P]
   - Create configuration validation tests (must fail first)
   - Implement maxConcurrency configuration loading
   - Add hot-reload support for maxConcurrency changes

2. **Model Enhancement Tasks**:
   - Update BulkProcessingOptions to use config-based concurrency [P]
   - Create concurrency validation tests
   - Implement configuration integration in BulkProcessingOptions

3. **Service Integration Tasks**:
   - Create contract tests for concurrent processing behavior
   - Implement configuration service integration
   - Add progress reporting enhancements for concurrent operations

4. **Integration Testing Tasks**:
   - Create hot-reload integration tests
   - Add concurrent processing integration tests
   - Implement error handling tests for concurrent operations

**Ordering Strategy**:

- **Red-Green-Refactor**: All tests written first and must fail before implementation
- **Contract First**: API contracts and interfaces before implementations
- **Dependencies First**: Configuration loading before processing logic
- **Parallel Tasks [P]**: Independent model changes can be implemented simultaneously
- **Integration Last**: Full workflow tests after component implementation

**Key Implementation Considerations**:

- Leverage existing BulkProcessingOptions infrastructure (minimal changes)
- Extend existing IAppConfigService without breaking changes
- Maintain backward compatibility with default maxConcurrency = 1
- Use existing async processing patterns and error handling
- Preserve existing JSON output format and progress reporting

**Estimated Output**: 12-15 numbered, ordered tasks focusing on:

- Configuration extension (3-4 tasks)
- Service integration (4-5 tasks)
- Testing implementation (4-5 tasks)
- Documentation updates (1-2 tasks)

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
- [x] All NEEDS CLARIFICATION resolved (spec is complete)
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
