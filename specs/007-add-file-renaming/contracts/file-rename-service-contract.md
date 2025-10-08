# Service Contract: File Rename Service


## Overview


Service contract for performing safe file rename operations with error handling and validation.

## IFileRenameService Interface


### RenameFileAsync Method


```csharp
Task<FileRenameResult> RenameFileAsync(FileRenameRequest request)
```


**Input Contract**:

```csharp
public class FileRenameRequest
{
    [Required]
    public string OriginalPath { get; set; }      // Full path to source file

    [Required]
    public string SuggestedFilename { get; set; } // Target filename (name only, not path)

    public bool ForceOverwrite { get; set; } = false; // Allow overwriting existing files
}
```


**Output Contract**:

```csharp
public class FileRenameResult
{
    public bool Success { get; set; }              // True if rename succeeded
    public string? NewPath { get; set; }           // Full path to renamed file
    public string? ErrorMessage { get; set; }      // Human-readable error message
    public FileRenameError? ErrorType { get; set; } // Categorized error type
}

public enum FileRenameError
{
    FileNotFound,      // Source file doesn't exist
    TargetExists,      // Target file already exists (and ForceOverwrite = false)
    PermissionDenied,  // Insufficient permissions
    InvalidPath,       // Invalid file path format
    DiskFull,          // Insufficient disk space
    PathTooLong        // Generated path exceeds system limits
}
```


### CanRenameFile Method


```csharp
bool CanRenameFile(string filePath)
```


**Validation Checks**:

- File exists at specified path
- File is not locked/in use
- Directory has write permissions
- Path is valid format

### GetTargetPath Method


```csharp
string GetTargetPath(string originalPath, string suggestedFilename)
```


**Behavior**:

- Extract directory from original path
- Combine with suggested filename
- Return full target path
- Validate path length and format

## Service Implementation Contract


### Pre-Operation Validation


```csharp
// Input validation
if (string.IsNullOrWhiteSpace(request.OriginalPath))
    return new FileRenameResult
    {
        Success = false,
        ErrorType = FileRenameError.InvalidPath,
        ErrorMessage = "Original path cannot be empty"
    };

// File existence check
if (!File.Exists(request.OriginalPath))
    return new FileRenameResult
    {
        Success = false,
        ErrorType = FileRenameError.FileNotFound,
        ErrorMessage = $"Source file not found: {request.OriginalPath}"
    };
```


### Target Path Generation


```csharp
string directory = Path.GetDirectoryName(request.OriginalPath);
string targetPath = Path.Combine(directory, request.SuggestedFilename);

// Path length validation
if (targetPath.Length > 260)  // Windows path limit
    return new FileRenameResult
    {
        Success = false,
        ErrorType = FileRenameError.PathTooLong,
        ErrorMessage = "Target path exceeds maximum length"
    };
```


### Collision Detection


```csharp
// Target existence check
if (File.Exists(targetPath) && !request.ForceOverwrite)
    return new FileRenameResult
    {
        Success = false,
        ErrorType = FileRenameError.TargetExists,
        ErrorMessage = $"Target file already exists: {targetPath}"
    };
```


### Atomic Rename Operation


```csharp
try
{
    File.Move(request.OriginalPath, targetPath);
    return new FileRenameResult
    {
        Success = true,
        NewPath = targetPath
    };
}
catch (UnauthorizedAccessException)
{
    return new FileRenameResult
    {
        Success = false,
        ErrorType = FileRenameError.PermissionDenied,
        ErrorMessage = "Permission denied: Cannot rename file"
    };
}
```


## Error Handling Contract


### File System Errors


```csharp
// File not found
{
    Success = false,
    ErrorType = FileRenameError.FileNotFound,
    ErrorMessage = "Source file not found: /path/to/file.mkv"
}

// Target exists
{
    Success = false,
    ErrorType = FileRenameError.TargetExists,
    ErrorMessage = "Target file already exists: New Name.mkv"
}

// Permission denied
{
    Success = false,
    ErrorType = FileRenameError.PermissionDenied,
    ErrorMessage = "Permission denied: Cannot write to directory"
}

// Disk full
{
    Success = false,
    ErrorType = FileRenameError.DiskFull,
    ErrorMessage = "Insufficient disk space for rename operation"
}
```


### Path Validation Errors


```csharp
// Invalid path format
{
    Success = false,
    ErrorType = FileRenameError.InvalidPath,
    ErrorMessage = "Invalid file path format"
}

// Path too long
{
    Success = false,
    ErrorType = FileRenameError.PathTooLong,
    ErrorMessage = "Target path exceeds maximum length (260 characters)"
}
```


## Safety Requirements


### Atomic Operations


- Use `File.Move()` for atomic rename operation
- No intermediate states or temporary files
- Either complete success or complete failure

### Data Preservation


- Original file preserved on any error
- No data loss under any circumstances
- Rollback not required (operation is atomic)

### Validation Sequence


1. Input parameter validation
2. Source file existence check
3. Target path generation and validation
4. Target collision detection
5. Permission verification
6. Atomic rename operation
7. Result verification

## Performance Contract


### Response Time


- **Target**: < 100ms for local file operations
- **Maximum**: < 1000ms for network drives

### Resource Usage


- **Memory**: < 1MB per operation (no file content loading)
- **CPU**: Minimal (file system operations only)
- **I/O**: Single file move operation

### Concurrency


- **Thread Safety**: All methods must be thread-safe
- **File Locking**: Handle file lock conflicts gracefully
- **Simultaneous Operations**: Support multiple concurrent renames

## Test Scenarios


### Success Cases


```csharp
// Standard rename
Request: { OriginalPath = "/path/video.mkv", SuggestedFilename = "Show - S01E01.mkv" }
Result: { Success = true, NewPath = "/path/Show - S01E01.mkv" }

// Force overwrite
Request: { OriginalPath = "/path/video.mkv", SuggestedFilename = "existing.mkv", ForceOverwrite = true }
Result: { Success = true, NewPath = "/path/existing.mkv" }
```


### Error Cases


```csharp
// File not found
Request: { OriginalPath = "/nonexistent.mkv", SuggestedFilename = "new.mkv" }
Result: { Success = false, ErrorType = FileNotFound }

// Target exists (no force)
Request: { OriginalPath = "/video.mkv", SuggestedFilename = "existing.mkv", ForceOverwrite = false }
Result: { Success = false, ErrorType = TargetExists }

// Permission denied
Request: { OriginalPath = "/readonly/video.mkv", SuggestedFilename = "new.mkv" }
Result: { Success = false, ErrorType = PermissionDenied }

// Path too long
Request: { OriginalPath = "/video.mkv", SuggestedFilename = "very-long-name-that-exceeds-limits..." }
Result: { Success = false, ErrorType = PathTooLong }
```


### Edge Cases


```csharp
// Same filename (no-op)
Request: { OriginalPath = "/video.mkv", SuggestedFilename = "video.mkv" }
Result: { Success = true, NewPath = "/video.mkv" }

// Special characters in path
Request: { OriginalPath = "/path with spaces/video.mkv", SuggestedFilename = "new name.mkv" }
Result: { Success = true, NewPath = "/path with spaces/new name.mkv" }

// Cross-directory rename (not supported)
Request: { OriginalPath = "/dir1/video.mkv", SuggestedFilename = "../dir2/video.mkv" }
Result: { Success = false, ErrorType = InvalidPath }
```


## Integration Points


### CLI Integration


- Called from Program.cs when --rename flag is present
- Result integrated into JSON response
- Error handling follows existing CLI error patterns

### Filename Service Integration


- Receives suggested filename from IFilenameService
- No direct dependency (filename passed as parameter)
- Validates filename format before attempting rename

### Logging Integration


- Log all rename attempts (success and failure)
- Include original and target paths in logs
- Use structured logging for error categorization

## Security Considerations


### Path Traversal Prevention


- Validate all paths stay within intended directory
- Reject paths containing "../" or similar patterns
- Use Path.GetFullPath() for normalization

### Permission Validation


- Check directory write permissions before attempting rename
- Handle access denied scenarios gracefully
- No elevation of privileges

### File System Safety


- No deletion of original file until rename confirmed
- Atomic operations prevent partial states
- Validate target filename for security (no executable extensions in unexpected contexts)
