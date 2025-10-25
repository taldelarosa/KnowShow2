#!/bin/bash
# Run this on your Unraid server to check vectorlite loading

echo "=== Checking if vectorlite files exist in container ==="
docker exec episodeidentifier ls -la /app/vectorlite.*

echo ""
echo "=== Checking database for embeddings ==="
docker exec episodeidentifier sqlite3 /data/database/production_hashes.db "SELECT COUNT(*) as TotalEntries, SUM(CASE WHEN Embedding IS NOT NULL THEN 1 ELSE 0 END) as WithEmbeddings FROM SubtitleHashes;"

echo ""
echo "=== Testing vectorlite extension loading ==="
docker exec episodeidentifier sqlite3 /data/database/production_hashes.db "SELECT load_extension('/app/vectorlite.so'); SELECT 'Vectorlite loaded successfully';"

echo ""
echo "=== Try identifying with verbose output ==="
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll --identify "/data/videos/C1_t00.mkv" --log-level Debug
