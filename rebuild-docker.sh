#!/bin/bash
# Rebuild EpisodeIdentifier Docker image with environment variable fixes
# This script rebuilds the container to pick up the new CONFIG_PATH and HASH_DB_PATH support

set -e

echo "=================================="
echo "EpisodeIdentifier Docker Rebuild"
echo "=================================="
echo ""
echo "This will:"
echo "1. Stop the current container"
echo "2. Rebuild the image from scratch"
echo "3. Start the container with proper volume mount support"
echo ""

# Check if docker-compose exists
if ! command -v docker-compose &> /dev/null; then
    echo "ERROR: docker-compose not found. Please install docker-compose first."
    exit 1
fi

# Confirm with user
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Cancelled."
    exit 0
fi

echo ""
echo "Step 1: Stopping existing container..."
docker-compose down

echo ""
echo "Step 2: Building new image (this may take a few minutes)..."
docker-compose build --no-cache

echo ""
echo "Step 3: Starting container..."
docker-compose up -d

echo ""
echo "Step 4: Waiting for container to initialize..."
sleep 5

echo ""
echo "Step 5: Checking container logs..."
echo "======================================="
docker logs episodeidentifier | tail -30
echo "======================================="

echo ""
echo "Step 6: Verifying configuration..."
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll config show || echo "Note: config show command may not be available"

echo ""
echo "Build complete!"
echo ""
echo "✅ If you see 'Configuration file ready at: /data/config/episodeidentifier.config.json'"
echo "   and 'Episode Identifier Ready' above, the container is working properly."
echo ""
echo "❌ If you see validation errors, check:"
echo "   - docker-data/config/episodeidentifier.config.json has valid JSON (no // comments)"
echo "   - Volume mounts are correct in docker-compose.yml"
echo ""
echo "To view full logs: docker logs episodeidentifier"
echo "To run commands: docker exec -it episodeidentifier bash"
echo ""
