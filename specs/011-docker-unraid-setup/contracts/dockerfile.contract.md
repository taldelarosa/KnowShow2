# Contract: Dockerfile Build Contract

**Purpose**: Define the expected structure, stages, and outputs of the Dockerfile  
**Date**: 2025-10-11

## Contract Overview

The Dockerfile MUST produce a working container image that:

1. Contains all application dependencies
2. Runs the Episode Identifier CLI
3. Supports PUID/PGID for file permissions
4. Passes health checks
5. Is under 2GB in size

---

## Build Stages

### Stage 1: Build (.NET SDK)

**Base Image**: `mcr.microsoft.com/dotnet/sdk:8.0`

**Responsibilities**:

- Restore NuGet packages
- Compile C# application
- Publish in Release configuration
- Output to `/app/publish`

**Expected Outputs**:

- `EpisodeIdentifier.Core.dll`
- `EpisodeIdentifier.Core.deps.json`
- `EpisodeIdentifier.Core.runtimeconfig.json`
- All NuGet package dependencies

**Test**: Build stage completes without errors

---

### Stage 2: Runtime (Debian + .NET Runtime)

**Base Image**: `mcr.microsoft.com/dotnet/runtime:8.0`

**Responsibilities**:

- Install system dependencies (FFmpeg, Tesseract, MKVToolNix)
- Install Python and pgsrip
- Install gosu for user switching
- Copy compiled application from build stage
- Create application user
- Set up entrypoint script

**Expected System Packages**:

- `ffmpeg` (video processing)
- `mkvtoolnix` (mkvextract command)
- `tesseract-ocr` (OCR engine)
- `tesseract-ocr-eng` (English language pack)
- `python3` (for pgsrip)
- `python3-pip` or `uv` (Python package manager)
- `gosu` (user switching)
- `ca-certificates` (SSL certificates)
- `sqlite3` (database CLI, optional for debugging)

**Expected Python Packages**:

- `pgsrip` (PGS subtitle processor)

**Test**: All commands available in PATH:

```bash
ffmpeg -version
mkvextract --version
tesseract --version
python3 --version
pgsrip --version
gosu --version
```

---

## File System Layout

### Application Directory: `/app`

```
/app/
├── EpisodeIdentifier.Core.dll          # Main assembly
├── EpisodeIdentifier.Core.deps.json    # Dependency manifest
├── EpisodeIdentifier.Core.runtimeconfig.json  # Runtime config
└── [NuGet dependencies]                # Third-party libraries
```

**Test**: Application can be executed:

```bash
dotnet /app/EpisodeIdentifier.Core.dll --version
```

---

### Data Directory: `/data`

**Purpose**: Default location for persistent database  
**Owner**: `appuser:appuser` (created at runtime with PUID/PGID)  
**Permissions**: `755`

**Test**: Directory exists and is writable

---

### Config Directory: `/config`

**Purpose**: Default location for configuration JSON  
**Owner**: `appuser:appuser`  
**Permissions**: `755`

**Test**: Directory exists and is readable

---

### Videos Directory: `/videos`

**Purpose**: Default location for video file access  
**Owner**: `appuser:appuser`  
**Permissions**: `755`

**Test**: Directory exists and is readable/writable

---

## Entrypoint Contract

### File: `/entrypoint.sh`

**Responsibilities**:

1. Read PUID and PGID environment variables
2. Update appuser UID/GID to match
3. Fix ownership of /data, /config, /videos
4. Switch to appuser
5. Execute passed command or default CLI

**Input Environment Variables**:

- `PUID` (default: 99)
- `PGID` (default: 100)

**Expected Behavior**:

```bash
# Container starts with: docker run ... episode-identifier --help
# entrypoint.sh receives: --help
# entrypoint.sh executes: gosu appuser dotnet /app/EpisodeIdentifier.Core.dll --help
```

**Test Cases**:

1. PUID=1000, PGID=1000 → files created as 1000:1000
2. PUID=99, PGID=100 → files created as 99:100
3. No PUID/PGID → defaults to 99:100

---

## Health Check Contract

### Command

```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD dotnet /app/EpisodeIdentifier.Core.dll --version || exit 1
```

**Success Criteria**:

- Command exits with code 0
- Output contains version string
- Completes within 3 seconds

**Failure Criteria**:

- Command exits with non-zero code
- Command times out (>3 seconds)
- Command crashes or hangs

**Test**: Health check passes after container start

---

## Image Size Contract

**Maximum Size**: 2GB (per NFR-001)  
**Target Size**: 1.5-1.8GB

**Breakdown**:

- Base runtime: ~200MB
- .NET application: ~80MB
- FFmpeg: ~300MB
- Tesseract: ~100MB
- MKVToolNix: ~50MB
- Python + pgsrip: ~400MB
- System libraries: ~200-400MB

**Test**: `docker images` shows size ≤ 2GB

---

## Build Performance Contract

**Maximum Build Time**: 10 minutes (without cache)  
**Expected Build Time**: 3-5 minutes (with layer cache)

**Test**: `docker build` completes successfully within time limit

---

## Environment Variable Contract

### Required Variables

| Variable | Type | Default | Validation |
|----------|------|---------|------------|
| `PUID` | integer | 99 | Must be 1-65535 |
| `PGID` | integer | 100 | Must be 1-65535 |

### Optional Variables

| Variable | Type | Default | Validation |
|----------|------|---------|------------|
| `TZ` | string | UTC | Must be valid IANA timezone |
| `LOG_LEVEL` | string | Information | Must be: Debug, Information, Warning, Error |

**Test**: Container starts with missing/invalid variables (uses defaults)

---

## Volume Mount Contract

### Required Mounts

| Mount Point | Purpose | Mode | Required |
|-------------|---------|------|----------|
| `/videos` | Video files | rw | No (can use --input with full paths) |
| `/data` | Database persistence | rw | Yes (or database is ephemeral) |
| `/config` | Configuration file | ro | No (uses defaults) |

**Test Cases**:

1. No volumes mounted → container starts, uses ephemeral storage
2. Only /data mounted → database persists across restarts
3. All volumes mounted → full functionality

---

## Command Interface Contract

### All Existing Commands Supported

The container MUST support ALL existing CLI commands:

```bash
# Single file identification
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --input /videos/file.mkv --hash-db /data/hashes.db

# Bulk processing
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --bulk-identify /videos --hash-db /data/hashes.db

# Storing subtitles
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --input /videos/file.mkv --hash-db /data/hashes.db --store --series "Show" --season 1 --episode 2

# Help/version
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --help
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --version
```

**Test**: All commands from README.md work identically in container

---

## Validation Tests

### Build Validation

```bash
# Build succeeds
docker build -t episode-identifier:test .

# Image size within limits
docker images episode-identifier:test | awk '{print $7}' | grep -E '^[0-9]+\.[0-9]+GB$'

# All commands available
docker run --rm episode-identifier:test ffmpeg -version
docker run --rm episode-identifier:test tesseract --version
docker run --rm episode-identifier:test pgsrip --version
```

### Runtime Validation

```bash
# Container starts
docker run -d --name test-container episode-identifier:test

# Health check passes
docker inspect test-container | grep '"Health"' -A 10 | grep '"Status": "healthy"'

# PUID/PGID works
docker run --rm -e PUID=1000 -e PGID=1000 -v /tmp/test:/data episode-identifier:test touch /data/test.txt
stat -c '%u:%g' /tmp/test/test.txt | grep '1000:1000'

# Application executes
docker exec test-container dotnet /app/EpisodeIdentifier.Core.dll --version
```

---

## Breaking Changes

Any change that breaks these contracts requires:

1. Major version bump
2. Migration guide
3. Deprecation notice (if possible)

**Examples of breaking changes**:

- Removing support for environment variable
- Changing default paths
- Removing system dependency
- Changing entrypoint behavior
