# Tasks: Docker Containerization for Unraid Deployment

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/011-docker-unraid-setup/`
**Prerequisites**: ✅ plan.md, ✅ research.md, ✅ data-model.md, ✅ contracts/ (4 files), ✅ quickstart.md

## Execution Flow (main)

```
1. Load plan.md from feature directory ✅
   → Tech stack: C# .NET 8.0, Docker, Alpine Linux, FFmpeg, Tesseract, pgsrip
   → Structure: Single project (packaging existing application)

2. Load optional design documents: ✅
   → data-model.md: 5 entities (Container Config, Volume Mounts, Environment Vars, Health Check)
   → contracts/: 4 files (Dockerfile, Volumes, Environment, Unraid Template)
   → research.md: 7 technical decisions documented

3. Generate tasks by category:
   → Setup: Dockerfile, entrypoint script, .dockerignore
   → Tests: 4 contract tests, 4 integration tests
   → Core: Unraid template, health check, documentation
   → Integration: Build pipeline, registry publication
   → Polish: Validation, manual testing

4. Apply task rules:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)

5. Number tasks sequentially (T001-T025) ✅
6. Generate dependency graph ✅
7. Create parallel execution examples ✅
8. Validate task completeness: ✅
   → All contracts have tests ✅
   → All artifacts specified in plan ✅
   → TDD order enforced ✅

9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- File paths are absolute from repository root

## Phase 3.1: Setup & Infrastructure (5 tasks)

- [ ] **T001** [P] Create `.dockerignore` file excluding unnecessary build context (bin/, obj/, tests/, specs/, *.md, .git/)
- [ ] **T002** [P] Create `docker/healthcheck.sh` script that validates `dotnet /app/EpisodeIdentifier.Core.dll --version` exits successfully
- [ ] **T003** [P] Create `docker/entrypoint.sh` script with PUID/PGID handling using gosu (validates env vars, updates appuser UID/GID, executes command as appuser)
- [ ] **T004** [P] Create example `docker-compose.yml` in repository root with volumes, environment variables, and health check configuration for local testing
- [ ] **T005** Ensure `docker/` directory structure exists and scripts are executable (chmod +x)

**Files Created**:

- `/mnt/c/Users/Ragma/KnowShow_Specd/.dockerignore`
- `/mnt/c/Users/Ragma/KnowShow_Specd/docker/healthcheck.sh`
- `/mnt/c/Users/Ragma/KnowShow_Specd/docker/entrypoint.sh`
- `/mnt/c/Users/Ragma/KnowShow_Specd/docker-compose.yml`

---

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests (4 tests - can run in parallel)

- [ ] **T006** [P] Contract test for Dockerfile build stages in `tests/contract/DockerBuildContractTests.cs`
    - Validates: Build stage uses .NET SDK 8.0, runtime stage uses .NET Runtime 8.0
    - Validates: All dependencies installed (ffmpeg, mkvtoolnix, tesseract, pgsrip, gosu)
    - Validates: Application files copied to `/app/`
    - Validates: Image size < 2GB
    - **Expected**: Test FAILS (Dockerfile doesn't exist yet)

- [ ] **T007** [P] Contract test for volume mounts in `tests/contract/DockerVolumeContractTests.cs`
    - Validates: `/videos` volume is read/write accessible
    - Validates: `/data` volume persists across container restarts
    - Validates: `/config` volume is read-only
    - Validates: Files created match PUID/PGID ownership
    - **Expected**: Test FAILS (container not built yet)

- [ ] **T008** [P] Contract test for environment variables in `tests/contract/DockerEnvironmentContractTests.cs`
    - Validates: PUID/PGID default to 99/100
    - Validates: Invalid PUID/PGID are rejected or defaulted
    - Validates: TZ environment variable sets timezone correctly
    - Validates: LOG_LEVEL environment variable is respected
    - **Expected**: Test FAILS (entrypoint.sh not implemented yet)

- [ ] **T009** [P] Contract test for Unraid template XML in `tests/contract/UnraidTemplateValidationTests.cs`
    - Validates: XML is well-formed
    - Validates: All required elements present (Name, Repository, Config entries)
    - Validates: Volume paths start with `/`
    - Validates: Default host paths use `/mnt/user/` convention
    - **Expected**: Test FAILS (template doesn't exist yet)

### Integration Tests (4 tests - can run in parallel after contracts pass)

- [ ] **T010** [P] Integration test for container startup in `tests/integration/DockerStartupTests.cs`
    - Test: Container builds successfully
    - Test: Container starts and reaches "healthy" status within 30 seconds
    - Test: Health check passes consistently
    - Test: Application responds to `--version` command
    - **Expected**: Test FAILS (Dockerfile not implemented yet)

- [ ] **T011** [P] Integration test for file permissions in `tests/integration/DockerPermissionsTests.cs`
    - Test: Files created with PUID=1000 PGID=1000 have correct ownership
    - Test: Files created with default PUID/PGID (99/100) have correct ownership
    - Test: Container can read/write to mounted volumes
    - Test: Config volume is read-only (write attempts fail)
    - **Expected**: Test FAILS (entrypoint not implemented yet)

- [ ] **T012** [P] Integration test for episode identification in `tests/integration/DockerIdentificationTests.cs`
    - Test: Store episode subtitle hash via container
    - Test: Identify episode from video file via container
    - Test: Database persists after container restart
    - Test: All CLI commands work identically inside container
    - **Expected**: Test FAILS (Dockerfile not complete yet)

- [ ] **T013** [P] Integration test for quickstart validation in `tests/integration/QuickstartValidationTests.cs`
    - Automates all steps from quickstart.md
    - Test: Container deployment (Phase 1)
    - Test: Configuration setup (Phase 2)
    - Test: First episode identification (Phase 3)
    - Test: Bulk processing (Phase 4)
    - **Expected**: Test FAILS (full pipeline not ready)

**Test Verification**: Run all tests and verify they FAIL before proceeding to Phase 3.3

---

## Phase 3.3: Core Implementation (ONLY after tests are failing)

### Docker Configuration (2 tasks)

- [ ] **T014** Create multi-stage `Dockerfile` in repository root
    - **Stage 1 (build)**: Base `mcr.microsoft.com/dotnet/sdk:8.0`, copy .csproj, restore packages, copy source, publish to `/app/publish`
    - **Stage 2 (runtime)**: Base `mcr.microsoft.com/dotnet/runtime:8.0`, install system packages (ffmpeg, mkvtoolnix, tesseract-ocr, tesseract-ocr-eng, python3, ca-certificates, gosu), install pgsrip via uv, copy app from build stage, create appuser, set working directory `/app`, copy entrypoint and healthcheck scripts, expose no ports, set HEALTHCHECK, set ENTRYPOINT to entrypoint.sh
    - **Optimize**: Combine RUN commands, clean apt cache, use --no-cache for uv
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/Dockerfile`

- [ ] **T015** Create Unraid Docker template XML at `docker/unraid-template.xml`
    - Include: Container metadata (Name, Repository, Icon, Overview, Category)
    - Include: Volume configs for /videos, /data, /config with descriptions
    - Include: Environment configs for PUID, PGID, TZ, LOG_LEVEL
    - Include: Support URL (GitHub issues), Project URL, Description with usage examples
    - Validate: XML is well-formed and follows Unraid schema v2
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/docker/unraid-template.xml`

### Documentation (2 tasks - can run in parallel)

- [ ] **T016** [P] Create comprehensive Unraid setup guide at `docs/unraid.md`
    - Section 1: Prerequisites and requirements
    - Section 2: Installation via Unraid Docker UI (step-by-step with screenshots placeholders)
    - Section 3: Configuration (PUID/PGID, volume paths, environment variables)
    - Section 4: Usage examples (docker exec commands for common operations)
    - Section 5: Integration with User Scripts plugin (post-processing examples)
    - Section 6: Troubleshooting common issues
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/docs/unraid.md`

- [ ] **T017** [P] Update main `README.md` with Docker deployment section
    - Add "Docker Deployment" section after "Quick Setup"
    - Include: Quick start with docker run command
    - Include: Link to docs/unraid.md for detailed Unraid instructions
    - Include: Volume mapping explanations
    - Include: Common docker exec commands
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/README.md`

---

## Phase 3.4: Integration & Build Pipeline (5 tasks)

- [ ] **T018** Build Docker image locally and verify contract tests pass
    - Command: `docker build -t episode-identifier:test .`
    - Verify: Image builds without errors
    - Verify: Image size is under 2GB
    - Run: All contract tests (T006-T009) should now PASS
    - Fix: Any test failures before proceeding

- [ ] **T019** Run integration tests against built image
    - Start container: `docker run -d --name test-container -e PUID=99 -e PGID=100 -v /tmp/test-videos:/videos -v /tmp/test-data:/data episode-identifier:test`
    - Run: All integration tests (T010-T013) should now PASS
    - Verify: Quickstart guide can be executed successfully
    - Fix: Any test failures before proceeding
    - Cleanup: Stop and remove test container

- [ ] **T020** Test image on actual Unraid server (manual validation)
    - Install container via Unraid Docker UI using template
    - Verify: Container starts and shows "Healthy" status
    - Verify: Execute quickstart.md steps on real server
    - Verify: File permissions work correctly with Unraid shares
    - Document: Any environment-specific issues discovered

- [ ] **T021** Create GitHub Actions workflow for automated builds at `.github/workflows/docker-build.yml`
    - Trigger: On push to main branch and tags matching v*
    - Jobs: Build multi-platform image (linux/amd64)
    - Jobs: Run contract tests
    - Jobs: Run integration tests
    - Jobs: Publish to Docker Hub (on tags only)
    - Include: Build cache optimization
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/.github/workflows/docker-build.yml`

- [ ] **T022** Publish Docker image to Docker Hub
    - Tag: `taldelarosa/episode-identifier:latest`
    - Tag: `taldelarosa/episode-identifier:1.0.0-docker`
    - Push: Both tags to Docker Hub
    - Verify: Image is publicly accessible
    - Update: Template XML with correct repository URL

---

## Phase 3.5: Polish & Finalization (3 tasks)

- [ ] **T023** [P] Create container icon and assets
    - Design: 256x256 PNG icon for Unraid template
    - Upload: Icon to repository (`docs/icon.png`)
    - Update: Template XML with icon URL
    - Create: Any additional visual assets needed
    - File: `/mnt/c/Users/Ragma/KnowShow_Specd/docs/icon.png`

- [ ] **T024** [P] Performance and size optimization review
    - Analyze: Docker image layers for optimization opportunities
    - Test: Container startup time (target: <10 seconds)
    - Test: Processing speed matches native installation (within 5%)
    - Optimize: Dockerfile if any bottlenecks found
    - Document: Performance benchmarks in docs/

- [ ] **T025** Final validation and documentation review
    - Execute: Complete quickstart.md end-to-end on fresh system
    - Review: All documentation for accuracy and completeness
    - Verify: All tests passing (contract + integration)
    - Verify: Template XML loads correctly in Unraid UI
    - Update: DEPLOYMENT_GUIDE.md with Docker option
    - Tag: Repository with v1.0.0-docker for release

---

## Dependencies Graph

```
Phase 3.1 (Setup): T001-T005 [ALL PARALLEL]
  ↓
Phase 3.2 (Tests): 
  Contract Tests: T006-T009 [ALL PARALLEL]
  Integration Tests: T010-T013 [ALL PARALLEL, after contracts written]
  ↓
Phase 3.3 (Implementation):
  T014 (Dockerfile) → Blocks T018
  T015 (Template) [PARALLEL with T014]
  T016-T017 (Docs) [BOTH PARALLEL]
  ↓
Phase 3.4 (Build & Test):
  T018 (Build) → T019 (Integration Tests) → T020 (Manual Validation) → T021 (CI/CD) → T022 (Publish)
  ↓
Phase 3.5 (Polish):
  T023 (Icon) [PARALLEL]
  T024 (Performance) [PARALLEL]
  T025 (Final Validation) [Depends on all above]
```

---

## Parallel Execution Examples

### Execute Setup Tasks in Parallel (T001-T004)

```bash
# All create different files, can run simultaneously
Task 1: "Create .dockerignore file excluding unnecessary build context"
Task 2: "Create docker/healthcheck.sh script"
Task 3: "Create docker/entrypoint.sh with PUID/PGID handling"
Task 4: "Create docker-compose.yml example"
# Run T005 after to set permissions
```

### Execute Contract Tests in Parallel (T006-T009)

```bash
# All test different contracts, independent files
Task 6: "Contract test for Dockerfile build in tests/contract/DockerBuildContractTests.cs"
Task 7: "Contract test for volumes in tests/contract/DockerVolumeContractTests.cs"
Task 8: "Contract test for environment in tests/contract/DockerEnvironmentContractTests.cs"
Task 9: "Contract test for Unraid template in tests/contract/UnraidTemplateValidationTests.cs"
```

### Execute Documentation Tasks in Parallel (T016-T017)

```bash
# Different files, no dependencies
Task 16: "Create comprehensive Unraid setup guide at docs/unraid.md"
Task 17: "Update main README.md with Docker deployment section"
```

### Execute Polish Tasks in Parallel (T023-T024)

```bash
# Independent optimization work
Task 23: "Create container icon and upload assets"
Task 24: "Performance and size optimization review"
# Run T025 final validation after both complete
```

---

## Notes

- **[P] tasks**: Different files, no dependencies, safe for parallel execution
- **TDD Enforcement**: Tests (T006-T013) MUST fail before implementation (T014+)
- **Sequential Pipeline**: Build (T018) → Test (T019) → Validate (T020) → CI/CD (T021) → Publish (T022)
- **Commit Strategy**: Commit after each task with descriptive message
- **Test Coverage**: 8 test files (4 contract, 4 integration) validate all contracts

---

## Task Generation Rules Applied

*Applied during main() execution*

1. **From Contracts** ✅:
   - dockerfile.contract.md → T006 (test), T014 (implementation)
   - volumes.contract.md → T007 (test)
   - environment.contract.md → T008 (test), T003 (entrypoint implementation)
   - unraid-template.contract.md → T009 (test), T015 (implementation)

2. **From Data Model** ✅:
   - Container Configuration → T014 (Dockerfile), T015 (template)
   - Volume Mounts → T007 (test), handled in Dockerfile
   - Environment Variables → T008 (test), T003 (entrypoint)
   - Health Check → T002 (healthcheck script), handled in Dockerfile

3. **From Quickstart** ✅:
   - Phase 1 (Deploy) → T010 (integration test), T020 (manual validation)
   - Phase 2 (Configure) → T011 (permissions test)
   - Phase 3 (Identify) → T012 (identification test)
   - Phase 4 (Bulk Process) → T012 (bulk processing test)
   - Complete Guide → T013 (automated quickstart test)

4. **From Research** ✅:
   - Multi-stage builds → T014 (Dockerfile implementation)
   - PUID/PGID pattern → T003 (entrypoint), T008 (test), T011 (test)
   - Health checks → T002 (script), validated in T010
   - Size optimization → T014 (implementation), T024 (validation)

5. **Ordering** ✅:
   - Setup (T001-T005) → Tests (T006-T013) → Implementation (T014-T017) → Integration (T018-T022) → Polish (T023-T025)
   - TDD: All tests before corresponding implementation
   - Dependencies respected: Build before test, test before publish

---

## Validation Checklist

*GATE: Checked by main() before returning*

- [x] All contracts have corresponding tests (4 contracts → 4 contract tests T006-T009)
- [x] All entities have implementation tasks (Container Config → Dockerfile + Template)
- [x] All tests come before implementation (T006-T013 before T014+)
- [x] Parallel tasks truly independent (different files verified)
- [x] Each task specifies exact file path (absolute paths provided)
- [x] No task modifies same file as another [P] task (validated)
- [x] TDD order enforced (tests MUST fail before implementation)
- [x] Quickstart scenarios covered (automated in T013)
- [x] Research decisions implemented (7 decisions → corresponding tasks)

---

## Execution Status

**Total Tasks**: 25  
**Parallel Tasks**: 11 marked [P]  
**Sequential Tasks**: 14  
**Estimated Time**: 2-3 days (with testing and validation)

**Ready for Execution**: ✅ YES

**Next Step**: Begin with Phase 3.1 (T001-T005), verify tests fail in Phase 3.2, then proceed to implementation.
