# Docker Deployment for Episode Identifier

This guide covers building, deploying, and using Episode Identifier as a Docker container.

## Quick Start

### Using Docker Compose (Recommended for Testing)

```bash
# Clone the repository
git clone https://github.com/taldelarosa/KnowShow2.git
cd KnowShow2

# Create data directories
mkdir -p docker-data/database docker-data/config test-videos

# Copy example configuration
cp episodeidentifier.config.example.json docker-data/config/episodeidentifier.config.json

# Build and start the container
docker-compose up -d

# Run identification on a video
docker-compose exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/yourfile.mkv \
  --hash-db /data/database/production_hashes.db
```

### Using Docker CLI

```bash
# Build the image
docker build -t episodeidentifier:latest .

# Run the container
docker run -d \
  --name episodeidentifier \
  -e PUID=99 \
  -e PGID=100 \
  -v /path/to/videos:/data/videos:rw \
  -v /path/to/database:/data/database:rw \
  -v /path/to/config:/data/config:rw \
  episodeidentifier:latest \
  tail -f /dev/null

# Execute commands
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll --help
```

### Using Pre-built Image from Docker Hub

```bash
# Pull the image
docker pull episodeidentifier/episodeidentifier:latest

# Run with your volumes
docker run -d \
  --name episodeidentifier \
  -e PUID=99 \
  -e PGID=100 \
  -v /path/to/videos:/data/videos:rw \
  -v /path/to/database:/data/database:rw \
  -v /path/to/config:/data/config:rw \
  episodeidentifier/episodeidentifier:latest \
  tail -f /dev/null
```

## Building the Image

### Standard Build

```bash
# Build with default settings
docker build -t episodeidentifier:latest .

# Build with specific tag
docker build -t episodeidentifier:v1.0.0 .

# Build with build arguments (if needed)
docker build \
  --build-arg DOTNET_VERSION=8.0 \
  -t episodeidentifier:latest .
```

### Multi-Architecture Build

```bash
# Setup buildx (one-time setup)
docker buildx create --name multiarch --use
docker buildx inspect --bootstrap

# Build for multiple platforms
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t episodeidentifier/episodeidentifier:latest \
  --push \
  .
```

### Build Statistics

Expected build time: 5-10 minutes (depending on internet speed)
Final image size: ~1.5GB
Layers: ~25

## Container Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUID` | `99` | User ID for file permissions |
| `PGID` | `100` | Group ID for file permissions |
| `CONFIG_PATH` | `/data/config/episodeidentifier.config.json` | Configuration file path |
| `HASH_DB_PATH` | `/data/database/production_hashes.db` | Database file path |
| `LOG_LEVEL` | `Information` | Logging verbosity |

### Volume Mounts

| Container Path | Purpose | Access | Required |
|---------------|---------|--------|----------|
| `/data/videos` | Video files to process | RW | Yes |
| `/data/database` | Persistent hash database | RW | Yes |
| `/data/config` | Configuration files | RW | Yes |

### Port Mappings

This application is CLI-only and doesn't expose any ports.

## Usage Examples

### Interactive Shell

```bash
# Open a shell in the container
docker exec -it episodeidentifier bash

# Run commands directly
dotnet /app/EpisodeIdentifier.Core.dll --help
```

### One-Shot Identification

```bash
# Identify a single video
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/unknown.mkv \
  --hash-db /data/database/production_hashes.db

# Store a known episode
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --store \
  --input /data/videos/S01E01.mkv \
  --season 1 \
  --episode 1 \
  --hash-db /data/database/production_hashes.db
```

### Bulk Processing

```bash
# Process all files in a directory
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --bulk-identify /data/videos/season1 \
  --hash-db /data/database/production_hashes.db

# Using docker-compose profile
docker-compose --profile bulk up episodeidentifier-bulk
```

### Auto-Rename

```bash
# Identify and rename
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/random_name.mkv \
  --hash-db /data/database/production_hashes.db \
  --rename \
  --rename-template "MyShow S{season:00}E{episode:00}"
```

## Deployment Options

### Docker Compose (Development/Testing)

Best for:

- Local development
- Testing new features
- Quick prototyping

See `docker-compose.yml` for full configuration.

### Unraid Docker (Production)

Best for:

- Home media servers
- Automated workflows
- Integration with media management tools

See [unraid.md](unraid.md) for detailed Unraid setup.

### Standalone Docker (Production)

Best for:

- Traditional Linux servers
- Custom automation scripts
- Integration with existing Docker infrastructure

```bash
# Create systemd service for auto-start
cat > /etc/systemd/system/episodeidentifier.service <<EOF
[Unit]
Description=Episode Identifier Container
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/usr/bin/docker start episodeidentifier
ExecStop=/usr/bin/docker stop episodeidentifier

[Install]
WantedBy=multi-user.target
EOF

# Enable and start
systemctl enable episodeidentifier
systemctl start episodeidentifier
```

### Kubernetes (Advanced)

For Kubernetes deployment, create manifests based on the docker-compose configuration. Example deployment manifest:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: episodeidentifier
spec:
  replicas: 1
  selector:
    matchLabels:
      app: episodeidentifier
  template:
    metadata:
      labels:
        app: episodeidentifier
    spec:
      containers:
      - name: episodeidentifier
        image: episodeidentifier/episodeidentifier:latest
        env:
        - name: PUID
          value: "99"
        - name: PGID
          value: "100"
        volumeMounts:
        - name: videos
          mountPath: /data/videos
        - name: database
          mountPath: /data/database
        - name: config
          mountPath: /data/config
      volumes:
      - name: videos
        persistentVolumeClaim:
          claimName: episodeidentifier-videos
      - name: database
        persistentVolumeClaim:
          claimName: episodeidentifier-database
      - name: config
        configMap:
          name: episodeidentifier-config
```

## Maintenance

### Viewing Logs

```bash
# Follow logs in real-time
docker logs -f episodeidentifier

# View last 100 lines
docker logs --tail 100 episodeidentifier

# View logs since specific time
docker logs --since 1h episodeidentifier
```

### Database Backup

```bash
# Manual backup
docker exec episodeidentifier sqlite3 /data/database/production_hashes.db \
  ".backup /data/database/backup_$(date +%Y%m%d).db"

# Automated backup script
cat > backup-episodeidentifier.sh <<'EOF'
#!/bin/bash
BACKUP_DIR="/path/to/backups"
DATE=$(date +%Y%m%d_%H%M%S)

docker exec episodeidentifier sqlite3 /data/database/production_hashes.db \
  ".backup /data/database/backup_${DATE}.db"

docker cp episodeidentifier:/data/database/backup_${DATE}.db \
  ${BACKUP_DIR}/

# Keep only last 10 backups
cd ${BACKUP_DIR}
ls -t backup_*.db | tail -n +11 | xargs rm -f
EOF

chmod +x backup-episodeidentifier.sh
```

### Updating the Container

```bash
# Pull latest image
docker pull episodeidentifier/episodeidentifier:latest

# Stop and remove old container
docker stop episodeidentifier
docker rm episodeidentifier

# Start new container with same volumes
docker run -d \
  --name episodeidentifier \
  -e PUID=99 \
  -e PGID=100 \
  -v /path/to/videos:/data/videos:rw \
  -v /path/to/database:/data/database:rw \
  -v /path/to/config:/data/config:rw \
  episodeidentifier/episodeidentifier:latest \
  tail -f /dev/null
```

### Container Health Check

```bash
# Check if container is running
docker ps | grep episodeidentifier

# Verify dependencies
docker exec episodeidentifier bash -c "
  echo 'Checking dependencies...'
  dotnet --version
  ffmpeg -version | head -n1
  mkvextract --version | head -n1
  tesseract --version | head -n1
  which pgsrip
"

# Test identification
docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll --help
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker logs episodeidentifier

# Inspect container
docker inspect episodeidentifier

# Common issues:
# - Missing volumes
# - Permission errors
# - Port conflicts (if any)
```

### Permission Errors

```bash
# Fix host directory permissions
sudo chown -R 99:100 /path/to/database
sudo chown -R 99:100 /path/to/config
sudo chmod -R 755 /path/to/database /path/to/config

# Or use your local user UID/GID
docker run -d \
  --name episodeidentifier \
  -e PUID=$(id -u) \
  -e PGID=$(id -g) \
  ...
```

### Build Failures

```bash
# Clean build cache
docker builder prune -a

# Rebuild without cache
docker build --no-cache -t episodeidentifier:latest .

# Check for network issues during build
docker build --network=host -t episodeidentifier:latest .
```

### Performance Issues

```bash
# Check resource usage
docker stats episodeidentifier

# Limit resources
docker update \
  --cpus="4.0" \
  --memory="4g" \
  episodeidentifier

# Or use docker-compose limits (see docker-compose.yml)
```

## Security Considerations

### Running as Non-Root

The container runs as non-root user by default (PUID/PGID configurable).

### Volume Permissions

Ensure host directories have appropriate permissions:

```bash
# Recommended permissions
chmod 755 /path/to/videos
chmod 755 /path/to/database
chmod 755 /path/to/config

# Files should be readable/writable by container user
chown -R 99:100 /path/to/{videos,database,config}
```

### Network Security

The container doesn't expose any ports, so network attack surface is minimal.

### Secrets Management

Don't store sensitive data in configuration files. Use environment variables or Docker secrets for production deployments.

## Advanced Topics

### Custom Dockerfile

To add additional dependencies or customize the image:

```dockerfile
# Extend the base image
FROM episodeidentifier/episodeidentifier:latest

# Install additional OCR languages
RUN apt-get update && apt-get install -y \
    tesseract-ocr-fra \
    tesseract-ocr-deu \
    && rm -rf /var/lib/apt/lists/*

# Add custom scripts
COPY custom-scripts/ /usr/local/bin/
RUN chmod +x /usr/local/bin/*.sh
```

### Integration with CI/CD

```yaml
# GitHub Actions example
name: Build and Push Docker Image

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2
      
      - name: Login to Docker Hub
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}
      
      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: .
          push: true
          tags: |
            episodeidentifier/episodeidentifier:latest
            episodeidentifier/episodeidentifier:${{ github.ref_name }}
```

## Additional Resources

- **Unraid Setup Guide**: [unraid.md](unraid.md)
- **Main Documentation**: [../README.md](../README.md)
- **Configuration Guide**: [../CONFIGURATION_GUIDE.md](../CONFIGURATION_GUIDE.md)
- **GitHub Repository**: <https://github.com/taldelarosa/KnowShow2>

## Support

For Docker-specific issues:

1. Check container logs: `docker logs episodeidentifier`
2. Verify volume mounts: `docker inspect episodeidentifier`
3. Test dependencies: See "Container Health Check" section above
4. Report issues: <https://github.com/taldelarosa/KnowShow2/issues>

## License

Episode Identifier is open source software. See LICENSE file in repository.
