# Research: NonPGS Subtitle Workflow


## Text Subtitle Format Analysis


### Decision: Support .srt, .ass, and .vtt formats initially


**Rationale**: These are the most common text-based subtitle formats found in video files:

- .srt (SubRip) - Simple text format, widely supported
- .ass/.ssa (Advanced SubStation Alpha) - Feature-rich format with styling
- .vtt (WebVTT) - Web standard, growing adoption

**Alternatives considered**:

- .sub files - Less common, multiple incompatible variants
- .idx/.sub DVDRip format - Binary format, not text-based
- .ttml (TTML) - XML-based, very rare in video files

### Decision: Use FFmpeg for subtitle track detection and extraction


**Rationale**:

- Already part of existing dependencies
- Excellent format support and reliability
- Can detect subtitle tracks without extraction
- Handles encoding issues automatically

**Alternatives considered**:

- Direct file parsing - Complex encoding handling, format variations
- MKVToolNix only - Limited to MKV containers
- Subtitle parsing libraries - Additional dependencies

### Decision: Sequential processing with early exit on match


**Rationale**:

- Minimizes processing time when early tracks succeed
- Allows user to prioritize subtitle languages/types via track ordering
- Consistent with existing fuzzy hash workflow

**Alternatives considered**:

- Parallel processing all tracks - Unnecessary resource usage
- Process all tracks then pick best - No clear ranking criteria
- User track selection - Adds complexity, breaks automation

## Format-Specific Extraction Patterns


### SRT Format Processing


**Pattern**: Time codes + line numbers + text content

- Extract dialogue text only (ignore timestamps and formatting)
- Handle encoding variations (UTF-8, UTF-16, Latin-1)
- Strip HTML-like tags if present

### ASS Format Processing


**Pattern**: Complex script format with events section

- Focus on Dialogue events only
- Extract Text field from dialogue lines
- Ignore styling commands and special effects
- Handle Advanced SSA variant differences

### VTT Format Processing


**Pattern**: WebVTT header + cue blocks

- Skip WEBVTT header and NOTE blocks
- Extract cue text content only
- Handle cue settings and positioning data
- Support for nested timestamps

## Integration with Existing Workflow


### Decision: Extend SubtitleExtractor service class


**Rationale**:

- Maintains single responsibility for subtitle operations
- Reuses existing error handling and logging patterns
- Consistent with current architecture

**Implementation approach**:

1. Add TryExtractTextSubtitles method
2. Return standardized SubtitleContent regardless of source format
3. Preserve existing PGS extraction priority

### Decision: Minimal changes to fuzzy hash workflow


**Rationale**:

- Text content can use same hashing algorithm
- Database schema unchanged
- Matching logic identical

**Required changes**:

- Update IdentificationResult to indicate subtitle source type
- Add logging to track which extraction method succeeded
- Preserve performance characteristics

## Performance Considerations


### Text Processing Optimization


- **Chunked reading**: Process large subtitle files in chunks to manage memory
- **Early validation**: Quick format detection before full parsing
- **Encoding detection**: Use System.Text.Encoding.GetEncoding with fallbacks

### Error Handling Strategy


- **Format corruption**: Skip to next track rather than failing completely
- **Encoding issues**: Try multiple encoding options before giving up
- **Missing files**: Handle embedded vs external subtitle scenarios

## Technical Dependencies


### Required Tools (Already Available)


- FFmpeg: Subtitle track detection and extraction
- System.Text.Json: Configuration and result serialization
- System.Text.Encoding: Handle subtitle file encodings

### New Dependencies (None Required)


All subtitle parsing can be implemented with .NET standard libraries:

- Regex for SRT parsing
- String manipulation for ASS parsing
- Built-in text processing for VTT

This maintains the project's principle of minimal external dependencies.
