# Episode Identifier - Deployment Guide








## Running Without `dotnet run`








There are several ways to run your Episode Identifier application without using `dotnet run`:

## Option 1: Framework-Dependent Executable








Build and run the executable that requires .NET runtime to be installed:

```bash

# Build in Release mode







dotnet build src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj --configuration Release

# Run the executable directly







./src/EpisodeIdentifier.Core/bin/Release/net8.0/EpisodeIdentifier.Core --input video.mkv --hash-db hashes.db
```








**Pros:**

- Smaller file size (~80KB + dependencies)
- Fast build time
- Easy to update

**Cons:**

- Requires .NET 8.0 runtime on target machine

## Option 2: Self-Contained Deployment








Create a deployment that includes the .NET runtime (no .NET installation required):

```bash

# Create self-contained deployment







dotnet publish src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj \
  --configuration Release \
  --self-contained true \
  --runtime linux-x64 \
  --output ./dist

# Run the self-contained executable







./dist/EpisodeIdentifier.Core --input video.mkv --hash-db hashes.db
```








**Pros:**

- No .NET runtime required on target machine
- Single directory contains everything
- Portable across machines

**Cons:**

- Larger size (~75MB)
- Longer build time

## Option 3: Single File Executable








Create a single executable file:

```bash

# Build single file executable







dotnet publish src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj \
  --configuration Release \
  --self-contained true \
  --runtime linux-x64 \
  --property:PublishSingleFile=true \
  --output ./single-file

# Run the single file







./single-file/EpisodeIdentifier.Core --input video.mkv --hash-db hashes.db
```








**Pros:**

- Single executable file
- No dependencies to manage
- Easy distribution

**Cons:**

- Largest size (~75MB+)
- Slower startup time

## Option 4: Global Tool Installation








Install as a global .NET tool:

```bash

# Pack as NuGet package (optional)







dotnet pack src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj

# Install globally (if packaged)







dotnet tool install --global episode-identifier

# Run from anywhere







episode-identifier --input video.mkv --hash-db hashes.db
```








## Option 5: Create Wrapper Script








Create a simple wrapper script for easier execution:

```bash

# Create identify-episode script







cat > identify-episode << 'EOF'
#!/bin/bash
cd "$(dirname "$0")"
./dist/EpisodeIdentifier.Core "$@"
EOF

chmod +x identify-episode

# Use the wrapper







./identify-episode --input video.mkv --hash-db hashes.db
```








## Docker Deployment








Create a Docker container for cross-platform deployment:

```dockerfile

# Dockerfile







FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY dist/ ./
ENTRYPOINT ["./EpisodeIdentifier.Core"]
```








```bash

# Build Docker image







docker build -t episode-identifier .

# Run in Docker







docker run -v "$(pwd)":/data episode-identifier \
  --input /data/video.mkv --hash-db /data/hashes.db
```








## Production Recommendations








### For Development/Testing








Use **Option 1** (Framework-Dependent) for fast iteration:

```bash
dotnet build --configuration Release
./src/EpisodeIdentifier.Core/bin/Release/net8.0/EpisodeIdentifier.Core --input video.mkv --hash-db hashes.db
```








### For Production Deployment








Use **Option 2** (Self-Contained) for reliability:

```bash
dotnet publish --configuration Release --self-contained true --runtime linux-x64 --output ./dist
./dist/EpisodeIdentifier.Core --input video.mkv --hash-db hashes.db
```








### For Distribution








Use **Option 3** (Single File) for easy sharing:

```bash
dotnet publish --configuration Release --self-contained true --runtime linux-x64 --property:PublishSingleFile=true --output ./release
```








## Dependencies








Remember that your application still requires external tools regardless of deployment method:

- **pgsrip** (primary subtitle extraction)
- **ffmpeg** (fallback and video validation)
- **mkvtoolnix** (container manipulation)
- **tesseract-ocr** (fallback OCR)

These need to be installed on the target system or included in your deployment strategy.
