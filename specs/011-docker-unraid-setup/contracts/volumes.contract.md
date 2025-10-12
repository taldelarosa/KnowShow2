# Contract: Volume Mounts

**Purpose**: Define volume mount interface and behavior  
**Date**: 2025-10-11

## Volume Mount Contracts

### `/videos` - Video Files Directory

**Purpose**: Read/write access to video files for processing

**Requirements**:
- **Mode**: `rw` (read-write) - Required for rename functionality
- **Owner**: Must match PUID/PGID for file operations
- **Contents**: Video files (.mkv, .mp4, etc.) with PGS subtitles

**Behavior**:
- Application reads video files from this directory
- Application can write (rename) files in this directory
- Subdirectories are processed in bulk operations

**Test Cases**:
1. Read video file → SUCCESS
2. Rename video file → SUCCESS  
3. Create/delete files → SUCCESS (if permissions allow)
4. Handle missing directory → FAIL gracefully with error message

---

### `/data` - Persistent Data Directory

**Purpose**: Persistent storage for SQLite database and application state

**Requirements**:
- **Mode**: `rw` (read-write) - Required for database updates
- **Owner**: Must match PUID/PGID
- **Contents**: `production_hashes.db` (SQLite database)
- **Persistence**: Must survive container restarts

**Behavior**:
- Database created automatically if not exists
- Database updated during identification and storage operations
- Configuration file can optionally be stored here

**Test Cases**:
1. Container start with empty `/data` → Creates new database
2. Container restart → Database persists, existing hashes remain
3. Database write → File ownership matches PUID/PGID
4. Corrupt database → Application handles gracefully

---

### `/config` - Configuration Directory

**Purpose**: Read-only access to configuration JSON

**Requirements**:
- **Mode**: `ro` (read-only) - Configuration managed by user
- **Owner**: Readable by PUID/PGID
- **Contents**: `episodeidentifier.config.json`
- **Optional**: Container uses defaults if not mounted

**Behavior**:
- Configuration loaded at startup
- Hot-reload: Application watches for file changes
- Missing config → Use built-in defaults

**Test Cases**:
1. Custom config provided → Application uses custom settings
2. No config mounted → Application uses defaults
3. Invalid JSON → Application fails fast with clear error
4. Config file updated → Application reloads without restart

---

## Permission Requirements

### PUID/PGID Mapping

All file operations inside container MUST run as:
- User ID = `$PUID` (environment variable)
- Group ID = `$PGID` (environment variable)

**Why**: Unraid uses specific UIDs (99=nobody, 100=users). Files created in container must match host ownership.

**Implementation**: entrypoint.sh changes appuser UID/GID before executing application

---

## Volume Mount Validation

### Pre-flight Checks

Container entrypoint SHOULD validate:
1. `/data` is writable (critical)
2. `/videos` exists and is readable (if specified)
3. `/config/episodeidentifier.config.json` is valid JSON (if exists)

**On Failure**: Log clear error message and exit with non-zero code

### Example Error Messages

```
ERROR: /data directory is not writable. Check volume mount and permissions.
ERROR: /config/episodeidentifier.config.json is invalid JSON: unexpected token at line 5
WARNING: /videos directory not mounted. Use --input with absolute paths.
```

---

## Directory Structure Examples

### Minimal Setup (database only)

```bash
docker run -v /mnt/user/appdata/episode-identifier:/data episode-identifier
```

Result:
- Database persists in `/mnt/user/appdata/episode-identifier/production_hashes.db`
- Videos accessed via absolute paths in --input argument

---

### Full Setup (all volumes)

```bash
docker run \
  -v /mnt/user/media/videos:/videos \
  -v /mnt/user/appdata/episode-identifier:/data \
  -v /mnt/user/appdata/episode-identifier/config:/config:ro \
  episode-identifier
```

Result:
- Videos accessible at `/videos` inside container
- Database persists across restarts
- Custom configuration loaded from file

---

## Testing Contract

### Integration Tests

```bash
# Test 1: Database persistence
docker run --name test1 -v /tmp/data:/data episode-identifier --input video.mkv --hash-db /data/hashes.db
docker rm test1
docker run --name test2 -v /tmp/data:/data episode-identifier --input video.mkv --hash-db /data/hashes.db
# Expected: Database from test1 still contains hashes

# Test 2: PUID/PGID ownership
docker run --rm -e PUID=1000 -e PGID=1000 -v /tmp/data:/data episode-identifier touch /data/testfile
stat -c '%u:%g' /tmp/data/testfile
# Expected: 1000:1000

# Test 3: Read-only config
docker run --rm -v /tmp/config:/config:ro episode-identifier
docker exec <container> touch /config/newfile
# Expected: Operation not permitted (read-only)
```

---

## Failure Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| `/data` not mounted | Warn: "Database will be ephemeral", continue |
| `/data` not writable | Error and exit immediately |
| `/videos` not mounted | Warn: "Use --input with absolute paths", continue |
| `/config` invalid JSON | Error: Show parsing error with line number, exit |
| Disk full | Error: "Cannot write to /data: disk full", exit |

---

## Security Considerations

1. **Never run as root**: entrypoint.sh switches to PUID/PGID
2. **Validate paths**: Reject volume mounts that point outside allowed directories
3. **Read-only where possible**: Config directory mounted `ro` by default

---

## Compatibility Notes

- Paths inside container are fixed (`/videos`, `/data`, `/config`)
- Host paths are user-configurable via volume mounts
- Application code does NOT need awareness of containerization
- Existing CLI arguments work unchanged
