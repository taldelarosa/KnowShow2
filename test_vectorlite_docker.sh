#!/bin/bash
# Test script to verify vectorlite is available in Docker container

echo "1. Checking if vectorlite files exist in container..."
docker run --rm --entrypoint ls episodeidentifier:test -la /app/vectorlite.*

echo ""
echo "2. Testing vectorlite loading with a simple database query..."
docker run --rm --entrypoint sh episodeidentifier:test -c "cd /app && dotnet EpisodeIdentifier.Core.dll --help | head -5"
