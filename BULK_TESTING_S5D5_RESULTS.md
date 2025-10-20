# Bulk Testing Results: Criminal Minds S5 Disc 5

**Date:** 2025-10-19 22:46  
**Feature Branch:** 013-ml-embedding-matching  
**Test Directory:** `\\10.0.0.200\KnowShow\KnowShowProcessing\CRIMINAL_MINDS_S5_D5-plEOaC`  
**Command:** `--bulk-identify [directory] --rename`  
**Strategy:** Embedding matching (ML-based)

## Test Files

| File | Size | Duration | Subtitle Codec | Subtitle Frames |
|------|------|----------|---------------|-----------------|
| B3_t02.mkv | 504 MB | 00:29:15 | dvd_subtitle | Unknown |
| B4_t03.mkv | 508 MB | 00:29:44 | dvd_subtitle | Unknown |
| C1_t00.mkv | 425 MB | 00:24:51 | dvd_subtitle | Unknown |
| D1_t01.mkv | 140 MB | 00:08:10 | dvd_subtitle | Unknown |
| D2_t04.mkv |  14 MB | 00:03:01 | dvd_subtitle | 75 frames |
| E3_t05.mkv |  98 MB | 00:16:54 | dvd_subtitle | 392 frames |

**Total:** 6 files, 1.69 GB

## Results Summary

**Status:** ❌ **FAILED** - Critical Bug Discovered  
**Processed:** 1/6 files (16.7%)  
**Failed:** 5/6 files (83.3%)  
**Processing Time:** 00:50 seconds

### Successful Files

- `D1_t01.mkv` - Status: Success (00.401s)

### Failed Files  

- `B3_t02.mkv` - Failed (26.558s) - DVD subtitle OCR error
- `B4_t03.mkv` - Failed (26.558s) - DVD subtitle OCR error  
- `C1_t00.mkv` - Failed (26.369s) - DVD subtitle OCR error
- `D2_t04.mkv` - Failed (22.097s) - DVD subtitle OCR error
- `E3_t05.mkv` - Failed (22.268s) - DVD subtitle OCR error

## Critical Bug Identified

### Issue: DVD Subtitles Misidentified as PGS

**Error Message:**

```
File processing failed: Failed to extract readable text from PGS subtitles using OCR and no text subtitle fallback available
```

**Root Cause:**  
Files with `dvd_subtitle` codec (VobSub) are being incorrectly routed to PGS OCR processing instead of VobSub OCR processing.

**Evidence:**

```
# ffmpeg reports dvd_subtitle codec
Stream #0:2(eng): Subtitle: dvd_subtitle, 720x480 (default)

# But error says "PGS subtitles"
Failed to extract readable text from PGS subtitles using OCR

# PGS OCR attempts fail
warn: EpisodeIdentifier.Core.Services.PgsRipService[0]
      pgsrip completed but no SRT files were generated

# ffmpeg extraction fails
Error selecting an encoder for stream 0:0
Automatic encoder selection failed for output stream #0:0
```

### Expected Behavior

1. **Detect** `dvd_subtitle` codec in MKV container
2. **Route** to VobSubExtractor service (Feature 012)
3. **Extract** .idx/.sub files using mkvextract
4. **Convert** to text using vobsub2srt (Tesseract OCR)
5. **Generate** embeddings from VobSub text
6. **Match** against database using ML similarity

### Actual Behavior

1. **Detect** `dvd_subtitle` codec (✅ Correct)
2. **Route** to EnhancedPgsToTextConverter (❌ **Wrong service**)
3. **Attempt** pgsrip extraction (fails - not PGS format)
4. **Fallback** to ffmpeg image extraction (fails - encoder issue)
5. **Fail** with "no text subtitle fallback available"

### Impact

- **Feature 012 (VobSub OCR):** Not being invoked for dvd_subtitle files
- **Feature 013 (ML Embeddings):** Cannot test with DVD subtitle files
- **Bulk processing:** Fails on most DVD rips (common format)
- **User experience:** 83.3% failure rate on test directory

## Technical Details

### Subtitle Type Detection Issue

The system correctly identifies `dvd_subtitle` codec:

```
info: EpisodeIdentifier.Core.Services.VideoFormatValidator[0]
      Found subtitle track: Index=2, Codec=dvd_subtitle, Language=eng, Title=untitled
info: EpisodeIdentifier.Core.Services.VideoFormatValidator[0]
      File .../E3_t05.mkv: MKV=True, Subtitles=True (Count: 1)
info: EpisodeIdentifier.Core.Services.VideoFormatValidator[0]
      Subtitle types found: dvd_subtitle
```

But then routes to wrong service:

```
info: EpisodeIdentifier.Core.Services.EnhancedPgsToTextConverter[0]
      Converting PGS from video: .../E3_t05.mkv, track 2, language: eng
```

**Bug Location:** Likely in `EpisodeIdentificationService` or subtitle routing logic that decides which converter to use based on codec type.

### Retry Behavior

System retries each file 4 times (configured max retries), wasting time on unrecoverable error:

```
fail: EpisodeIdentifier.Core.Services.BulkProcessorService[0]
      File processing failed (attempt 1/4) for .../E3_t05.mkv: ProcessingError
fail: EpisodeIdentifier.Core.Services.BulkProcessorService[0]
      File processing failed (attempt 2/4) for .../E3_t05.mkv: ProcessingError
fail: EpisodeIdentifier.Core.Services.BulkProcessorService[0]
      File processing failed (attempt 3/4) for .../E3_t05.mkv: ProcessingError
fail: EpisodeIdentifier.Core.Services.BulkProcessorService[0]
      File processing failed (attempt 4/4) for .../E3_t05.mkv: ProcessingError
```

**Recommendation:** Non-retryable errors (codec mismatch, service routing) should fail fast without retries.

### Secondary Bug: Progress Tracker Array Exception

```
System.ArgumentException: Destination array was not long enough. Check the destination index, length, and the array's lower bounds. (Parameter 'destinationArray')
    at EpisodeIdentifier.Core.Services.ProgressTracker.CloneProgress(BulkProcessingProgress original)
```

This appears to be a concurrency issue in progress tracking when handling multiple failures.

## Affected Features

### Feature 012: DVD Subtitle (VobSub) OCR Support

**Status:** ❌ **Not Working in Bulk Processing**  

- VobSubExtractor service not being invoked
- vobsub2srt tool not being used  
- DVD subtitle files failing completely

### Feature 013: ML Embedding Matching

**Status:** ⚠️ **Cannot Test with DVD Subtitles**  

- Needs text input from VobSub OCR
- Cannot validate embedding generation for VobSub format
- Cannot test lower confidence thresholds for DVD OCR (0.75 similarity, 0.50 match confidence)

## Recommendations

### Immediate Fixes Required

1. **Fix Subtitle Routing Logic**
   - Check codec type: `hdmv_pgs_subtitle` → PGS converter
   - Check codec type: `dvd_subtitle` → VobSub converter
   - Check codec type: `subrip`, `ass`, `webvtt` → Text converter

2. **Add VobSub Path to EpisodeIdentificationService**
   - Currently only has PGS and Text subtitle paths
   - Need to add VobSub → vobsub2srt → OCR → Embedding path

3. **Improve Error Messages**
   - Current: "Failed to extract readable text from PGS subtitles"
   - Better: "Failed to extract readable text from DVD subtitles (codec: dvd_subtitle)"

4. **Add Fail-Fast for Non-Retryable Errors**
   - Codec mismatches shouldn't retry
   - Service routing errors shouldn't retry
   - Only transient failures (network, timeout) should retry

### Testing Recommendations

1. **Unit Tests for Codec Detection**
   ```csharp
   [Fact]
   public void DetectsDvdSubtitleCodec() {
       var codec = GetSubtitleCodec("dvd_subtitle");
       Assert.Equal(SubtitleType.VobSub, codec);
   }
   ```

2. **Integration Tests for Subtitle Routing**
   - Test file with `hdmv_pgs_subtitle` → PgsToTextConverter
   - Test file with `dvd_subtitle` → VobSubOcrService  
   - Test file with `subrip` → Direct text extraction

3. **End-to-End Test with DVD Subtitles**
   - Use Criminal Minds S5 D5 files
   - Verify VobSub extraction
   - Verify vobsub2srt conversion
   - Verify embedding generation
   - Verify ML matching with 0.75 threshold

## Next Steps

1. ❌ **BLOCK MERGE** - Feature 013 cannot merge with Feature 012 broken
2. 🔧 **Debug subtitle routing** in EpisodeIdentificationService
3. 🧪 **Add codec detection tests** for all subtitle types
4. ✅ **Retest bulk processing** after fix
5. 📊 **Validate ML matching** works with VobSub OCR text (lower quality expected)

## Log File

Full log saved to: `/tmp/bulk_identify_s5d5_20251019_224640.log` (212 KB)

## Conclusion

The bulk testing revealed a **critical bug** where DVD subtitle files (`dvd_subtitle` codec) are incorrectly routed to PGS OCR processing, causing 83.3% failure rate.

**Feature 012 (VobSub OCR) is not integrated with episode identification flow**, blocking Feature 013 testing on DVD subtitle content.

This must be fixed before Feature 013 can be merged or properly validated.
