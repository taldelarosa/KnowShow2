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

# Install system dependencies
RUN apt-get update && apt-get install -y \
    # FFmpeg for video/audio processing
    ffmpeg \
    # MKVToolNix for MKV file manipulation
    mkvtoolnix \
    # Tesseract OCR with language packs
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-jpn \
    # Additional utilities
    curl \
    wget \
    ca-certificates \
    sqlite3 \
    gosu \
    # Build tools for pgsrip
    git \
    gcc \
    make \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Install pgsrip from source
WORKDIR /tmp
RUN git clone https://github.com/ratoaq2/pgsrip.git && \
    cd pgsrip && \
    make && \
    make install && \
    cd / && \
    rm -rf /tmp/pgsrip

# Create app user and directories
RUN groupadd -g 99 appuser && \
    useradd -u 99 -g 99 -m appuser

# Set up application directories
WORKDIR /app
RUN mkdir -p /data/videos /data/database /data/config /app/logs && \
    chown -R appuser:appuser /app /data

# Copy published application
COPY --from=build /app/publish .

# Copy example configuration to template location (not affected by volume mounts)
COPY episodeidentifier.config.example.json /app/config.template.json
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

# Default command (shows help)
CMD ["--help"]

# Labels for Docker/Unraid metadata
LABEL maintainer="Episode Identifier Team" \
      description="Episode Identifier - Identify TV episodes using PGS subtitle hashing" \
      version="1.0.0" \
      org.opencontainers.image.source="https://github.com/taldelarosa/KnowShow2" \
      org.opencontainers.image.description="Identify Season and Episode from AV1 video via PGS subtitle comparison"
