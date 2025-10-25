#!/bin/bash

# Find Missing Episodes Script
# Compares database episode records with actual video files to identify missing episodes
# Usage: ./find_missing_episodes.sh [database_path] [video_directory] [series_filter]

set -euo pipefail

# Configuration
DB_PATH="${1:-production_hashes.db}"
VIDEO_DIR="${2:-.}"
SERIES_FILTER="${3:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check dependencies
if ! command -v sqlite3 &> /dev/null; then
    echo -e "${RED}Error: sqlite3 is not installed${NC}"
    exit 1
fi

if [ ! -f "$DB_PATH" ]; then
    echo -e "${RED}Error: Database file not found: $DB_PATH${NC}"
    exit 1
fi

if [ ! -d "$VIDEO_DIR" ]; then
    echo -e "${RED}Error: Video directory not found: $VIDEO_DIR${NC}"
    exit 1
fi

echo -e "${BLUE}=== MISSING EPISODE FINDER ===${NC}"
echo "Database: $DB_PATH"
echo "Video Directory: $VIDEO_DIR"
echo ""

# Build SQL query with optional series filter
if [ -n "$SERIES_FILTER" ]; then
    SQL_QUERY="SELECT DISTINCT Series, Season, Episode FROM SubtitleHashes WHERE Series = '$SERIES_FILTER' ORDER BY Series, CAST(Season AS INTEGER), CAST(Episode AS INTEGER);"
    echo -e "${YELLOW}Filtering by series: $SERIES_FILTER${NC}"
else
    SQL_QUERY="SELECT DISTINCT Series, Season, Episode FROM SubtitleHashes ORDER BY Series, CAST(Season AS INTEGER), CAST(Episode AS INTEGER);"
    echo -e "${YELLOW}Checking all series in database${NC}"
fi
echo ""

# Create temporary files
EPISODES_FILE=$(mktemp)
MISSING_FILE=$(mktemp)
FOUND_FILE=$(mktemp)
trap "rm -f $EPISODES_FILE $MISSING_FILE $FOUND_FILE" EXIT

# Extract all episodes from database
sqlite3 -separator '|' "$DB_PATH" "$SQL_QUERY" > "$EPISODES_FILE"

# Statistics
TOTAL_EPISODES=0
MISSING_EPISODES=0
FOUND_EPISODES=0
CURRENT_SERIES=""

# Process each episode
while IFS='|' read -r series season episode; do
    TOTAL_EPISODES=$((TOTAL_EPISODES + 1))
    
    # Format as S##E## pattern
    SEASON_NUM=$(printf "%02d" "$season")
    EPISODE_NUM=$(printf "%02d" "$episode")
    PATTERN="S${SEASON_NUM}E${EPISODE_NUM}"
    
    # Print series header when it changes
    if [ "$series" != "$CURRENT_SERIES" ]; then
        CURRENT_SERIES="$series"
        echo -e "\n${BLUE}=== $series ===${NC}"
    fi
    
    # Search for files matching the pattern (case-insensitive)
    # Looking for common video extensions: mkv, mp4, avi, m4v
    if find "$VIDEO_DIR" -type f -iname "*${PATTERN}*" \
        \( -iname "*.mkv" -o -iname "*.mp4" -o -iname "*.avi" -o -iname "*.m4v" \) \
        -print -quit | grep -q .; then
        FOUND_EPISODES=$((FOUND_EPISODES + 1))
        echo "$series|S${SEASON_NUM}|E${EPISODE_NUM}|FOUND" >> "$FOUND_FILE"
    else
        MISSING_EPISODES=$((MISSING_EPISODES + 1))
        echo -e "${RED}  MISSING: ${series} S${SEASON_NUM}E${EPISODE_NUM}${NC}"
        echo "$series|S${SEASON_NUM}|E${EPISODE_NUM}" >> "$MISSING_FILE"
    fi
done < "$EPISODES_FILE"

# Print summary
echo ""
echo -e "${BLUE}=== SUMMARY ===${NC}"
echo -e "Total episodes in database: ${BLUE}$TOTAL_EPISODES${NC}"
echo -e "Episodes found in filesystem: ${GREEN}$FOUND_EPISODES${NC}"
echo -e "Episodes MISSING from filesystem: ${RED}$MISSING_EPISODES${NC}"

if [ $MISSING_EPISODES -gt 0 ]; then
    PERCENTAGE=$(awk "BEGIN {printf \"%.1f\", ($MISSING_EPISODES/$TOTAL_EPISODES)*100}")
    echo -e "Missing percentage: ${RED}${PERCENTAGE}%${NC}"
fi

# Generate detailed report if there are missing episodes
if [ $MISSING_EPISODES -gt 0 ]; then
    REPORT_FILE="missing_episodes_$(date +%Y%m%d_%H%M%S).txt"
    
    echo "" | tee "$REPORT_FILE"
    echo "=== MISSING EPISODES BY SERIES ===" | tee -a "$REPORT_FILE"
    
    # Group missing episodes by series
    CURRENT_SERIES=""
    while IFS='|' read -r series season episode; do
        if [ "$series" != "$CURRENT_SERIES" ]; then
            CURRENT_SERIES="$series"
            echo "" | tee -a "$REPORT_FILE"
            echo "$series:" | tee -a "$REPORT_FILE"
        fi
        echo "  ${season}${episode}" | tee -a "$REPORT_FILE"
    done < "$MISSING_FILE"
    
    echo "" | tee -a "$REPORT_FILE"
    echo -e "${GREEN}Detailed report saved to: $REPORT_FILE${NC}"
fi

# Exit with error code if episodes are missing
if [ $MISSING_EPISODES -gt 0 ]; then
    exit 1
else
    echo -e "\n${GREEN}✓ All episodes found!${NC}"
    exit 0
fi
