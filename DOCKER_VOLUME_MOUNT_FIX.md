# Docker Volume Mount Fix - Summary

## Date

October 15, 2025

## Problem Statement

Unraid container was experiencing two critical issues that made it unusable:

1. **Configuration parsing failures** with errors like:
   - "Version is required"
   - "FuzzyHashThreshold is required when HashingAlgorithm is CTPH"
   - "PrimaryPattern must contain named capture groups"

2. **Volume mounts ignored** - Users couldn't modify config or database files because the app was reading from `/app` instead of `/data`

## Root Causes Identified

### Cause 1: JSON Comments

- Template file `episodeidentifier.config.example.json` contained `//` comments
- Standard JSON parsers don't support comments
- System.Text.Json failed to parse, causing all values to be missing

### Cause 2: Environment Variables Not Read  

- Application hardcoded paths to `/app` directory
- Never read `CONFIG_PATH` or `HASH_DB_PATH` environment variables
- Docker volume mounts to `/data/*` were completely ignored

## Solutions Implemented

### 1. Created Clean JSON Template

**File:** `episodeidentifier.config.template.json`

- Valid JSON without comments
- Contains all required fields with sensible defaults
- Used by Docker entrypoint to create initial config

### 2. Added Environment Variable Support

**File:** `src/EpisodeIdentifier.Core/Program.cs`

**Changes:**

```csharp
// Read HASH_DB_PATH for database location
var defaultHashDbPath = Environment.GetEnvironmentVariable("HASH_DB_PATH") ?? "production_hashes.db";
hashDbOption.SetDefaultValue(new FileInfo(defaultHashDbPath));

// Read CONFIG_PATH for configuration location  
var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH");
var fuzzyHashConfigService = new ConfigurationService(
    loggerFactory.CreateLogger<ConfigurationService>(), 
    configFilePath: configPath);
```

**Result:** Application now respects Docker environment variables:

- `CONFIG_PATH=/data/config/episodeidentifier.config.json`
- `HASH_DB_PATH=/data/database/production_hashes.db`

### 3. Updated Build Configuration

**File:** `Dockerfile`

- Changed to copy clean template instead of commented example
- Template stored at `/app/config.template.json` (read-only)
- Entrypoint creates user config at `/data/config/` (volume-mounted, writable)

**File:** `.dockerignore`

- Blocks `episodeidentifier.config.json` from build context
- Allows `episodeidentifier.config.template.json` only
- Prevents accidentally including user configs in image

## Testing Required

### 1. Fresh Container (No Existing Config)

```bash
docker-compose down
docker-compose build --no-cache
docker-compose up -d
docker logs episodeidentifier
```

**Expected:**

- ✅ "Configuration file ready at: /data/config/episodeidentifier.config.json"
- ✅ "Episode Identifier Ready"
- ❌ NO validation errors

### 2. Existing Container (With Old Config)

```bash
# Backup and remove old config with comments
mv /path/to/docker-data/config/episodeidentifier.config.json /path/to/backup/
docker-compose restart
```

**Expected:**

- New config auto-created from template
- Application starts successfully

### 3. Config Modification Test

```bash
# Edit /data/config/episodeidentifier.config.json
# Change maxConcurrency from 3 to 5
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll config show
```

**Expected:**

- Configuration shows maxConcurrency: 5
- Changes are persisted and loaded

## Files Modified

| File | Change Type | Description |
|------|-------------|-------------|
| `episodeidentifier.config.template.json` | Created | Clean JSON template without comments |
| `src/EpisodeIdentifier.Core/Program.cs` | Modified | Read CONFIG_PATH and HASH_DB_PATH env vars |
| `Dockerfile` | Modified | Copy clean template instead of example |
| `.dockerignore` | Modified | Allow template, block example with comments |
| `DOCKER_CONFIG_JSON_FIX.md` | Created | Detailed documentation of the fix |

## User Impact

### Before Fix

- ❌ Container failed to start
- ❌ Configuration always invalid
- ❌ Volume mounts had no effect
- ❌ Unable to modify config or database
- ❌ Had to manually copy files into running container

### After Fix

- ✅ Container starts successfully
- ✅ Configuration loads properly
- ✅ Volume mounts work as expected
- ✅ Users can edit config in `/data/config/`
- ✅ Database persists in `/data/database/`
- ✅ Changes take effect immediately (hot-reload)

## Deployment Steps

### For Users Running the Container

1. Pull latest code from repository
2. Rebuild Docker image: `docker-compose build --no-cache`
3. Remove old config if it has comments: `rm docker-data/config/episodeidentifier.config.json`
4. Start container: `docker-compose up -d`
5. Verify in logs: `docker logs episodeidentifier`

### For Unraid Users

1. Stop the EpisodeIdentifier container
2. Remove the container (not the volumes)
3. Delete the old image
4. Re-create container from updated Dockerfile
5. Start container and verify logs

## Technical Notes

### Why Comments Aren't Supported

- JSON specification (RFC 8259) does not include comments
- `System.Text.Json` follows the spec strictly for security/performance
- JSON5/JSONC are non-standard extensions
- We keep the commented `.example` file for documentation only

### Volume Mount Architecture

```
Container:
/app/                           # Application binaries (read-only)
/app/config.template.json       # Clean JSON template (read-only)
/data/config/                   # User config (volume-mounted, read-write)
/data/database/                 # User database (volume-mounted, read-write)
/data/videos/                   # User videos (volume-mounted, read-write)

Environment:
CONFIG_PATH=/data/config/episodeidentifier.config.json
HASH_DB_PATH=/data/database/production_hashes.db
```

### Backwards Compatibility

- Legacy AppConfigService still supported
- Old config files without version field will be migrated
- Existing databases continue to work

## Related Issues

- Configuration validation failures in Docker
- Volume mounts not working in Unraid
- Unable to modify config after container start
- Database not persisting between restarts

## Verification Checklist

- [ ] Container starts without validation errors
- [ ] Config file created in `/data/config/`
- [ ] Config changes persist after restart
- [ ] Database persists in `/data/database/`
- [ ] `docker logs` shows "Episode Identifier Ready"
- [ ] Can run commands: `docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll --help`
- [ ] Hot-reload works when editing config file
