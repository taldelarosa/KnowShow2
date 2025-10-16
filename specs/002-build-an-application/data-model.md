# Data Model: Identify Season and Episode from AV1 Video via PGS Subtitle Comparison (CLI, JSON Output)


## Entities


### VideoFile


- fileName: string
- encodingType: string (must be AV1)
- embeddedSubtitles: List<PGSSubtitle>

### PGSSubtitle


- language: string
- timing: object (start, end)
- content: string or image data

### LabelledSubtitle


- series: string
- season: string
- episode: string
- subtitleText: string
- fuzzyHash: string

### IdentificationResult


- series: string
- season: string
- episode: string
- matchConfidence: float
- ambiguityNotes: string (optional)
- error: object (if any)

## Relationships


- VideoFile has many PGSSubtitles
- LabelledSubtitle is associated with a specific Series/Season/Episode
- IdentificationResult references LabelledSubtitle and VideoFile

## Validation Rules


- encodingType must be 'AV1' for processing
- subtitleText must not be empty
- matchConfidence must be between 0 and 1

## State Transitions


- VideoFile → PGSSubtitle extraction → Fuzzy hash comparison → IdentificationResult

---
