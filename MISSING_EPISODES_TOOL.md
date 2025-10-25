# Missing Episode Detection Tool

**Created**: October 23, 2025  
**Purpose**: Identify missing episodes from video collection to determine which discs need re-ripping

## Overview

The `find_missing_episodes.sh` script cross-references your database (which contains all known episodes from subtitle files) with your actual video file collection to identify gaps.

## What It Does

1. **Queries Database**: Extracts all Series/Season/Episode records from `SubtitleHashes` table
2. **Scans Filesystem**: Recursively searches for video files matching the S##E## pattern
3. **Reports Gaps**: Lists episodes that exist in the database but not in your collection
4. **Generates Report**: Creates timestamped report file with missing episodes grouped by series

## Key Features

- ✅ **Pattern-Based Matching**: Only checks S##E## format, ignores episode titles
- ✅ **Recursive Search**: Scans all subdirectories automatically
- ✅ **Series Filtering**: Can limit search to specific series for speed
- ✅ **Color-Coded Output**: Easy visual identification of missing episodes
- ✅ **Detailed Reporting**: Saves timestamped report for reference
- ✅ **Multiple Formats**: Supports .mkv, .mp4, .avi, .m4v files

## Files Created

```
scripts/
├── find_missing_episodes.sh    # Main script
├── FIND_MISSING_EPISODES.md    # Detailed documentation
├── quick_reference.sh          # Quick reference guide
└── example_usage.sh            # Example commands
```

## Quick Start

### Check All Series
```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
./scripts/find_missing_episodes.sh production_hashes.db /path/to/videos
```

### Check Specific Series
```bash
./scripts/find_missing_episodes.sh \
  production_hashes.db \
  /mnt/user/Media \
  "Criminal Minds"
```

### View Quick Reference
```bash
./scripts/quick_reference.sh
```

## Example Output

```
=== MISSING EPISODE FINDER ===
Database: production_hashes.db
Video Directory: /mnt/user/Media
Filtering by series: Criminal Minds

=== Criminal Minds ===
  MISSING: Criminal Minds S01E05
  MISSING: Criminal Minds S02E13
  MISSING: Criminal Minds S06E19

=== SUMMARY ===
Total episodes in database: 324
Episodes found in filesystem: 321
Episodes MISSING from filesystem: 3
Missing percentage: 0.9%

=== MISSING EPISODES BY SERIES ===

Criminal Minds:
  S01E05
  S02E13
  S06E19

Detailed report saved to: missing_episodes_20251023_143052.txt
```

## Use Cases

### 1. Pre-Ripping Audit
Before starting a ripping session, identify all missing episodes:
```bash
./scripts/find_missing_episodes.sh > missing_audit.log
```

### 2. Series-Specific Check
When you have specific DVD sets available:
```bash
./scripts/find_missing_episodes.sh production_hashes.db . "Bones"
./scripts/find_missing_episodes.sh production_hashes.db . "Breaking Bad"
```

### 3. Post-Ripping Verification
After re-ripping episodes, verify completeness:
```bash
# Before ripping
./scripts/find_missing_episodes.sh > before.log

# ... rip missing episodes ...

# After ripping
./scripts/find_missing_episodes.sh > after.log
diff before.log after.log
```

### 4. Collection Inventory
Generate comprehensive inventory of what you have vs. what you know exists:
```bash
for series in $(sqlite3 production_hashes.db "SELECT DISTINCT Series FROM SubtitleHashes;"); do
  echo "Checking: $series"
  ./scripts/find_missing_episodes.sh production_hashes.db /mnt/user/Media "$series"
done
```

## Integration with Existing Workflow

This tool complements your existing KnowShow pipeline:

```
┌─────────────────────────────────────────────────────────────┐
│ WORKFLOW                                                    │
├─────────────────────────────────────────────────────────────┤
│ 1. Import subtitles    → bulk-store (database populated)    │
│ 2. Check missing       → find_missing_episodes.sh           │
│ 3. Locate discs        → (manual: find DVD/Blu-ray)         │
│ 4. Rip missing eps     → (manual: MakeMKV, etc.)            │
│ 5. Verify completeness → find_missing_episodes.sh           │
│ 6. Identify episodes   → bulk-identify                      │
└─────────────────────────────────────────────────────────────┘
```

## Performance Considerations

- **Large Collections**: Use series filter to narrow search scope
- **Network Shares**: Searching network storage may be slower
- **Concurrent Usage**: Safe to run while database is in use (read-only)

## Troubleshooting

### False Positives
If episodes are reported missing but you know they exist:
1. Check filename contains S##E## pattern (case-insensitive)
2. Verify file extension is supported (.mkv, .mp4, .avi, .m4v)
3. Ensure files are in the specified search directory

### Series Name Mismatch
Database series names must match exactly:
```bash
# List actual series names in database
sqlite3 production_hashes.db \
  "SELECT DISTINCT Series FROM SubtitleHashes ORDER BY Series;"
```

### Slow Performance
- Narrow search directory to specific series folders
- Use series filter parameter
- Exclude unrelated directories from search path

## Related Tools

- `import_criminal_minds.sh` - Import subtitle data
- `--bulk-store` - Bulk import episodes
- `--bulk-identify` - Identify multiple episodes
- `migrate_database.sh` - Database schema updates

## Technical Details

### Database Query
```sql
SELECT DISTINCT Series, Season, Episode 
FROM SubtitleHashes 
WHERE Series = ? 
ORDER BY Series, CAST(Season AS INTEGER), CAST(Episode AS INTEGER);
```

### File Search
```bash
find "$VIDEO_DIR" -type f -iname "*S##E##*" \
  \( -iname "*.mkv" -o -iname "*.mp4" -o -iname "*.avi" -o -iname "*.m4v" \)
```

### Pattern Format
- Season: Zero-padded 2 digits (S01, S02, ... S15)
- Episode: Zero-padded 2 digits (E01, E02, ... E24)
- Combined: S##E## (e.g., S06E19)

## Exit Codes

- `0` - All episodes found in filesystem
- `1` - One or more episodes missing (or error occurred)

Use in scripts:
```bash
if ./scripts/find_missing_episodes.sh; then
  echo "Collection complete!"
else
  echo "Missing episodes detected - check report"
fi
```

## Future Enhancements

Potential improvements:
- [ ] JSON output format for programmatic consumption
- [ ] Integration with bulk-identify for automated workflows
- [ ] Support for episode ranges (S01E01-E05)
- [ ] Duplicate detection across different quality versions
- [ ] Export to CSV for spreadsheet analysis
