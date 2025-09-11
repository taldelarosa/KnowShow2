# Quickstart Guide: File Renaming Recommendations

## Overview
Quick validation guide for the file renaming recommendations feature, covering core functionality and integration points.

## Feature Validation Steps

### 1. Filename Suggestion (Core Feature)
```bash
# Test high-confidence episode identification with filename suggestion
dotnet run -- --input test-video.mkv --hash-db test-hashes.db

# Expected JSON response includes suggestedFilename
{
  "series": "Test Show",
  "season": "01",
  "episode": "01", 
  "matchConfidence": 0.95,
  "suggestedFilename": "Test Show - S01E01 - Pilot.mkv",
  "error": null
}
```

### 2. Automatic File Rename
```bash
# Test automatic rename with --rename flag
cp test-video.mkv temp-video.mkv
dotnet run -- --input temp-video.mkv --hash-db test-hashes.db --rename

# Expected: File renamed + confirmation in JSON
{
  "series": "Test Show", 
  "season": "01",
  "episode": "01",
  "matchConfidence": 0.95,
  "suggestedFilename": "Test Show - S01E01 - Pilot.mkv",
  "fileRenamed": true,
  "originalFilename": "temp-video.mkv",
  "error": null
}

# Verify file was actually renamed
ls -la "Test Show - S01E01 - Pilot.mkv"  # Should exist
ls -la temp-video.mkv                    # Should not exist
```

### 3. Low Confidence Behavior
```bash
# Test with low-confidence identification (no filename suggestion)
dotnet run -- --input unclear-video.mkv --hash-db test-hashes.db

# Expected: No suggestedFilename field in response
{
  "series": "Unclear Show",
  "season": "01", 
  "episode": "01",
  "matchConfidence": 0.75,
  "ambiguityNotes": "Multiple possible matches",
  "error": null
  // No suggestedFilename field
}
```

### 4. Windows Character Sanitization
```bash
# Test filename with Windows-invalid characters
# (Mock episode with series name containing invalid chars)
dotnet run -- --input video-with-colons.mkv --hash-db test-hashes.db

# Expected: Sanitized filename
{
  "suggestedFilename": "Show  Name - S01E01 - Episode  Title.mkv"
  // Notice: colons and quotes replaced with spaces
}
```

### 5. Database Schema Validation
```bash
# Check that EpisodeName column was added to database
sqlite3 test-hashes.db ".schema SubtitleHashes"

# Expected output includes EpisodeName column:
# CREATE TABLE SubtitleHashes (
#     Id INTEGER PRIMARY KEY AUTOINCREMENT,
#     Series TEXT NOT NULL,
#     Season TEXT NOT NULL, 
#     Episode TEXT NOT NULL,
#     EpisodeName TEXT NULL,    -- NEW COLUMN
#     OriginalText TEXT NOT NULL,
#     ...
# );
```

## Error Scenario Validation

### 1. File Rename Failure (Target Exists)
```bash
# Create conflicting target filename
touch "Test Show - S01E01 - Pilot.mkv"

# Attempt rename (should fail gracefully)
dotnet run -- --input test-video.mkv --hash-db test-hashes.db --rename

# Expected: Error with suggestion still provided
{
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Target file already exists: Test Show - S01E01 - Pilot.mkv"
  },
  "suggestedFilename": "Test Show - S01E01 - Pilot.mkv", 
  "fileRenamed": false,
  "originalFilename": "test-video.mkv"
}
```

### 2. Permission Denied
```bash
# Test in read-only directory (if possible on test system)
chmod 444 test-video.mkv
dotnet run -- --input test-video.mkv --hash-db test-hashes.db --rename

# Expected: Permission error with suggestion
{
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Permission denied: Cannot rename file"
  },
  "suggestedFilename": "Test Show - S01E01 - Pilot.mkv",
  "fileRenamed": false
}
```

### 3. Very Long Filename Handling
```bash
# Test with very long series/episode names (mock data)
# Expected: Truncated filename that fits Windows 260-char limit
{
  "suggestedFilename": "Very Long Series Name... - S01E01 - Very Long Episode Tit.mkv"
  // Notice truncation to fit limit
}
```

## Integration Testing Checklist

### ✅ CLI Integration
- [ ] New --rename flag recognized by argument parser
- [ ] Flag works in combination with existing parameters
- [ ] Help text updated to include --rename description
- [ ] Error handling maintains existing CLI patterns

### ✅ JSON Response Integration  
- [ ] suggestedFilename field added to IdentificationResult
- [ ] Field only present for high-confidence results (≥90%)
- [ ] fileRenamed and originalFilename fields work with --rename flag
- [ ] Backward compatibility maintained (existing clients unaffected)

### ✅ Database Integration
- [ ] EpisodeName column added to SubtitleHashes table
- [ ] Migration runs automatically on first startup
- [ ] Existing data unaffected by schema change
- [ ] Episode names retrieved and used in filename generation

### ✅ Service Integration
- [ ] FilenameService generates proper Windows-compatible names
- [ ] FileRenameService performs safe atomic rename operations
- [ ] Error handling integrated with existing error response patterns
- [ ] Logging includes filename operations for debugging

## Performance Validation

### Response Time Testing
```bash
# Test filename generation performance
time dotnet run -- --input test-video.mkv --hash-db test-hashes.db

# Expected: Total response time increase <50ms
# Filename generation should add <10ms to overall process
```

### Memory Usage Testing
```bash
# Monitor memory during filename operations
dotnet run -- --input test-video.mkv --hash-db test-hashes.db --rename

# Expected: No significant memory increase
# File rename should not load file content into memory
```

### Concurrent Operation Testing
```bash
# Test multiple simultaneous operations
for i in {1..5}; do
  cp test-video.mkv "test-video-$i.mkv"
  dotnet run -- --input "test-video-$i.mkv" --hash-db test-hashes.db --rename &
done
wait

# Expected: All operations complete successfully
# No race conditions or file conflicts
```

## Manual Testing Scenarios

### Test Data Setup
```bash
# Create test video files with known characteristics
cp sample.mkv "Breaking Bad - S01E05.mkv"        # Known episode
cp sample.mkv "unclear-episode.mkv"              # Unknown episode  
cp sample.mkv "Show: With Colons - Episode.mkv"  # Invalid chars
cp sample.mkv "very-long-series-name-that-exceeds-normal-limits-for-testing-truncation.mkv"

# Populate test database with known episodes
dotnet run -- --input "Breaking Bad - S01E05.mkv" --hash-db test-hashes.db --store \
  --series "Breaking Bad" --season "01" --episode "05" --episode-name "Gray Matter"
```

### User Workflow Testing
```bash
# Scenario 1: User wants filename suggestion only
dotnet run -- --input unclear-filename.mkv --hash-db test-hashes.db
# → Review suggested filename, manually rename if desired

# Scenario 2: User wants automatic rename
dotnet run -- --input unclear-filename.mkv --hash-db test-hashes.db --rename  
# → File automatically renamed, user confirms result

# Scenario 3: Batch processing workflow
for file in *.mkv; do
  echo "Processing: $file"
  dotnet run -- --input "$file" --hash-db test-hashes.db --rename
done
# → All identifiable files automatically renamed
```

## Validation Success Criteria

### ✅ Core Functionality
- [x] High-confidence episodes generate suggested filenames
- [x] Low-confidence episodes do not generate suggestions
- [x] --rename flag performs automatic file renaming
- [x] Error scenarios handled gracefully with informative messages

### ✅ Data Quality  
- [x] Filenames follow "SeriesName - S##E## - EpisodeName.ext" format
- [x] Windows invalid characters properly sanitized
- [x] Filename length respects 260-character limit
- [x] File extensions preserved correctly

### ✅ System Integration
- [x] Database schema migration successful
- [x] Existing CLI functionality unaffected  
- [x] JSON response format backward compatible
- [x] Error handling consistent with existing patterns

### ✅ Robustness
- [x] File operation errors handled safely
- [x] Original files preserved on any failure
- [x] Performance impact minimal (<50ms total)
- [x] Concurrent operations work correctly

## Rollback Procedures

### Feature Disable
```bash
# Feature can be disabled by not using new parameters
dotnet run -- --input video.mkv --hash-db hashes.db
# → Works exactly as before, no suggestedFilename field

# Database rollback (if needed)
sqlite3 hashes.db "ALTER TABLE SubtitleHashes DROP COLUMN EpisodeName;"
# → Removes new column, restores original schema
```

### Emergency Recovery
```bash
# If rename operations cause issues, original functionality preserved
# Users can continue using system without --rename flag
# No data loss possible (rename operations are atomic)
```
