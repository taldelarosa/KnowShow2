# Find Missing Episodes

This script identifies missing episodes from your video collection by comparing the database records with actual video files on the filesystem.

## Features

- Queries the database for all known episodes (Series, Season, Episode)
- Scans filesystem for video files matching S##E## pattern
- Reports missing episodes grouped by series
- Generates detailed report file with missing episodes
- Supports filtering by specific series
- Color-coded output for easy reading

## Usage

### Basic Usage
Check all episodes in current directory:
```bash
./scripts/find_missing_episodes.sh
```

### Specify Database and Video Directory
```bash
./scripts/find_missing_episodes.sh production_hashes.db /path/to/videos
```

### Filter by Series
Check only "Criminal Minds" episodes:
```bash
./scripts/find_missing_episodes.sh production_hashes.db /path/to/videos "Criminal Minds"
```

## Parameters

1. **database_path** (optional, default: `production_hashes.db`)
   - Path to the SQLite database containing episode information

2. **video_directory** (optional, default: current directory `.`)
   - Directory to search for video files (searches recursively)

3. **series_filter** (optional, default: all series)
   - Filter results to a specific series name (must match database exactly)

## Output

### Console Output
- Real-time display of missing episodes with color coding
- Summary statistics (total, found, missing)
- Missing percentage calculation

### Report File
If missing episodes are found, a detailed report is saved:
- Filename: `missing_episodes_YYYYMMDD_HHMMSS.txt`
- Format: Episodes grouped by series
- Example:
  ```
  Criminal Minds:
    S01E05
    S02E13
    S06E19
  ```

## Supported Video Formats

The script searches for these video file extensions:
- `.mkv`
- `.mp4`
- `.avi`
- `.m4v`

Search is case-insensitive.

## Pattern Matching

The script looks for the S##E## pattern in filenames:
- Example: `Criminal.Minds.S06E19.With.Friends.Like.These.mkv`
- Pattern extracted: `S06E19`
- The full filename doesn't need to match, only the episode identifier

## Exit Codes

- `0` - All episodes found
- `1` - One or more episodes missing (or error occurred)

## Examples

### Example 1: Check Criminal Minds in specific directory
```bash
./scripts/find_missing_episodes.sh \
  production_hashes.db \
  /mnt/user/Media/TV\ Shows \
  "Criminal Minds"
```

### Example 2: Check all series in current directory
```bash
cd /mnt/user/Media/TV\ Shows
/path/to/KnowShow_Specd/scripts/find_missing_episodes.sh \
  /path/to/production_hashes.db \
  .
```

### Example 3: Generate report for multiple series
```bash
# Run for each series and collect reports
for series in "Criminal Minds" "Bones" "Breaking Bad"; do
  ./scripts/find_missing_episodes.sh \
    production_hashes.db \
    /mnt/user/Media \
    "$series"
done
```

## Troubleshooting

### "Database file not found"
Ensure the database path is correct. Use absolute path if needed:
```bash
./scripts/find_missing_episodes.sh /full/path/to/production_hashes.db
```

### "Video directory not found"
Ensure the video directory path is correct and accessible:
```bash
./scripts/find_missing_episodes.sh production_hashes.db /mnt/user/Media
```

### No episodes found for a series
Check that the series name matches exactly (case-sensitive):
```bash
# List series names in database
sqlite3 production_hashes.db "SELECT DISTINCT Series FROM SubtitleHashes;"
```

### Script runs slowly
- The script searches recursively through all subdirectories
- Consider narrowing the search directory to specific series folders
- For large collections, filter by series to speed up search

## Integration with Re-ripping Workflow

1. **Identify missing episodes**
   ```bash
   ./scripts/find_missing_episodes.sh > missing.log 2>&1
   ```

2. **Review the generated report**
   ```bash
   cat missing_episodes_*.txt
   ```

3. **Locate physical discs**
   - Group missing episodes by season
   - Find corresponding DVD/Blu-ray discs

4. **Re-rip missing episodes**
   - Use your preferred ripping tool (MakeMKV, etc.)
   - Encode to AV1 with PGS subtitles

5. **Verify after re-ripping**
   ```bash
   ./scripts/find_missing_episodes.sh
   ```

## Related Tools

- `import_criminal_minds.sh` - Import subtitle data from files
- `--bulk-store` - Bulk import episodes into database
- `--bulk-identify` - Identify multiple episodes at once
