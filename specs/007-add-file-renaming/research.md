# Research: File Renaming Recommendations


## Overview


Research for implementing file renaming recommendations feature that adds suggested filename generation and optional automatic renaming to the episode identification system.

## Windows Filesystem Compatibility Research


### Decision: Use System.IO.Path for validation and sanitization


**Rationale**: Built-in .NET method provides reliable Windows compatibility checking
**Alternatives considered**: Custom regex patterns, third-party libraries

### Disallowed Characters


- Windows disallowed: `< > : " | ? * \`
- Replacement strategy: Single space character for each disallowed character
- Path length limit: 260 characters total (Windows MAX_PATH)

### Reference Implementation Pattern


```csharp
public static string SanitizeForWindows(string input)
{
    char[] invalidChars = Path.GetInvalidFileNameChars();
    return string.Join(" ", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
}
```


## JSON Response Extension Research


### Decision: Extend existing IdentificationResult model


**Rationale**: Maintains backward compatibility, follows existing pattern
**Alternatives considered**: New response wrapper, separate endpoint

### Current Structure Analysis


```csharp
public class IdentificationResult
{
    public string? Series { get; set; }
    public string? Season { get; set; }
    public string? Episode { get; set; }
    public double MatchConfidence { get; set; }
    // NEW: public string? SuggestedFilename { get; set; }
}
```


## Database Schema Extension Research


### Decision: Add nullable EpisodeName column to SubtitleHashes table


**Rationale**: Enables episode name storage without breaking existing data
**Alternatives considered**: Separate table, JSON field

### Current Schema Analysis


```sql
CREATE TABLE SubtitleHashes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Series TEXT NOT NULL,
    Season TEXT NOT NULL,
    Episode TEXT NOT NULL,
    -- NEW: EpisodeName TEXT NULL
    OriginalText TEXT NOT NULL,
    NoTimecodesText TEXT NOT NULL,
    NoHtmlText TEXT NOT NULL,
    CleanText TEXT NOT NULL
);
```


## CLI Integration Research


### Decision: Add --rename boolean flag to existing command structure


**Rationale**: Simple boolean flag, integrates with existing System.CommandLine setup
**Alternatives considered**: Separate rename command, auto-rename based on confidence

### Command Line Pattern


```bash
dotnet run -- --input video.mkv --hash-db hashes.db --rename
```


## Filename Format Research


### Decision: "SeriesName - S##E## - EpisodeName.ext" pattern


**Rationale**: Industry standard, Plex/Jellyfin compatible, human readable
**Alternatives considered**: S##E##SeriesName.ext, Series.S##.E##.ext

### Format Examples


- With episode name: "Breaking Bad - S01E05 - Gray Matter.mkv"
- Without episode name: "Breaking Bad - S01E05.mkv"
- With sanitization: "Show: Name - S01E01 - Episode  Title.mkv"

## File Operation Safety Research


### Decision: Use File.Move with collision detection


**Rationale**: Atomic operation, built-in error handling
**Alternatives considered**: Copy then delete, third-party file utilities

### Safety Measures


- Check file exists before rename
- Check target doesn't exist (prevent overwrites)
- Preserve original file on failure
- Log all file operations

## Performance Considerations


### Filename Generation


- Target: <10ms per filename generation
- String operations only, no file I/O during generation
- Pre-compile regex patterns for sanitization

### Database Operations


- Leverage existing SQLite connection
- Single query to check for episode names
- Batch updates for schema migration

## Integration Points


### Existing Services to Modify


- `FuzzyHashService.cs`: Add EpisodeName column, update queries
- `IdentificationResult.cs`: Add SuggestedFilename property
- `Program.cs`: Add --rename flag, file renaming logic

### New Services to Create


- `FilenameService.cs`: Generate and sanitize filenames
- `FileRenameService.cs`: Handle file rename operations

## Risk Mitigation


### File Operation Risks


- File locks: Check file accessibility before rename
- Permission errors: Graceful error handling with specific messages
- Disk space: No additional space required (rename, not copy)

### Compatibility Risks


- Cross-platform paths: Use Path.Combine for all path operations
- Character encoding: Use UTF-8 for all string operations
- Legacy data: Nullable EpisodeName column maintains compatibility

## Testing Strategy


### Contract Tests


- Filename generation with various inputs
- Windows character sanitization
- Length limit enforcement
- Episode name handling (present/missing)

### Integration Tests


- End-to-end filename suggestion
- Database schema migration
- File rename operations
- Error scenarios (permission denied, file not found)

### Unit Tests


- String sanitization edge cases
- Filename format validation
- Path length calculations
