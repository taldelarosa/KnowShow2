# Implementation Plan: NonPGS Subtitle Workflow

**Branch**: `006-adding-nonpgs-workflow` | **Date**: September 8, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/006-adding-nonpgs-workflow/spec.md`

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

Extend the episode identification system to support text-based subtitle formats (.srt, .ass, .vtt) when PGS subtitles are not available. The system will maintain PGS subtitle priority while providing fallback processing through sequential text subtitle track extraction and existing fuzzy hash comparison workflow.

## Technical Context

**Language/Version**: C# .NET 8.0  
**Primary Dependencies**: FFmpeg, MKVToolNix (mkvextract), Tesseract OCR, pgsrip, System.Text.Json  
**Storage**: SQLite database (existing fuzzy hash entries)  
**Testing**: xUnit, FluentAssertions  
**Target Platform**: Linux (Ubuntu/Debian primary), cross-platform .NET  
**Project Type**: single - CLI application with library structure  
**Performance Goals**: Process subtitle tracks within 5-10 seconds per track  
**Constraints**: Memory efficient for large subtitle files, handle encoding variations  
**Scale/Scope**: Support 3-5 common text subtitle formats, process multiple tracks per video

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (CLI application with libraries) ✅
- Using framework directly? Yes (direct FFmpeg/MKVToolNix calls) ✅
- Single data model? Yes (extend existing models) ✅
- Avoiding patterns? Yes (direct service calls, no unnecessary abstractions) ✅

**Architecture**:

- EVERY feature as library? Yes (TextSubtitleExtractor library planned) ✅
- Libraries listed: TextSubtitleExtractor (subtitle format parsing), SubtitleFormatHandler (format-specific logic)
- CLI per library: Extend existing CLI with --text-subtitles flag ✅
- Library docs: Yes, llms.txt format for AI context ✅

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? Yes, contract tests first ✅
- Git commits show tests before implementation? Will ensure this ✅
- Order: Contract→Integration→E2E→Unit strictly followed? Yes ✅
- Real dependencies used? Yes (actual video files, SQLite) ✅
- Integration tests for: new TextSubtitleExtractor library, contract changes ✅
- FORBIDDEN: Implementation before test, skipping RED phase ✅

**Observability**:

- Structured logging included? Yes, extend existing logging ✅
- Frontend logs → backend? N/A (CLI application) ✅
- Error context sufficient? Yes, subtitle format errors tracked ✅

**Versioning**:

- Version number assigned? 1.1.0 (minor feature addition) ✅
- BUILD increments on every change? Yes ✅
- Breaking changes handled? None expected, pure addition ✅

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
- Each contract → contract test task [P]: TextSubtitleExtractor, SubtitleFormatHandler contracts
- Each entity → model creation task [P]: TextSubtitleTrack, SubtitleFormat enum, TextSubtitleContent
- Each format handler → implementation task [P]: SrtFormatHandler, AssFormatHandler, VttFormatHandler
- Integration tasks: SubtitleExtractor service extension, CLI flag addition
- Quickstart validation tasks from test scenarios

**Ordering Strategy**:

- TDD order: Contract tests → Models → Format handlers → Service integration → CLI integration
- Dependency order: Core models → Format handlers → Extractor service → CLI interface
- Mark [P] for parallel execution: All format handlers, all contract tests, individual models
- Sequential dependencies: Models before services, services before CLI

**Specific Task Categories**:

1. **Contract Tests** (5 tasks, all [P]): Test interfaces before implementation
2. **Model Implementation** (4 tasks, all [P]): Create data structures
3. **Format Handlers** (3 tasks, all [P]): SRT, ASS, VTT parsing logic
4. **Service Integration** (3 tasks, sequential): Extend SubtitleExtractor service
5. **CLI Enhancement** (2 tasks, sequential): Add text subtitle flags and help
6. **Integration Tests** (4 tasks, sequential): Full workflow validation
7. **Documentation** (2 tasks, [P]): Update README and API docs

**Estimated Output**: 23 numbered, ordered tasks in tasks.md

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
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
