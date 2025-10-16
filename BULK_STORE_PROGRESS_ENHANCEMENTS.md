# Bulk-Store Progress Logging Enhancements


## Summary


Enhanced the `--bulk-store` command with real-time progress reporting, timing information, and console flushing to ensure reliable visibility during subtitle imports.

## Changes Made


### 1. Real-Time Progress Updates


- Added `Console.Error.Flush()` after each progress message to ensure immediate output
- No output buffering delays - progress appears instantly

### 2. Detailed Timing Metrics

Each file import now shows:

- **Read time**: How long it took to read the subtitle file from disk
- **Store time**: How long it took to hash and store in the database
- **Total time**: Total processing time for the file

Example output:

```
[1/30] Processing: Criminal Minds S01E01 Extreme Aggressor.srt
           Series: Criminal Minds, S01E01
           ✓ Successfully stored (read: 45ms, store: 123ms, total: 168ms)
```


### 3. Enhanced Summary Statistics

Final summary now includes:

- Total processing time
- Average time per file
- Success/failure breakdown

Example:

```
============================================================
Bulk Import Summary:
  Total Files:  30
  Successful:   30
  Failed:       0
  Total Time:   5.42s
  Avg per file: 181ms
============================================================
```


### 4. Error Reporting with Timing

Failed imports show how long the attempt took before failure:

```
[5/30] Processing: problematic_file.srt
           Series: Criminal Minds, S01E05
           ✗ Failed after 234ms: Invalid subtitle format
```


## Benefits


1. **Identify Slow Operations**: Timing data helps identify if file I/O or database operations are the bottleneck
2. **Real-Time Feedback**: Console flushing ensures users see progress immediately
3. **Progress Tracking**: Clear `[current/total]` counters show exactly where in the process you are
4. **Performance Monitoring**: Average times help establish baselines for expected performance

## Usage


### Using the Convenience Script


```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
./import_criminal_minds.sh
```


The script will:

- Validate paths and dependencies
- Run the import with real-time progress
- Save a timestamped log file
- Display database summary when complete

### Direct Command


```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
dotnet src/EpisodeIdentifier.Core/bin/Release/net8.0/EpisodeIdentifier.Core.dll \
    --bulk-store "/mnt/void_subtitles/Criminal Minds" \
    --hash-db production_hashes.db
```


## Checking Results


Use the provided SQL script to verify imports:

```bash
sqlite3 production_hashes.db < check_criminal_minds_import.sql
```


Or quick check:

```bash
sqlite3 production_hashes.db "SELECT Series, COUNT(*) FROM SubtitleHashes WHERE Series='Criminal Minds';"
```


## Technical Details


### No Multithreading in bulk-store

The `--bulk-store` operation is completely sequential:

- Uses a simple `foreach` loop
- Processes one file at a time
- No parallel operations or threading

This means:

- Progress is always accurate and predictable
- No race conditions or thread safety issues
- Timing measurements are precise

### Potential Performance Bottlenecks


1. **File I/O**: Reading large subtitle files from network shares
2. **SQLite Operations**: Hash generation and database writes
3. **Network Latency**: Accessing files over SMB/CIFS mounts

The new timing metrics will help identify which of these is causing slowdowns.

## Files Modified


- `src/EpisodeIdentifier.Core/Program.cs` - Enhanced bulk-store with timing and flushing
- `check_criminal_minds_import.sql` - Database verification script
- `import_criminal_minds.sh` - Convenience script for running imports

## Next Steps


1. Run the import: `./import_criminal_minds.sh`
2. Review timing metrics to identify any performance issues
3. Check database to verify all subtitles were imported
4. Proceed with `--bulk-identify` testing on video files
