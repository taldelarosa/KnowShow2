# Data Model: Docker Container Configuration

**Feature**: Docker Containerization for Unraid Deployment  
**Date**: 2025-10-11

## Overview

This document defines the configuration model for the Episode Identifier Docker container. Unlike traditional data models that represent application entities, this model describes the container's runtime configuration, volume mappings, and environment variables.

---

## Entity: Container Configuration

### Description

The container configuration defines how the Episode Identifier application runs within a Docker container on Unraid, including volume mounts, environment variables, and startup behavior.

### Attributes

| Attribute | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `image` | string | Yes | `taldelarosa/episode-identifier:latest` | Docker image repository and tag |
| `container_name` | string | Yes | `episode-identifier` | Unique container name on host |
| `restart_policy` | enum | Yes | `unless-stopped` | Container restart behavior: `no`, `always`, `unless-stopped`, `on-failure` |
| `volumes` | Volume[] | Yes | (see below) | Volume mount definitions |
| `environment` | Environment[] | Yes | (see below) | Environment variable definitions |
| `network_mode` | string | No | `bridge` | Docker network mode |

### Validation Rules

- `container_name` must be unique on the Docker host
- `image` must be a valid Docker Hub or registry URL
- At least 2 volumes must be configured (videos, data)
- PUID and PGID must be positive integers

---

## Entity: Volume Mount

### Description

Defines a mapping between host directory and container directory for file access.

### Attributes

| Attribute | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `host_path` | string | Yes | - | Absolute path on Unraid host |
| `container_path` | string | Yes | - | Absolute path inside container |
| `mode` | enum | Yes | `rw` | Access mode: `ro` (read-only), `rw` (read-write) |
| `purpose` | enum | Yes | - | Mount purpose: `videos`, `data`, `config` |

### Standard Volume Definitions

| Purpose | Container Path | Default Host Path | Mode | Description |
|---------|---------------|-------------------|------|-------------|
| `videos` | `/videos` | `/mnt/user/media/videos` | `rw` | Video files to process |
| `data` | `/data` | `/mnt/user/appdata/episode-identifier` | `rw` | Database and persistent state |
| `config` | `/config` | `/mnt/user/appdata/episode-identifier/config` | `ro` | Configuration JSON file |

### Validation Rules

- `host_path` must be absolute path starting with `/`
- `container_path` must be absolute path starting with `/`
- `host_path` must exist and be accessible on host
- Videos volume must be `rw` to support rename functionality
- Data volume must be `rw` to persist database changes

---

## Entity: Environment Variable

### Description

Runtime configuration passed to container via environment variables.

### Attributes

| Attribute | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `name` | string | Yes | - | Variable name |
| `value` | string | Yes | - | Variable value |
| `description` | string | No | - | Human-readable purpose |

### Standard Environment Variables

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `PUID` | integer | Yes | `99` | User ID for file operations (Unraid: 99=nobody) |
| `PGID` | integer | Yes | `100` | Group ID for file operations (Unraid: 100=users) |
| `TZ` | string | No | `UTC` | Timezone for logging (e.g., `America/New_York`) |
| `LOG_LEVEL` | enum | No | `Information` | Logging verbosity: `Debug`, `Information`, `Warning`, `Error` |

### Validation Rules

- `PUID` and `PGID` must be integers between 1 and 65535
- `TZ` must be valid IANA timezone identifier
- `LOG_LEVEL` must match .NET logging levels

---

## Entity: Application Configuration (JSON)

### Description

Application-specific settings stored in `episodeidentifier.config.json` file (mounted via config volume).

### Attributes

| Attribute | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `version` | string | Yes | `"2.0"` | Config schema version |
| `maxConcurrency` | integer | Yes | `3` | Parallel processing threads |
| `matchConfidenceThreshold` | float | Yes | `0.6` | Minimum match confidence |
| `renameConfidenceThreshold` | float | Yes | `0.7` | Minimum confidence for auto-rename |
| `fuzzyHashThreshold` | integer | Yes | `75` | Fuzzy hash similarity threshold |
| `hashingAlgorithm` | enum | Yes | `"CTPH"` | Algorithm: `"CTPH"` or `"ssdeep"` |
| `filenamePatterns` | object | Yes | (see spec) | Regex patterns for episode detection |
| `filenameTemplate` | string | Yes | (see spec) | Output filename format |

### Validation Rules

- `maxConcurrency` must be between 1 and 100
- Confidence thresholds must be between 0.0 and 1.0
- `fuzzyHashThreshold` must be between 0 and 100
- Configuration file must be valid JSON

### Hot-Reload Support

The application watches the config file and reloads changes without container restart (existing feature preserved in container).

---

## Entity: Health Check

### Description

Container health status validation performed by Docker engine.

### Attributes

| Attribute | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `test` | string | Yes | `dotnet ... --version` | Health check command |
| `interval` | duration | Yes | `30s` | Time between checks |
| `timeout` | duration | Yes | `3s` | Max time for check to complete |
| `start_period` | duration | Yes | `5s` | Grace period during startup |
| `retries` | integer | Yes | `3` | Consecutive failures before unhealthy |

### States

- **starting**: Container starting, within start_period
- **healthy**: Health check passing
- **unhealthy**: Health check failed >= retries times

---

## Relationships

```
Container Configuration (1)
  ├── has many (1..3) → Volume Mounts
  ├── has many (2..4) → Environment Variables
  ├── references one → Application Configuration (JSON file)
  └── performs → Health Check (continuous)

Volume Mount (data purpose)
  └── contains → SQLite Database (production_hashes.db)

Volume Mount (config purpose)
  └── contains → Application Configuration (episodeidentifier.config.json)
```

---

## State Transitions

```
Container Lifecycle:
  created → starting → healthy → running
                    ↓
                 unhealthy → stopped
                           ↓
                      (user restart) → starting

Volume Mount Lifecycle:
  defined → validated → mounted → accessible
                     ↓
                  (missing/permission error) → failed
```

---

## Example: Complete Configuration

```yaml
# docker-compose.yml equivalent (for reference)
services:
  episode-identifier:
    image: taldelarosa/episode-identifier:latest
    container_name: episode-identifier
    restart: unless-stopped
    
    volumes:
      - /mnt/user/media/videos:/videos:rw
      - /mnt/user/appdata/episode-identifier:/data:rw
      - /mnt/user/appdata/episode-identifier/config:/config:ro
    
    environment:
      - PUID=99
      - PGID=100
      - TZ=America/New_York
      - LOG_LEVEL=Information
    
    healthcheck:
      test: ["CMD", "dotnet", "/app/EpisodeIdentifier.Core.dll", "--version"]
      interval: 30s
      timeout: 3s
      start_period: 5s
      retries: 3
```

---

## Notes

- This data model describes **container configuration**, not application business logic
- The existing EpisodeIdentifier.Core data models (Episodes, Subtitles, Hashes) remain unchanged
- Configuration hot-reload is an existing feature being preserved, not added
- PUID/PGID handling is container-specific infrastructure, transparent to application
