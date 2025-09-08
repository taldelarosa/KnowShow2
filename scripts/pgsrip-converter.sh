#!/bin/bash

# PGS to Text Conversion Script using pgsrip
# This script demonstrates how to use pgsrip for better PGS subtitle extraction

set -e

# Configuration
LANGUAGE="eng"
FORCE_OVERWRITE=true
MAX_WORKERS=1

# Function to check if pgsrip is installed
check_pgsrip() {
    if ! command -v pgsrip &> /dev/null; then
        echo "Error: pgsrip is not installed"
        echo "Install with: pip install pgsrip"
        exit 1
    fi
}

# Function to check if required dependencies are installed
check_dependencies() {
    # Check for mkvextract (from mkvtoolnix)
    if ! command -v mkvextract &> /dev/null; then
        echo "Warning: mkvtoolnix not found. Install with: sudo apt-get install mkvtoolnix"
    fi
    
    # Check for tesseract
    if ! command -v tesseract &> /dev/null; then
        echo "Warning: tesseract not found. Install with: sudo apt-get install tesseract-ocr"
    fi
    
    # Check for tessdata
    if [ -z "$TESSDATA_PREFIX" ]; then
        echo "Warning: TESSDATA_PREFIX not set. Consider setting up tessdata_best for better OCR"
    fi
}

# Function to convert SUP file to SRT using pgsrip
convert_sup_to_srt() {
    local sup_file="$1"
    local language="${2:-$LANGUAGE}"
    
    echo "Converting $sup_file to SRT using pgsrip..."
    
    local args="--language $language --max-workers $MAX_WORKERS"
    if [ "$FORCE_OVERWRITE" = true ]; then
        args="$args --force"
    fi
    
    # Run pgsrip
    if pgsrip $args "$sup_file"; then
        echo "✓ Successfully converted $sup_file"
        
        # Find the generated SRT file
        local base_name=$(basename "$sup_file" .sup)
        local dir_name=$(dirname "$sup_file")
        local srt_file="$dir_name/$base_name.srt"
        
        if [ -f "$srt_file" ]; then
            echo "✓ Generated SRT file: $srt_file"
            echo "✓ File size: $(stat -f%z "$srt_file" 2>/dev/null || stat -c%s "$srt_file") bytes"
            
            # Show first few lines
            echo "✓ Preview:"
            head -n 10 "$srt_file"
        else
            echo "⚠ Warning: Expected SRT file not found at $srt_file"
        fi
    else
        echo "✗ Failed to convert $sup_file"
        return 1
    fi
}

# Function to extract subtitles from video and convert
convert_video_subtitles() {
    local video_file="$1"
    local language="${2:-$LANGUAGE}"
    
    echo "Processing video file: $video_file"
    
    # Run pgsrip directly on video
    local args="--language $language --max-workers $MAX_WORKERS"
    if [ "$FORCE_OVERWRITE" = true ]; then
        args="$args --force"
    fi
    
    if pgsrip $args "$video_file"; then
        echo "✓ Successfully processed $video_file"
        
        # Find generated SRT files
        local base_name=$(basename "$video_file" | sed 's/\.[^.]*$//')
        local dir_name=$(dirname "$video_file")
        
        find "$dir_name" -name "${base_name}*.srt" -type f | while read srt_file; do
            echo "✓ Generated: $srt_file"
            echo "✓ File size: $(stat -f%z "$srt_file" 2>/dev/null || stat -c%s "$srt_file") bytes"
        done
    else
        echo "✗ Failed to process $video_file"
        return 1
    fi
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS] <file>"
    echo ""
    echo "Convert PGS subtitles to text using pgsrip"
    echo ""
    echo "Options:"
    echo "  -l, --language LANG     Set language for OCR (default: eng)"
    echo "  -w, --workers NUM       Set max workers (default: 1)"
    echo "  -f, --force             Force overwrite existing files"
    echo "  -h, --help              Show this help"
    echo ""
    echo "Examples:"
    echo "  $0 subtitle.sup                    # Convert SUP file"
    echo "  $0 -l deu video.mkv                # Convert German video subtitles"
    echo "  $0 --language fra --workers 2 file.mks  # Convert French with 2 workers"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -l|--language)
            LANGUAGE="$2"
            shift 2
            ;;
        -w|--workers)
            MAX_WORKERS="$2"
            shift 2
            ;;
        -f|--force)
            FORCE_OVERWRITE=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        -*)
            echo "Unknown option: $1"
            show_usage
            exit 1
            ;;
        *)
            INPUT_FILE="$1"
            shift
            ;;
    esac
done

# Check if input file is provided
if [ -z "$INPUT_FILE" ]; then
    echo "Error: No input file specified"
    show_usage
    exit 1
fi

# Check if input file exists
if [ ! -f "$INPUT_FILE" ]; then
    echo "Error: File '$INPUT_FILE' not found"
    exit 1
fi

# Main execution
echo "PGS to Text Conversion using pgsrip"
echo "======================================"
echo "Input file: $INPUT_FILE"
echo "Language: $LANGUAGE"
echo "Max workers: $MAX_WORKERS"
echo "Force overwrite: $FORCE_OVERWRITE"
echo ""

# Check dependencies
check_pgsrip
check_dependencies

# Determine file type and process accordingly
case "${INPUT_FILE,,}" in
    *.sup)
        convert_sup_to_srt "$INPUT_FILE" "$LANGUAGE"
        ;;
    *.mkv|*.mks)
        convert_video_subtitles "$INPUT_FILE" "$LANGUAGE"
        ;;
    *)
        echo "Error: Unsupported file type. Supported: .sup, .mkv, .mks"
        exit 1
        ;;
esac

echo ""
echo "✓ Processing complete!"
