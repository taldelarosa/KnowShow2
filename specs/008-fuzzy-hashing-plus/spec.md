# Feature Specification: Fuzzy Hashing Plus Configuration System

**Feature Branch**: `008-fuzzy-hashing-plus`  
**Created**: September 12, 2025  
**Status**: Draft  
**Input**: User description: "fuzzy hashing plus configs. The app will now read a JSON config file for match threshold, name confidence threshold, and filename templates. It will also use Context-triggered piecewise hashing (CTPH) instead of SHA1/MD5 hashing."

## Execution Flow (main)

```
1. Parse user description from Input
   → Key concepts identified: configuration system, fuzzy hashing, CTPH algorithm
2. Extract key concepts from description
   → Actors: users/administrators configuring thresholds
   → Actions: reading config files, applying thresholds, using CTPH hashing
   → Data: match thresholds, confidence thresholds, filename templates, hash values
   → Constraints: JSON format, CTPH algorithm requirement
3. For each unclear aspect:
   → Marked configuration storage location and validation requirements
4. Fill User Scenarios & Testing section
   → Clear user flow: configure thresholds → apply to episode identification
5. Generate Functional Requirements
   → Each requirement testable and specific to configuration and hashing
6. Identify Key Entities
   → Configuration, HashingAlgorithm, MatchingThreshold entities
7. Run Review Checklist
   → Some configuration details need clarification
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines

- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a system administrator or power user, I need to configure the episode identification system with custom matching thresholds and filename templates so that the system can be tuned for different content libraries and identification accuracy requirements. The system should use more sophisticated fuzzy hashing to improve matching accuracy while allowing fine-tuned control over matching behavior.

### Acceptance Scenarios

1. **Given** a JSON configuration file with match threshold settings, **When** the system starts, **Then** it loads and applies these thresholds for episode identification
2. **Given** configured name confidence thresholds, **When** identifying episodes, **Then** the system uses these thresholds to determine match quality
3. **Given** filename templates in the configuration, **When** renaming files, **Then** the system applies the configured naming patterns
4. **Given** the CTPH hashing algorithm is enabled, **When** comparing files, **Then** the system uses fuzzy hashing instead of exact hash matching
5. **Given** invalid configuration values, **When** loading the config file, **Then** the system reports clear validation errors and uses safe defaults

### Quality Gates & Build Requirements

1. **Given** the project build process, **When** executing build commands, **Then** all tests must pass with zero failures
2. **Given** markdown documentation in the project, **When** running linting checks, **Then** all markdown files must pass markdownlint-cli validation with zero issues
3. **Given** code quality standards, **When** building the project, **Then** the build must complete successfully with no compilation errors
4. **Given** linting issues are detected, **When** running markdownlint-cli with --fix flag, **Then** all auto-fixable issues must be resolved automatically
5. **Given** the feature is ready for delivery, **When** evaluating completion criteria, **Then** the system must have clean builds, passing tests, and zero linting issues

### Edge Cases

- What happens when the configuration file is missing or corrupted?
- How does the system handle invalid threshold values (negative, > 100%, non-numeric)?
- What occurs when filename templates contain invalid characters or patterns?
- How does fuzzy hashing perform with very small or very large files?

### Build Process & Quality Assurance

The feature implementation must adhere to strict quality standards:

- **Linting Prerequisites**: All markdown documentation must pass markdownlint-cli validation before build completion
- **Auto-fix Capability**: Use `markdownlint --fix` to automatically resolve formatting issues
- **Zero-tolerance Policy**: Build pipeline fails if any linting issues remain unresolved
- **Continuous Validation**: Linting checks run as part of every build cycle
- **Documentation Standards**: All markdown files must follow consistent formatting enforced by automated tools

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST read configuration from a JSON file containing match thresholds, name confidence thresholds, and filename templates
- **FR-002**: System MUST validate all configuration values and reject invalid settings with clear error messages
- **FR-003**: System MUST use Context-triggered piecewise hashing (CTPH) algorithm for file comparison instead of SHA1/MD5
- **FR-004**: System MUST apply configurable match threshold values when determining episode identification accuracy
- **FR-005**: System MUST apply configurable name confidence threshold values when evaluating filename matches
- **FR-006**: System MUST use configurable filename templates when renaming or organizing identified episodes
- **FR-007**: System MUST provide default configuration values when config file is missing or invalid, using the values from the current config as defaults
- **FR-008**: System MUST reload configuration once per file processing to adapt to config changes as quickly as possible without requiring restart
- **FR-009**: System MUST log configuration loading success and any validation errors
- **FR-010**: System MUST maintain backward compatibility with existing file identification workflows

### Quality & Build Requirements

- **QR-001**: Build process MUST include markdown linting as a prerequisite using markdownlint-cli
- **QR-002**: All markdown documentation MUST pass linting validation with zero issues before build completion
- **QR-003**: Linting issues MUST be resolved using markdownlint-cli with --fix flag for auto-fixable problems
- **QR-004**: Feature acceptance MUST require clean builds with all tests passing and zero linting issues
- **QR-005**: Build pipeline MUST fail if markdown linting issues are detected and not resolved
- **QR-006**: Documentation MUST maintain consistent formatting standards enforced by automated linting

### Key Entities *(include if feature involves data)*

- **Configuration**: JSON structure containing match thresholds (numeric), name confidence thresholds (numeric), and filename templates (string patterns)
- **HashingAlgorithm**: CTPH-based fuzzy hashing implementation that produces similarity scores rather than exact hash matches
- **MatchingThreshold**: Configurable numeric values (0-100%) that determine when file similarities qualify as matches
- **FilenameTemplate**: Configurable string patterns that define how identified episodes should be named/organized

### Configuration Structure Details

The system uses a JSON configuration file (`episodeidentifier.config.json`) with the following structure:

**Match Confidence Thresholds**:

- `matchConfidenceThreshold` (0.0-1.0): Minimum confidence required for episode identification (default: 0.8)
- `renameConfidenceThreshold` (0.0-1.0): Minimum confidence required for automatic file renaming (default: 0.85)

**Filename Parsing Patterns**:

- `primaryPattern`: Default format "Series S##E## Episode Name"
- `secondaryPattern`: Alternative format "Series ##x## Episode Name"
- `tertiaryPattern`: Dot-separated format "Series.S##.E##.Episode Name"

**Filename Templates**:

- Configurable format string with placeholders: `{SeriesName}`, `{Season}`, `{Episode}`, `{EpisodeName}`, `{FileExtension}`
- Support for format specifiers (e.g., `:D2` for zero-padding)

**Fuzzy Hashing Settings**:

- `fuzzyHashThreshold` (0-100): Minimum similarity percentage for fuzzy hash matches
- `hashingAlgorithm`: Algorithm selection (CTPH for Context-triggered piecewise hashing)

---

## Review & Acceptance Checklist

*GATE: Automated checks run during main() execution*

### Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified
- [x] Quality gates and build requirements defined
- [x] Linting requirements specified with tooling details
- [x] Configuration structure and validation requirements detailed---

## Execution Status

*Updated by main() during processing*

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked (now resolved)
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] Review checklist passed

---
