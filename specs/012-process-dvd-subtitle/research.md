# Phase 0: Research - DVD Subtitle (VobSub) OCR Processing

**Feature**: 012-process-dvd-subtitle  
**Date**: 2025-10-17  
**Status**: Complete

## Research Questions & Decisions

### 1. VobSub Format and Extraction

**Question**: How to extract DVD subtitle (VobSub) data from MKV files?

**Decision**: Use `mkvextract` from mkvtoolnix package

**Rationale**:
- Industry-standard tool for MKV manipulation
- Directly outputs VobSub format (.idx + .sub files)
- Cross-platform (Windows, Linux, macOS)
- Reliable and well-maintained
- Command: `mkvextract tracks input.mkv {trackId}:output.sub`
  - Automatically creates both .idx and .sub files
  - Track ID obtained from VideoFormatValidator (already implemented)

**Alternatives Considered**:
1. **ffmpeg**: Cannot properly extract VobSub format; dvd_subtitle codec not supported for extraction
2. **Custom parser**: Would require implementing DVD subtitle format spec; complex and error-prone
3. **VobSub2SRT + extraction**: Additional dependency; we only need extraction, not permanent conversion

**Implementation Notes**:
- Check for mkvextract availability before processing (PATH check)
- Use `System.Diagnostics.Process` to execute mkvextract
- Parse stderr for errors (mkvextract outputs progress to stderr, errors are distinct)
- Validate both .idx and .sub files created successfully

---

### 2. OCR Engine Selection

**Question**: Which OCR engine to use for VobSub bitmap subtitle recognition?

**Decision**: Tesseract OCR

**Rationale**:
- Open source and free
- Excellent accuracy for text recognition (70%+ achievable)
- Support for multiple languages
- Cross-platform
- Actively maintained
- Can process various image formats
- Command-line interface easy to integrate

**Alternatives Considered**:
1. **Google Cloud Vision API**: Costs money; requires internet; overkill for subtitle OCR
2. **Azure Computer Vision**: Same issues as Google; unnecessary cloud dependency
3. **OCRopus**: Less mature than Tesseract; smaller community
4. **VobSub2SRT**: Uses Tesseract under the hood; adds extra layer; we can use Tesseract directly

**Implementation Notes**:
- Tesseract requires language data files (e.g., `eng.traineddata`)
- Check for tesseract availability before processing
- Use `tesseract {image} stdout -l {lang}` for text output
- May need to extract images from VobSub first or use Tesseract's VobSub support

---

### 3. VobSub Image Extraction

**Question**: How to extract individual subtitle images from VobSub files for OCR?

**Decision**: Use Tesseract's native VobSub support OR extract images first with ffmpeg/custom tool

**Research Findings**:
- **Option A**: Tesseract can read certain subtitle formats directly
  - May support VobSub/idx files with proper configuration
  - Needs testing with actual files
  
- **Option B**: Extract bitmap images first, then OCR each image
  - Use ffmpeg or SubtitleEdit library to extract PNGs from VobSub
  - Process each PNG with Tesseract
  - Concatenate results

**Decision**: Start with Option B (extract images first)

**Rationale**:
- More control over the process
- Can validate/preprocess images before OCR
- Can sample images to estimate OCR quality
- Similar pattern to existing PgsToTextConverter approach
- More debuggable (can inspect extracted images)

**Implementation Notes**:
- Use `ffmpeg -i input.idx -f image2 output_%04d.png` to extract frames
- Process images in batches to avoid memory issues
- Track timestamp/sequence information from .idx file
- Concatenate text from all images with proper ordering

---

### 4. Integration with Existing Subtitle Processing

**Question**: How to integrate DVD subtitle processing with existing PgsToTextConverter and subtitle priority system?

**Decision**: Create parallel VobSubExtractor service, update Program.cs priority logic

**Rationale**:
- Current code in Program.cs already has priority logic (lines 545-568)
- PgsToTextConverter handles PGS format specifically
- DVD subtitle extraction is different enough to warrant separate service
- Separation of concerns - each service handles one format
- Easier to test independently

**Architecture**:
```
Program.cs
├─ Check subtitle tracks (VideoFormatValidator)
├─ Priority: Text > PGS > DVD
│  ├─ Text subtitles → VideoTextSubtitleExtractor (existing)
│  ├─ PGS subtitles → PgsRipService/PgsToTextConverter (existing)
│  └─ DVD subtitles → VobSubExtractor + VobSubOcrService (NEW)
└─ Episode identification (existing)
```

**Changes Required**:
1. Update Program.cs DVD subtitle detection (currently returns error)
2. Add VobSubExtractor service instantiation
3. Add VobSubOcrService service instantiation
4. Wire up DVD subtitle path before OCR_FAILED error
5. Reuse existing subtitle normalization logic

**Alternatives Considered**:
1. **Modify PgsToTextConverter**: Would mix PGS and DVD logic; violates single responsibility
2. **Generic SubtitleExtractor**: Over-engineering; only 2 bitmap formats currently
3. **Separate executable**: Unnecessary complexity; fits well as part of existing app

---

### 5. Error Handling Strategy

**Question**: How to handle errors specific to DVD subtitle processing?

**Decision**: Use existing error code patterns with DVD-specific codes

**Existing Error Codes** (already in codebase):
- `NO_SUBTITLES`: No subtitle tracks found
- `UNSUPPORTED_FILE_TYPE`: Not an MKV file
- `MISSING_DEPENDENCY`: Required tool not available
- `OCR_FAILED`: OCR extraction failed

**New Error Scenarios**:
- mkvextract not found → Use existing `MISSING_DEPENDENCY`
- tesseract not found → Use existing `MISSING_DEPENDENCY`
- VobSub extraction fails → Use existing `OCR_FAILED` with specific message
- Subtitle track >50MB → New: `SUBTITLE_TOO_LARGE`
- Timeout during extraction → Use existing timeout handling from configuration

**Rationale**:
- Consistent with existing error handling patterns
- Reuses infrastructure
- Users get familiar error format
- JSON error responses already implemented

**Implementation Notes**:
- Check dependencies before attempting extraction
- Wrap extraction in try-catch with specific error messages
- Log detailed errors for debugging
- Return user-friendly messages in JSON output

---

### 6. Temporary File Management

**Question**: How to manage temporary files created during VobSub extraction and OCR?

**Decision**: Use `System.IO.Path.GetTempPath()` with unique subdirectories, cleanup in finally blocks

**Rationale**:
- Cross-platform (works on Windows and Linux)
- Automatic temp directory per platform
- Unique directory per extraction prevents collisions
- Finally blocks ensure cleanup even on errors
- No caching needed (per specification FR-010)

**Implementation Pattern**:
```csharp
string tempDir = Path.Combine(Path.GetTempPath(), $"vobsub_{Guid.NewGuid()}");
try {
    Directory.CreateDirectory(tempDir);
    // Extract and process
} finally {
    if (Directory.Exists(tempDir)) {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

**Alternatives Considered**:
1. **Fixed temp directory**: Risk of file collisions with concurrent processing
2. **Project-relative directory**: Pollutes project space; requires manual cleanup
3. **Keep temp files for caching**: Specification explicitly forbids caching (FR-010)

---

### 7. Performance and Size Constraints

**Question**: How to handle performance requirements and size limits?

**Decisions**:

1. **50MB Size Limit** (FR-013):
   - Check subtitle track size before extraction
   - Use ffprobe to get stream size: `ffprobe -v error -select_streams s:{index} -show_entries stream=size`
   - Reject with `SUBTITLE_TOO_LARGE` error if > 50MB

2. **5-Minute Timeout** (FR-011, NFR-001):
   - Use CancellationTokenSource with timeout from configuration
   - Pass CancellationToken to all async operations
   - Process tracks as OperationCanceledException

3. **Memory Management** (NFR-002):
   - Process images incrementally (don't load all in memory)
   - Stream OCR results to string builder
   - Delete processed images immediately after OCR

**Rationale**:
- Prevents runaway processing
- Protects system resources
- Matches existing timeout configuration
- Reasonable limits for typical DVD subtitle tracks

---

### 8. OCR Quality and Confidence Thresholds

**Question**: How to assess and report OCR quality?

**Decision**: Use character recognition rate and existing matchConfidenceThreshold

**Quality Metrics**:
1. **Character Recognition Rate**: Total characters recognized / total characters expected
   - Target: 70% minimum (NFR-003)
   - Tesseract provides confidence scores per character
   - Calculate aggregate confidence

2. **Episode Match Confidence**: Use existing `matchConfidenceThreshold` (50%)
   - After OCR, pass text to episode identification
   - Existing fuzzy matching applies normal confidence rules
   - No special handling needed

**Implementation Notes**:
- Tesseract outputs confidence with `--psm 6` and `-c tessedit_create_hocr=1`
- Parse HOCR output to extract confidence scores
- Log confidence metrics for monitoring
- Don't fail on low confidence - let episode matching decide

**Rationale**:
- Consistent with existing confidence system
- OCR quality doesn't need special thresholds
- Episode matching already handles low-quality text
- Simplifies implementation

---

## Best Practices Identified

### mkvextract Usage
- Always check exit code (0 = success)
- Parse stderr for actual errors (progress goes to stderr too)
- Validate both .idx and .sub files exist after extraction
- Handle spaces in file paths properly (use quotes)

### Tesseract Usage
- Specify language explicitly (`-l eng`)
- Use appropriate Page Segmentation Mode (`--psm 6` for uniform blocks)
- Set output to stdout for easy capture
- Handle multi-line output properly (subtitles span multiple lines)

### Cross-Platform Considerations
- Use `Path.Combine()` for all path operations
- Check tool availability with `which` (Linux) or `where` (Windows)
- Handle path separators correctly
- Test on both Windows and Linux (WSL counts as Linux)

### Testing Strategy
- Use actual tools (no mocks) for integration tests
- Include test files with known content for validation
- Test error scenarios (missing tools, corrupt files, timeouts)
- Verify temp file cleanup in all code paths

---

## Dependencies Summary

### Required External Tools
1. **mkvextract** (from mkvtoolnix package)
   - Install: `apt install mkvtoolnix` (Linux) or download from mkvtoolnix.download (Windows)
   - Version: Any recent version (5.0+)
   - Verification: `mkvextract --version`

2. **tesseract** (Tesseract OCR)
   - Install: `apt install tesseract-ocr tesseract-ocr-eng` (Linux) or from GitHub releases (Windows)
   - Version: 4.0+ recommended
   - Verification: `tesseract --version`
   - Requires language data files in tessdata directory

### NuGet Packages (if needed)
- No new NuGet packages required
- All functionality achievable with process execution and existing packages

---

## Risk Assessment

### Low Risk
- ✓ Tools are mature and stable (mkvextract, tesseract)
- ✓ VobSub format is well-documented
- ✓ Integration point is clear (extends existing subtitle processing)
- ✓ Test files available (Criminal Minds S5 files)

### Medium Risk
- ⚠ OCR accuracy depends on subtitle image quality
  - Mitigation: Document minimum requirements; fail gracefully
- ⚠ Different DVD subtitle resolutions may affect OCR
  - Mitigation: Tesseract handles various resolutions; test with multiple sources

### Mitigated Risks
- ✗ Performance concerns
  - Mitigated: 5-minute timeout, 50MB size limit
- ✗ Memory usage with large tracks
  - Mitigated: Incremental processing, immediate cleanup
- ✗ Concurrent processing conflicts
  - Mitigated: Unique temp directories per extraction

---

## Next Steps (Phase 1)

1. Create data-model.md with VobSubExtractionResult and VobSubOcrResult models
2. Generate service contracts (IVobSubExtractor, IVobSubOcrService)
3. Write contract tests (failing tests first - TDD)
4. Create integration test scenarios
5. Generate quickstart.md with test commands
6. Update .github/copilot-instructions.md

---

**Research Complete**: All technical decisions made, ready for design phase.
