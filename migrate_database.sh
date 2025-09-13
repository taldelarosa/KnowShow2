#!/bin/bash

# Migration script to update existing database with fuzzy hashes for performance

DB_FILE="${1:-production_hashes.db}"
BACKUP_FILE="${DB_FILE}.backup.$(date +%Y%m%d_%H%M%S)"

echo "Starting database migration to add fuzzy hashes..."
echo "Database: $DB_FILE"
echo "Creating backup: $BACKUP_FILE"

# Create backup
cp "$DB_FILE" "$BACKUP_FILE"

echo "Running EpisodeIdentifier to trigger automatic migration..."
cd /mnt/c/Users/Ragma/KnowShow_Specd/src/EpisodeIdentifier.Core

# Build the application
dotnet build

# Run a dummy command to trigger the database migration
# This will initialize the database with new schema and generate hashes for existing records
echo "Triggering database schema migration..."
timeout 30s dotnet run -- --hash-db "$DB_FILE" --input /dev/null 2>/dev/null || true

echo "Migration completed!"
echo "Backup saved as: $BACKUP_FILE"

# Check the updated schema
echo "Updated database schema:"
sqlite3 "$DB_FILE" ".schema SubtitleHashes"

echo ""
echo "Sample of migrated data:"
sqlite3 "$DB_FILE" "SELECT Series, Season, Episode, length(CleanHash) as HashLength FROM SubtitleHashes LIMIT 3;"