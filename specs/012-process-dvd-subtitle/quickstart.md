# DVD Subtitle Processing - Quickstart Guide

## Overview

This guide covers testing the DVD subtitle OCR feature using Criminal Minds Season 5 files.

## Prerequisites

### Install Dependencies

```bash
# Install mkvtoolnix (for mkvextract)
sudo apt-get install mkvtoolnix

# Install Tesseract OCR with English language data
sudo apt-get install tesseract-ocr tesseract-ocr-eng

# Verify installation
mkvextract --version
tesseract --version
```

### Build Project

```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
dotnet build
```

## Quick Test Commands

### 1. Test DVD Subtitle Detection

```bash
# Should return UNSUPPORTED_SUBTITLE_FORMAT error with DVD subtitle info
dotnet run --project src/EpisodeIdentifier.Core -- \
  identify \
  --video "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv"
```

**Expected JSON:**

```json
{
  "matched": false,
  "confidence": 0.0,
  "errorCode": "UNSUPPORTED_SUBTITLE_FORMAT",
  "errorMessage": "File contains DVD subtitles which require OCR processing (not yet supported)"
}
```

### 2. Test DVD Subtitle Processing (After Implementation)

```bash
# Should extract VobSub, perform OCR, and match episode
dotnet run --project src/EpisodeIdentifier.Core -- \
  identify \
  --video "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv" \
  --ocr-language eng
```

**Expected JSON (Success):**

```json
{
  "matched": true,
  "series": "Criminal Minds",
  "season": 5,
  "episode": 10,
  "episodeTitle": "The Slave of Duty",
  "confidence": 85.5,
  "subtitleType": "dvd_subtitle",
  "ocrConfidence": 88.2,
  "processingTimeMs": 12400
}
```

### 3. Test Subtitle Priority

```bash
# File with both text and DVD subtitles - should prefer text
dotnet run --project src/EpisodeIdentifier.Core -- \
  identify \
  --video "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D2-McMIEk/CRIMINAL_MINDS_S5_D2_T01.mkv"
```

**Expected:** Uses text subtitles, NOT DVD subtitles (check `subtitleType` field)

### 4. Test Missing Dependencies

```bash
# Temporarily rename mkvextract to simulate missing tool
sudo mv /usr/bin/mkvextract /usr/bin/mkvextract.bak

dotnet run --project src/EpisodeIdentifier.Core -- \
  identify \
  --video "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv"

# Should return MISSING_DEPENDENCY error
# Restore tool
sudo mv /usr/bin/mkvextract.bak /usr/bin/mkvextract
```

**Expected JSON:**

```json
{
  "matched": false,
  "confidence": 0.0,
  "errorCode": "MISSING_DEPENDENCY",
  "errorMessage": "mkvextract or tesseract not found. Install mkvtoolnix and tesseract-ocr packages."
}
```

## Test Files

### Files with DVD Subtitles Only

```bash
# These should trigger DVD subtitle OCR path
/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv
/mnt/z/mkvs/CRIMINAL_MINDS_S5_D5-plEOaC/CRIMINAL_MINDS_S5_D5_T02.mkv
/mnt/z/mkvs/CRIMINAL_MINDS_S5_D5-plEOaC/CRIMINAL_MINDS_S5_D5_T04.mkv
```

### Files with Text Subtitles

```bash
# These should use fast text subtitle path (baseline for comparison)
/mnt/z/mkvs/CRIMINAL_MINDS_S5_D2-McMIEk/CRIMINAL_MINDS_S5_D2_T01.mkv
```

## Manual Validation Steps

### 1. Extract VobSub Manually

```bash
# Find DVD subtitle track number
ffprobe -v error -show_entries stream=index,codec_name \
  /mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv

# Extract using mkvextract (track index from ffprobe)
mkdir -p /tmp/vobsub_test
mkvextract tracks \
  "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf/CRIMINAL_MINDS_S5_D3_T02.mkv" \
  3:/tmp/vobsub_test/subtitles.idx

# Should create subtitles.idx and subtitles.sub files
ls -lh /tmp/vobsub_test/
```

### 2. Convert VobSub to Images

```bash
# Use ffmpeg to extract subtitle images
ffmpeg -i /tmp/vobsub_test/subtitles.idx \
  -f image2 /tmp/vobsub_test/frame_%04d.png

# Check extracted images
ls -lh /tmp/vobsub_test/*.png | head -10
```

### 3. Test Tesseract OCR

```bash
# OCR a sample image
tesseract /tmp/vobsub_test/frame_0001.png stdout -l eng

# Expected: Text from subtitle image
```

## Performance Expectations

### DVD Subtitle Processing Times

- **Extraction:** ~2-5 seconds per file
- **OCR Processing:** ~8-15 seconds per file (50MB limit)
- **Total:** ~10-20 seconds vs. <1 second for text subtitles

### Bulk Processing Test

```bash
# Process all DVD subtitle files in directory
dotnet run --project src/EpisodeIdentifier.Core -- \
  bulk-identify \
  --directory "/mnt/z/mkvs/CRIMINAL_MINDS_S5_D3-IfFoMf" \
  --ocr-language eng

# Monitor processing times in JSON output
```

**Expected:** Each file processes within 5-minute timeout

## Troubleshooting

### Issue: "MISSING_DEPENDENCY" Error

**Solution:** Install mkvtoolnix and tesseract-ocr:

```bash
sudo apt-get update
sudo apt-get install mkvtoolnix tesseract-ocr tesseract-ocr-eng
```

### Issue: "OCR_FAILED" Error

**Possible Causes:**

1. Subtitle file exceeds 50MB limit
2. OCR processing exceeds 5-minute timeout
3. Language data file missing (e.g., eng.traineddata)

**Solution:**

```bash
# Check subtitle size
ls -lh /tmp/extracted_subtitles/

# Install additional language data
sudo apt-get install tesseract-ocr-eng tesseract-ocr-spa

# List available languages
tesseract --list-langs
```

### Issue: Low OCR Confidence (<70%)

**Solution:** Files with poor OCR results will still match if confidence > matchConfidenceThreshold (50% default)

### Issue: Files Not Renaming

**Checklist:**

1. Match confidence > renameConfidenceThreshold? (Check JSON output)
2. OCR confidence > 70%? (Check ocrConfidence field)
3. File permissions allow rename?

## Configuration

### OCR Settings (in episodeidentifier.config.json)

```json
{
  "matchConfidenceThreshold": 50,
  "renameConfidenceThreshold": 50,
  "maxProcessingTimeMinutes": 5,
  "maxSubtitleSizeMb": 50,
  "ocrLanguage": "eng"
}
```

## Integration Test Checklist

- [ ] DVD subtitle detection works
- [ ] VobSub extraction creates .idx and .sub files
- [ ] OCR extracts readable text from VobSub
- [ ] Episode matching works with OCR text
- [ ] Subtitle priority (text > PGS > DVD) enforced
- [ ] Missing dependency error handling
- [ ] Timeout handling for long OCR operations
- [ ] File size limit enforcement (50MB)
- [ ] JSON error responses for all failure modes

## Next Steps

After completing Phase 2-5 implementation:

1. Run all quick test commands above
2. Verify JSON responses match expected output
3. Validate performance metrics
4. Test with various Criminal Minds episodes
5. Compare OCR confidence scores across files
