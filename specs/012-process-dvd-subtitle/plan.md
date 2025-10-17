# Implementation Plan: DVD Subtitle (VobSub) OCR Processing

**Branch**: `012-process-dvd-subtitle` | **Date**: 2025-10-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/spec.md`

## Execution Flow (/plan command scope)

```
1. Load feature spec from Input path
   ✓ Spec loaded successfully with all clarifications resolved

2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   ✓ Project Type: Single C# .NET 8.0 console application
   ✓ Structure Decision: Option 1 (single project with src/ and tests/)

3. Evaluate Constitution Check section below
   ✓ No violations - extending existing library structure
   ✓ Using existing test infrastructure (xUnit)
   → Update Progress Tracking: Initial Constitution Check PASSED

4. Execute Phase 0 → research.md
   ✓ All technical decisions already made in clarifications
   → Generate research.md with DVD subtitle extraction approaches

5. Execute Phase 1 → contracts, data-model.md, quickstart.md
   → Design VobSubExtractor service contract
   → Update existing models for DVD subtitle support
   → Create quickstart test scenarios

6. Re-evaluate Constitution Check section
   → Verify TDD approach maintained
   → Update Progress Tracking: Post-Design Constitution Check

7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
   → TDD order: Contract tests → Integration tests → Implementation

8. STOP - Ready for /tasks command
```

## Summary

Add DVD subtitle (VobSub) format support to the episode identifier by extracting bitmap subtitles using mkvextract, performing OCR with Tesseract, and using the extracted text for episode identification. DVD subtitles will be the lowest priority format (text > PGS > DVD) and only processed when no other subtitle formats are available.

## Technical Context

**Language/Version**: C# .NET 8.0
**Primary Dependencies**: 
- Existing: System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
- New: mkvextract (mkvtoolnix), Tesseract OCR
**Storage**: SQLite database (existing hash storage), temporary files for extraction
**Testing**: xUnit, FluentAssertions (existing test framework)
**Target Platform**: Linux/Windows cross-platform
**Project Type**: Single console application with library structure
**Performance Goals**: Complete within 5-minute timeout, process up to 50MB subtitle tracks
**Constraints**: 
- 5-minute processing timeout (existing configuration)
- 50MB maximum subtitle track size
- 70% minimum OCR character recognition rate
**Scale/Scope**: Extends existing episode identifier, ~5 new classes, ~500 LOC

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:
- Projects: 1 (EpisodeIdentifier.Core) ✓
- Using framework directly? Yes - direct process execution for mkvextract/Tesseract ✓
- Single data model? Yes - extending existing subtitle track model ✓
- Avoiding patterns? Yes - no new patterns, extending existing services ✓

**Architecture**:
- EVERY feature as library? Yes - VobSubExtractor as service in Services/ ✓
- Libraries listed: 
  - VobSubExtractor: Extract VobSub (.idx/.sub) from MKV using mkvextract
  - VobSubOcrService: Perform OCR on VobSub images using Tesseract
- CLI per library: Existing CLI with --input flag handles all subtitle types ✓
- Library docs: Will update .github/copilot-instructions.md ✓

**Testing (NON-NEGOTIABLE)**:
- RED-GREEN-Refactor cycle enforced? YES - contract tests first ✓
- Git commits show tests before implementation? Will enforce ✓
- Order: Contract→Integration→E2E→Unit strictly followed? YES ✓
- Real dependencies used? YES - actual mkvextract and Tesseract installations ✓
- Integration tests for: new VobSubExtractor library, contract changes, OCR service ✓
- FORBIDDEN: Implementation before test, skipping RED phase ✓

**Observability**:
- Structured logging included? YES - using existing ILogger infrastructure ✓
- Frontend logs → backend? N/A - CLI application ✓
- Error context sufficient? YES - specific error codes for DVD subtitle failures ✓

**Versioning**:
- Version number assigned? Will increment BUILD number ✓
- BUILD increments on every change? YES ✓
- Breaking changes handled? NO breaking changes - pure addition ✓

**Constitution Check Result**: ✓ PASS

## Project Structure

### Documentation (this feature)

```
specs/012-process-dvd-subtitle/
├── spec.md              # Feature specification (complete)
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
│   ├── vobsub-extractor.json
│   └── vobsub-ocr.json
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
src/EpisodeIdentifier.Core/
├── Models/
│   ├── SubtitleTrackInfo.cs        # Existing - no changes needed
│   ├── VobSubExtractionResult.cs   # NEW
│   └── VobSubOcrResult.cs          # NEW
├── Services/
│   ├── VideoFormatValidator.cs     # Existing - already updated for dvd_subtitle
│   ├── PgsToTextConverter.cs       # Existing - will be refactored/extended
│   ├── VobSubExtractor.cs          # NEW
│   ├── VobSubOcrService.cs         # NEW
│   └── DependencyValidator.cs      # NEW - check for mkvextract/Tesseract
├── Interfaces/
│   ├── IVobSubExtractor.cs         # NEW
│   └── IVobSubOcrService.cs        # NEW
└── Program.cs                       # UPDATE - integrate DVD subtitle processing

tests/
├── contract/
│   ├── VobSubExtractorContractTests.cs     # NEW
│   └── VobSubOcrServiceContractTests.cs    # NEW
├── integration/
│   ├── VobSubExtractionIntegrationTests.cs # NEW
│   └── VobSubOcrIntegrationTests.cs        # NEW
└── unit/
    ├── VobSubExtractorTests.cs             # NEW
    └── VobSubOcrServiceTests.cs            # NEW
```

**Structure Decision**: Option 1 (single project) - Extends existing EpisodeIdentifier.Core

## Phase 0: Outline & Research

### Research Tasks

1. ✓ **DVD Subtitle Format**: Already researched - VobSub format uses .idx/.sub files
2. ✓ **Extraction Tool**: mkvextract from mkvtoolnix package - standard tool
3. ✓ **OCR Engine**: Tesseract OCR - open source, well-supported
4. ✓ **Priority Handling**: Clarified - text > PGS > DVD
5. ✓ **Size/Timeout Limits**: Clarified - 50MB max, 5-minute timeout
6. **Integration with Existing PgsToTextConverter**: Research refactoring options

### Research Questions to Address in research.md

1. **VobSub Extraction Process**:
   - Decision: Use mkvextract with command: `mkvextract tracks input.mkv {trackId}:output.sub`
   - Rationale: Standard tool, reliable, produces idx+sub files
   - Alternatives: ffmpeg (doesn't support VobSub extraction well), custom parser (complex)

2. **Image Extraction from VobSub**:
   - Decision: Use Tesseract directly on VobSub via SubtitleEdit or custom image extraction
   - Rationale: Tesseract can process VobSub images after extraction
   - Alternatives: VobSub2SRT (additional dependency), custom OCR (reinventing wheel)

3. **Code Organization**:
   - Decision: Create new VobSubExtractor and VobSubOcrService, extend PgsToTextConverter to be more generic or create parallel path
   - Rationale: Separation of concerns, testable components
   - Alternatives: Modify PgsToTextConverter directly (violates single responsibility)

4. **Error Handling**:
   - Decision: Use existing error code patterns (NO_SUBTITLES, UNSUPPORTED_FORMAT, OCR_FAILED, MISSING_DEPENDENCY)
   - Rationale: Consistent with existing error handling
   - Alternatives: New error system (unnecessary complexity)

5. **Temp File Management**:
   - Decision: Use System.IO.Path.GetTempPath() with unique directory per extraction, cleanup in finally block
   - Rationale: Cross-platform, automatic cleanup
   - Alternatives: Fixed temp directory (collision risk), manual cleanup (leak risk)

**Output**: research.md (generating next)

## Phase 1: Design & Contracts

### Data Model Updates (data-model.md)

**Existing Entities** (no changes):
- `SubtitleTrackInfo`: Already supports CodecName="dvd_subtitle"

**New Entities**:

1. **VobSubExtractionResult**:
   - Fields:
     - `bool Success`: Whether extraction succeeded
     - `string IdxFilePath`: Path to extracted .idx file
     - `string SubFilePath`: Path to extracted .sub file  
     - `string ErrorMessage`: Error details if failed
     - `TimeSpan ExtractionDuration`: Time taken
   - Validation: Both files must exist if Success=true

2. **VobSubOcrResult**:
   - Fields:
     - `bool Success`: Whether OCR succeeded
     - `string ExtractedText`: OCR output text
     - `double ConfidenceScore`: OCR confidence (0-1)
     - `int CharacterCount`: Total characters recognized
     - `string ErrorMessage`: Error details if failed
     - `TimeSpan OcrDuration`: Time taken
   - Validation: Text must be non-empty if Success=true

### Service Contracts

**IVobSubExtractor**:
```csharp
Task<VobSubExtractionResult> ExtractAsync(string videoPath, int trackIndex, string outputDirectory, CancellationToken cancellationToken);
Task<bool> IsMkvExtractAvailableAsync();
```

**IVobSubOcrService**:
```csharp
Task<VobSubOcrResult> PerformOcrAsync(string idxFilePath, string subFilePath, string language, CancellationToken cancellationToken);
Task<bool> IsTesseractAvailableAsync();
string GetOcrLanguageCode(string language);
```

### Contract Test Scenarios

1. **VobSubExtractor Contract Tests**:
   - ExtractAsync with valid MKV and DVD subtitle track returns Success=true
   - ExtractAsync with invalid path throws ArgumentException
   - ExtractAsync with non-existent track returns Success=false
   - IsMkvExtractAvailableAsync returns true when mkvextract on PATH
   - IsMkvExtractAvailableAsync returns false when mkvextract missing

2. **VobSubOcrService Contract Tests**:
   - PerformOcrAsync with valid idx/sub files returns Success=true with text
   - PerformOcrAsync with invalid files returns Success=false
   - IsTesseractAvailableAsync returns true when tesseract on PATH
   - GetOcrLanguageCode("eng") returns "eng"

### Integration Test Scenarios

1. **End-to-End DVD Subtitle Processing**:
   - Given MKV with DVD subtitles, extract and OCR successfully
   - Given MKV with corrupted DVD subtitles, fail gracefully
   - Given MKV with DVD subtitles >50MB, reject before processing
   - Given MKV with text and DVD subtitles, prefer text subtitles
   - Given processing exceeds timeout, cancel and report timeout error

### Quickstart Test Scenario

Test file: Use one of the Criminal Minds S5 files with DVD subtitles
```bash
# 1. Verify tools available
which mkvextract
which tesseract

# 2. Test DVD subtitle detection
dotnet run --project src/EpisodeIdentifier.Core -- \
  --input "/path/to/dvd_subtitle_video.mkv" \
  --hash-db production_hashes.db

# 3. Verify OCR extraction
# Expected: JSON output with series/season/episode or appropriate error

# 4. Verify priority (text > DVD)
# Test with file containing both formats
# Expected: Uses text subtitles, ignores DVD
```

**Output**: data-model.md, contracts/*.json, failing contract tests, quickstart.md

## Phase 2: Task Planning Approach

*This section describes what the /tasks command will do - DO NOT execute during /plan*

### Task Generation Strategy

**From Contracts** (Contract Tests - Parallel [P]):
1. [P] Create VobSubExtractorContractTests with failing tests
2. [P] Create VobSubOcrServiceContractTests with failing tests
3. [P] Create DependencyValidatorContractTests with failing tests

**From Data Model**:
4. [P] Create VobSubExtractionResult model class
5. [P] Create VobSubOcrResult model class

**From Integration Scenarios**:
6. Create VobSubExtractionIntegrationTests (end-to-end extraction)
7. Create VobSubOcrIntegrationTests (end-to-end OCR)
8. Create DvdSubtitlePriorityIntegrationTests (format preference)
9. Create DvdSubtitleTimeoutIntegrationTests (timeout handling)

**Implementation Tasks** (Make Tests Pass):
10. Implement IVobSubExtractor interface
11. Implement VobSubExtractor service (mkvextract wrapper)
12. Implement IVobSubOcrService interface
13. Implement VobSubOcrService (Tesseract wrapper)
14. Implement DependencyValidator (check tool availability)
15. Update Program.cs to integrate DVD subtitle path
16. Update VideoFormatValidator if needed (already done)
17. Add temp file cleanup logic
18. Add size validation (50MB check)

**Refinement Tasks**:
19. Run all contract tests - verify PASS
20. Run all integration tests - verify PASS
21. Execute quickstart.md - verify end-to-end
22. Add unit tests for edge cases
23. Update .github/copilot-instructions.md

**Ordering Strategy**:
- Contract tests first (3 parallel tasks)
- Models next (2 parallel tasks)
- Integration tests (sequential, depend on contracts)
- Implementation to pass tests (sequential)
- Validation and documentation (sequential)

**Estimated Output**: ~23 numbered, ordered tasks in tasks.md

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation

*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following TDD)
**Phase 5**: Validation (run all tests, execute quickstart.md)

## Complexity Tracking

*No violations - all checks passed*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Progress Tracking

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [ ] Phase 1: Design complete (/plan command) - IN PROGRESS
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [ ] Post-Design Constitution Check: PASS (pending Phase 1 completion)
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none)

---
*Based on Constitution principles - extending existing library with TDD approach*
