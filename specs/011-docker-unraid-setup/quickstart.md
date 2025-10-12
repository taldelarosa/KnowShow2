# Quickstart: Docker Container on Unraid

**Purpose**: Validate Docker container deployment and functionality  
**Target**: 15 minutes from start to first episode identified  
**Date**: 2025-10-11

## Prerequisites

- Unraid 6.9+ server
- Docker service enabled
- At least 2GB free disk space for container image
- Video files with PGS subtitles (e.g., `.mkv` from Blu-ray rips)

---

## Phase 1: Deploy Container (5 minutes)

### Step 1.1: Add Docker Container via UI

1. Open Unraid web interface
2. Navigate to **Docker** tab
3. Click **Add Container**
4. Click **Template repositories**
5. Add URL: `https://raw.githubusercontent.com/taldelarosa/KnowShow2/main/docker/unraid-template.xml`
6. Or manually configure:

**Container Settings**:
- **Name**: `episode-identifier`
- **Repository**: `taldelarosa/episode-identifier:latest`
- **Network Type**: `Bridge`

**Volume Mappings**:
```
Container Path: /videos       → Host Path: /mnt/user/media/videos        (Read/Write)
Container Path: /data         → Host Path: /mnt/user/appdata/episode-identifier  (Read/Write)
Container Path: /config       → Host Path: /mnt/user/appdata/episode-identifier/config  (Read-Only)
```

**Environment Variables**:
```
PUID=99
PGID=100
TZ=UTC
```

### Step 1.2: Start Container

1. Click **Apply**
2. Wait for image download (30-60 seconds for first time)
3. Container status should show **Started** with green icon
4. Health check should show **Healthy** (wait 30 seconds after start)

**Validation**: 
```bash
docker ps | grep episode-identifier
# Should show: UP, healthy
```

---

## Phase 2: Configure Application (3 minutes)

### Step 2.1: Create Configuration Directory (Optional)

```bash
mkdir -p /mnt/user/appdata/episode-identifier/config
```

### Step 2.2: Create Config File (Optional)

```bash
cat > /mnt/user/appdata/episode-identifier/config/episodeidentifier.config.json << 'EOF'
{
  "version": "2.0",
  "maxConcurrency": 3,
  "matchConfidenceThreshold": 0.6,
  "renameConfidenceThreshold": 0.7,
  "fuzzyHashThreshold": 75,
  "hashingAlgorithm": "CTPH",
  "filenamePatterns": {
    "primaryPattern": "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
  },
  "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
}
EOF
```

**Note**: Container uses defaults if config not provided

---

## Phase 3: First Episode Identification (5 minutes)

### Step 3.1: Prepare Test Video

Place a known episode in videos directory:
```bash
# Example: Copy Bones S01E01 to videos directory
cp "/path/to/Bones S01E01.mkv" /mnt/user/media/videos/
```

**Requirements**:
- Video MUST have PGS subtitles
- Filename should indicate series/season/episode (for learning)

### Step 3.2: Store Known Episode Subtitle

First, teach the system about a known episode:

```bash
docker exec episode-identifier \
  dotnet /app/EpisodeIdentifier.Core.dll \
  --input /videos/Bones\ S01E01.mkv \
  --hash-db /data/production_hashes.db \
  --store \
  --series "Bones" \
  --season 1 \
  --episode 1
```

**Expected Output**:
```
Extracting subtitles from video...
Calculating fuzzy hash...
Storing hash for Bones S01E01
✓ Subtitle hash stored successfully
```

**Validation**:
```bash
# Check database was created
ls -lh /mnt/user/appdata/episode-identifier/production_hashes.db
# Should show file size > 0
```

### Step 3.3: Identify Unknown Episode

Copy an unidentified episode (same series, different episode):
```bash
cp "/path/to/unknown_episode.mkv" /mnt/user/media/videos/test_video.mkv
```

Run identification:
```bash
docker exec episode-identifier \
  dotnet /app/EpisodeIdentifier.Core.dll \
  --input /videos/test_video.mkv \
  --hash-db /data/production_hashes.db
```

**Expected Output** (if match found):
```
Extracting subtitles from video...
Calculating fuzzy hash...
Comparing against database...
✓ Match found: Bones S01E02 (confidence: 85%)
```

**Expected Output** (if no match):
```
Extracting subtitles from video...
Calculating fuzzy hash...
Comparing against database...
✗ No match found in database
```

---

## Phase 4: Bulk Processing (2 minutes)

### Step 4.1: Process Multiple Files

```bash
docker exec episode-identifier \
  dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /videos \
  --hash-db /data/production_hashes.db
```

**Expected Output**:
```
Processing 5 video files...
[1/5] Bones S01E01.mkv → Already known
[2/5] test_video.mkv → Identified as Bones S01E02
[3/5] another_video.mkv → No match found
[4/5] ...
Completed: 3 identified, 2 unknown
```

### Step 4.2: Auto-Rename Identified Files

```bash
docker exec episode-identifier \
  dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /videos \
  --hash-db /data/production_hashes.db \
  --rename
```

**Expected Behavior**:
- Files with confident matches are renamed
- Original files preserved (rename, not move)
- Unmatched files left unchanged

---

## Validation Checklist

- [ ] Container started and shows "Healthy" status
- [ ] Database file created in `/mnt/user/appdata/episode-identifier/`
- [ ] Known episode stored successfully
- [ ] Unknown episode identified (or "no match" if expected)
- [ ] Bulk processing completed without errors
- [ ] File ownership matches PUID/PGID (99:100)
- [ ] Container logs accessible via Unraid Docker UI

---

## Common Issues & Solutions

### Issue: "Permission denied" errors

**Cause**: PUID/PGID mismatch with file ownership  
**Solution**:
```bash
# Check file ownership
ls -ln /mnt/user/media/videos

# Update PUID/PGID in container settings to match
# Or change file ownership:
chown -R 99:100 /mnt/user/media/videos
```

### Issue: "No PGS subtitles found"

**Cause**: Video file lacks PGS subtitle track  
**Solution**:
```bash
# Check subtitle tracks
docker exec episode-identifier mkvmerge -i /videos/video.mkv

# Look for "subtitles (PGS)" in output
```

### Issue: Container status "Unhealthy"

**Cause**: Application crashed or dependencies missing  
**Solution**:
```bash
# Check container logs
docker logs episode-identifier

# Verify --version command works
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --version
```

### Issue: Database not persisting

**Cause**: /data volume not mounted correctly  
**Solution**:
- Verify volume mapping in container settings
- Check host path exists: `ls -la /mnt/user/appdata/episode-identifier`

---

## Next Steps

After successful quickstart:

1. **Build Subtitle Database**: Store known episodes for your series
2. **Bulk Processing**: Process entire libraries with `--bulk-identify`
3. **Auto-Rename**: Enable `--rename` for automatic file organization
4. **Integration**: Set up post-processing scripts (see docs/unraid.md)
5. **Monitoring**: Check container logs periodically for errors

---

## Success Criteria

✅ Quickstart is successful when:

1. Container starts and shows healthy status
2. Database file persists across container restarts
3. At least one episode successfully stored
4. At least one episode successfully identified (or no match confirmed)
5. File permissions allow read/write operations
6. All commands execute without permission errors

---

## Cleanup (Optional)

To remove test container:
```bash
# Stop container
docker stop episode-identifier

# Remove container (preserves volumes)
docker rm episode-identifier

# Remove image
docker rmi taldelarosa/episode-identifier

# Delete appdata (WARNING: destroys database)
rm -rf /mnt/user/appdata/episode-identifier
```

---

## Troubleshooting Commands

```bash
# View container logs
docker logs episode-identifier

# Interactive shell
docker exec -it episode-identifier /bin/bash

# Check application version
docker exec episode-identifier dotnet /app/EpisodeIdentifier.Core.dll --version

# Check file permissions
docker exec episode-identifier ls -la /data /videos /config

# Test database access
docker exec episode-identifier sqlite3 /data/production_hashes.db ".tables"
```

---

**Estimated Time**: 15 minutes  
**Difficulty**: Beginner (basic Docker/Unraid knowledge required)
