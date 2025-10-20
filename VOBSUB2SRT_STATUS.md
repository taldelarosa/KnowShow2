# VobSub2SRT Status and Solution

## Current Status

The `vobsub2srt` tool has been **temporarily disabled** in the Docker build due to incompatibility with Tesseract 5.x (available in Debian Bookworm).

## Issue Details

### Problem
- `vobsub2srt` (from https://github.com/ruediger/VobSub2SRT) was written for older Tesseract versions (2.x/3.x)
- Tesseract 5.x introduced API changes that break compilation:
  - C++11 `constexpr` usage
  - Namespace changes for `TessBaseAPI`
  - Header file restructuring

### Build Errors Encountered
1. **Missing TIFF library** - Fixed by adding `libtiff-dev`
2. **C++ standard** - Fixed by adding `-DCMAKE_CXX_STANDARD=11`
3. **Tesseract API incompatibility** - Cannot be fixed without major code changes to vobsub2srt

### Current Docker Build Status
✅ **Docker builds successfully** with all dependencies EXCEPT vobsub2srt:
- FFmpeg ✅
- mkvtoolnix ✅  
- tesseract-ocr ✅
- pgsrip ✅
- All .NET dependencies ✅

## Impact

### What Still Works
- ✅ Text subtitle extraction (SRT, ASS, WebVTT)
- ✅ PGS subtitle OCR via pgsrip
- ✅ VobSub extraction via mkvextract
- ✅ All database and identification features

### What's Affected
- ❌ VobSub OCR using `vobsub2srt` tool
- ⚠️ `VobSubOcrService.cs` will fail at runtime when trying to use vobsub2srt
- ⚠️ Contract tests for VobSub OCR are skipped in CI

## Solution Options

### Option 1: Implement Direct Tesseract OCR (Recommended)
**Approach**: Modify `VobSubOcrService.cs` to use Tesseract directly instead of vobsub2srt

**Steps**:
1. Use `mkvextract` to extract VobSub .idx/.sub files (already working)
2. Convert VobSub images to individual PNGs
3. Run Tesseract directly on each PNG image
4. Parse Tesseract output and combine into SRT format
5. Handle timing information from .idx file

**Advantages**:
- No external tool dependency beyond tesseract (already installed)
- More control over OCR process
- Can tune Tesseract parameters for DVD subtitles
- Works with Tesseract 5.x

**Implementation Effort**: Medium (2-3 days)

### Option 2: Use Compatible Fork
**Approach**: Find or create a fork of vobsub2srt compatible with Tesseract 5.x

**Challenges**:
- No actively maintained fork found
- Original project unmaintained since 2015
- Would require significant code modernization

**Advantages**:
- Minimal code changes to `VobSubOcrService.cs`

**Implementation Effort**: High (requires forking and modernizing vobsub2srt)

### Option 3: Use Alternative Tool
**Approach**: Replace vobsub2srt with modern alternative

**Candidates**:
- SubRip (part of `transcode` package) - focuses on subtitle extraction, not OCR
- Custom Python script using pytesseract
- FFmpeg + Tesseract pipeline

**Advantages**:
- Potentially more maintained
- Modern API support

**Disadvantages**:
- Need to research alternatives
- May require significant code changes

**Implementation Effort**: Medium-High

## Recommended Action Plan

### Phase 1: Document Current State ✅
- [x] Document vobsub2srt incompatibility
- [x] Note in Dockerfile why it's disabled
- [x] Ensure Docker builds successfully

### Phase 2: Implement Workaround (Priority: Medium)
1. Create new `DirectTesseractVobSubOcr` service
2. Implement PNG extraction from VobSub
3. Run Tesseract on individual frames
4. Generate SRT output
5. Update `VobSubOcrService` to use new implementation
6. Add integration tests

### Phase 3: Update Documentation
1. Update `.github/copilot-instructions.md`
2. Update Feature 012 spec
3. Note in README that vobsub2srt is replaced

## Temporary Workaround

Until Option 1 is implemented, VobSub subtitle files cannot be processed. Users should:
1. Use videos with text-based subtitles (SRT, ASS, WebVTT) 
2. Use videos with PGS subtitles (Blu-ray)
3. Convert VobSub to text manually using online tools

## Files Affected

### Docker
- `Dockerfile` - vobsub2srt build section commented out

### Application Code  
- `src/EpisodeIdentifier.Core/Services/VobSubOcrService.cs` - Will fail at runtime
- `src/EpisodeIdentifier.Core/Services/SubtitleWorkflowCoordinator.cs` - VobSub workflow will fail

### Tests
- `tests/contract/VobSubOcrServiceContractTests.cs` - Tests skipped with `Skip = "Requires vobsub2srt"`

## Timeline

- **Current**: Docker builds successfully, vobsub2srt disabled
- **Short-term (1 week)**: Deploy with VobSub functionality unavailable
- **Medium-term (1 month)**: Implement Option 1 (direct Tesseract) if VobSub support is needed

## References

- Original vobsub2srt: https://github.com/ruediger/VobSub2SRT
- Tesseract 5 API docs: https://tesseract-ocr.github.io/tessapi/5.x/
- Feature 012 Spec: `specs/012-process-dvd-subtitle/spec.md`
