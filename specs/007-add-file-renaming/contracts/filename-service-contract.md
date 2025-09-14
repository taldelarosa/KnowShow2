# Service Contract: Filename Generation Service

## Overview

Service contract for generating Windows-compatible filenames from episode identification data.

## IFilenameService Interface

### GenerateFilename Method

```csharp
FilenameGenerationResult GenerateFilename(FilenameGenerationRequest request)
```

**Input Contract**:

```csharp
public class FilenameGenerationRequest
{
    [Required]
    public string Series { get; set; }        // Required, non-empty

    [Required]
    public string Season { get; set; }        // Required, format: "01", "02", etc.

    [Required]
    public string Episode { get; set; }       // Required, format: "01", "02", etc.

    public string? EpisodeName { get; set; }  // Optional episode name

    [Required]
    public string FileExtension { get; set; } // Required, e.g., ".mkv", ".mp4"

    [Range(0.0, 1.0)]
    public double MatchConfidence { get; set; } // Must be ≥ 0.9 for generation
}
```

**Output Contract**:

```csharp
public class FilenameGenerationResult
{
    public string SuggestedFilename { get; set; }     // Generated filename
    public bool IsValid { get; set; }                 // True if valid Windows filename
    public string? ValidationError { get; set; }      // Error message if invalid
    public int TotalLength { get; set; }              // Character count
    public bool WasTruncated { get; set; }            // True if length limited
    public List<string> SanitizedCharacters { get; set; } // Characters that were replaced
}
```

### SanitizeForWindows Method

```csharp
string SanitizeForWindows(string input)
```

**Behavior**:

- Replace `< > : " | ? * \` with single space
- Collapse multiple consecutive spaces to single space
- Trim leading and trailing spaces
- Return sanitized string

**Examples**:

```csharp
SanitizeForWindows("Show: Title") → "Show  Title"
SanitizeForWindows("File\"Name\"") → "File Name "
SanitizeForWindows("Path\\To\\File") → "Path To File"
```

### IsValidWindowsFilename Method

```csharp
bool IsValidWindowsFilename(string filename)
```

**Validation Rules**:

- Length ≤ 260 characters
- No Windows invalid characters
- Not empty or whitespace only
- Valid file extension present

### TruncateToLimit Method

```csharp
string TruncateToLimit(string filename, int maxLength = 260)
```

**Behavior**:

- Preserve file extension
- Truncate episode name first
- Truncate series name if necessary
- Maintain format structure
- Ensure result ≤ maxLength

## Service Contract Validation

### Confidence Threshold

- **Requirement**: MatchConfidence ≥ 0.9
- **Behavior**: Generate filename only for high confidence
- **Error**: Return validation error for low confidence

### Required Fields Validation

```csharp
// All required fields must be non-null and non-empty
if (string.IsNullOrWhiteSpace(request.Series))
    return new FilenameGenerationResult
    {
        IsValid = false,
        ValidationError = "Series name is required"
    };
```

### Season/Episode Format Validation

```csharp
// Season and Episode must be numeric strings
if (!int.TryParse(request.Season, out int seasonNum) || seasonNum < 1)
    return new FilenameGenerationResult
    {
        IsValid = false,
        ValidationError = "Invalid season format"
    };
```

### File Extension Validation

```csharp
// Extension must start with dot and be valid
if (!request.FileExtension.StartsWith(".") || request.FileExtension.Length < 2)
    return new FilenameGenerationResult
    {
        IsValid = false,
        ValidationError = "Invalid file extension format"
    };
```

## Filename Generation Algorithm

### Step 1: Format Construction

```csharp
string baseFormat = EpisodeName != null
    ? "{Series} - S{Season:D2}E{Episode:D2} - {EpisodeName}"
    : "{Series} - S{Season:D2}E{Episode:D2}";
```

### Step 2: Component Sanitization

```csharp
string sanitizedSeries = SanitizeForWindows(request.Series);
string sanitizedEpisodeName = SanitizeForWindows(request.EpisodeName ?? "");
```

### Step 3: Length Validation and Truncation

```csharp
string fullFilename = $"{baseFormat}.{extension}";
if (fullFilename.Length > 260)
{
    // Truncation logic preserving structure
    result.WasTruncated = true;
}
```

### Step 4: Final Validation

```csharp
result.IsValid = IsValidWindowsFilename(result.SuggestedFilename);
result.TotalLength = result.SuggestedFilename.Length;
```

## Error Handling Contract

### Input Validation Errors

```csharp
// Low confidence
{ IsValid = false, ValidationError = "Confidence below threshold (0.9)" }

// Missing series
{ IsValid = false, ValidationError = "Series name is required" }

// Invalid season
{ IsValid = false, ValidationError = "Season must be numeric (01, 02, etc.)" }

// Invalid episode
{ IsValid = false, ValidationError = "Episode must be numeric (01, 02, etc.)" }

// Invalid extension
{ IsValid = false, ValidationError = "File extension must start with dot" }
```

### Generation Errors

```csharp
// Filename too long after truncation
{ IsValid = false, ValidationError = "Cannot generate valid filename within length limit" }

// All characters invalid
{ IsValid = false, ValidationError = "Series name contains only invalid characters" }
```

## Performance Contract

### Response Time

- **Target**: < 10ms per filename generation
- **Maximum**: < 50ms for complex sanitization

### Memory Usage

- **Target**: < 1MB per request
- **Behavior**: No memory leaks for repeated calls

### Thread Safety

- **Requirement**: All methods must be thread-safe
- **Implementation**: Stateless service design

## Test Scenarios

### Valid Inputs

```csharp
// Standard case with episode name
new FilenameGenerationRequest
{
    Series = "Breaking Bad",
    Season = "01",
    Episode = "05",
    EpisodeName = "Gray Matter",
    FileExtension = ".mkv",
    MatchConfidence = 0.95
}
// Expected: "Breaking Bad - S01E05 - Gray Matter.mkv"

// No episode name
new FilenameGenerationRequest
{
    Series = "The Office",
    Season = "02",
    Episode = "01",
    EpisodeName = null,
    FileExtension = ".mp4",
    MatchConfidence = 0.92
}
// Expected: "The Office - S02E01.mp4"
```

### Sanitization Cases

```csharp
// Windows invalid characters
Series = "Show: Name"  // → "Show  Name"
EpisodeName = "Episode \"Title\""  // → "Episode  Title "

// Multiple spaces
Series = "Show    Name"  // → "Show Name"
```

### Edge Cases

```csharp
// Very long names
Series = "Very Long Series Name That Exceeds Normal Limits"
EpisodeName = "Very Long Episode Title That Also Exceeds Normal Limits"
// Expected: Truncated to fit 260 chars

// Low confidence (should fail)
MatchConfidence = 0.75
// Expected: IsValid = false, ValidationError about confidence

// Empty series (should fail)
Series = ""
// Expected: IsValid = false, ValidationError about required series
```

## Integration Points

### Database Integration

- Service retrieves episode names from SubtitleHashes.EpisodeName column
- Handles null episode names gracefully
- No direct database dependencies (data passed via request)

### CLI Integration

- Called from Program.cs after successful episode identification
- Result.SuggestedFilename added to JSON response
- Error handling integrated with existing error patterns

### File System Integration

- Generated filename passed to IFileRenameService
- No direct file system access in this service
- Pure string manipulation and validation only
