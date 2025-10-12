# Research: Docker Containerization for Unraid

**Feature**: Docker Containerization for Unraid Deployment  
**Date**: 2025-10-11  
**Status**: Complete

## Research Areas

### 1. Multi-Stage Docker Builds for .NET 8.0 Applications

**Decision**: Use multi-stage build with `mcr.microsoft.com/dotnet/sdk:8.0` for build stage and `mcr.microsoft.com/dotnet/runtime:8.0` for runtime stage

**Rationale**:
- SDK image (~1GB) only needed for compilation, not runtime
- Runtime image (~200MB) significantly smaller for final container
- Microsoft official images have security updates and best practices built-in
- Self-contained publish can reduce runtime dependencies but increases size (~75MB)

**Alternatives Considered**:
- **Single-stage with SDK**: Rejected - 800MB+ larger final image
- **Self-contained single file**: Rejected - larger artifact, no shared runtime benefits
- **Alpine-based .NET images**: Considered but runtime images with musl libc have limited testing

**Implementation**:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Build application
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
# Copy compiled output
```

---

### 2. Base Image Selection for FFmpeg/Tesseract Dependencies

**Decision**: Use Debian-based .NET runtime image and install dependencies via apt

**Rationale**:
- FFmpeg, MKVToolNix, Tesseract readily available in Debian repos
- Microsoft's .NET runtime images based on Debian Bullseye/Bookworm
- Better compatibility with pgsrip and Python dependencies
- Well-documented package versions

**Alternatives Considered**:
- **Alpine Linux**: Rejected - musl libc compatibility issues with some dependencies, smaller package ecosystem
- **Ubuntu**: Considered - similar to Debian but larger base image
- **Build from source**: Rejected - significantly increases build time and maintenance

**Implementation**:
```dockerfile
RUN apt-get update && apt-get install -y \
    ffmpeg \
    mkvtoolnix \
    tesseract-ocr \
    tesseract-ocr-eng \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*
```

---

### 3. pgsrip Installation in Docker Containers

**Decision**: Install pgsrip via uv (fast Python package manager) during build stage

**Rationale**:
- pgsrip requires Python 3.10+ and specific dependencies (Pillow, pytesseract)
- uv is significantly faster than pip (10-100x) for dependency resolution
- Can pin specific versions for reproducibility
- Already used in project's setup scripts

**Alternatives Considered**:
- **pip**: Rejected - slower, less reliable dependency resolution
- **System packages**: Rejected - pgsrip not available in Debian repos
- **Manual installation**: Rejected - harder to maintain and version control

**Implementation**:
```dockerfile
RUN curl -LsSf https://astral.sh/uv/install.sh | sh && \
    uv pip install --system pgsrip
```

---

### 4. Unraid Docker Template XML Format

**Decision**: Create XML template following Unraid Community Applications schema

**Rationale**:
- Unraid's Docker UI parses XML templates to generate configuration forms
- Users can add repositories directly via URL or manual entry
- Supports variable types (Path, Port, Variable), descriptions, defaults
- Industry-standard format used by hundreds of Unraid apps

**Alternatives Considered**:
- **docker-compose.yml**: Rejected - not native to Unraid UI
- **JSON format**: Rejected - Unraid uses XML exclusively
- **No template**: Rejected - poor user experience, requires manual config

**Template Structure**:
```xml
<?xml version="1.0"?>
<Container version="2">
  <Name>Episode Identifier</Name>
  <Repository>taldelarosa/episode-identifier</Repository>
  <Config Name="Videos" Target="/videos" Default="/mnt/user/media/videos" Mode="rw" Type="Path"/>
  <Config Name="Database" Target="/data" Default="/mnt/user/appdata/episode-identifier" Mode="rw" Type="Path"/>
  <Config Name="PUID" Target="PUID" Default="99" Type="Variable"/>
  <Config Name="PGID" Target="PGID" Default="100" Type="Variable"/>
</Container>
```

**Reference**: https://unraid.net/community/apps/templates

---

### 5. PUID/PGID Implementation for File Permissions

**Decision**: Use gosu for user switching with PUID/PGID environment variables

**Rationale**:
- Unraid uses specific UIDs (99=nobody, 100=users by default)
- Files created in container must match host permissions
- gosu switches user without sudo overhead and setuid complications
- Standard pattern in Unraid-compatible containers (linuxserver.io style)

**Alternatives Considered**:
- **su/sudo**: Rejected - more complex, security implications
- **runuser**: Considered - similar but gosu is lighter
- **Fixed UID/GID**: Rejected - inflexible, doesn't match all Unraid configs

**Implementation**:
```bash
#!/bin/bash
# entrypoint.sh
PUID=${PUID:-99}
PGID=${PGID:-100}

groupmod -o -g "$PGID" appuser
usermod -o -u "$PUID" appuser

exec gosu appuser "$@"
```

---

### 6. Docker Health Checks for CLI Applications

**Decision**: Implement health check verifying CLI responds to --version command

**Rationale**:
- CLI tools don't have HTTP endpoints to ping
- --version command validates binary works and dependencies load
- Fast (<1 second) and reliable indicator
- Unraid displays health status in Docker UI

**Alternatives Considered**:
- **No health check**: Rejected - harder to diagnose container issues
- **Database check**: Considered - but database might not exist on first start
- **File system check**: Rejected - doesn't validate application works

**Implementation**:
```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD dotnet /app/EpisodeIdentifier.Core.dll --version || exit 1
```

---

### 7. Container Image Size Optimization

**Decision**: Multi-layered approach to minimize final image size

**Rationale**:
- Target <2GB for reasonable download time (per NFR-001)
- Each optimization technique provides cumulative benefit
- Smaller images = faster deployments and lower bandwidth

**Techniques Applied**:
1. **Multi-stage builds**: Exclude SDK (~800MB savings)
2. **Apt cache cleanup**: Remove package lists after install (~100MB savings)
3. **Layer ordering**: Place changing layers last (better caching)
4. **Publish trimmed**: Use `dotnet publish --configuration Release` (removes debug symbols)
5. **Combine RUN commands**: Fewer layers = smaller image

**Alternatives Considered**:
- **Scratch/distroless base**: Rejected - can't install FFmpeg/Tesseract
- **Aggressive trimming**: Rejected - might break dependencies
- **External volume for tools**: Rejected - defeats purpose of self-contained container

**Expected Final Size**: 1.5-1.8GB
- Base runtime: ~200MB
- .NET app: ~80MB
- FFmpeg: ~300MB
- Tesseract: ~100MB
- MKVToolNix: ~50MB
- Python + pgsrip: ~400MB
- System libraries: ~200-400MB

---

## Summary of Decisions

| Area | Decision | Size Impact |
|------|----------|-------------|
| Base Image | Debian-based .NET 8.0 runtime | +200MB |
| Build Strategy | Multi-stage (SDK→Runtime) | -800MB |
| Dependencies | Apt packages (FFmpeg, Tesseract, etc.) | +750MB |
| pgsrip | Install via uv | +400MB |
| Permissions | gosu + PUID/PGID | +5MB |
| Health Check | --version command | 0MB |
| **Total Estimated** | | **~1.5-1.8GB** |

---

## Open Questions

None - all research areas resolved

---

## References

- [.NET Docker Official Images](https://hub.docker.com/_/microsoft-dotnet-runtime/)
- [Docker Multi-Stage Builds](https://docs.docker.com/build/building/multi-stage/)
- [Unraid Docker Template Schema](https://github.com/selfhosters/unRAID-CA-templates)
- [LinuxServer.io Base Images](https://github.com/linuxserver/docker-baseimage-alpine) (PUID/PGID reference)
- [gosu GitHub](https://github.com/tianon/gosu)
- [pgsrip Documentation](https://github.com/ratoaq2/pgsrip)
