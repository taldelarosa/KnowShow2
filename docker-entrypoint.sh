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

# Set default CONFIG_PATH if not provided
CONFIG_PATH=${CONFIG_PATH:-/data/config/episodeidentifier.config.json}

# Create default config if it doesn't exist
if [ ! -f "${CONFIG_PATH}" ]; then
    echo "Creating default configuration file at ${CONFIG_PATH}..."
    # Copy from the template included in the container image
    if [ -f "/app/config.template.json" ]; then
        cp /app/config.template.json "${CONFIG_PATH}"
        echo "Configuration copied from template"
    else
        echo "ERROR: Template configuration not found at /app/config.template.json"
        exit 1
    fi
    chown appuser:appuser "${CONFIG_PATH}" 2>/dev/null || true
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

# If sleep command, keep container running for Unraid
if [ "$1" = "sleep" ]; then
    echo "Container running in daemon mode for Unraid."
    echo "Use 'docker exec' or Unraid console to run commands:"
    echo "  dotnet /app/EpisodeIdentifier.Core.dll --help"
    echo ""
    exec gosu appuser sleep infinity
fi

# If no arguments provided, show help
if [ $# -eq 0 ]; then
    echo "No command provided. Showing help..."
    exec gosu appuser dotnet /app/EpisodeIdentifier.Core.dll --help
fi

# Execute the command as the app user
echo "Executing: dotnet /app/EpisodeIdentifier.Core.dll $@"
echo ""
exec gosu appuser dotnet /app/EpisodeIdentifier.Core.dll "$@"
