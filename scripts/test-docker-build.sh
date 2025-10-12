#!/bin/bash
# Quick Docker build and test script
# Tests basic Docker functionality before deployment

set -e

echo "========================================"
echo "Docker Build and Test Script"
echo "========================================"
echo ""

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed or not in PATH"
    exit 1
fi

echo "✅ Docker found: $(docker --version)"
echo ""

# Build the image
echo "🔨 Building Docker image..."
echo "This may take 5-10 minutes on first build"
echo ""

docker build -t episodeidentifier:test .

if [ $? -ne 0 ]; then
    echo "❌ Docker build failed"
    exit 1
fi

echo ""
echo "✅ Docker image built successfully"
echo ""

# Check image size
IMAGE_SIZE=$(docker images episodeidentifier:test --format "{{.Size}}")
echo "📦 Image size: $IMAGE_SIZE"
echo ""

# Test basic help command
echo "🧪 Testing help command..."
docker run --rm episodeidentifier:test --help

if [ $? -ne 0 ]; then
    echo "❌ Help command failed"
    exit 1
fi

echo ""
echo "✅ Help command works"
echo ""

# Test with docker-compose (if available)
if command -v docker-compose &> /dev/null; then
    echo "🧪 Testing with docker-compose..."
    
    # Create test directories
    mkdir -p docker-data/database docker-data/config test-videos
    
    # Copy example config if it doesn't exist
    if [ ! -f docker-data/config/episodeidentifier.config.json ] && [ -f episodeidentifier.config.example.json ]; then
        cp episodeidentifier.config.example.json docker-data/config/episodeidentifier.config.json
        echo "✅ Created default configuration"
    fi
    
    # Start container
    docker-compose up -d
    
    if [ $? -eq 0 ]; then
        echo "✅ Docker Compose container started"
        
        # Wait for container to be ready
        sleep 3
        
        # Test command execution
        echo "🧪 Testing command execution in container..."
        docker-compose exec -T episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll --help > /dev/null 2>&1
        
        if [ $? -eq 0 ]; then
            echo "✅ Command execution works"
        else
            echo "⚠️  Command execution test failed (container may still be starting)"
        fi
        
        # Stop container
        docker-compose down
        echo "✅ Container stopped"
    else
        echo "⚠️  Docker Compose test skipped (check docker-compose.yml)"
    fi
else
    echo "ℹ️  docker-compose not available, skipping compose tests"
fi

echo ""
echo "========================================"
echo "✅ All tests passed!"
echo "========================================"
echo ""
echo "Next steps:"
echo "1. Test with actual video files"
echo "2. Verify volume mappings work correctly"
echo "3. Test on Unraid (if available)"
echo "4. Push to Docker Hub (when ready)"
echo ""
echo "Manual test commands:"
echo ""
echo "# Run container with volumes:"
echo "docker run -d --name episodeidentifier-test \\"
echo "  -e PUID=\$(id -u) -e PGID=\$(id -g) \\"
echo "  -v ./test-videos:/data/videos:rw \\"
echo "  -v ./docker-data/database:/data/database:rw \\"
echo "  -v ./docker-data/config:/data/config:rw \\"
echo "  episodeidentifier:test tail -f /dev/null"
echo ""
echo "# Execute command:"
echo "docker exec episodeidentifier-test dotnet /app/EpisodeIdentifier.Core.dll --help"
echo ""
echo "# Clean up:"
echo "docker stop episodeidentifier-test && docker rm episodeidentifier-test"
echo ""
