# Multi-stage build for Episode Identifier
# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY EpisodeIdentifier.sln .
COPY src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj src/EpisodeIdentifier.Core/

# Restore dependencies
RUN dotnet restore src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj

# Copy all source files and build
COPY src/ src/
RUN dotnet build src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj -c Release -o /app/build

# Publish the application
RUN dotnet publish src/EpisodeIdentifier.Core/EpisodeIdentifier.Core.csproj -c Release -o /app/publish

# Stage 2: Create runtime image with all dependencies
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Add Debian Bullseye repository for Tesseract 4.x
RUN echo "deb http://deb.debian.org/debian bullseye main" >> /etc/apt/sources.list.d/bullseye.list \
    && echo "Package: *\nPin: release n=bullseye\nPin-Priority: 100" > /etc/apt/preferences.d/bullseye \
    && echo "Package: tesseract-ocr* libtesseract*\nPin: release n=bullseye\nPin-Priority: 900" >> /etc/apt/preferences.d/bullseye

# Install system dependencies and build vobsub2srt
# Build dependencies are removed in the same layer to reduce image size
RUN apt-get update && apt-get install -y \
    # FFmpeg for video/audio processing
    ffmpeg \
    # MKVToolNix for MKV file manipulation
    mkvtoolnix \
    # Tesseract OCR with language packs
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-jpn \
    # Build dependencies for vobsub2srt (will be removed)
    build-essential \
    cmake \
    git \
    libtesseract-dev \
    libleptonica-dev \
    libavformat-dev \
    libavcodec-dev \
    libavutil-dev \
    libswscale-dev \
    libtiff-dev \
    # Python for pgsrip
    python3 \
    python3-pip \
    # Additional utilities
    curl \
    wget \
    ca-certificates \
    sqlite3 \
    gosu \
    && rm -rf /var/lib/apt/lists/* \
    # Build vobsub2srt with Tesseract 4/5 compatibility patches from PR #101
    && cd /tmp \
    && git clone https://github.com/ruediger/VobSub2SRT.git \
    && cd VobSub2SRT \
    # Fetch and apply PR #101 (Tesseract 4 support)
    && curl -L https://patch-diff.githubusercontent.com/raw/ruediger/VobSub2SRT/pull/101.patch | git apply \
    && mkdir build \
    && cd build \
    && cmake .. \
    && make -j$(nproc) \
    && make install \
    && cd / \
    && rm -rf /tmp/VobSub2SRT \
    # Remove build dependencies to reduce image size
    # Keep the runtime libraries that were installed as dependencies
    && apt-get purge -y --auto-remove \
        build-essential \
        cmake \
        git \
        libtesseract-dev \
        libleptonica-dev \
        libavformat-dev \
        libavcodec-dev \
        libavutil-dev \
        libswscale-dev \
        libtiff-dev \
    && rm -rf /var/lib/apt/lists/*

# Install pgsrip from source
# Note: pgsrip is a Python tool, use --break-system-packages for Docker container
RUN pip3 install --no-cache-dir --break-system-packages pgsrip

# Create app user and directories
RUN groupadd -g 99 appuser && \
    useradd -u 99 -g 99 -m appuser

# Set up application directories
WORKDIR /app
RUN mkdir -p /data/videos /data/database /data/config /app/logs && \
    chown -R appuser:appuser /app /data

# Copy published application
COPY --from=build /app/publish .

# Copy clean configuration template (without comments for JSON parser compatibility)
COPY episodeidentifier.config.template.json /app/config.template.json
RUN chown appuser:appuser /app/config.template.json

# Copy entrypoint script
COPY docker-entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Environment variables for Unraid compatibility
ENV PUID=99 \
    PGID=100 \
    CONFIG_PATH=/data/config/episodeidentifier.config.json \
    HASH_DB_PATH=/data/database/production_hashes.db \
    LOG_LEVEL=Information

# Volume mount points
VOLUME ["/data/videos", "/data/database", "/data/config"]

# Set entrypoint
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]

# Default command: Keep container running for manual execution (Unraid compatible)
# Users can exec into the container to run commands
CMD ["sleep", "infinity"]

# Labels for Docker/Unraid metadata
LABEL maintainer="Episode Identifier Team" \
      description="Episode Identifier - Identify TV episodes using PGS subtitle hashing" \
      version="1.0.0" \
      org.opencontainers.image.source="https://github.com/taldelarosa/KnowShow2" \
      org.opencontainers.image.description="Identify Season and Episode from AV1 video via PGS subtitle comparison"
