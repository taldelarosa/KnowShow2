# Feature Specification: DVD Subtitle (VobSub) OCR Processing# Feature Specification: DVD Subtitle (VobSub) OCR Processing# Feature Specification: [FEATURE NAME]


**Feature Branch**: `012-process-dvd-subtitle`

**Created**: 2025-10-17

**Status**: Draft - Ready for Planning**Feature Branch**: `012-process-dvd-subtitle`**Feature Branch**: `[###-feature-name]`

**Input**: User description: "Process dvd_subtitle (vobsub) format out of videos if they dont have text subtitles or PGS subtitles"

**Created**: 2025-10-17**Created**: [DATE]

## Execution Flow (main)

**Status**: Draft**Status**: Draft

```

1. Parse user description from Input**Input**: User description: "Process dvd_subtitle (vobsub) format out of videos if they dont have text subtitles or PGS subtitles"**Input**: User description: "$ARGUMENTS"

   ✓ Feature: Add support for extracting and OCR'ing DVD subtitle format (VobSub)



2. Extract key concepts from description

   → Actors: Episode identification system, video files with DVD subtitles## Execution Flow (main)## Execution Flow (main)

   → Actions: Extract VobSub subtitles, perform OCR, convert to text

   → Data: DVD subtitle tracks (bitmap format), OCR'ed text output

   → Constraints: Only process when text or PGS subtitles unavailable

``````

3. For each unclear aspect:

   ✓ CLARIFIED: DVD subtitles are lowest priority (text > PGS > DVD)1. Parse user description from Input

   ✓ CLARIFIED: Use existing matchConfidenceThreshold (50%) for OCR text quality

   ✓ CLARIFIED: No caching of extracted files (process on-demand)   ✓ Feature: Add support for extracting and OCR'ing DVD subtitle format (VobSub)1. Parse user description from Input

   ✓ CLARIFIED: Use existing 5-minute timeout from configuration

   ✓ CLARIFIED: Support up to 50MB subtitle tracks   → If empty: ERROR "No feature description provided"

   ✓ CLARIFIED: Use mkvextract + Tesseract OCR

   ✓ CLARIFIED: Report standard success/fail/ambiguous states2. Extract key concepts from description


4. Fill User Scenarios & Testing section   → Actors: Episode identification system, video files with DVD subtitles

   ✓ User flow: User attempts to identify episode with DVD subtitle format

   → Actions: Extract VobSub subtitles, perform OCR, convert to text

5. Generate Functional Requirements

   ✓ Each requirement is testable   → Data: DVD subtitle tracks (bitmap format), OCR'ed text output


6. Identify Key Entities   → Constraints: Only process when text or PGS subtitles unavailable

   ✓ VobSub subtitle track, extracted images, OCR text output


7. Run Review Checklist

   ✓ All clarifications resolved with reasonable defaults3. For each unclear aspect:


8. Return: SUCCESS (spec ready for planning)   [NEEDS CLARIFICATION: Should DVD subtitles be prioritized over text/PGS if all are present?]

```

   [NEEDS CLARIFICATION: What OCR quality threshold is acceptable for episode identification?]2. Extract key concepts from description

---

   [NEEDS CLARIFICATION: Should extracted text be cached to avoid re-processing?]   → Identify: actors, actions, data, constraints

## ⚡ Quick Guidelines

   [NEEDS CLARIFICATION: Maximum file size or processing time limits for VobSub extraction?]

- ✅ Focus on WHAT users need and WHY

- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)

- 👥 Written for business stakeholders, not developers

4. Fill User Scenarios & Testing section

---

   ✓ User flow: User attempts to identify episode with DVD subtitle format

## User Scenarios & Testing



### Primary User Story

5. Generate Functional Requirements

A user has video files containing DVD subtitles (VobSub format) ripped from physical DVDs or Blu-rays. These files do not have text-based subtitles (SRT, ASS) or PGS subtitles. The user wants the episode identification system to extract the subtitle content from the DVD subtitle track, convert it to text through OCR, and use that text to identify which episode the video file represents.

   ✓ Each requirement is testable

Currently, these files are rejected with an error message stating that DVD subtitle format is unsupported.



### Acceptance Scenarios

6. Identify Key Entities3. For each unclear aspect:

1. **Given** a video file with only DVD subtitle tracks and no text/PGS subtitles, **When** user runs episode identification, **Then** system extracts DVD subtitle content, performs OCR, and identifies the episode successfully

   ✓ VobSub subtitle track, extracted images, OCR text output   → Mark with [NEEDS CLARIFICATION: specific question]

2. **Given** a video file with both DVD subtitles and text subtitles, **When** user runs episode identification, **Then** system uses text subtitles (preferred format) and does not process DVD subtitles



3. **Given** a video file with DVD subtitles in multiple languages, **When** user specifies target language, **Then** system selects the correct DVD subtitle track matching the language

7. Run Review Checklist

4. **Given** a video file with DVD subtitles that OCR produces low-quality text, **When** identification confidence is below threshold, **Then** system reports ambiguous result with appropriate error message

   ⚠ WARN "Spec has uncertainties - clarifications needed"

5. **Given** a video file with DVD subtitles containing special characters or formatting, **When** OCR processes the images, **Then** system normalizes the text output for episode matching



### Edge Cases

8. Return: SUCCESS (spec ready for planning after clarifications)

- What happens when DVD subtitle extraction fails due to corrupted subtitle data?

  - System logs error and returns appropriate error code without crashing```

- How does system handle DVD subtitles with unusual resolutions or color palettes?

  - Tesseract processes images regardless of format; poor quality results in lower confidence

- What happens when OCR produces no readable text from subtitle images?

  - System reports "OCR_FAILED" error with clear message---

- How does system handle very large subtitle tracks that could cause memory issues?

  - Tracks over 50MB are rejected with "SUBTITLE_TOO_LARGE" error before processing4. Fill User Scenarios & Testing section

- What happens when DVD subtitle track is detected but extraction tools are missing?

  - System checks for mkvextract/Tesseract availability and reports "MISSING_DEPENDENCY" error## ⚡ Quick Guidelines   → If no clear user flow: ERROR "Cannot determine user scenarios"



## Requirements



### Functional Requirements- ✅ Focus on WHAT users need and WHY



- **FR-001**: System MUST detect when a video file contains DVD subtitle (VobSub/dvd_subtitle codec) tracks- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)

- **FR-002**: System MUST extract DVD subtitle content from video files using mkvextract tool

- **FR-003**: System MUST perform OCR on extracted DVD subtitle images using Tesseract to convert them to text- 👥 Written for business stakeholders, not developers

- **FR-004**: System MUST prioritize text-based subtitles (SRT, ASS, WebVTT) and PGS subtitles over DVD subtitles when multiple subtitle formats are present

- **FR-005**: System MUST select DVD subtitle track matching user-specified language when multiple language tracks exist

- **FR-006**: System MUST normalize OCR output text (remove formatting, timecodes, HTML tags) before episode identification

- **FR-007**: System MUST handle OCR failures gracefully with clear error messages---

- **FR-008**: System MUST validate mkvextract and Tesseract OCR tools are available before attempting processing

- **FR-009**: System MUST report when DVD subtitle OCR quality is insufficient for confident episode identification

- **FR-010**: System MUST process DVD subtitles on-demand without permanent caching (temporary extraction files deleted after processing)

- **FR-011**: System MUST respect the configured file processing timeout (default 5 minutes) when extracting DVD subtitles## User Scenarios & Testing

- **FR-012**: System MUST use the configured matchConfidenceThreshold (default 50%) for OCR text quality assessment

- **FR-013**: System MUST reject DVD subtitle tracks larger than 50MB with appropriate error message before processing5. Generate Functional Requirements



### Non-Functional Requirements### Primary User Story   → Each requirement must be testable



- **NFR-001**: DVD subtitle processing MUST complete within the configured timeout (default 5 minutes per file)

- **NFR-002**: System MUST handle DVD subtitle tracks up to 50MB in size without memory issues (process images incrementally)

- **NFR-003**: OCR accuracy MUST achieve at least 70% character recognition rate for reliable episode matching (measured against ground truth)A user has video files containing DVD subtitles (VobSub format) ripped from physical DVDs or Blu-rays. These files do not have text-based subtitles (SRT, ASS) or PGS subtitles. The user wants the episode identification system to extract the subtitle content from the DVD subtitle track, convert it to text through OCR, and use that text to identify which episode the video file represents.

- **NFR-004**: Temporary extraction files MUST be cleaned up after processing completion or failure



### Key Entities

Currently, these files are rejected with an error message stating that DVD subtitle format is unsupported.

- **DVD Subtitle Track**: A bitmap-based subtitle format embedded in video files, typically from DVD/Blu-ray sources. Contains subtitle images with timing information. Identified by codec name "dvd_subtitle" or "S_VOBSUB".

- **VobSub Files**: Intermediate extracted format consisting of .idx (index) and .sub (subtitle data) files extracted from the video container

- **Subtitle Images**: Individual PNG/bitmap images extracted from VobSub data, each representing one subtitle frame

- **OCR Text Output**: Text content extracted from subtitle images through optical character recognition, normalized and prepared for episode identification### Acceptance Scenarios



---



## Dependencies & Assumptions1. **Given** a video file with only DVD subtitle tracks and no text/PGS subtitles, **When** user runs episode identification, **Then** system extracts DVD subtitle content, performs OCR, and identifies the episode successfully   → Mark ambiguous requirements



### Dependencies



- Video files MUST be in MKV container format2. **Given** a video file with both DVD subtitles and text subtitles, **When** user runs episode identification, **Then** system uses text subtitles (preferred format) and does not process DVD subtitles6. Identify Key Entities (if data involved)

- DVD subtitle tracks MUST be embedded in the video file

- **mkvextract** tool (from mkvtoolnix package) MUST be available on system PATH7. Run Review Checklist

- **Tesseract OCR** engine MUST be available on system PATH with appropriate language data files

3. **Given** a video file with DVD subtitles in multiple languages, **When** user specifies target language, **Then** system selects the correct DVD subtitle track matching the language   → If any [NEEDS CLARIFICATION]: WARN "Spec has uncertainties"

### Assumptions



- DVD subtitles contain readable English text (or specified language)

- DVD subtitle image quality is sufficient for OCR to extract meaningful text4. **Given** a video file with DVD subtitles that OCR produces low-quality text, **When** identification confidence is below threshold, **Then** system reports ambiguous result with appropriate error message

- Users have necessary system permissions to create temporary files for extraction

- Video files are not DRM-protected or encrypted

- Standard DVD subtitle resolution (720x480 or similar) is used

5. **Given** a video file with DVD subtitles containing special characters or formatting, **When** OCR processes the images, **Then** system normalizes the text output for episode matching

---



## Out of Scope

### Edge Cases

- Conversion of DVD subtitles to SRT format for permanent storage

- Support for DVD subtitle formats not embedded in MKV containers

- Real-time subtitle extraction during video playback

- Manual correction or editing of OCR output- What happens when DVD subtitle extraction fails due to corrupted subtitle data?   → If implementation details found: ERROR "Remove tech details"

- Training or improving OCR models for better accuracy

- Support for DVD subtitles with unusual codecs beyond standard VobSub- How does system handle DVD subtitles with unusual resolutions or color palettes?

- Batch conversion of DVD subtitles to text format

- GUI for previewing subtitle extraction results- What happens when OCR produces no readable text from subtitle images?8. Return: SUCCESS (spec ready for planning)



---- How does system handle very large subtitle tracks that could cause memory issues?```



## Review & Acceptance Checklist- What happens when DVD subtitle track is detected but file extraction tools are missing?



### Content Quality---



- [x] No implementation details (languages, frameworks, APIs)## Requirements

- [x] Focused on user value and business needs

- [x] Written for non-technical stakeholders## ⚡ Quick Guidelines

- [x] All mandatory sections completed

### Functional Requirements

### Requirement Completeness

- ✅ Focus on WHAT users need and WHY

- [x] No [NEEDS CLARIFICATION] markers remain (all clarified)

- [x] Requirements are testable and unambiguous- **FR-001**: System MUST detect when a video file contains DVD subtitle (VobSub/dvd_subtitle codec) tracks- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)

- [x] Success criteria are measurable (thresholds defined)

- [x] Scope is clearly bounded- **FR-002**: System MUST extract DVD subtitle content from video files into processable format (images)- 👥 Written for business stakeholders, not developers

- [x] Dependencies and assumptions identified

- **FR-003**: System MUST perform OCR on extracted DVD subtitle images to convert them to text

---

- **FR-004**: System MUST prioritize text-based subtitles (SRT, ASS, WebVTT) and PGS subtitles over DVD subtitles when multiple subtitle formats are present### Section Requirements

## Execution Status

- **FR-005**: System MUST select DVD subtitle track matching user-specified language when multiple language tracks exist

- [x] User description parsed

- [x] Key concepts extracted- **FR-006**: System MUST normalize OCR output text (remove formatting, timecodes, HTML tags) before episode identification- **Mandatory sections**: Must be completed for every feature

- [x] Ambiguities clarified (all 7 items resolved)

- [x] User scenarios defined- **FR-007**: System MUST handle OCR failures gracefully with clear error messages- **Optional sections**: Include only when relevant to the feature

- [x] Requirements generated

- [x] Entities identified- **FR-008**: System MUST validate DVD subtitle extraction tools are available before attempting processing- When a section doesn't apply, remove it entirely (don't leave as "N/A")

- [x] Review checklist passed

- **FR-009**: System MUST report when DVD subtitle OCR quality is insufficient for confident episode identification

---

- **FR-010**: System MUST [NEEDS CLARIFICATION: Should extracted VobSub files be cached? Retention policy?]### For AI Generation

## Clarification Decisions

- **FR-011**: System MUST [NEEDS CLARIFICATION: Maximum processing time limit for VobSub extraction?]

All ambiguities have been resolved with the following decisions based on existing codebase patterns:

- **FR-012**: System MUST [NEEDS CLARIFICATION: Minimum OCR confidence threshold for episode identification?]When creating this spec from a user prompt:

1. **Priority**: DVD subtitles are **lowest priority**. Priority order: Text subtitles (SRT/ASS/WebVTT) > PGS subtitles > DVD subtitles. Only process DVD when no other formats available.



2. **Performance**: Use existing configuration `DefaultFileProcessingTimeout` (default **5 minutes**). DVD subtitle extraction must complete within this timeframe or fail with timeout error.

### Non-Functional Requirements1. **Mark all ambiguities**: Use [NEEDS CLARIFICATION: specific question] for any assumption you'd need to make

3. **Caching**: **No permanent caching**. Temporary files created during extraction are deleted after processing. This keeps storage requirements minimal and avoids cache management complexity.

2. **Don't guess**: If the prompt doesn't specify something (e.g., "login system" without auth method), mark it

4. **Quality**: Use existing `matchConfidenceThreshold` (default **50%**) for episode identification confidence. OCR must achieve **70% character recognition** rate for the text to be usable.

- **NFR-001**: DVD subtitle processing MUST not significantly increase overall episode identification time [NEEDS CLARIFICATION: Define "significant" - 2x? 5x?]3. **Think like a tester**: Every vague requirement should fail the "testable and unambiguous" checklist item

5. **Scale**: Support DVD subtitle tracks up to **50MB** in size. Process images incrementally to avoid memory issues. Larger tracks should fail with appropriate error message.

- **NFR-002**: System MUST handle DVD subtitle files of varying sizes without running out of memory [NEEDS CLARIFICATION: Maximum file size to support?]4. **Common underspecified areas**:

6. **Tooling**: Require **mkvextract** (from mkvtoolnix package) for extraction and **Tesseract OCR** for text recognition. Both must be available on system PATH.

- **NFR-003**: OCR accuracy MUST be sufficient for episode matching [NEEDS CLARIFICATION: Define minimum accuracy percentage]   - User types and permissions

7. **Confidence**: Report standard success/fail/ambiguous states consistent with existing subtitle processing. Include OCR-specific error codes when recognition fails.

   - Data retention/deletion policies

### Rationale

### Key Entities   - Performance targets and scale

These decisions align with existing system patterns:

- Timeout matches existing `DefaultFileProcessingTimeout` configuration   - Error handling behaviors

- Confidence threshold uses existing `matchConfidenceThreshold` setting

- Error handling follows existing error code patterns (MISSING_DEPENDENCY, OCR_FAILED, etc.)- **DVD Subtitle Track**: A bitmap-based subtitle format embedded in video files, typically from DVD/Blu-ray sources. Contains subtitle images with timing information. Identified by codec name "dvd_subtitle" or "S_VOBSUB".   - Integration requirements

- No caching keeps system simple and stateless like current subtitle processing

- 50MB limit provides reasonable boundary for typical DVD subtitle tracks- **VobSub Files**: Intermediate extracted format consisting of .idx (index) and .sub (subtitle data) files extracted from the video container   - Security/compliance needs

- Tool requirements (mkvextract, Tesseract) are industry-standard open-source tools

- **Subtitle Images**: Individual PNG/bitmap images extracted from VobSub data, each representing one subtitle frame

- **OCR Text Output**: Text content extracted from subtitle images through optical character recognition, normalized and prepared for episode identification---



---## User Scenarios & Testing *(mandatory)*



## Dependencies & Assumptions### Primary User Story



### Dependencies[Describe the main user journey in plain language]



- Video files MUST be in MKV container format### Acceptance Scenarios

- DVD subtitle tracks MUST be embedded in the video file

- External extraction tools MUST be available on the system [NEEDS CLARIFICATION: Which specific tools are required? mkvextract? vobsub2srt?]1. **Given** [initial state], **When** [action], **Then** [expected outcome]

- OCR engine MUST be available and configured [NEEDS CLARIFICATION: Tesseract? Other OCR tools?]2. **Given** [initial state], **When** [action], **Then** [expected outcome]



### Assumptions### Edge Cases



- DVD subtitles contain readable English text (or specified language)- What happens when [boundary condition]?

- DVD subtitle image quality is sufficient for OCR to extract meaningful text- How does system handle [error scenario]?

- Users have necessary system permissions to create temporary files for extraction

- Video files are not DRM-protected or encrypted## Requirements *(mandatory)*



---### Functional Requirements



## Out of Scope- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]

- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]

- Conversion of DVD subtitles to SRT format for permanent storage- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]

- Support for DVD subtitle formats not embedded in MKV containers- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]

- Real-time subtitle extraction during video playback- **FR-005**: System MUST [behavior, e.g., "log all security events"]

- Manual correction or editing of OCR output

- Training or improving OCR models for better accuracy*Example of marking unclear requirements:*

- Support for DVD subtitles with unusual codecs beyond standard VobSub

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]

---- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]



## Review & Acceptance Checklist### Key Entities *(include if feature involves data)*



### Content Quality- **[Entity 1]**: [What it represents, key attributes without implementation]

- **[Entity 2]**: [What it represents, relationships to other entities]

- [x] No implementation details (languages, frameworks, APIs)

- [x] Focused on user value and business needs---

- [x] Written for non-technical stakeholders

- [x] All mandatory sections completed## Review & Acceptance Checklist



### Requirement Completeness*GATE: Automated checks run during main() execution*



- [ ] No [NEEDS CLARIFICATION] markers remain (7 clarifications needed)### Content Quality

- [x] Requirements are testable and unambiguous (where specified)

- [ ] Success criteria are measurable (needs thresholds defined)- [ ] No implementation details (languages, frameworks, APIs)

- [x] Scope is clearly bounded- [ ] Focused on user value and business needs

- [x] Dependencies and assumptions identified- [ ] Written for non-technical stakeholders

- [ ] All mandatory sections completed

---

### Requirement Completeness

## Execution Status

- [ ] No [NEEDS CLARIFICATION] markers remain

- [x] User description parsed- [ ] Requirements are testable and unambiguous

- [x] Key concepts extracted- [ ] Success criteria are measurable

- [x] Ambiguities marked (7 items need clarification)- [ ] Scope is clearly bounded

- [x] User scenarios defined- [ ] Dependencies and assumptions identified

- [x] Requirements generated

- [x] Entities identified---

- [ ] Review checklist passed (pending clarifications)

## Execution Status

---

*Updated by main() during processing*

## Questions for Product Owner / Stakeholder

- [ ] User description parsed

1. **Priority**: When a video has both DVD subtitles AND text/PGS subtitles, should DVD subtitles ever be used, or always skip them?- [ ] Key concepts extracted

2. **Performance**: What is acceptable processing time for DVD subtitle extraction? Should there be a timeout?- [ ] Ambiguities marked

3. **Caching**: Should extracted VobSub data be cached to speed up re-processing? How long should cache be retained?- [ ] User scenarios defined

4. **Quality**: What minimum OCR accuracy percentage is required for reliable episode identification?- [ ] Requirements generated

5. **Scale**: What is the maximum expected file size for DVD subtitle tracks? Should there be file size limits?- [ ] Entities identified

6. **Tooling**: Which external tools should be required dependencies (mkvextract, vobsub2srt, etc.)?- [ ] Review checklist passed

7. **Confidence**: Should the system report OCR confidence levels to users, or only succeed/fail states?

---
