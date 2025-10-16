# Data Model: File Renaming Recommendations


## Overview


Data model extensions for the file renaming recommendations feature, including enhanced response models, database schema changes, and new service models.

## Enhanced Models


### IdentificationResult Extension


```csharp
public class IdentificationResult
{
    // Existing properties
    public string? Series { get; set; }
    public string? Season { get; set; }
    public string? Episode { get; set; }
    public double MatchConfidence { get; set; }
    public string? AmbiguityNotes { get; set; }
    public IdentificationError? Error { get; set; }

    // NEW: File renaming support
    public string? SuggestedFilename { get; set; }
    public bool? FileRenamed { get; set; }  // Only present when rename flag used
    public string? OriginalFilename { get; set; }  // Only present when rename flag used

    // Existing computed properties
    public bool IsAmbiguous => MatchConfidence < 0.9 && !string.IsNullOrEmpty(AmbiguityNotes);
    public bool HasError => Error != null;

    // NEW: Computed property
    public bool HasSuggestedFilename => !string.IsNullOrEmpty(SuggestedFilename);
}
```


### New Service Models


#### FilenameGenerationRequest


```csharp
public class FilenameGenerationRequest
{
    public string Series { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Episode { get; set; } = string.Empty;
    public string? EpisodeName { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public double MatchConfidence { get; set; }
}
```


#### FilenameGenerationResult


```csharp
public class FilenameGenerationResult
{
    public string SuggestedFilename { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
    public int TotalLength { get; set; }
    public bool WasTruncated { get; set; }
    public List<string> SanitizedCharacters { get; set; } = new();
}
```


#### FileRenameRequest


```csharp
public class FileRenameRequest
{
    public string OriginalPath { get; set; } = string.Empty;
    public string SuggestedFilename { get; set; } = string.Empty;
    public bool ForceOverwrite { get; set; } = false;
}
```


#### FileRenameResult


```csharp
public class FileRenameResult
{
    public bool Success { get; set; }
    public string? NewPath { get; set; }
    public string? ErrorMessage { get; set; }
    public FileRenameError? ErrorType { get; set; }
}

public enum FileRenameError
{
    FileNotFound,
    TargetExists,
    PermissionDenied,
    InvalidPath,
    DiskFull,
    PathTooLong
}
```


## Database Schema Changes


### SubtitleHashes Table Extension


```sql
-- Migration: Add EpisodeName column
ALTER TABLE SubtitleHashes ADD COLUMN EpisodeName TEXT NULL;

-- Updated table structure
CREATE TABLE SubtitleHashes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Series TEXT NOT NULL,
    Season TEXT NOT NULL,
    Episode TEXT NOT NULL,
    EpisodeName TEXT NULL,  -- NEW: For episode name storage
    OriginalText TEXT NOT NULL,
    NoTimecodesText TEXT NOT NULL,
    NoHtmlText TEXT NOT NULL,
    CleanText TEXT NOT NULL,
    UNIQUE(Series, Season, Episode)
);
```


### Database Migration Strategy


```csharp
public class DatabaseMigration_007
{
    public static void AddEpisodeNameColumn(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        // Check if column exists
        command.CommandText = @"
            SELECT COUNT(*) FROM pragma_table_info('SubtitleHashes')
            WHERE name='EpisodeName'";

        var exists = (long)command.ExecuteScalar() > 0;

        if (!exists)
        {
            command.CommandText = "ALTER TABLE SubtitleHashes ADD COLUMN EpisodeName TEXT NULL";
            command.ExecuteNonQuery();
        }
    }
}
```


## Entity Relationships


### Core Entities


- **Video File**: Input file requiring episode identification
- **Episode Identification**: Process that produces confidence score and metadata
- **Suggested Filename**: Generated based on identification results
- **Episode Name**: Optional metadata stored in database
- **File Rename Operation**: Optional file system operation

### Data Flow


```
Video File
    → Episode Identification (with confidence)
    → [High Confidence?] → Filename Generation
    → Suggested Filename in JSON Response
    → [Rename Flag?] → File Rename Operation
```


### Validation Rules


#### Filename Generation Rules


- Series name: Required, max 100 characters after sanitization
- Season: Required, format S## (zero-padded)
- Episode: Required, format E## (zero-padded)
- Episode name: Optional, max 150 characters after sanitization
- File extension: Required, preserve original
- Total length: Must not exceed 260 characters

#### High Confidence Criteria


- MatchConfidence >= 0.9 (90% confidence threshold)
- No error conditions present
- Valid series, season, episode metadata available

#### Windows Compatibility Rules


- Replace characters: `< > : " | ? * \` with single space
- Trim multiple consecutive spaces to single space
- Remove leading/trailing spaces
- Ensure path length ≤ 260 characters total

## Service Interfaces


### IFilenameService


```csharp
public interface IFilenameService
{
    FilenameGenerationResult GenerateFilename(FilenameGenerationRequest request);
    string SanitizeForWindows(string input);
    bool IsValidWindowsFilename(string filename);
    string TruncateToLimit(string filename, int maxLength = 260);
}
```


### IFileRenameService


```csharp
public interface IFileRenameService
{
    Task<FileRenameResult> RenameFileAsync(FileRenameRequest request);
    bool CanRenameFile(string filePath);
    string GetTargetPath(string originalPath, string suggestedFilename);
}
```


### IDatabaseMigrationService


```csharp
public interface IDatabaseMigrationService
{
    void MigrateToVersion(SqliteConnection connection, int targetVersion);
    bool IsVersionApplied(SqliteConnection connection, int version);
    void AddEpisodeNameColumn(SqliteConnection connection);
}
```


## State Transitions


### Filename Generation States


1. **Input Validation**: Validate required fields
2. **Confidence Check**: Verify ≥90% confidence
3. **Format Generation**: Apply naming pattern
4. **Sanitization**: Windows compatibility
5. **Length Validation**: Check 260-char limit
6. **Result Generation**: Return suggested filename

### File Rename States


1. **Pre-flight Check**: File exists, writable
2. **Target Validation**: Target path available
3. **Rename Operation**: Atomic file move
4. **Result Capture**: Success/failure status
5. **Error Handling**: Rollback if needed

## Configuration Values


### Default Settings


```csharp
public static class FilenameDefaults
{
    public const int MaxFilenameLength = 260;
    public const double MinConfidenceThreshold = 0.9;
    public const string FilenamePattern = "{Series} - S{Season:D2}E{Episode:D2}{EpisodeName}.{Extension}";
    public const string FallbackPattern = "{Series} - S{Season:D2}E{Episode:D2}.{Extension}";

    public static readonly char[] WindowsInvalidChars = { '<', '>', ':', '"', '|', '?', '*', '\\' };
    public const char ReplacementChar = ' ';
}
```


## Error Handling Patterns


### Validation Errors


- Invalid series/season/episode format
- Filename too long after sanitization
- Invalid Windows characters (logged but handled)

### Runtime Errors


- File not found during rename
- Permission denied for file operations
- Target file already exists
- Disk space insufficient

### Recovery Strategies


- Graceful degradation: Skip rename on error, still return suggestion
- Detailed error messages for debugging
- Preserve original file state on any failure
