# Implementation Plan: Docker Containerization for Unraid Deployment

**Branch**: `011-docker-unraid-setup` | **Date**: 2025-10-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/011-docker-unraid-setup/spec.md`

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

Package the Episode Identifier .NET application as a Docker container optimized for Unraid servers. The container will include all dependencies (FFmpeg, MKVToolNix, Tesseract OCR, pgsrip) and support volume mapping for videos, database, and configuration. Users will deploy via Unraid's Docker UI using an XML template, then execute CLI commands for episode identification. The implementation uses multi-stage Docker builds for size optimization and handles PUID/PGID for file permissions.

## Technical Context

**Language/Version**: C# .NET 8.0  
**Primary Dependencies**: Docker (multi-stage builds), Alpine Linux (base image), FFmpeg, MKVToolNix, Tesseract OCR 5.x, pgsrip (Python-based), uv (Python package manager)  
**Storage**: SQLite database (production_hashes.db) persisted via volume mount  
**Testing**: xUnit for integration tests, manual container testing on Unraid  
**Target Platform**: Linux containers (amd64), Unraid 6.9+ Docker engine  
**Project Type**: Single project - packaging/deployment infrastructure (no new application code)  
**Performance Goals**: Container start <10 seconds, image size <2GB, processing speed equivalent to native installation  
**Constraints**: Must support PUID/PGID environment variables, no privileged mode, read/write access to mapped volumes  
**Scale/Scope**: Single-user deployment, supports multiple concurrent container instances on same host

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:

- Projects: 1 (deployment/packaging - no new application code)
- Using framework directly? YES - Docker standard practices, no abstraction layers
- Single data model? N/A - no new data models, reusing existing SQLite schema
- Avoiding patterns? YES - direct Dockerfile, no complex orchestration

**Architecture**:

- EVERY feature as library? N/A - This is infrastructure/packaging, not a feature library
- Libraries listed: Reusing existing EpisodeIdentifier.Core library
- CLI per library: Existing CLI preserved (all commands work identically in container)
- Library docs: N/A - creating deployment documentation (unraid.md)

**Testing (NON-NEGOTIABLE)**:

- RED-GREEN-Refactor cycle enforced? MODIFIED for infrastructure:
    - Contract tests: Verify container exposes expected volumes, env vars
    - Integration tests: Verify app runs identically in container vs native
    - No unit tests needed for Dockerfile/template XML
- Git commits show tests before implementation? YES - write validation scripts first
- Order: Contract→Integration→E2E→Unit strictly followed? YES (limited to applicable test types)
- Real dependencies used? YES - testing on actual Unraid or Docker environment
- Integration tests for: Container startup, volume permissions, CLI execution, database persistence
- FORBIDDEN: Deploying container before validation scripts pass

**Observability**:

- Structured logging included? YES - existing application logging preserved
- Frontend logs → backend? N/A - CLI-only application
- Error context sufficient? YES - Docker logs capture stdout/stderr from application

**Versioning**:

- Version number assigned? 1.0.0-docker (MAJOR.MINOR.BUILD)
- BUILD increments on every change? YES - Docker image tags will increment
- Breaking changes handled? N/A - first container release, no existing containerized version

## Project Structure

### Documentation (this feature)

```
specs/011-docker-unraid-setup/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0: Docker best practices, Unraid template format
├── data-model.md        # Phase 1: Container configuration model
├── quickstart.md        # Phase 1: Step-by-step deployment guide
├── contracts/           # Phase 1: Container interface contracts
│   ├── dockerfile.contract.md      # Expected build stages, dependencies
│   ├── volumes.contract.md         # Required volume mounts
│   ├── environment.contract.md     # Environment variables (PUID/PGID)
│   └── unraid-template.contract.md # XML template schema
└── tasks.md             # Phase 2: (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
KnowShow_Specd/
├── Dockerfile                       # Multi-stage build definition
├── docker-compose.yml               # (Optional) for local testing
├── .dockerignore                    # Build context exclusions
├── docker/
│   ├── entrypoint.sh               # Container startup script (PUID/PGID handling)
│   ├── unraid-template.xml         # Unraid Docker UI template
│   └── healthcheck.sh              # Container health validation
├── docs/
│   └── unraid.md                   # User-facing setup guide
├── src/EpisodeIdentifier.Core/     # Existing application (unchanged)
└── tests/
    ├── contract/
    │   └── DockerContractTests.cs  # Verify container interface
    └── integration/
        └── DockerIntegrationTests.cs  # End-to-end container tests
```

**Structure Decision**: Option 1 (Single project) - Adding Docker packaging to existing project

## Phase 0: Outline & Research

1. **Extract unknowns from Technical Context** above:
   - Multi-stage Docker build strategy for .NET applications
   - Alpine Linux vs Debian base image trade-offs
   - pgsrip installation and Python dependencies in containers
   - Unraid XML template format and parameter types
   - PUID/PGID implementation patterns for file permissions
   - Docker health check strategies for CLI applications
   - Image size optimization techniques

2. **Research tasks dispatched**:
   - Research: "Multi-stage Docker builds for .NET 8.0 applications"
   - Research: "Best base images for .NET containers with FFmpeg/Tesseract"
   - Research: "Unraid Docker template XML schema and examples"
   - Research: "PUID/PGID implementation for Linux containers"
   - Research: "Installing pgsrip in Docker containers"
   - Research: "Docker health checks for batch-processing CLI tools"
   - Research: "Container image size optimization for .NET apps"

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all technical decisions documented

**Status**: Executing now →

## Phase 1: Design & Contracts

*Prerequisites: research.md complete* ✅

1. **Extract entities from feature spec** → `data-model.md`: ✅
   - Container Configuration entity (image, volumes, environment)
   - Volume Mount entity (host_path, container_path, mode, purpose)
   - Environment Variable entity (PUID, PGID, TZ, LOG_LEVEL)
   - Application Configuration entity (JSON file schema)
   - Health Check entity (test, interval, timeout, retries)

2. **Generate API contracts** from functional requirements: ✅
   - dockerfile.contract.md: Build stages, dependencies, file system layout
   - volumes.contract.md: Volume mount interface and behavior
   - environment.contract.md: Environment variable validation and defaults
   - unraid-template.contract.md: XML template schema

3. **Generate contract tests** from contracts: ✅
   - Tests described in each contract file
   - Validation tests for build, runtime, permissions
   - Integration tests for container lifecycle

4. **Extract test scenarios** from user stories: ✅
   - quickstart.md contains step-by-step validation
   - Covers: deployment, configuration, identification, bulk processing
   - Includes troubleshooting and success criteria

5. **Update agent file incrementally** (O(1) operation):
   - Run: `scripts/update-agent-context.sh copilot`
   - Add Docker/Unraid context
   - Update recent changes

**Output**: ✅ data-model.md, /contracts/* (4 files), quickstart.md

**Status**: Complete → Moving to Phase 2 planning

## Phase 2: Task Planning Approach

*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:

1. **Infrastructure Tasks** (Dockerfile, entrypoint, healthcheck):
   - Task: Write Dockerfile with multi-stage build [P]
   - Task: Write entrypoint.sh with PUID/PGID handling [P]
   - Task: Write healthcheck.sh script [P]
   - Task: Write .dockerignore file [P]

2. **Unraid Integration Tasks**:
   - Task: Create unraid-template.xml [P]
   - Task: Write docs/unraid.md user guide
   - Task: Create example docker-compose.yml [P]

3. **Contract Test Tasks** (TDD order - tests before implementation):
   - Task: Write DockerBuildContractTests.cs (validates build output)
   - Task: Write DockerVolumeContractTests.cs (validates volume behavior)
   - Task: Write DockerEnvironmentContractTests.cs (validates PUID/PGID)
   - Task: Write UnraidTemplateValidationTests.cs (validates XML)

4. **Integration Test Tasks**:
   - Task: Write DockerStartupTests.cs (container lifecycle)
   - Task: Write DockerPermissionTests.cs (file ownership)
   - Task: Write DockerIdentificationTests.cs (E2E episode identification)
   - Task: Write QuickstartValidationTests.cs (automate quickstart steps)

5. **Build & Documentation Tasks**:
   - Task: Build Docker image locally
   - Task: Test image on Docker (non-Unraid)
   - Task: Test image on Unraid server
   - Task: Publish image to Docker Hub
   - Task: Update README.md with Docker instructions

**Ordering Strategy**:

- **Phase A** (Parallel): Dockerfile, entrypoint, template, docs [P]
- **Phase B** (Sequential): Contract tests → Verify failures → Implement fixes
- **Phase C** (Sequential): Integration tests → Manual validation → Quickstart test
- **Phase D** (Sequential): Build → Test → Publish

**Dependencies**:

- Template depends on: Dockerfile (to know volume paths)
- Integration tests depend on: Built image
- Publish depends on: All tests passing

**Estimated Output**: 20-25 numbered, ordered tasks in tasks.md

**Parallelization**:

- Tasks marked [P] can execute in parallel (independent files)
- Test execution must be sequential (shares Docker environment)

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation

*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

**No violations** - This feature follows constitutional principles:

- Single project (packaging only)
- Uses Docker standards directly (no wrappers)
- Tests before implementation (contract → integration)
- Existing library reused (EpisodeIdentifier.Core)

No complexity deviations to document.

---

## Progress Tracking

*This checklist is updated during execution flow*

**Phase Status**:

- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - approach described)
- [ ] Phase 3: Tasks generated (/tasks command - next step)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (none in technical context)
- [x] Complexity deviations documented (none)

**Artifacts Generated**:

- [x] research.md (7 research areas, all decisions documented)
- [x] data-model.md (5 entities with validation rules)
- [x] contracts/dockerfile.contract.md (build stages, file system, health check)
- [x] contracts/volumes.contract.md (3 volume mounts with permissions)
- [x] contracts/environment.contract.md (4 environment variables)
- [x] contracts/unraid-template.contract.md (XML schema)
- [x] quickstart.md (4-phase deployment guide, 15 minutes)

**Next Command**: `/tasks` to generate tasks.md from this plan

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
