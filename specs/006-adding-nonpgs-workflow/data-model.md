# Data Model: NonPGS Subtitle Workflow

## Core Entities

### TextSubtitleTrack

Represents a text-based subtitle track within a video file.

**Properties**:

- `Index`: int - Track index within the video file
- `Format`: SubtitleFormat enum - Format type (.srt, .ass, .vtt)
- `Language`: string? - Language code if detected (e.g., "en", "es")
- `Title`: string? - Track title/description from metadata
- `IsDefault`: bool - Whether this is the default subtitle track
- `IsForced`: bool - Whether this is a forced subtitle track

### SubtitleFormat (Enum)

Supported text subtitle formats.

**Values**:

- `SRT = 1` - SubRip format (.srt)
- `ASS = 2` - Advanced SubStation Alpha (.ass, .ssa)
- `VTT = 3` - WebVTT format (.vtt)

### TextSubtitleContent

Extracted and processed text content from subtitle tracks.

**Properties**:

- `SourceTrack`: TextSubtitleTrack - Reference to source track
- `ExtractedText`: string - Cleaned dialogue text
- `LineCount`: int - Number of subtitle lines processed
- `Duration`: TimeSpan - Total subtitle duration if available
- `Encoding`: string - Detected text encoding
- `ExtractedAt`: DateTime - When extraction occurred

### SubtitleProcessingResult

Extended result containing metadata about processing method.

**Properties** (extends existing IdentificationResult):

- `SubtitleSource`: SubtitleSourceType enum - PGS or Text
- `ProcessedTracks`: List&lt;TextSubtitleTrack&gt; - All tracks attempted
- `SuccessfulTrack`: TextSubtitleTrack? - Track that produced the match
- `ExtractionErrors`: List&lt;string&gt; - Errors encountered during processing

### SubtitleSourceType (Enum)

Indicates which subtitle extraction method was used.

**Values**:

- `PGS = 1` - Presentation Graphic Stream (existing workflow)
- `TextBased = 2` - Text-based subtitle formats (new workflow)

## Enhanced Existing Models

### IdentificationResult (Modified)

Add properties to track subtitle source information:

**New Properties**:

- `SubtitleSource`: SubtitleSourceType - Processing method used
- `SubtitleMetadata`: Dictionary&lt;string, object&gt;? - Additional metadata

### LabelledSubtitle (Enhanced)

Extend to support text subtitle sources:

**New Properties**:

- `SourceFormat`: SubtitleFormat? - Format if from text subtitle
- `SourceTrackIndex`: int? - Track index if from text subtitle

## Relationships

```
VideoFile
├── PGSSubtitles (existing)
└── TextSubtitleTracks (new)
    └── TextSubtitleContent
        └── SubtitleProcessingResult
            └── IdentificationResult
```

## Validation Rules

### TextSubtitleTrack

- `Index` must be >= 0
- `Format` must be valid enum value
- `Language` must be valid ISO 639-1 code if provided

### TextSubtitleContent

- `ExtractedText` must not be empty or whitespace
- `LineCount` must be > 0
- `Duration` must be positive if provided

### SubtitleProcessingResult

- Must have at least one ProcessedTrack
- If match found, SuccessfulTrack must be in ProcessedTracks
- ExtractionErrors should be empty for SuccessfulTrack

## State Transitions

### Track Processing Flow

```
TextSubtitleTrack
├─ Detected → Extracting → Extracted → Processed
│                      └─ Failed
└─ Skipped (if previous track succeeded)
```

### Content Processing Flow

```
TextSubtitleContent
├─ Raw → Cleaned → Hashed → Matched
│                         └─ NotMatched
└─ Invalid (corrupted or empty content)
```
