# Tasks: DVD Subtitle (VobSub) OCR Processing

**Input**: Design documents from `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Execution Flow

```
1. Load plan.md from feature directory
   ✓ Tech stack: C# .NET 8.0, xUnit, FluentAssertions
   ✓ Libraries: VobSubExtractor, VobSubOcrService
   ✓ Structure: Single project (src/EpisodeIdentifier.Core)

2. Load design documents:
   ✓ data-model.md: VobSubExtractionResult, VobSubOcrResult
   ✓ contracts/: IVobSubExtractor, IVobSubOcrService
   ✓ research.md: mkvextract + Tesseract approach
   ✓ quickstart.md: DVD subtitle test scenarios

3. Generate tasks by category:
   ✓ Setup: Interfaces, models
   ✓ Tests: Contract tests (2), Integration tests (4)
   ✓ Core: VobSubExtractor, VobSubOcrService implementations
   ✓ Integration: Program.cs, dependency validation
   ✓ Polish: Edge cases, documentation

4. Task ordering: TDD approach
   ✓ Interfaces → Contract tests → Models → Integration tests → Implementation
   ✓ Tests MUST FAIL before implementation begins
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions
- **Path convention**: Single project structure (src/EpisodeIdentifier.Core/, tests/)

---

## Phase 3.1: Setup & Interfaces

### T001 Create IVobSubExtractor interface

**File**: `src/EpisodeIdentifier.Core/Interfaces/IVobSubExtractor.cs`
**Description**: Define interface contract for VobSub extraction service
**Contract**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/contracts/vobsub-extractor.json`

**Requirements**:

- Method: `Task<VobSubExtractionResult> ExtractAsync(string videoPath, int trackIndex, string outputDirectory, CancellationToken cancellationToken)`
- Method: `Task<bool> IsMkvExtractAvailableAsync()`
- Add XML documentation comments from contract

**Acceptance**:

- Interface compiles successfully
- Method signatures match contract exactly
- No implementation code (interface only)

---

### T002 Create IVobSubOcrService interface

**File**: `src/EpisodeIdentifier.Core/Interfaces/IVobSubOcrService.cs`
**Description**: Define interface contract for VobSub OCR service
**Contract**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/contracts/vobsub-ocr.json`

**Requirements**:

- Method: `Task<VobSubOcrResult> PerformOcrAsync(string idxFilePath, string subFilePath, string language, CancellationToken cancellationToken)`
- Method: `Task<bool> IsTesseractAvailableAsync()`
- Method: `string GetOcrLanguageCode(string language)`
- Add XML documentation comments from contract

**Acceptance**:

- Interface compiles successfully
- Method signatures match contract exactly
- No implementation code (interface only)

---

## Phase 3.2: Data Models

### T003 [P] Create VobSubExtractionResult model

**File**: `src/EpisodeIdentifier.Core/Models/VobSubExtractionResult.cs`
**Description**: Create data model for VobSub extraction results
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/data-model.md` (lines 26-97)

**Requirements**:

- Properties: `Success` (bool), `IdxFilePath` (string?), `SubFilePath` (string?), `ErrorMessage` (string?), `ExtractionDuration` (TimeSpan), `TrackIndex` (int), `SourceVideoPath` (string)
- Validation logic: If Success=true, IdxFilePath and SubFilePath must be non-null
- Add XML documentation comments for each property

**Acceptance**:

- Model compiles successfully
- All properties defined with correct types
- No validation logic yet (just data holder)

---

### T004 [P] Create VobSubOcrResult model

**File**: `src/EpisodeIdentifier.Core/Models/VobSubOcrResult.cs`
**Description**: Create data model for VobSub OCR results
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/data-model.md` (lines 99-178)

**Requirements**:

- Properties: `Success` (bool), `ExtractedText` (string?), `ErrorMessage` (string?), `ConfidenceScore` (double), `ImageCount` (int), `ProcessedImageCount` (int), `OcrDuration` (TimeSpan), `SourceIdxPath` (string), `SourceSubPath` (string)
- Add XML documentation comments for each property
- ConfidenceScore range: 0.0 to 100.0

**Acceptance**:

- Model compiles successfully
- All properties defined with correct types
- No validation logic yet (just data holder)

---

## Phase 3.3: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.4

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### T005 [P] Contract test for IVobSubExtractor

**File**: `tests/contract/VobSubExtractorContractTests.cs`
**Description**: Write failing contract tests for VobSub extraction service
**Contract**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/contracts/vobsub-extractor.json`

**Test Scenarios** (from contract):

1. `ExtractAsync_WithValidMkvAndDvdSubtitle_ReturnsSuccessWithPaths`
   - Given: MKV file with DVD subtitle track at index 3
   - When: ExtractAsync called with valid parameters
   - Then: Returns Success=true with valid .idx and .sub file paths

2. `ExtractAsync_WithNonExistentFile_ThrowsArgumentException`
   - Given: Non-existent video file path
   - When: ExtractAsync called
   - Then: Throws ArgumentException

3. `ExtractAsync_WithInvalidTrackIndex_ReturnsFailureResult`
   - Given: Track index that doesn't exist in video
   - When: ExtractAsync called
   - Then: Returns Success=false with error message

4. `IsMkvExtractAvailableAsync_WhenToolInstalled_ReturnsTrue`
   - Given: System with mkvextract installed
   - When: IsMkvExtractAvailableAsync called
   - Then: Returns true

5. `ExtractAsync_WhenCancelled_ThrowsOperationCanceledException`
   - Given: Valid extraction started
   - When: CancellationToken triggered during extraction
   - Then: Throws OperationCanceledException

**Requirements**:

- Use xUnit framework
- Use FluentAssertions for assertions
- Create mock/stub implementation that throws NotImplementedException
- All tests must FAIL initially

**Acceptance**:

- All 5 tests compile
- All 5 tests FAIL (NotImplementedException)
- Tests follow naming convention: MethodName_Scenario_ExpectedResult

---

### T006 [P] Contract test for IVobSubOcrService

**File**: `tests/contract/VobSubOcrServiceContractTests.cs`
**Description**: Write failing contract tests for VobSub OCR service
**Contract**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/contracts/vobsub-ocr.json`

**Test Scenarios** (from contract):

1. `PerformOcrAsync_WithValidVobSubFiles_ReturnsSuccessWithText`
   - Given: Valid .idx and .sub files with readable text
   - When: PerformOcrAsync called with language='eng'
   - Then: Returns Success=true with ExtractedText containing subtitle content

2. `PerformOcrAsync_WithMissingFiles_ThrowsArgumentException`
   - Given: Non-existent .idx or .sub file path
   - When: PerformOcrAsync called
   - Then: Throws ArgumentException

3. `PerformOcrAsync_WithNoTextInImages_ReturnsSuccessWithEmptyText`
   - Given: VobSub files with no recognizable text
   - When: PerformOcrAsync called
   - Then: Returns Success=true with empty ExtractedText and ConfidenceScore=0

4. `IsTesseractAvailableAsync_WhenToolInstalled_ReturnsTrue`
   - Given: System with Tesseract installed
   - When: IsTesseractAvailableAsync called
   - Then: Returns true

5. `GetOcrLanguageCode_WithValidLanguage_ReturnsCode`
   - Given: User language 'eng'
   - When: GetOcrLanguageCode('eng') called
   - Then: Returns 'eng'

6. `PerformOcrAsync_WhenCancelled_ThrowsOperationCanceledException`
   - Given: Valid OCR started
   - When: CancellationToken triggered during processing
   - Then: Throws OperationCanceledException

**Requirements**:

- Use xUnit framework
- Use FluentAssertions for assertions
- Create mock/stub implementation that throws NotImplementedException
- All tests must FAIL initially

**Acceptance**:

- All 6 tests compile
- All 6 tests FAIL (NotImplementedException)
- Tests follow naming convention: MethodName_Scenario_ExpectedResult

---

### T007 [P] Integration test for VobSub extraction end-to-end

**File**: `tests/integration/VobSubExtractionIntegrationTests.cs`
**Description**: Write failing integration test for complete VobSub extraction workflow
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/quickstart.md` (Test scenario 1)

**Test Scenarios**:

1. `ExtractDvdSubtitleFromCriminalMindsFile_CompletesSuccessfully`
   - Given: Criminal Minds S5 DVD subtitle MKV file
   - When: Full extraction pipeline runs
   - Then: Creates valid .idx and .sub files within 5 seconds

2. `ExtractFromFileWithoutDvdSubtitles_ReturnsFailure`
   - Given: MKV with only text subtitles
   - When: Extraction attempted on non-existent DVD subtitle track
   - Then: Returns failure with clear error message

**Requirements**:

- Use actual Criminal Minds test file: `/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv`
- Use real mkvextract tool (not mocked)
- Create temporary directory for output
- Clean up temp files after test
- Skip test if mkvextract not available (use `[SkippableFact]`)

**Acceptance**:

- Tests compile successfully
- Tests FAIL with NotImplementedException
- Tests have proper setup/teardown for temp directories

---

### T008 [P] Integration test for VobSub OCR end-to-end

**File**: `tests/integration/VobSubOcrIntegrationTests.cs`
**Description**: Write failing integration test for complete VobSub OCR workflow
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/quickstart.md` (Test scenario 2)

**Test Scenarios**:

1. `OcrExtractedVobSubFiles_ReturnsReadableText`
   - Given: Valid .idx and .sub files extracted from Criminal Minds
   - When: OCR processing runs with language='eng'
   - Then: Returns text containing expected subtitle content (e.g., character names, dialogue)

2. `OcrWithMissingTesseract_ReturnsFailure`
   - Given: Tesseract not available on PATH
   - When: OCR attempted
   - Then: Returns failure indicating missing dependency

**Requirements**:

- Use real Tesseract OCR (not mocked)
- Pre-extract VobSub files in test setup
- Validate ConfidenceScore > 0 for valid subtitles
- Skip test if Tesseract not available (use `[SkippableFact]`)
- Clean up temp files after test

**Acceptance**:

- Tests compile successfully
- Tests FAIL with NotImplementedException
- Tests have proper setup/teardown

---

### T009 Integration test for subtitle priority logic

**File**: `tests/integration/SubtitlePriorityIntegrationTests.cs`
**Description**: Write failing integration test verifying text > PGS > DVD priority
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/quickstart.md` (Test scenario 3)

**Test Scenarios**:

1. `IdentifyFileWithTextAndDvdSubtitles_PrefersTextSubtitles`
   - Given: MKV with both text and DVD subtitles
   - When: Episode identification runs
   - Then: Uses text subtitles, ignores DVD subtitles

2. `IdentifyFileWithPgsAndDvdSubtitles_PrefersPgsSubtitles`
   - Given: MKV with both PGS and DVD subtitles
   - When: Episode identification runs
   - Then: Uses PGS subtitles, ignores DVD subtitles

3. `IdentifyFileWithOnlyDvdSubtitles_UsesDvdSubtitles`
   - Given: MKV with only DVD subtitles
   - When: Episode identification runs
   - Then: Extracts and OCRs DVD subtitles successfully

**Requirements**:

- Test against actual Criminal Minds files
- Verify JSON output includes `subtitleType` field
- This test depends on Program.cs integration (will fail until T015)

**Acceptance**:

- Tests compile successfully
- Tests FAIL (integration not yet complete)
- Test cases cover all priority scenarios

---

### T010 Integration test for timeout handling

**File**: `tests/integration/DvdSubtitleTimeoutIntegrationTests.cs`
**Description**: Write failing integration test for 5-minute timeout enforcement
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/spec.md` FR-011

**Test Scenarios**:

1. `ExtractLargeVobSubFile_RespectsTimeout`
   - Given: Very large DVD subtitle track (approaching 50MB)
   - When: Extraction runs with 5-second test timeout
   - Then: Operation cancels and throws OperationCanceledException

2. `OcrManyImages_RespectsTimeout`
   - Given: VobSub with many subtitle images
   - When: OCR runs with 5-second test timeout
   - Then: Operation cancels and throws OperationCanceledException

**Requirements**:

- Use short timeouts for testing (5 seconds, not 5 minutes)
- Use CancellationTokenSource with timeout
- Verify proper cleanup after cancellation
- May need to create synthetic large test files

**Acceptance**:

- Tests compile successfully
- Tests FAIL (timeout logic not yet implemented)
- Tests properly handle cancellation

---

## Phase 3.4: Core Implementation (ONLY after tests T005-T010 are failing)

### T011 Implement VobSubExtractor service

**File**: `src/EpisodeIdentifier.Core/Services/VobSubExtractor.cs`
**Description**: Implement IVobSubExtractor to extract VobSub files using mkvextract
**Dependencies**: T001 (interface), T003 (model), T005 (contract tests)

**Requirements**:

- Implement `ExtractAsync()` using `System.Diagnostics.Process` to call mkvextract
- Command: `mkvextract tracks "{videoPath}" {trackIndex}:{outputPath}`
- Parse stderr for errors (mkvextract outputs progress to stderr)
- Validate both .idx and .sub files created after extraction
- Implement `IsMkvExtractAvailableAsync()` by checking PATH
- Add structured logging for extraction start/completion/errors
- Handle process exit codes (0 = success, non-zero = failure)
- Implement timeout via CancellationToken

**Acceptance**:

- Contract tests T005 now PASS (all 5 tests green)
- Integration test T007 now PASS
- Code follows existing service patterns (e.g., PgsRipService.cs)
- Proper error handling and logging

---

### T012 Implement VobSubOcrService - Image extraction

**File**: `src/EpisodeIdentifier.Core/Services/VobSubOcrService.cs`
**Description**: Implement image extraction from VobSub files (Phase 1 of OCR)
**Dependencies**: T002 (interface), T004 (model), T006 (contract tests)

**Requirements**:

- Implement method to extract PNG images from VobSub using ffmpeg
- Command: `ffmpeg -i "{idxFilePath}" -f image2 "{outputDir}/frame_%04d.png"`
- Create temporary directory for images
- Track image count and timestamps
- Handle ffmpeg errors
- Add structured logging

**Acceptance**:

- Can extract images from .idx file
- Images saved to temp directory with sequential naming
- Proper error handling if extraction fails
- Logs image extraction progress

---

### T013 Implement VobSubOcrService - Tesseract OCR

**File**: `src/EpisodeIdentifier.Core/Services/VobSubOcrService.cs` (continued)
**Description**: Implement Tesseract OCR processing for extracted images
**Dependencies**: T012 (image extraction)

**Requirements**:

- Implement `PerformOcrAsync()` using `System.Diagnostics.Process` to call Tesseract
- Command: `tesseract "{imagePath}" stdout -l {language}`
- Process each extracted image sequentially or in batches
- Concatenate text from all images
- Calculate confidence score (average across images)
- Implement `IsTesseractAvailableAsync()` by checking PATH
- Implement `GetOcrLanguageCode()` to normalize language codes
- Handle timeout via CancellationToken
- Clean up temporary image files after processing

**Acceptance**:

- Contract tests T006 now PASS (all 6 tests green)
- Integration test T008 now PASS
- Returns meaningful text from VobSub files
- Proper cleanup of temp files

---

### T014 Implement dependency validation

**File**: `src/EpisodeIdentifier.Core/Services/DependencyValidator.cs`
**Description**: Create service to check for mkvextract and Tesseract availability
**Dependencies**: None (standalone utility)

**Requirements**:

- Method: `Task<bool> IsMkvExtractAvailableAsync()`
- Method: `Task<bool> IsTesseractAvailableAsync()`
- Check both by attempting `--version` command
- Cache results (don't check every time)
- Add structured logging for dependency checks
- Used by Program.cs before attempting DVD subtitle processing

**Acceptance**:

- Returns true when tools installed
- Returns false when tools missing
- Caches results for performance
- Logs dependency check results

---

### T015 Integrate DVD subtitle processing into Program.cs

**File**: `src/EpisodeIdentifier.Core/Program.cs`
**Description**: Update main CLI logic to support DVD subtitle extraction and OCR
**Dependencies**: T011, T013, T014

**Current State** (lines 549-560):

- Detects DVD subtitles
- Returns UNSUPPORTED_SUBTITLE_FORMAT error

**Required Changes**:

1. Remove temporary UNSUPPORTED_SUBTITLE_FORMAT error (lines 549-560)
2. Add dependency checks using DependencyValidator
3. Implement subtitle priority logic:
   - First: Try text subtitle processing (existing)
   - Second: Try PGS subtitle processing (existing)
   - Third: Try DVD subtitle processing (NEW)
4. For DVD subtitle path:
   - Call VobSubExtractor.ExtractAsync()
   - Call VobSubOcrService.PerformOcrAsync()
   - Pass OCR text to EpisodeIdentificationService
5. Return appropriate error codes:
   - MISSING_DEPENDENCY if tools not available
   - OCR_FAILED if OCR processing fails
   - Standard identification errors if matching fails
6. Add subtitleType field to JSON output: "text", "pgs", or "dvd_subtitle"
7. Add ocrConfidence field for DVD subtitle results
8. Handle cleanup of temp files

**Acceptance**:

- Integration test T009 now PASS (priority logic works)
- quickstart.md test scenario 2 passes
- Files with only DVD subtitles process successfully
- Files with multiple subtitle types prefer text > PGS > DVD
- JSON output includes subtitleType field

---

### T016 Add file size validation

**File**: `src/EpisodeIdentifier.Core/Services/VobSubExtractor.cs` (update)
**Description**: Add 50MB size limit check before extraction
**Dependencies**: T011
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/spec.md` FR-013

**Requirements**:

- Check subtitle track size before extraction (use ffprobe)
- Command: `ffprobe -v error -show_entries stream=size -of default=noprint_wrappers=1:nokey=1 -select_streams s:{trackIndex} "{videoPath}"`
- If size > 50MB, return failure with SUBTITLE_TOO_LARGE error code
- Log size information

**Acceptance**:

- Rejects tracks over 50MB
- Returns clear error message
- Doesn't attempt extraction for oversized tracks
- Logs subtitle track size

---

### T017 Implement temp file cleanup

**File**: `src/EpisodeIdentifier.Core/Services/VobSubExtractor.cs` and `VobSubOcrService.cs` (update)
**Description**: Ensure temporary files are cleaned up after processing
**Dependencies**: T011, T013
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/spec.md` FR-013

**Requirements**:

- Create temp directories with unique names (use Guid)
- Use try-finally blocks to ensure cleanup
- Delete temp directory and all contents after processing
- Handle cleanup errors gracefully (log but don't throw)
- Clean up even if extraction/OCR fails

**Acceptance**:

- No temp files remain after successful processing
- No temp files remain after failed processing
- Logs cleanup operations
- Handles permission errors during cleanup

---

## Phase 3.5: Integration & Polish

### T018 [P] Add unit tests for edge cases

**File**: `tests/unit/VobSubExtractorTests.cs` and `tests/unit/VobSubOcrServiceTests.cs`
**Description**: Add unit tests for error handling and edge cases

**Test Coverage**:

1. VobSubExtractor:
   - Null/empty video path
   - Negative track index
   - Non-existent output directory
   - mkvextract returns non-zero exit code
   - Partial extraction (.idx created but .sub missing)

2. VobSubOcrService:
   - Null/empty file paths
   - Invalid language code
   - Zero images extracted
   - Tesseract returns no text
   - Confidence score calculation edge cases

**Requirements**:

- Use xUnit + FluentAssertions
- Mock file system operations (use IFileSystem)
- Mock process execution where needed
- Test boundary conditions

**Acceptance**:

- All unit tests PASS
- Code coverage > 80% for new services
- Edge cases properly handled

---

### T019 Add MISSING_DEPENDENCY error to quickstart.md test

**File**: `tests/integration/DependencyValidationIntegrationTests.cs` (NEW)
**Description**: Integration test for missing tool error handling
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/quickstart.md` (Test scenario 4)

**Test Scenarios**:

1. `IdentifyDvdSubtitleFileWithoutMkvextract_ReturnsMissingDependencyError`
   - Given: mkvextract not on PATH
   - When: DVD subtitle file identification attempted
   - Then: Returns MISSING_DEPENDENCY error with message about mkvextract

2. `IdentifyDvdSubtitleFileWithoutTesseract_ReturnsMissingDependencyError`
   - Given: Tesseract not on PATH
   - When: DVD subtitle file identification attempted
   - Then: Returns MISSING_DEPENDENCY error with message about Tesseract

**Requirements**:

- Use integration test approach (not unit test)
- Temporarily modify PATH in test to hide tools
- Restore PATH after test
- Verify JSON error response format

**Acceptance**:

- Tests PASS
- Error messages are clear and actionable
- Tests don't permanently modify system PATH

---

### T020 Performance test DVD subtitle processing

**File**: `tests/integration/DvdSubtitlePerformanceTests.cs` (NEW)
**Description**: Verify DVD subtitle processing meets performance requirements
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/plan.md` (5-minute timeout)

**Test Scenarios**:

1. `ProcessCriminalMindsEpisode_CompletesWithin30Seconds`
   - Given: Standard Criminal Minds episode with DVD subtitles
   - When: Full extraction + OCR pipeline runs
   - Then: Completes within 30 seconds

2. `ProcessMultipleEpisodesSequentially_AverageTime`
   - Given: 5 different episodes with DVD subtitles
   - When: Each processed sequentially
   - Then: Average time per episode < 25 seconds

**Requirements**:

- Use real Criminal Minds test files
- Measure actual wall-clock time
- Log performance metrics
- Test runs as part of CI/CD if test files available

**Acceptance**:

- Tests PASS
- Performance meets 5-minute timeout requirement
- Logs include timing breakdowns (extraction vs OCR)

---

### T021 Update .github/copilot-instructions.md

**File**: `.github/copilot-instructions.md`
**Description**: Document DVD subtitle feature in agent context file
**Dependencies**: T015 (integration complete)

**Requirements**:

- Add VobSubExtractor and VobSubOcrService to Active Technologies section
- Update Project Structure section with new files
- Document error codes: MISSING_DEPENDENCY, OCR_FAILED, SUBTITLE_TOO_LARGE
- Add quickstart command examples for DVD subtitle processing
- Update Recent Changes with feature summary

**Acceptance**:

- File compiles/validates
- New services documented
- Agent context reflects DVD subtitle capability

---

### T022 [P] Update README.md with DVD subtitle support

**File**: `README.md`
**Description**: Document DVD subtitle OCR feature in user-facing documentation

**Requirements**:

- Add DVD subtitle support to features list
- Document required dependencies (mkvextract, Tesseract)
- Installation instructions for mkvtoolnix and tesseract-ocr
- Example commands for DVD subtitle processing
- Note about subtitle priority (text > PGS > DVD)
- Troubleshooting section for MISSING_DEPENDENCY errors

**Acceptance**:

- README accurately describes DVD subtitle support
- Installation instructions are clear
- Examples are executable

---

### T023 Execute manual testing from quickstart.md

**File**: N/A (manual testing)
**Description**: Run through all manual test scenarios in quickstart.md
**Reference**: `/mnt/c/Users/Ragma/KnowShow_Specd/specs/012-process-dvd-subtitle/quickstart.md`

**Test Checklist**:

- [ ] DVD subtitle detection (Test 1)
- [ ] DVD subtitle processing end-to-end (Test 2)
- [ ] Subtitle priority verification (Test 3)
- [ ] Missing dependencies error handling (Test 4)
- [ ] Manual VobSub extraction (Validation 1)
- [ ] Manual image extraction (Validation 2)
- [ ] Manual Tesseract OCR (Validation 3)
- [ ] Bulk processing with DVD subtitles
- [ ] All troubleshooting scenarios

**Acceptance**:

- All quickstart test scenarios PASS
- JSON output matches expected format
- Performance within acceptable limits
- Error messages are clear and actionable

---

## Dependencies

```
Setup Phase (T001-T004):
  T001, T002 (interfaces) → No dependencies [can run in parallel]
  T003, T004 (models) → No dependencies [can run in parallel]

Test Phase (T005-T010):
  T005 → T001 (needs interface)
  T006 → T002 (needs interface)
  T007 → T001, T003 (needs interface + model)
  T008 → T002, T004 (needs interface + model)
  T009 → T001, T002, T003, T004 (needs all setup)
  T010 → T001, T002, T003, T004 (needs all setup)

Implementation Phase (T011-T017):
  T011 → T005 (contract tests must be failing first)
  T012 → T006 (contract tests must be failing first)
  T013 → T012 (needs image extraction)
  T014 → None (standalone)
  T015 → T011, T013, T014 (needs all services)
  T016 → T011 (updates VobSubExtractor)
  T017 → T011, T013 (updates both services)

Polish Phase (T018-T023):
  T018 → T011, T013 (tests implementation)
  T019 → T015 (tests integration)
  T020 → T015 (tests integration)
  T021 → T015 (documents completed feature)
  T022 → T015 (documents completed feature)
  T023 → ALL (final validation)
```

## Parallel Execution Examples

### Phase 3.1 - Setup (all parallel)

```
Task: "Create IVobSubExtractor interface in src/EpisodeIdentifier.Core/Interfaces/IVobSubExtractor.cs"
Task: "Create IVobSubOcrService interface in src/EpisodeIdentifier.Core/Interfaces/IVobSubOcrService.cs"
Task: "Create VobSubExtractionResult model in src/EpisodeIdentifier.Core/Models/VobSubExtractionResult.cs"
Task: "Create VobSubOcrResult model in src/EpisodeIdentifier.Core/Models/VobSubOcrResult.cs"
```

### Phase 3.2 - Contract Tests (parallel after T001-T004)

```
Task: "Contract test for IVobSubExtractor in tests/contract/VobSubExtractorContractTests.cs"
Task: "Contract test for IVobSubOcrService in tests/contract/VobSubOcrServiceContractTests.cs"
```

### Phase 3.3 - Integration Tests (parallel after T005-T006)

```
Task: "Integration test for VobSub extraction in tests/integration/VobSubExtractionIntegrationTests.cs"
Task: "Integration test for VobSub OCR in tests/integration/VobSubOcrIntegrationTests.cs"
```

### Phase 3.5 - Documentation (parallel after T015)

```
Task: "Update .github/copilot-instructions.md with DVD subtitle documentation"
Task: "Update README.md with DVD subtitle support documentation"
```

## Task Summary

**Total Tasks**: 23
**Parallel Tasks**: 10 (marked with [P])
**Sequential Tasks**: 13
**Estimated Completion**: 5-7 days (with TDD approach)

**Breakdown by Phase**:

- Setup & Interfaces: 4 tasks (2-3 hours)
- Tests First: 6 tasks (1-2 days) ⚠️ CRITICAL
- Implementation: 7 tasks (2-3 days)
- Polish: 6 tasks (1 day)

## Validation Checklist

*GATE: Verify before starting implementation*

- [x] All contracts have corresponding tests (T005, T006)
- [x] All entities have model tasks (T003, T004)
- [x] All tests come before implementation (T005-T010 before T011-T017)
- [x] Parallel tasks truly independent (different files)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] Integration tests cover all user scenarios from quickstart.md
- [x] TDD RED-GREEN-Refactor cycle enforced

## Notes

- **TDD Enforcement**: Tests T005-T010 MUST fail before starting implementation T011-T017
- **Real Dependencies**: Integration tests use actual mkvextract and Tesseract (not mocked)
- **Test Files**: Use Criminal Minds S5 files from `/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/`
- **Cleanup**: Always clean up temp files in finally blocks
- **Performance**: Target < 30 seconds per episode (well under 5-minute timeout)
- **Error Handling**: Use existing error code patterns (NO_SUBTITLES, MISSING_DEPENDENCY, etc.)
- **Logging**: Add structured logging for all operations (extraction, OCR, cleanup)
- **Commit Strategy**: Commit after each task completion

---

**Ready for execution** ✅ - Begin with T001-T004 (setup phase)
