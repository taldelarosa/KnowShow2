#!/bin/bash
# Reimport all subtitles from network share with corrected normalization
# This script processes all .srt files and stores them in the production database

set -e  # Exit on error

SUBTITLE_DIR="/mnt/void_subtitles/subtitles"
PROJECT_DIR="/mnt/c/Users/Ragma/KnowShow_Specd"
HASH_DB="$PROJECT_DIR/production_hashes.db"
LOG_FILE="$PROJECT_DIR/reimport_$(date +%Y%m%d_%H%M%S).log"

echo "=== Subtitle Reimport Script ===" | tee -a "$LOG_FILE"
echo "Start time: $(date)" | tee -a "$LOG_FILE"
echo "Subtitle directory: $SUBTITLE_DIR" | tee -a "$LOG_FILE"
echo "Hash database: $HASH_DB" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Count total files
TOTAL_FILES=$(find "$SUBTITLE_DIR" -name "*.srt" | wc -l)
echo "Total subtitle files found: $TOTAL_FILES" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

echo "Starting bulk import..." | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Run bulk-store command
cd "$PROJECT_DIR/src/EpisodeIdentifier.Core"

dotnet run -- --bulk-store "$SUBTITLE_DIR" --hash-db "$HASH_DB" 2>&1 | tee -a "$LOG_FILE"

echo "" | tee -a "$LOG_FILE"
echo "=== Import Complete ===" | tee -a "$LOG_FILE"
echo "End time: $(date)" | tee -a "$LOG_FILE"

# Show statistics
echo "" | tee -a "$LOG_FILE"
echo "Database statistics:" | tee -a "$LOG_FILE"
sqlite3 "$HASH_DB" "SELECT COUNT(*) as TotalRecords FROM SubtitleHashes;" | tee -a "$LOG_FILE"
sqlite3 "$HASH_DB" "SELECT Series, COUNT(*) as Episodes FROM SubtitleHashes GROUP BY Series;" | tee -a "$LOG_FILE"

echo "" | tee -a "$LOG_FILE"
echo "Log file: $LOG_FILE"
