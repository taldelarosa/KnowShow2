#!/bin/bash
# Import Criminal Minds subtitles into the hash database
# This script provides real-time progress with timing information

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Paths
SUBTITLE_DIR="/mnt/void_subtitles/subtitles/Criminal Minds"
APP_DLL="./src/EpisodeIdentifier.Core/bin/Release/net8.0/EpisodeIdentifier.Core.dll"
HASH_DB="./production_hashes.db"
LOG_FILE="./criminal_minds_import_$(date +%Y%m%d_%H%M%S).log"

echo "=========================================="
echo "Criminal Minds Subtitle Import"
echo "=========================================="
echo "Subtitle Directory: $SUBTITLE_DIR"
echo "Database: $HASH_DB"
echo "Log File: $LOG_FILE"
echo "=========================================="
echo ""

# Check if subtitle directory exists
if [ ! -d "$SUBTITLE_DIR" ]; then
    echo "ERROR: Subtitle directory not found: $SUBTITLE_DIR"
    echo "Please ensure the network share is mounted."
    exit 1
fi

# Check if application exists
if [ ! -f "$APP_DLL" ]; then
    echo "ERROR: Application not found: $APP_DLL"
    echo "Please build the application first: cd src/EpisodeIdentifier.Core && dotnet build -c Release"
    exit 1
fi

# Run the import
echo "Starting import... (Progress will be shown in real-time)"
echo ""

dotnet "$APP_DLL" \
    --bulk-store "$SUBTITLE_DIR" \
    --hash-db "$HASH_DB" \
    2>&1 | tee "$LOG_FILE"

EXIT_CODE=${PIPESTATUS[0]}

echo ""
echo "=========================================="
if [ $EXIT_CODE -eq 0 ]; then
    echo "✓ Import completed successfully!"
else
    echo "✗ Import failed with exit code: $EXIT_CODE"
fi
echo "Full log saved to: $LOG_FILE"
echo "=========================================="

# Show database summary
echo ""
echo "Database Summary:"
sqlite3 "$HASH_DB" "SELECT Series, COUNT(*) as Episodes FROM SubtitleHashes GROUP BY Series ORDER BY Series;"

exit $EXIT_CODE
