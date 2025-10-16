# Episode Identifier - PGS Subtitle Extraction


This application identifies Season and Episode numbers from AV1 encoded video files by extracting PGS (Presentation Graphics Stream) subtitles and comparing them to known labeled subtitles.

## Quick Setup (Fresh Linux System)


**⚡ One-Command Setup:**

```bash

# Check what's needed









./scripts/setup-prerequisites.sh --check-only

# Install everything automatically (requires sudo)









./scripts/setup-prerequisites.sh --install

# Build and test









cd src/EpisodeIdentifier.Core && dotnet build
dotnet test
```


**Manual verification:** The setup script will install all required dependencies including:

- .NET 8.0 SDK
- FFmpeg & MKVToolNix (video processing)
- Tesseract OCR with language packs
- pgsrip (advanced PGS processor)
- Enhanced OCR training data

## Docker Deployment


**🐳 Run in Docker (Recommended for Unraid/Production):**

The Episode Identifier is available as a Docker container with all dependencies pre-installed. Perfect for Unraid servers and production deployments.

### Quick Start with Docker


```bash

# Pull the image

docker pull episodeidentifier/episodeidentifier:latest

# Run the container

docker run -d \
  --name episodeidentifier \
  -e PUID=99 -e PGID=100 \
  -v /path/to/videos:/data/videos:rw \
  -v /path/to/database:/data/database:rw \
  -v /path/to/config:/data/config:rw \
  episodeidentifier/episodeidentifier:latest \
  tail -f /dev/null

# Execute commands

docker exec episodeidentifier dotnet /app/EpisodeIdentifier.Core.dll \
  --input /data/videos/yourfile.mkv \
  --hash-db /data/database/production_hashes.db
```


### Deployment Guides


- **🏠 Unraid Users**: See [docs/unraid.md](docs/unraid.md) for complete Unraid setup guide
- **🐳 Docker Users**: See [docs/DOCKER.md](docs/DOCKER.md) for Docker deployment options
- **📦 Building from Source**: `docker build -t episodeidentifier:latest .`

### Key Features


- ✅ All dependencies pre-installed (FFmpeg, Tesseract, pgsrip, etc.)
- ✅ PUID/PGID support for proper file permissions
- ✅ Persistent database and configuration storage
- ✅ Compatible with Docker Compose and Unraid Docker UI
- ✅ Multi-architecture support (amd64/arm64)

## Git Workflow


**⚠️ Important**: This project uses a **feature branch workflow** with branch protection:

```bash

# ✅ Correct workflow









git checkout -b 005-new-feature    # Create feature branch

# Make changes...









git push origin 005-new-feature    # Push feature branch

# Create Pull Request via GitHub










# Get code review → Merge via PR










# ❌ Direct pushes to main are blocked









git push origin main  # This will fail!
```


**Required for all changes:**

- Feature branches (`###-feature-name` format)
- Pull Request with code review
- All CI/CD checks must pass
- See [branch protection guide](.github/branch-protection-config.md) for setup

## Development & CI/CD


### Code Quality Standards


**Automated Formatting & Linting:**

```bash

# C# code formatting


dotnet format EpisodeIdentifier.sln

# Markdown documentation linting (manual check)


./scripts/lint-markdown.sh --fix

# or directly: markdownlint --config .markdownlint.json '**/*.md' --fix


```


**Configuration Files:**

- `.markdownlint.json` - Markdown linting rules optimized for technical documentation
- Solution-wide formatting enforced via `dotnet format`

**Automatic CI Enforcement:**

- Markdown linting runs on every build with auto-fix enabled
- If auto-fixes are needed, the CI will fail and show required changes
- All fixable markdown issues are resolved automatically during CI
- Contributors should run `markdownlint --fix` locally before committing

**Configuration Files:**

- `.markdownlint.json` - Markdown linting rules optimized for technical documentation
- Solution-wide formatting enforced via `dotnet format`

### GitHub Actions Workflow


**Build Process:**

- Solution-based builds using `EpisodeIdentifier.sln`
- Parallel test execution: Unit tests (8) + Contract tests (30) = 38 total
- Integration tests temporarily disabled due to API compatibility issues

**Quality Gates:**

- ✅ .NET Code formatting validation via `dotnet format`
- ✅ Markdown documentation linting via `markdownlint-cli2`
- ✅ Automated security scanning with Trivy
- ✅ Dependency caching for faster builds
- ✅ All deprecated GitHub Actions updated to current versions

**Environment:**

- NPM 11.6.0 (latest)
- markdownlint-cli2 for documentation quality
- Updated GitHub Actions (upload-artifact@v4, cache@v4, codeql-action@v3)

## Features


### Core Functionality


- **AV1 Video Support**: Validates that input files are AV1 encoded
- **PGS Subtitle Extraction**: Extracts PGS subtitles from video containers
- **OCR Conversion**: Converts PGS image-based subtitles to searchable text
- **Fuzzy Matching**: Compares extracted text against known subtitle database
- **JSON Output**: All responses formatted as JSON for automation

### New File Renaming Features (FR-007 Implementation)


- **Filename Suggestions**: Automatically generates standardized filenames for identified episodes
- **Automatic Renaming**: Optional `--rename` flag to automatically rename files
- **Windows Compatibility**: Sanitizes filenames for Windows file system compatibility
- **Smart Confidence Thresholds**: Only suggests filenames for high-confidence matches (≥90%)
- **Error Recovery**: Preserves original files if rename operations fail
- **Standardized Format**: Uses "SeriesName - S##E## - EpisodeName.ext" naming convention

### New PGS Extraction Features (FR-002 Implementation)


- **Multi-Track Support**: Automatically detects all PGS subtitle tracks in video
- **Language Selection**: Supports preferred language selection (e.g., `--language eng`)
- **Smart Track Selection**: Defaults to English, falls back to first available track
- **Dual Extraction Tools**: Uses mkvextract (preferred) with ffmpeg fallback
- **OCR Processing**: Converts PGS graphics to text using Tesseract OCR
- **Error Handling**: Comprehensive error reporting for missing dependencies

## Dependencies


**🎯 Automated Setup:** Use `./scripts/setup-prerequisites.sh --install` to install everything automatically.

### Required External Tools


```bash

# Video processing









sudo apt-get install ffmpeg mkvtoolnix-cli

# OCR processing









sudo apt-get install tesseract-ocr tesseract-ocr-eng

# Additional language packs (optional)









sudo apt-get install tesseract-ocr-spa tesseract-ocr-fra tesseract-ocr-deu

# Advanced PGS processor (recommended)









pip install pgsrip
```


### .NET Dependencies (auto-restored)


- Microsoft.Data.Sqlite
- Microsoft.Extensions.Logging
- System.CommandLine
- FuzzySharp

## Usage


### Basic Identification


## Basic Identification


```bash
dotnet run -- --input video.mkv --hash-db hashes.sqlite
```


### With Series/Season Filtering (Faster Search)


```bash

# Filter by series only (~22% faster for multi-series databases)

dotnet run -- --input video.mkv --hash-db hashes.sqlite --series "Bones"

# Filter by series AND season (~93% faster for large databases)

dotnet run -- --input video.mkv --hash-db hashes.sqlite --series "Bones" --season 1
```


### With Language Preference


### With Language Preference


```bash
dotnet run -- --input video.mkv --hash-db hashes.sqlite --language eng
```


### With Automatic File Renaming


```bash
dotnet run -- --input video.mkv --hash-db hashes.sqlite --rename
```


### Store Known Subtitle


```bash
dotnet run -- --input subtitle.txt --hash-db hashes.sqlite --store --series "Show Name" --season "01" --episode "02"
```


## Command Line Options


| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `--input` | Path to AV1 video file or subtitle file | ✅ | - |
| `--hash-db` | Path to SQLite hash database | ✅ | - |
| `--bulk-identify` | Process all video files from a directory | ❌ | - |
| `--bulk-store` | Store all subtitle files from a directory | ❌ | - |
| `--series` | Filter by series name (case-insensitive) | ❌ | - |
| `--season` | Filter by season number (requires `--series`) | ❌ | - |
| `--store` | Store mode instead of identify | ❌ | false |
| `--episode` | Episode number (store mode only) | ✅** | - |
| `--episode-name` | Episode title (store mode only) | ❌ | - |
| `--language` | Preferred subtitle language | ❌ | eng |
| `--rename` | Automatically rename file to suggested filename | ❌ | false |

*Note: When using `--series` and `--season` for filtering, the search will only scan matching episodes, improving performance significantly (up to 93% faster for targeted searches).*

**Required when using `--store`

### Bulk Processing


Process multiple files efficiently with concurrent operations:

```bash

# Identify all videos in a directory

dotnet run -- --bulk-identify /path/to/videos --hash-db hashes.sqlite

# Store all subtitles from a directory (extracts series/season/episode from filenames)

dotnet run -- --bulk-store /path/to/subtitles --hash-db hashes.sqlite

# Bulk identify with automatic renaming

dotnet run -- --bulk-identify /path/to/videos --hash-db hashes.sqlite --rename
```


## Configuration


The application uses `episodeidentifier.config.json` for advanced settings and performance tuning. Configuration changes are automatically detected and applied during runtime (hot-reload).

### Configuration File Location


The application looks for the configuration file in this order:

1. `episodeidentifier.config.json` (current directory)
2. Default built-in configuration if file not found

### Sample Configuration


```json
{
  "version": "2.0",
  "maxConcurrency": 3,
  "matchConfidenceThreshold": 0.6,
  "renameConfidenceThreshold": 0.7,
  "fuzzyHashThreshold": 75,
  "hashingAlgorithm": "CTPH",
  "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
}
```


### Concurrency Configuration


The `maxConcurrency` setting controls how many files are processed simultaneously during bulk operations:

| Value | Behavior | Use Case |
|-------|----------|----------|
| `1` | Sequential processing | Single-core systems, debugging, or when minimizing system load |
| `2-3` | Light concurrency | Most desktop systems, balanced performance and stability |
| `4-8` | Moderate concurrency | Multi-core systems with good I/O performance |
| `9-20` | High concurrency | Powerful systems with fast SSDs and plenty of RAM |
| `21-100` | Maximum concurrency | Server-grade hardware with exceptional I/O capabilities |

**Performance Guidelines:**

- **Start with 3-5** for most systems
- **Monitor system resources** (CPU, memory, disk I/O) during bulk processing
- **Increase gradually** if system handles load well
- **Reduce if experiencing** slow performance, high memory usage, or I/O bottlenecks

**Hot-Reload:** Changes to `maxConcurrency` apply to new bulk operations immediately - no restart required.

**Validation:** Invalid values are automatically clamped to the valid range (1-100) with fallback to 1.

### Other Configuration Options


| Setting | Description | Valid Range | Default |
|---------|-------------|-------------|---------|
| `matchConfidenceThreshold` | Minimum confidence for episode matches | 0.0 - 1.0 | 0.6 |
| `renameConfidenceThreshold` | Minimum confidence for automatic renaming | 0.0 - 1.0 | 0.7 |
| `fuzzyHashThreshold` | Subtitle content similarity threshold | 0 - 100 | 75 |
| `hashingAlgorithm` | Content hashing method | "CTPH" | "CTPH" |

**Example Configurations:**

```bash

# Conservative (safe, slower)

echo '{"maxConcurrency": 1, "matchConfidenceThreshold": 0.8}' > episodeidentifier.config.json

# Balanced (recommended for most users)

echo '{"maxConcurrency": 3, "matchConfidenceThreshold": 0.6}' > episodeidentifier.config.json

# Aggressive (fast, requires good hardware)

echo '{"maxConcurrency": 10, "matchConfidenceThreshold": 0.5}' > episodeidentifier.config.json
```


## Supported Languages


The application supports multiple subtitle languages through Tesseract OCR:

| Language | Code | Tesseract Package |
|----------|------|-------------------|
| English | `eng` | tesseract-ocr-eng |
| Spanish | `spa` | tesseract-ocr-spa |
| French | `fra` | tesseract-ocr-fra |
| German | `deu` | tesseract-ocr-deu |
| Italian | `ita` | tesseract-ocr-ita |
| Portuguese | `por` | tesseract-ocr-por |
| Russian | `rus` | tesseract-ocr-rus |
| Japanese | `jpn` | tesseract-ocr-jpn |
| Korean | `kor` | tesseract-ocr-kor |
| Chinese | `chi_sim` | tesseract-ocr-chi-sim |

## Output Examples


## Output Examples


### Successful Identification with Filename Suggestion


```json
{
  "series": "Example Show",
  "season": "01",
  "episode": "02",
  "matchConfidence": 0.95,
  "suggestedFilename": "Example Show - S01E02 - Episode Title.mkv",
  "ambiguityNotes": null,
  "error": null
}
```


### Successful Identification with Automatic Rename


```json
{
  "series": "Example Show",
  "season": "01",
  "episode": "02",
  "matchConfidence": 0.95,
  "suggestedFilename": "Example Show - S01E02 - Episode Title.mkv",
  "fileRenamed": true,
  "originalFilename": "unclear-filename.mkv",
  "ambiguityNotes": null,
  "error": null
}
```


### Low Confidence Identification (No Filename Suggestion)


```json
{
  "series": "Example Show",
  "season": "01",
  "episode": "02",
  "matchConfidence": 0.75,
  "ambiguityNotes": "Multiple possible matches found",
  "error": null
}
```


### File Rename Error


```json
{
  "series": "Example Show",
  "season": "01",
  "episode": "02",
  "matchConfidence": 0.95,
  "suggestedFilename": "Example Show - S01E02 - Episode Title.mkv",
  "fileRenamed": false,
  "originalFilename": "video.mkv",
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Target file already exists: Example Show - S01E02 - Episode Title.mkv"
  }
}
```


### AV1 Validation Error


```json
{
  "error": {
    "code": "UNSUPPORTED_FILE_TYPE",
    "message": "The provided file is not AV1 encoded. Non-AV1 files will be supported in a later release."
  }
}
```


### Missing OCR Dependencies


```json
{
  "error": {
    "code": "MISSING_DEPENDENCY",
    "message": "Tesseract OCR is required but not available. Please install tesseract-ocr."
  }
}
```


### No Subtitles Found


```json
{
  "error": {
    "code": "NO_SUBTITLES_FOUND",
    "message": "No PGS subtitles could be extracted from the video file"
  }
}
```


### OCR Processing Failed


```json
{
  "error": {
    "code": "OCR_FAILED",
    "message": "Failed to extract readable text from PGS subtitles using OCR"
  }
}
```


## Technical Implementation


### PGS Extraction Process


1. **Format Validation**: Verify input is AV1 encoded using ffprobe
2. **Track Discovery**: Identify all PGS subtitle tracks with metadata
3. **Track Selection**: Choose best track based on language preference
4. **Subtitle Extraction**: Extract PGS data using mkvextract or ffmpeg
5. **Image Conversion**: Convert PGS to PNG images using ffmpeg
6. **OCR Processing**: Extract text from images using Tesseract
7. **Text Combination**: Combine all extracted text for matching

### Error Handling


- **Dependency Checks**: Validates external tools before processing
- **File Validation**: Ensures input files exist and are accessible
- **Format Verification**: Confirms AV1 encoding before processing
- **Graceful Degradation**: Falls back from mkvextract to ffmpeg
- **Resource Cleanup**: Automatically removes temporary files

### Performance Considerations


- **Temporary Files**: Uses unique temporary filenames to avoid conflicts
- **Memory Management**: Streams large files instead of loading into memory
- **Parallel Processing**: Supports concurrent OCR processing of multiple images
- **Caching**: Stores fuzzy hashes for fast repeated lookups

## Testing


### Unit Tests


```bash
dotnet test tests/unit/
```


### Integration Tests


```bash
dotnet test tests/integration/
```


### Contract Tests


```bash
dotnet test tests/contract/
```


## Architecture


### New Service Classes


- `VideoFormatValidator`: AV1 validation and subtitle track discovery
- `PgsToTextConverter`: PGS to text conversion via OCR
- `SubtitleExtractor`: Enhanced PGS extraction with track selection

### Models


- `SubtitleTrackInfo`: Represents discovered subtitle tracks
- `IdentificationResult`: Enhanced with new error types
- `IdentificationError`: Extended error codes for new scenarios

## Troubleshooting


### Setup Issues


**"Command not found" or missing dependencies**

```bash

# Run the comprehensive setup checker









./scripts/setup-prerequisites.sh --check-only --verbose

# Install any missing dependencies









./scripts/setup-prerequisites.sh --install
```


**"Permission denied" when running setup**

```bash

# Make the script executable









chmod +x scripts/setup-prerequisites.sh
```


### Common Issues


**"Tesseract OCR is required but not available"**

- Install tesseract-ocr: `sudo apt-get install tesseract-ocr`
- Verify installation: `tesseract --version`

**"No PGS subtitles could be extracted"**

- Check if video has embedded subtitles: `ffprobe -show_streams video.mkv`
- Verify subtitle format is PGS/hdmv_pgs_subtitle

**"The provided file is not AV1 encoded"**

- Verify video codec: `ffprobe -show_streams video.mkv | grep codec_name`
- Look for `av01` or `libaom-av1` codec

**"Failed to extract readable text from PGS subtitles"**

- Check if PGS contains text (some may be graphics only)
- Try different language pack: `--language spa`
- Verify image quality is sufficient for OCR

**"Target file already exists" when using --rename**

- Check if suggested filename already exists in directory
- Original file is preserved when rename fails
- Review suggested filename in JSON output before retrying

**"Permission denied" during file rename**

- Ensure write permissions to directory
- Check if file is in use by another application
- Verify sufficient disk space for rename operation

### Debug Logging


Set environment variable for detailed logging:

```bash
export DOTNET_LOGGING_CONSOLE_DISABLECOLORS=true
dotnet run -- --input video.mkv --hash-db hashes.db
```

