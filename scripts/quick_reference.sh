#!/bin/bash
# Quick Reference Card for find_missing_episodes.sh

cat << 'EOF'
┌──────────────────────────────────────────────────────────────────┐
│          FIND MISSING EPISODES - QUICK REFERENCE                 │
└──────────────────────────────────────────────────────────────────┘

BASIC USAGE:
  ./scripts/find_missing_episodes.sh [database] [video_dir] [series]

COMMON SCENARIOS:

  1. Check all series in current directory:
     $ ./scripts/find_missing_episodes.sh

  2. Check specific directory:
     $ ./scripts/find_missing_episodes.sh production_hashes.db /mnt/user/Media

  3. Check one series only:
     $ ./scripts/find_missing_episodes.sh production_hashes.db . "Criminal Minds"

  4. Scan Unraid media share:
     $ ./scripts/find_missing_episodes.sh \
         /mnt/c/Users/Ragma/KnowShow_Specd/production_hashes.db \
         /mnt/user/Media/TV\ Shows

OUTPUT:
  - Console: Color-coded missing episodes list
  - File: missing_episodes_YYYYMMDD_HHMMSS.txt (if any found)

WHAT IT DOES:
  ✓ Queries database for all known episodes
  ✓ Searches filesystem for S##E## pattern in video files
  ✓ Reports episodes that exist in DB but not on disk
  ✓ Groups missing episodes by series
  ✓ Calculates missing percentage

SUPPORTED VIDEO FORMATS:
  .mkv, .mp4, .avi, .m4v (case-insensitive)

EXIT CODES:
  0 = All episodes found
  1 = Missing episodes detected (or error)

WORKFLOW:
  1. Run script to find missing episodes
  2. Review generated report file
  3. Locate corresponding DVD/Blu-ray discs
  4. Re-rip missing episodes
  5. Run script again to verify

LIST SERIES IN DATABASE:
  $ sqlite3 production_hashes.db \
    "SELECT DISTINCT Series FROM SubtitleHashes ORDER BY Series;"

COUNT EPISODES PER SERIES:
  $ sqlite3 production_hashes.db \
    "SELECT Series, COUNT(*) as Episodes FROM SubtitleHashes \
     GROUP BY Series ORDER BY Series;"

EXAMPLE REPORT OUTPUT:
  === MISSING EPISODES BY SERIES ===
  
  Criminal Minds:
    S01E05
    S02E13
    S06E19
  
  Bones:
    S03E08

TIP: Use series filter for faster searching in large collections!

EOF
