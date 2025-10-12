#!/bin/bash
# Setup Performance Test Configurations
# Creates various configuration files for testing different concurrency levels

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_DIR="$PROJECT_ROOT/test_configs"

echo "Setting up performance test configurations..."
echo ""

# Create config directory
mkdir -p "$CONFIG_DIR"

# Function to create a config file
create_config() {
    local concurrency=$1
    local filename=$2
    local description=$3
    
    cat > "$CONFIG_DIR/$filename" << EOF
{
  "maxConcurrency": $concurrency,
  "databasePath": "./production_hashes.db",
  "logLevel": "Information",
  "fuzzyMatching": {
    "enabled": true,
    "scoreThreshold": 85,
    "tokenSetRatio": true
  },
  "renaming": {
    "enabled": true,
    "confirmationRequired": false,
    "confidenceThreshold": 0.7,
    "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
  },
  "bulkProcessing": {
    "recursive": true,
    "maxDepth": 0,
    "batchSize": 100,
    "continueOnError": true,
    "reportProgress": true,
    "includeExtensions": [".mkv", ".mp4", ".avi"],
    "excludeExtensions": []
  },
  "performance": {
    "enableCaching": true,
    "cacheExpirationMinutes": 60,
    "maxDatabaseConnections": 10
  }
}
EOF
    
    echo "✓ Created $filename ($description)"
}

# Create configurations for different scenarios
echo "Creating configuration files..."
echo ""

create_config 1 "config_sequential.json" "Sequential processing - baseline"
create_config 2 "config_low_concurrency.json" "Low concurrency - 2 parallel operations"
create_config 4 "config_medium_concurrency.json" "Medium concurrency - 4 parallel operations"
create_config 8 "config_high_concurrency.json" "High concurrency - 8 parallel operations"
create_config 16 "config_max_concurrency.json" "Maximum concurrency - 16 parallel operations"
create_config 100 "config_extreme.json" "Extreme (clamped to max) - 100 will be clamped to valid range"

echo ""
echo "Configuration files created in: $CONFIG_DIR"
echo ""
echo "Usage examples:"
echo ""
echo "  # Test with sequential processing"
echo "  cp $CONFIG_DIR/config_sequential.json episodeidentifier.config.json"
echo ""
echo "  # Test with medium concurrency"
echo "  cp $CONFIG_DIR/config_medium_concurrency.json episodeidentifier.config.json"
echo ""
echo "  # Run bulk identification"
echo "  dotnet run --project src/EpisodeIdentifier.Core -- --bulk-identify ./test-videos"
echo ""
echo "Configuration Summary:"
echo "  - Sequential (1): Baseline, safest option"
echo "  - Low (2): Good for limited resources"
echo "  - Medium (4): Balanced performance"
echo "  - High (8): Optimized for modern CPUs"
echo "  - Max (16): Aggressive, for high-end systems"
echo "  - Extreme (100): Test clamping behavior"
echo ""
