#!/bin/bash
set -e

# Episode Identifier Docker Entrypoint
# Handles PUID/PGID for Unraid compatibility and ensures proper permissions

echo "Episode Identifier Container Starting..."
echo "========================================="

# Default values if not provided
PUID=${PUID:-99}
PGID=${PGID:-100}

echo "Using PUID: ${PUID}"
echo "Using PGID: ${PGID}"

# Update user/group IDs to match Unraid settings
groupmod -o -g "${PGID}" appuser 2>/dev/null || true
usermod -o -u "${PUID}" appuser 2>/dev/null || true

echo "User permissions updated"

# Ensure data directories exist and have correct permissions
mkdir -p /data/videos /data/database /data/config /app/logs

echo "Setting directory permissions..."
chown -R appuser:appuser /data /app/logs 2>/dev/null || true
chmod -R u+rw /data 2>/dev/null || true

# Create default config if it doesn't exist
if [ ! -f "${CONFIG_PATH:-/data/config/episodeidentifier.config.json}" ]; then
    echo "Creating default configuration file..."
    if [ -f "/data/config/episodeidentifier.config.json" ]; then
        cp /data/config/episodeidentifier.config.json "${CONFIG_PATH}"
    else
        cat > "${CONFIG_PATH}" <<'EOF'
{
  "thresholds": {
    "minimumTextMatchRatio": 0.65,
    "minimumFuzzyHashSimilarity": 85,
    "minimumTotalScore": 75
  },
  "bulkProcessing": {
    "maxConcurrency": 4
  },
  "filenamePatterns": {
    "standardPatterns": [
      "(?<series>.+?)[._ ]S(?<season>\\d{2})E(?<episode>\\d{2})",
      "(?<series>.+?)[._ ](?<season>\\d{1,2})x(?<episode>\\d{2})"
    ],
    "animePatterns": [
      "(?<series>.+?)[._ ]-[._ ](?<episode>\\d{2,3})"
    ]
  },
  "logging": {
    "logLevel": "Information"
  }
}
EOF
        chown appuser:appuser "${CONFIG_PATH}" 2>/dev/null || true
    fi
fi

echo "Configuration file ready at: ${CONFIG_PATH}"

# Initialize database if it doesn't exist
if [ ! -f "${HASH_DB_PATH:-/data/database/production_hashes.db}" ]; then
    echo "Database will be created on first use: ${HASH_DB_PATH}"
fi

# Verify required dependencies
echo ""
echo "Verifying dependencies..."
echo "- .NET Runtime: $(dotnet --version 2>/dev/null || echo 'ERROR: Not found')"
echo "- FFmpeg: $(ffmpeg -version 2>/dev/null | head -n1 | cut -d' ' -f3 || echo 'ERROR: Not found')"
echo "- mkvextract: $(mkvextract --version 2>/dev/null | head -n1 || echo 'ERROR: Not found')"
echo "- Tesseract: $(tesseract --version 2>/dev/null | head -n1 || echo 'ERROR: Not found')"
echo "- pgsrip: $(which pgsrip 2>/dev/null || echo 'ERROR: Not found')"

echo ""
echo "========================================="
echo "Episode Identifier Ready"
echo "========================================="
echo ""

# If no arguments provided, show help
if [ $# -eq 0 ]; then
    echo "No command provided. Showing help..."
    exec gosu appuser dotnet /app/EpisodeIdentifier.Core.dll --help
fi

# Execute the command as the app user
echo "Executing: dotnet /app/EpisodeIdentifier.Core.dll $@"
echo ""
exec gosu appuser dotnet /app/EpisodeIdentifier.Core.dll "$@"
