# Contract: Environment Variables

**Purpose**: Define environment variable interface and behavior  
**Date**: 2025-10-11

## Required Environment Variables

### `PUID` - Process User ID

**Type**: Integer  
**Default**: `99` (Unraid default for 'nobody')  
**Range**: 1-65535  
**Purpose**: Set UID for file operations inside container

**Behavior**:

- Container entrypoint sets appuser UID to this value
- All file operations run as this UID
- Files created in volumes will have this ownership

**Test**:

```bash
docker run -e PUID=1000 -v /tmp/test:/data episode-identifier touch /data/file
stat -c '%u' /tmp/test/file  # Should output: 1000
```

---

### `PGID` - Process Group ID

**Type**: Integer  
**Default**: `100` (Unraid default for 'users')  
**Range**: 1-65535  
**Purpose**: Set GID for file operations inside container

**Behavior**:

- Container entrypoint sets appuser GID to this value
- All file operations run with this GID
- Files created in volumes will have this group ownership

**Test**:

```bash
docker run -e PGID=1000 -v /tmp/test:/data episode-identifier touch /data/file
stat -c '%g' /tmp/test/file  # Should output: 1000
```

---

## Optional Environment Variables

### `TZ` - Timezone

**Type**: String (IANA timezone identifier)  
**Default**: `UTC`  
**Examples**: `America/New_York`, `Europe/London`, `Asia/Tokyo`  
**Purpose**: Set timezone for container and application logs

**Behavior**:

- Sets system timezone via `/etc/localtime`
- Application timestamps use this timezone
- Log entries show times in specified zone

**Test**:

```bash
docker run -e TZ=America/New_York episode-identifier date
# Should show time in EST/EDT
```

---

### `LOG_LEVEL` - Logging Verbosity

**Type**: String (enum)  
**Default**: `Information`  
**Valid Values**: `Debug`, `Information`, `Warning`, `Error`  
**Purpose**: Control application log verbosity

**Behavior**:

- Passed to .NET logging configuration
- Controls which log messages are output
- Lower levels include higher levels (Debug includes Information, Warning, Error)

**Test**:

```bash
docker run -e LOG_LEVEL=Debug episode-identifier --help
# Should show debug-level log messages
```

---

## Environment Variable Validation

### Entrypoint Validation

The `entrypoint.sh` script MUST validate:

1. **PUID is numeric**: `[[ "$PUID" =~ ^[0-9]+$ ]]`
2. **PGID is numeric**: `[[ "$PGID" =~ ^[0-9]+$ ]]`
3. **PUID in valid range**: 1 ≤ PUID ≤ 65535
4. **PGID in valid range**: 1 ≤ PGID ≤ 65535

**On Invalid Value**: Log error and use default (99/100)

---

## Unraid-Specific Defaults

### Standard Unraid UIDs/GIDs

| User | UID | GID | Description |
|------|-----|-----|-------------|
| root | 0 | 0 | Root user (NEVER use for file operations) |
| nobody | 99 | 99 | Default unprivileged user |
| users | - | 100 | Default users group |

**Recommended**: Use PUID=99, PGID=100 for Unraid deployments

---

## Docker Compose Example

```yaml
services:
  episode-identifier:
    image: taldelarosa/episode-identifier:latest
    environment:
      - PUID=99
      - PGID=100
      - TZ=America/New_York
      - LOG_LEVEL=Information
    volumes:
      - /mnt/user/media/videos:/videos
      - /mnt/user/appdata/episode-identifier:/data
```

---

## Unraid Template Mapping

Environment variables map to Unraid template XML:

```xml
<Config Name="PUID" Target="PUID" Default="99" Type="Variable" Required="true" />
<Config Name="PGID" Target="PGID" Default="100" Type="Variable" Required="true" />
<Config Name="Timezone" Target="TZ" Default="UTC" Type="Variable" Required="false" />
<Config Name="Log Level" Target="LOG_LEVEL" Default="Information" Type="Variable" Required="false" />
```

---

## Security Considerations

1. **Never run as root**: PUID/PGID should never be 0
2. **Match host permissions**: Use same UID/GID as host file owner
3. **Validate inputs**: Prevent command injection via environment variables

---

## Testing Contract

### Test Matrix

| PUID | PGID | Expected Behavior |
|------|------|-------------------|
| 99 | 100 | ✅ Standard Unraid, files owned by nobody:users |
| 1000 | 1000 | ✅ Custom user, files owned by 1000:1000 |
| 0 | 0 | ⚠️ Warn: Running as root (security risk) |
| "abc" | 100 | ❌ Error: Invalid PUID, fallback to default |
| 99 | "xyz" | ❌ Error: Invalid PGID, fallback to default |

---

## Error Handling

### Invalid PUID/PGID

```bash
# Invalid input
docker run -e PUID=invalid episode-identifier

# Expected output:
WARNING: Invalid PUID 'invalid', using default 99
```

### Missing Variables

```bash
# No environment variables
docker run episode-identifier

# Expected: Uses defaults (PUID=99, PGID=100)
```

---

## Compatibility Notes

- Environment variables are Docker standard (not Unraid-specific)
- PUID/PGID pattern common in LinuxServer.io images
- TZ follows IANA timezone database standard
- LOG_LEVEL follows .NET logging level names
