# Unraid Quick Start Guide

## Container is Now Running! 🎉

The new Docker image keeps the container running in daemon mode, perfect for Unraid.

## What Changed

- **Before**: Container exited immediately after showing help
- **After**: Container stays running with `sleep infinity`
- **Usage**: Execute commands via Unraid console or `docker exec`

## Using the Container on Unraid

### Method 1: Unraid Console (Easiest)

1. In Unraid Docker tab, click your **KnowShow** container icon
2. Click **Console**
3. Run commands directly:

```bash
# Show help
dotnet /app/EpisodeIdentifier.Core.dll --help

# Identify a video
dotnet /app/EpisodeIdentifier.Core.dll --input /data/videos/episode.mkv --hash-db /data/database/production_hashes.db

# Store a known episode
dotnet /app/EpisodeIdentifier.Core.dll --store --input /data/videos/ShowName.S01E01.mkv --season 1 --episode 1 --hash-db /data/database/production_hashes.db

# Bulk identify directory
dotnet /app/EpisodeIdentifier.Core.dll --bulk-identify /data/videos/Season1 --hash-db /data/database/production_hashes.db

# Auto-rename after identification
dotnet /app/EpisodeIdentifier.Core.dll --input /data/videos/unknown.mkv --hash-db /data/database/production_hashes.db --rename
```

### Method 2: SSH/Terminal

```bash
docker exec -it KnowShow dotnet /app/EpisodeIdentifier.Core.dll --help
```

### Method 3: User Scripts Plugin (Automated)

Install **User Scripts** plugin from Community Applications, then create a script:

```bash
#!/bin/bash
# Runs daily at 2 AM to process new downloads

docker exec KnowShow dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos \
  --hash-db /data/database/production_hashes.db \
  --rename
```

Schedule: `0 2 * * *` (2 AM daily)

## Current Image Details

- **Repository**: `ghcr.io/taldelarosa/knowshow2:latest`
- **Registry**: GitHub Container Registry
- **Multi-arch**: linux/amd64, linux/arm64
- **Size**: ~1.5GB
- **Auto-updates**: Pull `:latest` tag to get updates

## Volume Mappings

Your current setup from Unraid template:

| Host Path | Container Path | Purpose |
|-----------|---------------|---------|
| `/mnt/user/downloads/KnowShowProcessing` | `/data/videos` | Video files to identify |
| `/mnt/user/appdata/episodeidentifier/database` | `/data/database` | Hash database (persistent) |
| `/mnt/user/appdata/episodeidentifier/config` | `/data/config` | Configuration file |

## Configuration

Edit: `/mnt/user/appdata/episodeidentifier/config/episodeidentifier.config.json`

Key settings:

- `textSimilarityThreshold`: 0.70 (70% similarity required)
- `fuzzyHashSimilarityThreshold`: 70 (fuzzy hash similarity)
- `totalScoreThreshold`: 0.80 (80% overall confidence)
- `maxConcurrency`: 4 (parallel processing)

Changes are hot-reloaded (no container restart needed).

## Typical Workflow

### First Time: Store Known Episodes

```bash
# Store your known episodes to build the database
docker exec KnowShow dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-store /data/videos/MyShow/Season01 \
  --hash-db /data/database/production_hashes.db
```

### Ongoing: Identify Unknown Files

```bash
# Identify and rename new downloads
docker exec KnowShow dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/downloads \
  --hash-db /data/database/production_hashes.db \
  --rename
```

## Troubleshooting

### Container Exits Immediately

- **Old image**: Pull latest: `docker pull ghcr.io/taldelarosa/knowshow2:latest`
- **Check logs**: Unraid Docker → KnowShow → Logs

### Permission Errors

- Verify PUID/PGID: Should be `99/100` for Unraid
- Check: Container variables → PUID=99, PGID=100

### No Matches Found

- Store known episodes first using `--bulk-store`
- Check database has data: Should see `production_hashes.db` file grow

### Missing Subtitles Error

- File must contain PGS subtitle track
- Check with: `docker exec KnowShow mkvinfo /data/videos/file.mkv`

## Next Steps

1. **Wait for build to complete** (~5 min)
2. **Stop your current container**: Unraid Docker → KnowShow → Stop
3. **Update container**: Click container → Force Update → Pull latest
4. **Start container**: Should stay running now
5. **Test it**: Console → `dotnet /app/EpisodeIdentifier.Core.dll --help`

## Support

- GitHub Issues: <https://github.com/taldelarosa/KnowShow2/issues>
- Full Unraid Guide: `docs/unraid.md`
- Docker Guide: `docs/DOCKER.md`
