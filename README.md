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

## Features

### Core Functionality
- **AV1 Video Support**: Validates that input files are AV1 encoded
- **PGS Subtitle Extraction**: Extracts PGS subtitles from video containers
- **OCR Conversion**: Converts PGS image-based subtitles to searchable text
- **Fuzzy Matching**: Compares extracted text against known subtitle database
- **JSON Output**: All responses formatted as JSON for automation

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
```bash
dotnet run -- --input video.mkv --hash-db hashes.sqlite
```

### With Language Preference
```bash
dotnet run -- --input video.mkv --hash-db hashes.sqlite --language eng
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
| `--store` | Store mode instead of identify | ❌ | false |
| `--series` | Series name (store mode only) | ✅** | - |
| `--season` | Season number (store mode only) | ✅** | - |
| `--episode` | Episode number (store mode only) | ✅** | - |
| `--language` | Preferred subtitle language | ❌ | eng |

*Required for identification mode  
**Required when using `--store`

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

### Successful Identification
```json
{
  "series": "Example Show",
  "season": "01", 
  "episode": "02",
  "matchConfidence": 0.95,
  "ambiguityNotes": null,
  "error": null
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

### Debug Logging
Set environment variable for detailed logging:
```bash
export DOTNET_LOGGING_CONSOLE_DISABLECOLORS=true
dotnet run -- --input video.mkv --hash-db hashes.db
```
