#!/bin/bash

# Example usage of find_missing_episodes.sh

echo "=== Example 1: Check all series in current directory ==="
./scripts/find_missing_episodes.sh production_hashes.db .

echo ""
echo "=== Example 2: Check only Criminal Minds ==="
./scripts/find_missing_episodes.sh production_hashes.db /mnt/user/Media "Criminal Minds"

echo ""
echo "=== Example 3: Use default database in specific directory ==="
cd /mnt/user/Media/TV\ Shows
/path/to/KnowShow_Specd/scripts/find_missing_episodes.sh

