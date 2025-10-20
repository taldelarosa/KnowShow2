# Docker Configuration Fix - JSON Comments Issue

## Problem

The Unraid container was failing with two critical issues:

1. **JSON Comments**: The config template contained JSON comments (lines starting with `//`), which are not valid in standard JSON
2. **Ignored Volumes**: The application was looking for config/database files in `/app` directory (baked into the image) instead of reading from `/data` volumes (user-modifiable)

This caused:

- Configuration validation errors (Version required, FuzzyHashThreshold required, etc.)
- Changes to mounted volumes being completely ignored
- Poor user experience as config changes had no effect

## Root Cause

### Issue 1: Invalid JSON

The `episodeidentifier.config.example.json` file contained JSON comments (lines starting with `//`), which are **not valid in standard JSON**. When the Docker entrypoint script copied this file as the default configuration template, the .NET JSON parser (`System.Text.Json`) failed to parse it.

### Issue 2: Environment Variables Not Read

The application defaulted to looking in `AppContext.BaseDirectory` (`/app`) for config and database files, completely ignoring the `CONFIG_PATH` and `HASH_DB_PATH` environment variables set in the Docker container.

**In ConfigurationService.cs:**

```csharp
// Old code - always used /app directory:
_configFilePath = configFilePath ?? Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
```

**In Program.cs:**

```csharp
// Old code - never read environment variables:
hashDbOption.SetDefaultValue(new FileInfo("production_hashes.db"));
var fuzzyHashConfigService = new ConfigurationService(...); // No config path passed
```

## Solution

### 1. Clean JSON Template

Created `episodeidentifier.config.template.json` with:

- **No JSON comments** - pure, valid JSON
- All required configuration fields
- Default values that work for most use cases

### 2. Environment Variable Support

Updated the application to read environment variables:

**Program.cs changes:**

```csharp
// Read HASH_DB_PATH environment variable
var defaultHashDbPath = Environment.GetEnvironmentVariable("HASH_DB_PATH") ?? "production_hashes.db";
hashDbOption.SetDefaultValue(new FileInfo(defaultHashDbPath));

// Read CONFIG_PATH environment variable
var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH");
var fuzzyHashConfigService = new ConfigurationService(..., configFilePath: configPath);
```

This ensures the application now respects the Docker environment variables:

- `CONFIG_PATH=/data/config/episodeidentifier.config.json`
- `HASH_DB_PATH=/data/database/production_hashes.db`

### 3. Updated .dockerignore

Updated to allow the clean template while blocking the commented example:

```
episodeidentifier.config.json
!episodeidentifier.config.template.json
```

### Files Changed

1. **Created:** `episodeidentifier.config.template.json` - Clean JSON template without comments
2. **Updated:** `Dockerfile` - Now copies the clean template instead of the example file
3. **Updated:** `Program.cs` - Now reads `CONFIG_PATH` and `HASH_DB_PATH` environment variables
4. **Updated:** `.dockerignore` - Allows clean template, blocks commented example
5. **Preserved:** `episodeidentifier.config.example.json` - Kept for documentation purposes with helpful comments

## For Users

After rebuilding the Docker image:

1. The container will automatically create a valid config file on first run
2. If you already have a config file with comments, replace it with valid JSON
3. Use `episodeidentifier.config.template.json` as a reference for valid JSON format
4. Use `episodeidentifier.config.example.json` for documentation (but remember to remove comments if copying)

## Rebuild Instructions

### Docker Compose

```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### Unraid

1. Stop the container
2. Remove the container
3. Delete the old image
4. Rebuild from the updated Dockerfile
5. Start the container

### Manual Docker Build

```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd
docker build -t episodeidentifier:latest .
docker run -d --name episodeidentifier \
  -e PUID=99 -e PGID=100 \
  -v /path/to/videos:/data/videos:rw \
  -v /path/to/database:/data/database:rw \
  -v /path/to/config:/data/config:rw \
  episodeidentifier:latest
```

## Verification

After starting the container, check logs:

```bash
docker logs episodeidentifier
```

You should see:

- ✅ "Configuration file ready at: /data/config/episodeidentifier.config.json"
- ✅ "Episode Identifier Ready"
- ❌ NO validation errors about missing Version, FuzzyHashThreshold, or pattern groups

## Technical Notes

- **System.Text.Json** does NOT support comments by default (requires `JsonDocumentOptions.CommentHandling`)
- The application uses standard JSON deserialization without comment handling enabled
- This is by design for performance and security reasons
- Comments in JSON are a non-standard extension (JSON5, JSONC), not part of official JSON spec

## Related Issues

- Docker container startup failures in Unraid
- Configuration validation errors despite valid-looking config
- All configuration values appearing as "not set" or "required"

## Date

October 15, 2025
