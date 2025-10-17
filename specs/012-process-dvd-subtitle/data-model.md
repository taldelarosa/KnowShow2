# Data Model: DVD Subtitle (VobSub) OCR Processing

**Feature**: 012-process-dvd-subtitle  
**Date**: 2025-10-17  
**Phase**: 1 - Design

## Overview

This document defines the data models required for DVD subtitle extraction and OCR processing. These models represent intermediate state and results from the extraction and OCR pipeline.

## Existing Models (No Changes)

### SubtitleTrackInfo
**Location**: `src/EpisodeIdentifier.Core/Models/SubtitleTrackInfo.cs`

Already supports DVD subtitles with `CodecName = "dvd_subtitle"`. No changes needed.

**Fields**:
- `int Index`: Stream index in video file
- `string CodecName`: Codec identifier (e.g., "dvd_subtitle", "hdmv_pgs_subtitle", "subrip")
- `string? Language`: Language code (e.g., "eng", "spa")
- `string? Title`: Track title/description

---

## New Models

### 1. VobSubExtractionResult

**Purpose**: Represents the result of extracting VobSub files from an MKV container.

**Location**: `src/EpisodeIdentifier.Core/Models/VobSubExtractionResult.cs`

**Fields**:

| Field | Type | Required | Description | Validation |
|-------|------|----------|-------------|------------|
| `Success` | `bool` | Yes | Whether extraction succeeded | - |
| `IdxFilePath` | `string?` | No | Absolute path to extracted .idx file | Must exist if Success=true |
| `SubFilePath` | `string?` | No | Absolute path to extracted .sub file | Must exist if Success=true |
| `ErrorMessage` | `string?` | No | Error details if extraction failed | Required if Success=false |
| `ExtractionDuration` | `TimeSpan` | Yes | Time taken for extraction | Must be >= 0 |
| `TrackIndex` | `int` | Yes | Source subtitle track index | Must be >= 0 |
| `SourceVideoPath` | `string` | Yes | Source video file path | Must be non-empty |

**Validation Rules**:
- If `Success == true`:
  - `IdxFilePath` must be non-null and file must exist
  - `SubFilePath` must be non-null and file must exist
  - `ErrorMessage` should be null
- If `Success == false`:
  - `ErrorMessage` must be non-null and non-empty
  - `IdxFilePath` and `SubFilePath` may be null

**State Transitions**:
```
[Initial] → [Extracting] → [Success/Failure]
```

**Example**:
```csharp
// Success case
new VobSubExtractionResult
{
    Success = true,
    IdxFilePath = "/tmp/vobsub_abc123/subtitle.idx",
    SubFilePath = "/tmp/vobsub_abc123/subtitle.sub",
    ErrorMessage = null,
    ExtractionDuration = TimeSpan.FromSeconds(2.5),
    TrackIndex = 3,
    SourceVideoPath = "/path/to/video.mkv"
}

// Failure case
new VobSubExtractionResult
{
    Success = false,
    IdxFilePath = null,
    SubFilePath = null,
    ErrorMessage = "mkvextract exited with code 1: Track 3 not found",
    ExtractionDuration = TimeSpan.FromSeconds(0.5),
    TrackIndex = 3,
    SourceVideoPath = "/path/to/video.mkv"
}
```

---

### 2. VobSubOcrResult

**Purpose**: Represents the result of performing OCR on VobSub subtitle images.

**Location**: `src/EpisodeIdentifier.Core/Models/VobSubOcrResult.cs`

**Fields**:

| Field | Type | Required | Description | Validation |
|-------|------|----------|-------------|------------|
| `Success` | `bool` | Yes | Whether OCR succeeded | - |
| `ExtractedText` | `string?` | No | OCR output text (normalized) | Must be non-empty if Success=true |
| `ConfidenceScore` | `double` | Yes | Aggregate OCR confidence (0.0-1.0) | Must be 0.0-1.0 |
| `CharacterCount` | `int` | Yes | Total characters recognized | Must be >= 0 |
| `ErrorMessage` | `string?` | No | Error details if OCR failed | Required if Success=false |
| `OcrDuration` | `TimeSpan` | Yes | Time taken for OCR processing | Must be >= 0 |
| `ImageCount` | `int` | Yes | Number of subtitle images processed | Must be >= 0 |
| `Language` | `string` | Yes | OCR language code used | Must be non-empty |

**Validation Rules**:
- If `Success == true`:
  - `ExtractedText` must be non-null and non-empty
  - `CharacterCount` must be > 0
  - `ConfidenceScore` should be > 0.0 (typically 0.5-1.0)
  - `ErrorMessage` should be null
- If `Success == false`:
  - `ErrorMessage` must be non-null and non-empty
  - `ExtractedText` may be null or empty
  - `CharacterCount` may be 0

**Text Normalization**:
The `ExtractedText` field should contain normalized text:
- Whitespace collapsed
- Timecodes removed (if present)
- HTML tags removed
- Multiple newlines reduced to single newlines
- Leading/trailing whitespace trimmed

**Confidence Calculation**:
```
ConfidenceScore = (Sum of all character confidences) / (Total characters)
```

**State Transitions**:
```
[Initial] → [Image Extraction] → [OCR Processing] → [Text Normalization] → [Success/Failure]
```

**Example**:
```csharp
// Success case
new VobSubOcrResult
{
    Success = true,
    ExtractedText = "Previously on Criminal Minds...\nThe team investigates a series of murders.",
    ConfidenceScore = 0.87,
    CharacterCount = 89,
    ErrorMessage = null,
    OcrDuration = TimeSpan.FromSeconds(12.3),
    ImageCount = 45,
    Language = "eng"
}

// Failure case
new VobSubOcrResult
{
    Success = false,
    ExtractedText = null,
    ConfidenceScore = 0.0,
    CharacterCount = 0,
    ErrorMessage = "Tesseract failed: No text regions detected in images",
    OcrDuration = TimeSpan.FromSeconds(3.1),
    ImageCount = 45,
    Language = "eng"
}

// Low quality case (still success, but low confidence)
new VobSubOcrResult
{
    Success = true,
    ExtractedText = "Prv ousl on Cr m nal M nds...",
    ConfidenceScore = 0.42,  // Below recommended 70% threshold
    CharacterCount = 35,
    ErrorMessage = null,
    OcrDuration = TimeSpan.FromSeconds(8.7),
    ImageCount = 45,
    Language = "eng"
}
```

---

## Model Relationships

```
VideoFile (MKV)
    ↓ (contains)
SubtitleTrackInfo (dvd_subtitle codec)
    ↓ (extracted by VobSubExtractor)
VobSubExtractionResult (IdxFilePath, SubFilePath)
    ↓ (processed by VobSubOcrService)
VobSubOcrResult (ExtractedText)
    ↓ (used by)
EpisodeIdentificationService
    ↓ (produces)
IdentificationResult (existing)
```

## Data Flow

1. **Input**: `FileInfo` (video file) + `SubtitleTrackInfo` (dvd_subtitle track)
2. **Extraction**: `VobSubExtractor.ExtractAsync()` → `VobSubExtractionResult`
3. **OCR**: `VobSubOcrService.PerformOcrAsync()` → `VobSubOcrResult`
4. **Normalization**: Extract `ExtractedText` from `VobSubOcrResult`
5. **Identification**: Pass text to `EpisodeIdentificationService.IdentifyEpisodeAsync()`
6. **Output**: `IdentificationResult` (existing model)

## Error Handling

### Extraction Errors
- **mkvextract not found**: Return failure with `ErrorMessage = "mkvextract tool not found. Please install mkvtoolnix."`
- **Invalid track index**: Return failure with `ErrorMessage = "Track {index} not found or not a subtitle track"`
- **Extraction failed**: Return failure with stderr from mkvextract
- **Timeout**: Throw `OperationCanceledException` (handled by caller)

### OCR Errors
- **Tesseract not found**: Return failure with `ErrorMessage = "tesseract OCR engine not found. Please install tesseract-ocr."`
- **No images extracted**: Return failure with `ErrorMessage = "No subtitle images could be extracted from VobSub files"`
- **No text recognized**: Return success with `ExtractedText = ""` and `ConfidenceScore = 0.0`
- **Timeout**: Throw `OperationCanceledException` (handled by caller)

## Serialization

Both models should be serializable to JSON for logging and debugging purposes:

```csharp
// VobSubExtractionResult JSON
{
  "success": true,
  "idxFilePath": "/tmp/vobsub_abc123/subtitle.idx",
  "subFilePath": "/tmp/vobsub_abc123/subtitle.sub",
  "errorMessage": null,
  "extractionDuration": "00:00:02.5000000",
  "trackIndex": 3,
  "sourceVideoPath": "/path/to/video.mkv"
}

// VobSubOcrResult JSON
{
  "success": true,
  "extractedText": "Previously on Criminal Minds...",
  "confidenceScore": 0.87,
  "characterCount": 89,
  "errorMessage": null,
  "ocrDuration": "00:00:12.3000000",
  "imageCount": 45,
  "language": "eng"
}
```

## Testing Considerations

### Unit Tests
- Test validation rules for both models
- Test serialization/deserialization
- Test state transitions

### Integration Tests
- Use real video files with DVD subtitles
- Verify file paths in extraction results point to actual files
- Verify OCR results contain expected text patterns
- Test error scenarios with missing tools

### Contract Tests
- Verify model structure matches interface contracts
- Ensure all required fields are populated
- Validate error message formats

---

**Data Model Complete**: Ready for contract generation (Phase 1 continued)
