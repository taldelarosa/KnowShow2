# Feature Specification: Docker Containerization for Unraid Deployment

**Feature Branch**: `011-docker-unraid-setup`
**Created**: 2025-10-11
**Status**: Draft
**Input**: User description: "docker unraid setup. Now that the app has the features that we want lets containerize it with the intent of having it run on an Unraid server"

## Execution Flow (main)

```
1. Parse user description from Input
   → Feature clear: Containerize Episode Identifier for Unraid deployment

2. Extract key concepts from description
   → Actors: Unraid server administrators, automated media processing workflows
   → Actions: Deploy container, process video files, manage configuration
   → Data: Video files, subtitle hashes database, configuration files
   → Constraints: Unraid compatibility, volume mapping, persistent storage

3. For each unclear aspect:
   → [NEEDS CLARIFICATION: Watch folder mode vs on-demand processing?]
   → [NEEDS CLARIFICATION: Web UI or CLI-only operation?]
   → [NEEDS CLARIFICATION: Integration with existing Unraid apps (Radarr/Sonarr)?]

4. Fill User Scenarios & Testing section
   → Primary: User maps video folders and runs identification
   → Edge cases: Configuration updates, database persistence

5. Generate Functional Requirements
   → Container must run on Unraid
   → Must support volume mapping for videos and database
   → Must preserve all current CLI functionality

6. Identify Key Entities (if data involved)
   → Container configuration, volume mounts, persistent database

7. Run Review Checklist
   → WARN "Spec has uncertainties regarding automation mode and UI"
   → No implementation details (Docker specifics belong in planning)

8. Return: SUCCESS (spec ready for planning with clarifications needed)
```

---

## ⚡ Quick Guidelines

- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As an Unraid server administrator, I want to deploy the Episode Identifier as a container so that I can automatically or manually identify and organize my video files without installing dependencies directly on the host system.

### Acceptance Scenarios

1. **Given** a fresh Unraid server, **When** I install the Episode Identifier container, **Then** the container starts successfully without requiring manual dependency installation
2. **Given** a running container with mapped video directories, **When** I execute the identification command on a video file, **Then** the file is correctly identified using PGS subtitle extraction
3. **Given** an existing subtitle hash database, **When** I restart or recreate the container, **Then** the database persists and previously learned subtitles remain available
4. **Given** a configuration file with custom settings, **When** I update the configuration, **Then** the container reflects the new settings without rebuilding
5. **Given** a directory of video files, **When** I run bulk processing, **Then** all files are processed with the configured concurrency level
6. **Given** processed files with identified episodes, **When** I enable auto-rename, **Then** files are renamed according to the configured template

### Edge Cases

- What happens when the hash database file is corrupted or missing?
- How does the system handle video files that are actively being written (partial downloads)?
- What happens if OCR language packs are missing from the container?
- How does configuration hot-reload work within the container environment?
- What happens when mapped volumes are unavailable or permissions are incorrect?
- How are container logs accessed for troubleshooting?

### ✅ Operational Mode - RESOLVED

**Decision**: Manual execution via CLI commands

**Rationale**:
- Users typically process files in batches after downloads complete
- Watch mode adds complexity and potential resource overhead
- Can be added in future phases if demand exists
- Fits existing CLI-based workflow
- Users can schedule execution via Unraid's User Scripts plugin or cron

### ✅ User Interface - RESOLVED

**Decision**: CLI-only operation with Unraid Docker UI template

**Rationale**:
- Unraid users are familiar with docker console/exec commands
- Unraid Docker UI template makes installation and configuration user-friendly
- Web UI adds significant complexity and maintenance burden
- Template can expose key config options via UI (paths, thresholds, concurrency)
- Keeps implementation focused on core functionality
- Documentation will guide setup via Unraid's Docker container UI

### ✅ Integration Requirements - RESOLVED

**Decision**: Standalone operation (Phase 1)

**Rationale**:
- External integrations (Radarr/Sonarr/etc.) can be achieved via post-processing scripts
- Users can integrate via Unraid's User Scripts plugin
- Keeps container focused and maintainable
- Documentation will provide integration patterns and examples
- Future phases can add native API integrations if needed

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST package the Episode Identifier application as a container image that runs on Unraid
- **FR-002**: System MUST support volume mapping for video file directories (read/write access)
- **FR-003**: System MUST support volume mapping for persistent hash database storage
- **FR-004**: System MUST support volume mapping for configuration files
- **FR-005**: System MUST include all required dependencies (FFmpeg, MKVToolNix, Tesseract OCR, pgsrip)
- **FR-006**: System MUST preserve all current CLI functionality within the container
- **FR-007**: System MUST support bulk processing with configurable concurrency
- **FR-008**: System MUST support auto-rename functionality for identified files
- **FR-009**: System MUST provide container logs accessible via Unraid interface
- **FR-010**: System MUST handle configuration file hot-reload within container environment
- **FR-011**: System MUST operate correctly with Unraid's user ID/group ID mapping for file permissions
- **FR-012**: System MUST validate video files before processing to prevent corruption
- **FR-013**: System MUST support all current hashing algorithms (CTPH/ssdeep)
- **FR-014**: System MUST support all current filename pattern matching capabilities
- **FR-015**: Container MUST start successfully on system reboot if configured for auto-start
- **FR-016**: System MUST support manual command execution via docker exec or Unraid console
- **FR-017**: System MUST provide CLI-only interface accessible through Unraid's Docker container console
- **FR-018**: System MUST operate as standalone container without requiring external service integrations
- **FR-019**: Container configuration MUST be manageable through Unraid's Docker container UI template
- **FR-020**: System MUST document integration patterns for User Scripts plugin and post-processing workflows

### Non-Functional Requirements

- **NFR-001**: Container image SHOULD be under 2GB for reasonable download time over typical home internet
- **NFR-002**: Container MUST start within 10 seconds after creation (excluding initial image download)
- **NFR-003**: System MUST perform video processing without significant overhead compared to native installation
- **NFR-004**: Container MUST support multiple concurrent instances on same Unraid server (different paths)
- **NFR-005**: System MUST provide clear error messages when dependencies fail within container
- **NFR-006**: Documentation MUST guide Unraid users through Docker UI setup and common operations
- **NFR-007**: Documentation MUST include unraid.md with step-by-step Docker UI template setup instructions

### Key Entities *(include if feature involves data)*

- **Container Image**: Packaged application with all dependencies, ready for deployment on Unraid
- **Volume Mounts**: Mapped directories for videos, database, and configuration
  - Video directory: Source files for identification (read/write)
  - Database directory: Persistent storage for subtitle hashes (read/write)
  - Config directory: Application configuration files (read)
- **Hash Database**: SQLite database persisted across container lifecycle containing learned subtitle hashes
- **Configuration File**: JSON settings for thresholds, concurrency, and filename patterns
- **Container Logs**: Output stream capturing application execution, errors, and processing results
- **Unraid Docker Template**: XML template defining container parameters, volume paths, and environment variables for Unraid's Docker UI
- **Setup Documentation**: unraid.md file with step-by-step instructions for adding container via Unraid's Docker UI

---

## Constraints & Assumptions

### Constraints

- Must work within Unraid's Docker implementation
- Must respect file permissions and ownership (Unraid uses PUID/PGID)
- Must not require privileged container mode unless absolutely necessary
- External dependencies (FFmpeg, Tesseract, etc.) must be included in container

### Assumptions

- Users have basic familiarity with Unraid container management
- Video files are accessible via network shares or local storage
- Unraid server has sufficient resources (CPU, RAM) for video processing
- Users understand the need for persistent volumes for database and config

### Dependencies

- Unraid 6.9+ (or compatible Docker environment)
- Sufficient storage for container image and video processing
- Network access for initial container image download
- Existing video files with PGS subtitles for identification

---

## Success Criteria

The feature is successful when:

1. ✅ A user can install the container on Unraid without manual dependency installation
2. ✅ The container successfully identifies episodes from video files using mapped directories
3. ✅ The subtitle hash database persists across container restarts
4. ✅ Configuration changes take effect without container rebuild
5. ✅ All current application features work identically within the container
6. ✅ Container logs provide sufficient information for troubleshooting
7. ✅ Documentation enables non-technical users to deploy and use the container
8. ✅ Container can process files with correct ownership/permissions on Unraid shares

---

## Out of Scope

The following are explicitly NOT included in this feature:

- Web-based user interface (CLI-only for Phase 1)
- Automatic watch folder monitoring (manual execution only)
- Native API integration with Radarr/Sonarr/Plex (documentation for script-based integration will be provided)
- GPU acceleration for video processing
- Container orchestration (Kubernetes, Docker Swarm)
- Multi-architecture support (ARM builds) in initial release
- Automatic updates mechanism (Unraid handles container updates)
- Built-in backup/restore for database (users manage via Unraid backup tools)
- Network-based processing (container accessing remote servers)
- Unraid Community Applications store submission (can be added in future phase)

---

## Review & Acceptance Checklist

*GATE: Automated checks run during main() execution*

### Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - **All clarifications resolved**
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

---

## Execution Status

*Updated by main() during processing*

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities resolved (3 areas clarified)
- [x] User scenarios defined
- [x] Requirements generated (20 functional + 7 non-functional)
- [x] Entities identified
- [x] Review checklist passed - **READY for planning phase**

---

## Next Steps

**✅ Specification Complete** - Ready to proceed to planning phase

### Implementation Priorities

1. **Create Dockerfile** with all dependencies (FFmpeg, MKVToolNix, Tesseract, pgsrip)
2. **Create Unraid Docker template** (XML) for easy installation via UI
3. **Write unraid.md documentation** with step-by-step setup instructions including:
   - How to add container via Unraid Docker UI
   - Volume mapping configuration (videos, database, config)
   - PUID/PGID setup for proper permissions
   - Common commands for execution via console
   - Integration examples with User Scripts plugin
4. **Test on Unraid server** to validate all functionality
5. **Document integration patterns** for post-processing workflows

### Future Enhancements (Post-MVP)

- Watch folder monitoring mode
- Web-based UI for configuration and monitoring
- Unraid Community Applications store submission
- Native API integration with media management tools
- Multi-architecture support (ARM for Raspberry Pi)
