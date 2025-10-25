# Updating Configuration on Unraid Server

The identification is working correctly with 40% thresholds locally, but your Unraid server is using the old configuration with higher thresholds (70-85%).

## Quick Fix for Unraid

### Option 1: Edit Config File Directly (Recommended)

SSH into your Unraid server and edit:
```bash
nano /mnt/user/appdata/episodeidentifier/config/episodeidentifier.config.json
```

Change these sections to use 40% thresholds:

```json
  "matchingThresholds": {
    "textBased": {
      "matchConfidence": 0.4,
      "renameConfidence": 0.4,
      "fuzzyHashSimilarity": 40
    },
    "pgs": {
      "matchConfidence": 0.4,
      "renameConfidence": 0.4,
      "fuzzyHashSimilarity": 40
    },
    "vobSub": {
      "matchConfidence": 0.4,
      "renameConfidence": 0.4,
      "fuzzyHashSimilarity": 40
    }
  },
  "embeddingThresholds": {
    "textBased": {
      "embedSimilarity": 0.40,
      "matchConfidence": 0.40,
      "renameConfidence": 0.40
    },
    "pgs": {
      "embedSimilarity": 0.40,
      "matchConfidence": 0.40,
      "renameConfidence": 0.40
    },
    "vobSub": {
      "embedSimilarity": 0.40,
      "matchConfidence": 0.40,
      "renameConfidence": 0.40
    }
  },
```

Save (Ctrl+O, Enter, Ctrl+X) - No container restart needed (hot-reload).

### Option 2: Copy Updated Config from WSL

From your WSL terminal:
```bash
scp /mnt/c/Users/Ragma/KnowShow_Specd/episodeidentifier.config.json root@10.0.0.200:/mnt/user/appdata/episodeidentifier/config/episodeidentifier.config.json
```

## Then Test on Unraid

```bash
docker exec KnowShow dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/Bones/1/BONES_S1_D1-lphiHj/aaaaa.mkv \
  --hash-db /data/database/production_hashes.db \
  --series "Bones" --season 1
```

Should now identify as: **Bones S01E03** with ~67-80% confidence.

## Bulk Identify

```bash
docker exec KnowShow dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/Bones/1/BONES_S1_D1-lphiHj \
  --hash-db /data/database/production_hashes.db \
  --series "Bones" --season 1
```

This will rename files to proper Bones episode names.
