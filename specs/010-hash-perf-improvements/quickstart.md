# Quickstart Guide: Hash Performance Improvements with Series/Season Filtering


**Date**: October 7, 2025
**Feature**: 010-hash-perf-improvements
**Purpose**: Quick validation and testing guide for filtered hash searching

## Prerequisites


- .NET 8.0 SDK installed
- EpisodeIdentifier.Core project built
- Test database with multiple series and seasons
- Test video files for identification

## Setup Test Database


### Create Multi-Series Test Database


```bash

# Navigate to project directory

cd /mnt/c/Users/Ragma/KnowShow2-charlie/src/EpisodeIdentifier.Core

# Use existing production database (has 246 Bones episodes)

DB_PATH="../../production_hashes.db"

# Or create fresh test database

TEST_DB="../../test_filtered_search.db"
rm -f "$TEST_DB"

# Import subtitles from multiple series


# (Assumes you have subtitle files organized by series)

dotnet run -- import --input "../../subtitles/Bones/" --hash-db "$TEST_DB" --recursive
dotnet run -- import --input "../../subtitles/BreakingBad/" --hash-db "$TEST_DB" --recursive
dotnet run -- import --input "../../subtitles/TheOffice/" --hash-db "$TEST_DB" --recursive
```


### Verify Database Contents


```bash
sqlite3 "$TEST_DB" "SELECT Series, COUNT(*) as Episodes FROM SubtitleHashes GROUP BY Series;"
```


**Expected Output**:

```
Bones|246
Breaking Bad|62
The Office|201
```


## Feature Validation Steps


### 1. No Filtering (Baseline Behavior)


```bash

# Test existing functionality still works

dotnet run -- identify \
  --input test_video.mkv \
  --hash-db "$TEST_DB" \
  --verbose
```


**Expected**:

- Searches all 509 episodes (246 + 62 + 201)
- Returns best match across all series
- Log shows: "scanned 509 records"

### 2. Series Filtering Only


```bash

# Filter to Bones series only

dotnet run -- identify \
  --input bones_video.mkv \
  --hash-db "$TEST_DB" \
  --series "Bones" \
  --verbose
```


**Expected**:

- Searches only 246 Bones episodes
- Returns best match from Bones series
- Log shows: "Search filter applied: Series='Bones', Season=null"
- Log shows: "scanned 246 records"
- Faster execution time than baseline

**Success Criteria**:

- JSON output has `"series": "Bones"`
- Execution time < baseline time
- No episodes from other series in comparisons

### 3. Series + Season Filtering (Maximum Performance)


```bash

# Filter to Bones Season 2 only

dotnet run -- identify \
  --input bones_s02_video.mkv \
  --hash-db "$TEST_DB" \
  --series "Bones" \
  --season 2 \
  --verbose
```


**Expected**:

- Searches only ~20 Bones Season 2 episodes
- Returns best match from Bones S02
- Log shows: "Search filter applied: Series='Bones', Season=2"
- Log shows: "scanned 20 records" (approximate)
- Significantly faster than both baseline and series-only filter

**Success Criteria**:

- JSON output has `"series": "Bones"` and `"season": "02"`
- Execution time << series-only time << baseline time
- ~92% reduction in records scanned compared to baseline

### 4. Case-Insensitive Series Matching


```bash

# Test with different case variations

dotnet run -- identify --input bones_video.mkv --hash-db "$TEST_DB" --series "BONES"
dotnet run -- identify --input bones_video.mkv --hash-db "$TEST_DB" --series "bones"
dotnet run -- identify --input bones_video.mkv --hash-db "$TEST_DB" --series "Bones"
dotnet run -- identify --input bones_video.mkv --hash-db "$TEST_DB" --series "BoNeS"
```


**Expected**:

- All four commands return identical results
- Case variations properly matched to database Series column

**Success Criteria**:

- All JSON outputs match exactly
- All confidence scores identical
- Log shows same number of records scanned

### 5. Error Handling - Season Without Series


```bash

# Attempt to filter by season only (should fail)

dotnet run -- identify \
  --input video.mkv \
  --hash-db "$TEST_DB" \
  --season 2
```


**Expected Output**:

```
Error: Season parameter requires series parameter
```


**Expected Exit Code**: 1

**Success Criteria**:

- Clear error message displayed
- Non-zero exit code
- No database query executed
- No partial results returned

### 6. Non-Existent Series Handling


```bash

# Search for series not in database

dotnet run -- identify \
  --input video.mkv \
  --hash-db "$TEST_DB" \
  --series "NonExistentShow" \
  --verbose
```


**Expected Output**:

```json
{
  "series": null,
  "season": null,
  "episode": null,
  "matchConfidence": 0,
  "error": "No matching episodes found in database"
}
```


**Expected Exit Code**: 0 (not an error, just no matches)

**Success Criteria**:

- Log shows: "scanned 0 records"
- Empty result set, not exception
- Exit code 0 (normal operation)

### 7. Performance Measurement


```bash

# Measure performance difference

time dotnet run -- identify --input bones_s02_video.mkv --hash-db "$TEST_DB"
time dotnet run -- identify --input bones_s02_video.mkv --hash-db "$TEST_DB" --series "Bones"
time dotnet run -- identify --input bones_s02_video.mkv --hash-db "$TEST_DB" --series "Bones" --season 2
```


**Expected Results**:

- Filtered queries demonstrably faster
- Series+Season filter fastest of all
- Log timestamps show measurable improvement

**Success Criteria (approximate for 500-episode database)**:

- No filter: ~1000ms
- Series filter: ~500ms (50% improvement)
- Series+Season filter: ~100ms (90% improvement)

## Integration Testing


### Test Scenario 1: Real-World Workflow


```bash

# User knows video is from Bones Season 2


# Use filtering for fast identification


dotnet run -- identify \
  --input "video_files/unknown_bones_episode.mkv" \
  --hash-db "$TEST_DB" \
  --series "Bones" \
  --season 2 \
  --rename \
  --verbose
```


**Expected Workflow**:

1. Rapid identification using filtered search (~100ms)
2. High confidence match found
3. File renamed to "Bones - S02E13 - The Girl in the Gator.mkv"
4. Operation completes much faster than full database search

### Test Scenario 2: Uncertainty Fallback


```bash

# User not sure which series, omit filters


# System falls back to full database search


dotnet run -- identify \
  --input "video_files/completely_unknown.mkv" \
  --hash-db "$TEST_DB" \
  --verbose
```


**Expected Workflow**:

1. Full database search (baseline performance)
2. Best match found across all series
3. System works exactly as it did before feature addition
4. Backwards compatibility maintained

## Automated Test Execution


### Run Contract Tests


```bash
cd /mnt/c/Users/Ragma/KnowShow2-charlie/tests/contract

# Run contract tests for filtered searching

dotnet test --filter "FullyQualifiedName~FilteredHashSearch"
```


**Expected**:

- All contract tests pass
- Backwards compatibility tests pass
- Parameter validation tests pass
- Case-insensitive matching tests pass

### Run Integration Tests


```bash
cd /mnt/c/Users/Ragma/KnowShow2-charlie/tests/integration

# Run integration tests

dotnet test --filter "FullyQualifiedName~SeriesSeasonFiltering"
```


**Expected**:

- Multi-series database tests pass
- Performance measurement tests pass
- Real database filtering tests pass

## Performance Benchmarks


### Benchmark Test Script


```bash
#!/bin/bash

# performance_benchmark.sh


DB="../../production_hashes.db"
VIDEO="../../test_video.mkv"

echo "Running performance benchmarks..."

echo "1. No filter (baseline):"
time dotnet run -- identify --input "$VIDEO" --hash-db "$DB" 2>&1 | grep "Search completed"

echo "2. Series filter:"
time dotnet run -- identify --input "$VIDEO" --hash-db "$DB" --series "Bones" 2>&1 | grep "Search completed"

echo "3. Series + Season filter:"
time dotnet run -- identify --input "$VIDEO" --hash-db "$DB" --series "Bones" --season 2 2>&1 | grep "Search completed"
```


**Expected Output**:

```
Running performance benchmarks...

1. No filter (baseline):
Search completed: 850ms, scanned 246 records, found 1 matches

real    0m0.950s

2. Series filter:
Search completed: 850ms, scanned 246 records, found 1 matches

real    0m0.950s

3. Series + Season filter:
Search completed: 85ms, scanned 20 records, found 1 matches

real    0m0.185s
```


*(Note: For Bones-only database, series filter shows no improvement since all records are Bones)*

## Rollback Procedures


### Feature Disable


```bash

# Feature can be disabled by not using new parameters


# System works exactly as before when --series/--season omitted


dotnet run -- identify --input video.mkv --hash-db hashes.db

# → Works exactly as before feature addition

```


### Code Rollback


```bash

# Revert to previous commit

git revert <feature-commit-hash>

# Or checkout previous version

git checkout <previous-branch>

# No database migrations needed - feature is purely additive

```


## Troubleshooting


### Issue: "Season parameter requires series parameter"


**Cause**: Attempted to use --season without --series

**Solution**:

```bash

# Wrong:

dotnet run -- identify --input video.mkv --hash-db hashes.db --season 2

# Correct:

dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones" --season 2
```


### Issue: No performance improvement observed


**Possible Causes**:

1. Database has only one series (filter doesn't reduce records)
2. Testing with small database (overhead dominates)
3. Season spans entire series (no filtering effect)

**Verification**:

```bash

# Check series distribution

sqlite3 hashes.db "SELECT Series, Season, COUNT(*) FROM SubtitleHashes GROUP BY Series, Season;"

# Ensure multiple series and seasons present

```


### Issue: Case-sensitive matching not working


**Cause**: Database has inconsistent Series name casing

**Solution**:

```bash

# Check actual Series values in database

sqlite3 hashes.db "SELECT DISTINCT Series FROM SubtitleHashes;"

# Use exact casing from database, system will handle case-insensitivity

```


## Success Criteria Summary


✅ **Backwards Compatibility**: All existing commands work unchanged
✅ **Series Filtering**: Reduces records scanned when multiple series present
✅ **Season Filtering**: Dramatically reduces records scanned (~80-95%)
✅ **Case-Insensitive**: Series names matched regardless of case
✅ **Error Handling**: Clear error messages for invalid parameter combinations
✅ **Performance**: Measurable speed improvements with filtering
✅ **No Breaking Changes**: No schema changes, no config changes, no API changes

---

*Quickstart guide ready for feature validation and testing*
