# Episode Identifier for Unraid

Complete guide for deploying and using Episode Identifier on Unraid servers.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Installation via Unraid Docker UI](#installation-via-unraid-docker-ui)
- [Configuration](#configuration)
- [Usage Examples](#usage-examples)
- [Integration Patterns](#integration-patterns)
- [Troubleshooting](#troubleshooting)
- [Advanced Configuration](#advanced-configuration)

## Overview

Episode Identifier is a tool that identifies TV episodes from video files using PGS subtitle extraction and OCR. Instead of relying on filenames, it compares actual subtitle content to identify episodes with high accuracy.

**Key Features:**

- Subtitle-based identification (not filename-dependent)
- Fuzzy hashing (CTPH/ssdeep) for robust matching
- Bulk processing with configurable concurrency
- Auto-rename capability with custom templates
- Persistent database for known episodes

## Prerequisites

- Unraid 6.9 or later
- Docker service enabled
- Video files with PGS subtitles (common in Blu-ray rips)
- Sufficient disk space for container image (~1.5GB) and database
- PUID/PGID configured (typically 99/100 for standard Unraid setup)

## Installation via Unraid Docker UI

### Method 1: Using the Template (Recommended)

1. **Add the Template Repository**
   - Open Unraid Web UI
   - Navigate to **Docker** tab
   - Click **Add Container** button at bottom
   - In the **Template repositories** section, click the gear icon
   - Add: `https://github.com/taldelarosa/KnowShow2`

2. **Install from Template**
   - Search for "Episode Identifier" in the templates list
   - Click on the template
   - Configure the paths (see Configuration section below)
   - Click **Apply**

### Method 2: Manual Setup

1. **Open Docker Tab**
   - Navigate to **Docker** in the Unraid Web UI
   - Click **Add Container**

2. **Configure Basic Settings**
   - **Name**: `EpisodeIdentifier`
   - **Repository**: `episodeidentifier/episodeidentifier:latest` (or use local build)
   - **Network Type**: `bridge`

3. **Configure Volume Mappings**

   | Container Path | Host Path | Access Mode | Description |
   |---------------|-----------|-------------|-------------|
   | `/data/videos` | `/mnt/user/media/videos` | Read/Write | Your video files directory |
   | `/data/database` | `/mnt/user/appdata/episodeidentifier/database` | Read/Write | Persistent hash database |
   | `/data/config` | `/mnt/user/appdata/episodeidentifier/config` | Read/Write | Configuration files |

4. **Configure Environment Variables**

   | Variable | Value | Description |
   |----------|-------|-------------|
   | `PUID` | `99` | User ID (99 = nobody user) |
   | `PGID` | `100` | Group ID (100 = users group) |
   | `LOG_LEVEL` | `Information` | Logging verbosity |

5. **Apply and Start**
   - Click **Apply** to create the container
   - Container will start automatically

### Method 3: Building from Source

If you prefer to build the image yourself:

```bash
# Clone the repository
cd /mnt/user/appdata
git clone https://github.com/taldelarosa/KnowShow2.git
cd KnowShow2

# Build the Docker image
docker build -t episodeidentifier-local:latest .

# Use episodeidentifier-local:latest as the Repository in Unraid Docker UI
```

## Configuration

### Directory Structure

After installation, create this directory structure on your Unraid server:

```
/mnt/user/appdata/episodeidentifier/
├── config/
│   └── episodeidentifier.config.json
└── database/
    └── production_hashes.db (created automatically)
```

### Configuration File

The default configuration is created automatically on first run. To customize:

1. Navigate to `/mnt/user/appdata/episodeidentifier/config/`
2. Edit `episodeidentifier.config.json`:

```json
{
  "thresholds": {
    "minimumTextMatchRatio": 0.65,
    "minimumFuzzyHashSimilarity": 85,
    "minimumTotalScore": 75
  },
  "bulkProcessing": {
    "maxConcurrency": 4
  },
  "filenamePatterns": {
    "standardPatterns": [
      "(?<series>.+?)[._ ]S(?<season>\\d{2})E(?<episode>\\d{2})",
      "(?<series>.+?)[._ ](?<season>\\d{1,2})x(?<episode>\\d{2})"
    ],
    "animePatterns": [
      "(?<series>.+?)[._ ]-[._ ](?<episode>\\d{2,3})"
    ]
  },
  "logging": {
    "logLevel": "Information"
  }
}
```

**Configuration Hot-Reload**: Changes to the configuration file are automatically detected and applied without restarting the container.

### PUID/PGID Setup

**Standard Unraid Setup:**

- PUID: `99` (nobody user)
- PGID: `100` (users group)

**To verify your user/group IDs:**

```bash
# SSH into Unraid and run:
id nobody
# Output: uid=99(nobody) gid=100(users) groups=100(users)
```

**Custom User Setup:**

If you use a different user for media files:

```bash
# Find your user ID
id yourusername

# Use the returned uid/gid values in the container environment variables
```

## Usage Examples

Access the container console from Unraid Docker UI or via SSH:

```bash
# From Unraid: Click container icon → Console
# From SSH: docker exec -it EpisodeIdentifier bash
```

### Basic Identification

**Identify a single video file:**

```bash
dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/unknown_episode.mkv \
  --hash-db /data/database/production_hashes.db
```

**Output:**

```
Match found!
Season: 2
Episode: 5
Match Score: 92.5%
```

### Storing Known Episodes

**Store a subtitle hash for future matching:**

```bash
dotnet /app/EpisodeIdentifier.Core.dll \
  --store \
  --input /data/videos/MyShow.S01E01.mkv \
  --season 1 \
  --episode 1 \
  --hash-db /data/database/production_hashes.db
```

**Store from a subtitle file directly:**

```bash
dotnet /app/EpisodeIdentifier.Core.dll \
  --store \
  --input /data/videos/subtitles/S01E01.sup \
  --season 1 \
  --episode 1 \
  --hash-db /data/database/production_hashes.db
```

### Bulk Processing

**Process all videos in a directory:**

```bash
dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/Season1 \
  --hash-db /data/database/production_hashes.db
```

**With custom concurrency:**

Edit the configuration file to adjust `maxConcurrency`, or:

```bash
# The configuration file controls concurrency
# Default: 4 concurrent files
# Range: 1-100
```

**Bulk results summary:**

```
Processing 24 files with concurrency: 4
✓ S01E01.mkv → Season 1, Episode 1 (95% confidence)
✓ S01E02.mkv → Season 1, Episode 2 (93% confidence)
? unknown.mkv → No match found
...
Completed: 22/24 identified (91.7%)
```

### Auto-Rename Feature

**Identify and rename in one command:**

```bash
dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/random_name.mkv \
  --hash-db /data/database/production_hashes.db \
  --rename \
  --rename-template "MyShow S{season:00}E{episode:00}"
```

**Result:** `random_name.mkv` → `MyShow S02E05.mkv`

**Available template placeholders:**

- `{series}` - Series name (from config or parameter)
- `{season}` - Season number
- `{season:00}` - Season with zero-padding
- `{episode}` - Episode number
- `{episode:00}` - Episode with zero-padding

### Verification Commands

**Check database contents:**

```bash
sqlite3 /data/database/production_hashes.db "SELECT season, episode, COUNT(*) FROM subtitle_hashes GROUP BY season, episode ORDER BY season, episode;"
```

**View configuration:**

```bash
cat /data/config/episodeidentifier.config.json
```

**Check container logs:**

```bash
# From Unraid Docker UI: Click container icon → Logs
# From SSH:
docker logs EpisodeIdentifier --tail 100 -f
```

## Integration Patterns

### Integration with Unraid User Scripts

Create automated workflows using the **User Scripts** plugin:

1. **Install User Scripts Plugin**
   - Apps → Search "User Scripts" → Install

2. **Create a New Script**
   - Settings → User Scripts → Add New Script
   - Name: "Process Downloaded Episodes"

3. **Example Script**:

```bash
#!/bin/bash

# Process new downloads folder
docker exec EpisodeIdentifier bash -c "
dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/new_downloads \
  --hash-db /data/database/production_hashes.db \
  --rename \
  --rename-template '{series} S{season:00}E{episode:00}'
"

# Move processed files
# Add your logic here to move identified files to proper locations
```

4. **Schedule the Script**
   - Set schedule: Hourly, Daily, or Custom cron expression

### Post-Processing with Download Clients

**Radarr/Sonarr Custom Script**:

Create `/mnt/user/scripts/episode-identifier-post-process.sh`:

```bash
#!/bin/bash

# Radarr/Sonarr passes these environment variables:
# sonarr_episodefile_path
# radarr_moviefile_path

FILE_PATH="${sonarr_episodefile_path:-${radarr_moviefile_path}}"

if [ -n "$FILE_PATH" ]; then
  docker exec EpisodeIdentifier bash -c "
  dotnet /app/EpisodeIdentifier.Core.dll \
    --input '$FILE_PATH' \
    --hash-db /data/database/production_hashes.db \
    --store \
    --season ${sonarr_episodefile_seasonnumber} \
    --episode ${sonarr_episodefile_episodenumbers}
  "
fi
```

Make executable: `chmod +x /mnt/user/scripts/episode-identifier-post-process.sh`

**Configure in Sonarr**:

- Settings → Connect → Add → Custom Script
- Path: `/mnt/user/scripts/episode-identifier-post-process.sh`
- Triggers: On Import, On Upgrade

### Integration with rclone

Process files as they're uploaded:

```bash
#!/bin/bash

# After rclone sync completes
rclone sync remote:media /mnt/user/media/videos

# Process new files
docker exec EpisodeIdentifier bash -c "
dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/recently_synced \
  --hash-db /data/database/production_hashes.db
"
```

## Troubleshooting

### Container Won't Start

**Check Docker service:**

```bash
/etc/rc.d/rc.docker start
docker ps -a
```

**View container logs:**

```bash
docker logs EpisodeIdentifier
```

**Common issues:**

- Incorrect PUID/PGID values
- Volume paths don't exist
- Insufficient permissions on host directories

**Fix permissions:**

```bash
chown -R nobody:users /mnt/user/appdata/episodeidentifier
chmod -R 755 /mnt/user/appdata/episodeidentifier
```

### Files Not Processing

**Verify video file format:**

```bash
docker exec EpisodeIdentifier ffprobe /data/videos/yourfile.mkv
```

**Check for PGS subtitles:**

```bash
docker exec EpisodeIdentifier bash -c "
mkvmerge -i /data/videos/yourfile.mkv | grep 'HDMV PGS'
"
```

**Enable debug logging:**

Edit `/mnt/user/appdata/episodeidentifier/config/episodeidentifier.config.json`:

```json
{
  "logging": {
    "logLevel": "Debug"
  }
}
```

### Permission Errors

**Symptom:** "Permission denied" when accessing files

**Solution:**

1. Verify PUID/PGID match your Unraid user
2. Check host directory permissions:

   ```bash
   ls -la /mnt/user/media/videos
   ```

3. Update container environment variables if needed
4. Restart container after changes

### Database Corruption

**Symptom:** SQLite errors in logs

**Solution:**

```bash
# Backup current database
cp /mnt/user/appdata/episodeidentifier/database/production_hashes.db \
   /mnt/user/appdata/episodeidentifier/database/production_hashes.db.backup

# Check database integrity
sqlite3 /mnt/user/appdata/episodeidentifier/database/production_hashes.db "PRAGMA integrity_check;"

# If corrupted, restore from backup or start fresh:
mv /mnt/user/appdata/episodeidentifier/database/production_hashes.db \
   /mnt/user/appdata/episodeidentifier/database/production_hashes.db.corrupted

# Restart container (new database will be created)
docker restart EpisodeIdentifier
```

### OCR/Language Issues

**Symptom:** "OCR failed" or incorrect text extraction

**Solution:**

Check installed Tesseract languages:

```bash
docker exec EpisodeIdentifier tesseract --list-langs
```

The container includes English and Japanese by default. For other languages, you may need to customize the Dockerfile.

### Performance Issues

**Symptom:** Slow processing

**Optimization:**

1. Reduce concurrency in config (e.g., `maxConcurrency: 2`)
2. Process during off-peak hours
3. Monitor CPU/RAM usage in Unraid dashboard
4. Consider processing in smaller batches

### No Matches Found

**Troubleshooting steps:**

1. **Verify database has known episodes:**

   ```bash
   sqlite3 /data/database/production_hashes.db "SELECT COUNT(*) FROM subtitle_hashes;"
   ```

2. **Store known episodes first:**

   ```bash
   # Store the correct episode
   dotnet /app/EpisodeIdentifier.Core.dll --store \
     --input /data/videos/known/S01E01.mkv \
     --season 1 --episode 1 \
     --hash-db /data/database/production_hashes.db
   ```

3. **Adjust matching thresholds:**
   Edit config and lower thresholds:

   ```json
   {
     "thresholds": {
       "minimumTextMatchRatio": 0.55,
       "minimumFuzzyHashSimilarity": 75,
       "minimumTotalScore": 65
     }
   }
   ```

4. **Check subtitle extraction:**

   ```bash
   docker exec EpisodeIdentifier bash -c "
   pgsrip /data/videos/yourfile.mkv -o /tmp/test.sup
   "
   ```

## Advanced Configuration

### Multiple Container Instances

Run separate instances for different libraries:

**Container 1: TV Shows**

- Name: `EpisodeIdentifier-TV`
- Videos: `/mnt/user/media/tv`
- Database: `/mnt/user/appdata/episodeidentifier-tv/database`

**Container 2: Anime**

- Name: `EpisodeIdentifier-Anime`
- Videos: `/mnt/user/media/anime`
- Database: `/mnt/user/appdata/episodeidentifier-anime/database`
- Config: Adjusted for anime naming patterns

### Custom Network Configuration

For accessing the container from other containers:

1. Create custom Docker network:

   ```bash
   docker network create media-network
   ```

2. Update container settings in Unraid UI:
   - Network Type: `media-network`

3. Access from other containers:

   ```bash
   docker exec OtherContainer curl http://episodeidentifier:8080
   ```

### Resource Limits

Limit CPU/RAM usage in Unraid Docker UI:

- Click container icon → Edit
- Advanced View → CPU Pinning
- Set CPU cores (e.g., `0-3` for first 4 cores)
- Memory limit: Add `--memory="4g"` to Extra Parameters

### Backup Strategy

**Automated backup script:**

```bash
#!/bin/bash
# /mnt/user/scripts/backup-episode-identifier.sh

BACKUP_DIR="/mnt/user/backups/episodeidentifier"
DATE=$(date +%Y%m%d_%H%M%S)

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Backup database
cp /mnt/user/appdata/episodeidentifier/database/production_hashes.db \
   "$BACKUP_DIR/production_hashes_$DATE.db"

# Backup config
cp /mnt/user/appdata/episodeidentifier/config/episodeidentifier.config.json \
   "$BACKUP_DIR/config_$DATE.json"

# Keep only last 10 backups
cd "$BACKUP_DIR"
ls -t production_hashes_*.db | tail -n +11 | xargs rm -f
ls -t config_*.json | tail -n +11 | xargs rm -f

echo "Backup completed: $DATE"
```

Schedule via User Scripts plugin.

## Additional Resources

- **Project Repository**: <https://github.com/taldelarosa/KnowShow2>
- **Issue Tracker**: <https://github.com/taldelarosa/KnowShow2/issues>
- **Docker Hub**: <https://hub.docker.com/r/episodeidentifier/episodeidentifier>
- **Configuration Examples**: See `/data/config/episodeidentifier.config.example.json`

## Support

For issues or questions:

1. Check this documentation thoroughly
2. Review container logs: `docker logs EpisodeIdentifier`
3. Search existing issues: <https://github.com/taldelarosa/KnowShow2/issues>
4. Create a new issue with:
   - Unraid version
   - Container version/tag
   - Relevant log excerpts
   - Steps to reproduce

## License

Episode Identifier is open source software. See LICENSE file in repository.
