# Test Data for NonPGS Subtitle Workflow

This directory contains test video files with various subtitle configurations for validating the text subtitle processing workflow.

## Required Test Files

### 1. sample_episode_with_srt.mkv

- Video file containing only SRT subtitle track
- Language: English
- Duration: ~45 minutes
- Should match an entry in the episode database

### 2. sample_episode_with_ass.mkv  

- Video file containing only ASS/SSA subtitle track
- Language: Japanese with English translation
- Duration: ~24 minutes (anime episode)
- Should match an entry in the episode database

### 3. sample_episode_with_vtt.mkv

- Video file containing WebVTT subtitle track
- Language: Spanish
- Duration: ~60 minutes
- Should match an entry in the episode database

### 4. sample_episode_multi_subs.mkv

- Video file with multiple text subtitle tracks:
  - Track 0: SRT (English) - no match
  - Track 1: ASS (Japanese) - no match  
  - Track 2: SRT (Spanish) - should match
- Test sequential processing until match found

### 5. sample_episode_mixed_subs.mkv

- Video file with both PGS and text subtitles
- Should prioritize PGS processing
- PGS track should match database entry

### 6. unknown_episode.mkv

- Video file with text subtitles but no database match
- Test "no match found" scenario
- Multiple subtitle tracks for exhaustive processing test

## Setup Instructions

1. Obtain sample video files with the required subtitle configurations
2. Place files in this directory with exact filenames above
3. Verify subtitle tracks using: `ffprobe -v quiet -show_streams filename.mkv`
4. Ensure at least one file matches existing database entries for positive tests

## Validation Commands

```bash
# Check subtitle tracks
for file in *.mkv; do
    echo "=== $file ==="
    ffprobe -v quiet -print_format json -show_streams "$file" | jq '.streams[] | select(.codec_type=="subtitle") | {index, codec_name, tags}'
done

# Test file accessibility
ls -la *.mkv
```

Note: Actual video files are not included in git repository due to size constraints.
