# Quickstart: NonPGS Subtitle Workflow

## Overview

This quickstart validates the nonPGS subtitle workflow by testing text-based subtitle processing when PGS subtitles are not available.

## Prerequisites

- .NET 8.0 SDK
- FFmpeg installed and in PATH
- Sample video files with text subtitles
- Existing episode database with fuzzy hashes

## Test Data Setup

### Prepare Test Videos

```bash
# Create test directory
mkdir -p tests/data/nonpgs-workflow

# Copy sample videos with different subtitle configurations:
# 1. Video with SRT subtitles only
cp sample_episode_with_srt.mkv tests/data/nonpgs-workflow/
# 2. Video with ASS subtitles only  
cp sample_episode_with_ass.mkv tests/data/nonpgs-workflow/
# 3. Video with multiple text subtitle tracks
cp sample_episode_multi_subs.mkv tests/data/nonpgs-workflow/
# 4. Video with both PGS and text subtitles (should use PGS)
cp sample_episode_mixed_subs.mkv tests/data/nonpgs-workflow/
```

### Verify Video Contents

```bash
# Check subtitle tracks in test videos
ffprobe -v quiet -print_format json -show_streams tests/data/nonpgs-workflow/sample_episode_with_srt.mkv | jq '.streams[] | select(.codec_type=="subtitle")'
```

## Validation Steps

### Step 1: Build and Test Setup

```bash
# Build the project
dotnet build

# Run existing tests to ensure no regressions
dotnet test --filter "Category!=Integration"
```

### Step 2: Text Subtitle Detection

```bash
# Test subtitle track detection
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --detect-subtitles tests/data/nonpgs-workflow/sample_episode_with_srt.mkv

# Expected output:
# Found 1 text subtitle track(s):
# Track 0: SRT format, Language: en, Default: true
```

### Step 3: Text Subtitle Extraction

```bash
# Test SRT extraction
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --extract-text-subtitles tests/data/nonpgs-workflow/sample_episode_with_srt.mkv \
  --track-index 0

# Expected output:
# Extracted 245 lines of dialogue text
# Format: SRT, Encoding: UTF-8
# Duration: 00:42:15
```

### Step 4: Full Workflow Test (SRT)

```bash
# Test complete identification workflow with SRT
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --identify tests/data/nonpgs-workflow/sample_episode_with_srt.mkv \
  --enable-text-subtitles

# Expected output (if match found):
# Episode identified: Series Name S01E05
# Source: Text subtitles (SRT format)
# Confidence: 85.2%
# Processing time: 4.3 seconds
```

### Step 5: Full Workflow Test (ASS)

```bash
# Test complete identification workflow with ASS
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --identify tests/data/nonpgs-workflow/sample_episode_with_ass.mkv \
  --enable-text-subtitles

# Expected output (if match found):
# Episode identified: Anime Series S01E12
# Source: Text subtitles (ASS format)
# Confidence: 92.1%
# Processing time: 3.8 seconds
```

### Step 6: Multi-Track Processing

```bash
# Test sequential track processing
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --identify tests/data/nonpgs-workflow/sample_episode_multi_subs.mkv \
  --enable-text-subtitles \
  --verbose

# Expected output:
# Processing track 0 (SRT, en): No match
# Processing track 1 (ASS, ja): No match  
# Processing track 2 (SRT, es): Match found!
# Episode identified: Drama Series S02E03
# Source: Text subtitles (SRT format, track 2)
```

### Step 7: PGS Priority Test

```bash
# Verify PGS subtitles still take priority
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --identify tests/data/nonpgs-workflow/sample_episode_mixed_subs.mkv \
  --enable-text-subtitles

# Expected output:
# Episode identified: Mixed Series S01E08
# Source: PGS subtitles (primary method)
# Confidence: 88.7%
# Note: Text subtitles available but not processed
```

### Step 8: No Match Scenario

```bash
# Test when no matches are found
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --identify tests/data/nonpgs-workflow/unknown_episode.mkv \
  --enable-text-subtitles

# Expected output:
# No episode match found
# Processed 2 text subtitle tracks
# Formats attempted: SRT, VTT
# Total processing time: 6.1 seconds
```

## Success Criteria

### Functional Validation

- ✅ Text subtitle tracks are correctly detected
- ✅ SRT, ASS, and VTT formats are successfully parsed
- ✅ Extracted text is clean (no timestamps, minimal formatting)
- ✅ Sequential track processing stops on first match
- ✅ PGS subtitles maintain priority when available
- ✅ Clear indication of subtitle source in results

### Performance Validation

- ✅ Text subtitle extraction completes within 10 seconds per track
- ✅ No memory leaks during large subtitle file processing
- ✅ Graceful handling of encoding issues and corrupted data

### Integration Validation  

- ✅ Existing PGS workflow remains unchanged
- ✅ Database fuzzy hash matching works with text content
- ✅ All existing tests continue to pass
- ✅ CLI interface maintains backward compatibility

## Troubleshooting

### Common Issues

1. **FFmpeg not found**: Ensure FFmpeg is installed and in system PATH
2. **No subtitle tracks detected**: Verify video contains text subtitles with `ffprobe`
3. **Encoding errors**: Check subtitle file encoding, may need manual encoding specification
4. **Performance issues**: Large subtitle files may require chunked processing

### Debug Commands

```bash
# Verbose logging
export EPISODEIDENTIFIER_LOG_LEVEL=Debug

# Test specific format handler
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --test-format-handler SRT subtitle_file.srt

# Validate subtitle content
./src/EpisodeIdentifier.Core/bin/Debug/net8.0/EpisodeIdentifier.Core \
  --validate-subtitle-content extracted_text.txt
```
