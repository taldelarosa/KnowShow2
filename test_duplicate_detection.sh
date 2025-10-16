#!/bin/bash
# Test hash-based duplicate detection with Criminal Minds S01E01 variants

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

TEST_DB="test_duplicate_detection.db"
APP_DLL="./src/EpisodeIdentifier.Core/bin/Release/net8.0/EpisodeIdentifier.Core.dll"
SUBTITLE_DIR="/mnt/void_subtitles/Criminal Minds/Season 1/Episode 1"

echo "=========================================="
echo "Hash-Based Duplicate Detection Test"
echo "=========================================="
echo "Test Directory: $SUBTITLE_DIR"
echo "Test Database: $TEST_DB"
echo "=========================================="
echo ""

# Remove old test database
rm -f "$TEST_DB" "$TEST_DB-shm" "$TEST_DB-wal"

# Test 1: Import HI (Hearing Impaired) version
echo "Test 1: Importing HI (Hearing Impaired) subtitle..."
HI_FILE=$(ls "$SUBTITLE_DIR"/*HI*.srt | head -1)
echo "  File: $(basename "$HI_FILE")"

dotnet "$APP_DLL" \
    --bulk-store "$SUBTITLE_DIR" \
    --hash-db "$TEST_DB" \
    2>&1 | grep -E "Found|Parsing|Processing|Successfully stored|duplicate|variant"

echo ""
echo "=========================================="
echo "Checking database..."
sqlite3 "$TEST_DB" "SELECT Id, Series, Season, Episode, 
    substr(OriginalHash, 1, 20) || '...' as HashPreview,
    substr(EpisodeName, 1, 30) as EpisodeName
FROM SubtitleHashes;"

echo ""
echo "Total records: $(sqlite3 "$TEST_DB" "SELECT COUNT(*) FROM SubtitleHashes;")"
echo ""
echo "=========================================="
echo "✓ Test complete!"
echo ""
echo "Expected results:"
echo "  - Should have imported 2 files"
echo "  - Both should be stored (different hashes)"
echo "  - One log line should say 'variant subtitle'"
echo "=========================================="
