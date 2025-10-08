# Data Model: Bulk Processing Extension for Episode Identification

**Date**: September 13, 2025
**Feature**: 009-bulk-processing-extension
**Purpose**: Define data structures and models for bulk processing functionality

## Core Entities

### BulkProcessingRequest

Primary request object for bulk operations.

```csharp
public class BulkProcessingRequest
{
    public string InputPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public BulkProcessingOptions Options { get; set; } = new();
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}
```

**Properties**:

- `InputPath`: File or directory path to process
- `IsDirectory`: True if processing directory, false for single file
- `Options`: Processing configuration and behavior options
- `CancellationToken`: Support for operation cancellation

**Validation Rules**:

- `InputPath` must be non-empty and exist on file system
- If `IsDirectory` is true, path must be a directory
- If `IsDirectory` is false, path must be a file with supported video extension

### BulkProcessingOptions

Configuration object for bulk processing behavior.

```csharp
public class BulkProcessingOptions
{
    public bool Recursive { get; set; } = true;
    public int MaxDepth { get; set; } = -1; // -1 = unlimited
    public string[] SupportedExtensions { get; set; } = { ".mkv", ".mp4", ".avi", ".mov" };
    public int BatchSize { get; set; } = 100;
    public int MaxErrors { get; set; } = -1; // -1 = unlimited
    public bool ContinueOnError { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public int MaxConcurrentFiles { get; set; } = 1;
}
```

**Properties**:

- `Recursive`: Enable subdirectory processing
- `MaxDepth`: Maximum directory recursion depth
- `SupportedExtensions`: File extensions to process
- `BatchSize`: Files processed before memory cleanup
- `MaxErrors`: Maximum errors before stopping
- `ContinueOnError`: Continue processing after individual file errors
- `ShowProgress`: Display progress information
- `MaxConcurrentFiles`: Concurrent processing limit

**Validation Rules**:

- `MaxDepth` must be -1 or positive integer
- `SupportedExtensions` must contain at least one extension
- `BatchSize` must be positive integer
- `MaxErrors` must be -1 or positive integer
- `MaxConcurrentFiles` must be positive integer

### BulkProcessingResult

Complete result object for bulk processing operations.

```csharp
public class BulkProcessingResult
{
    public int TotalFilesDiscovered { get; set; }
    public int FilesProcessedSuccessfully { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesWithErrors { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public List<FileProcessingResult> FileResults { get; set; } = new();
    public List<BulkProcessingError> Errors { get; set; } = new();
    public BulkProcessingStatus Status { get; set; }
}
```

**Properties**:

- `TotalFilesDiscovered`: Total files found in discovery phase
- `FilesProcessedSuccessfully`: Files processed without errors
- `FilesSkipped`: Files skipped (unsupported format, access denied, etc.)
- `FilesWithErrors`: Files that encountered processing errors
- `TotalProcessingTime`: Total elapsed time for operation
- `FileResults`: Individual file processing results
- `Errors`: Collection of errors encountered during processing
- `Status`: Overall operation status

**Calculated Properties**:

- `SuccessRate`: `FilesProcessedSuccessfully / (TotalFilesDiscovered - FilesSkipped)`
- `ErrorRate`: `FilesWithErrors / (TotalFilesDiscovered - FilesSkipped)`
- `ProcessingRate`: `FilesProcessedSuccessfully / TotalProcessingTime.TotalSeconds`

### FileProcessingResult

Result for individual file processing.

```csharp
public class FileProcessingResult
{
    public string FilePath { get; set; } = string.Empty;
    public FileProcessingStatus Status { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public IdentificationResult? IdentificationResult { get; set; }
}
```

**Properties**:

- `FilePath`: Absolute path to processed file
- `Status`: Processing outcome status
- `ProcessingTime`: Time taken to process this file
- `FileSizeBytes`: File size in bytes
- `ProcessedAt`: Timestamp when processing completed
- `ErrorMessage`: Error description if processing failed
- `IdentificationResult`: Episode identification result (if successful)

### BulkProcessingProgress

Progress information for long-running bulk operations.

```csharp
public class BulkProcessingProgress
{
    public int FilesCompleted { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public TimeSpan Elapsed { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
    public double ProcessingRate { get; set; } // files per second
    public int ErrorCount { get; set; }
    public BulkProcessingPhase CurrentPhase { get; set; }
}
```

**Properties**:

- `FilesCompleted`: Number of files processed so far
- `TotalFiles`: Total files to process (may be estimated during discovery)
- `CurrentFile`: Path of file currently being processed
- `Elapsed`: Time elapsed since operation started
- `EstimatedRemaining`: Estimated time to completion
- `ProcessingRate`: Current processing rate in files per second
- `ErrorCount`: Number of errors encountered so far
- `CurrentPhase`: Current processing phase

**Calculated Properties**:

- `PercentComplete`: `FilesCompleted / TotalFiles * 100`
- `FilesRemaining`: `TotalFiles - FilesCompleted`

### BulkProcessingError

Detailed error information for bulk processing failures.

```csharp
public class BulkProcessingError
{
    public string FilePath { get; set; } = string.Empty;
    public BulkProcessingErrorType ErrorType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ExceptionDetails { get; set; }
    public DateTime Timestamp { get; set; }
    public BulkProcessingPhase Phase { get; set; }
}
```

**Properties**:

- `FilePath`: Path of file that caused error
- `ErrorType`: Category of error encountered
- `Message`: Human-readable error description
- `ExceptionDetails`: Technical exception information
- `Timestamp`: When error occurred
- `Phase`: Processing phase where error occurred

## Enumerations

### BulkProcessingStatus

Overall status of bulk processing operation.

```csharp
public enum BulkProcessingStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Cancelled = 4,
    Failed = 5
}
```

### FileProcessingStatus

Status of individual file processing.

```csharp
public enum FileProcessingStatus
{
    NotProcessed = 0,
    InProgress = 1,
    Success = 2,
    Skipped = 3,
    Error = 4,
    Cancelled = 5
}
```

### BulkProcessingPhase

Current phase of bulk processing operation.

```csharp
public enum BulkProcessingPhase
{
    Initialization = 0,
    Discovery = 1,
    Validation = 2,
    Processing = 3,
    Finalization = 4,
    Completed = 5
}
```

### BulkProcessingErrorType

Categories of errors that can occur during bulk processing.

```csharp
public enum BulkProcessingErrorType
{
    FileSystemAccess = 1,      // File/directory access denied
    UnsupportedFormat = 2,     // File format not supported
    CorruptedFile = 3,         // File appears corrupted or invalid
    ProcessingFailure = 4,     // Episode identification failed
    ConfigurationError = 5,    // Invalid configuration or options
    SystemError = 6,           // System-level error (out of memory, etc.)
    CancellationRequested = 7  // Operation cancelled by user
}
```

## Service Interfaces

### IBulkProcessor

Primary service interface for bulk processing operations.

```csharp
public interface IBulkProcessor
{
    Task<BulkProcessingResult> ProcessAsync(
        BulkProcessingRequest request,
        IProgress<BulkProcessingProgress>? progress = null);

    Task<IEnumerable<string>> DiscoverFilesAsync(
        string path,
        BulkProcessingOptions options,
        CancellationToken cancellationToken = default);
}
```

### IFileDiscoveryService

Service interface for file discovery and validation.

```csharp
public interface IFileDiscoveryService
{
    IAsyncEnumerable<string> EnumerateFilesAsync(
        string path,
        BulkProcessingOptions options,
        CancellationToken cancellationToken = default);

    Task<bool> IsValidVideoFileAsync(
        string filePath,
        string[] supportedExtensions,
        CancellationToken cancellationToken = default);
}
```

### IProgressTracker

Service interface for progress tracking and reporting.

```csharp
public interface IProgressTracker
{
    void Initialize(int totalFiles);
    void ReportFileStarted(string filePath);
    void ReportFileCompleted(FileProcessingResult result);
    void ReportError(BulkProcessingError error);
    BulkProcessingProgress GetCurrentProgress();
}
```

## Data Flow

### Input Processing Flow

1. **Request Validation**: Validate `BulkProcessingRequest` parameters
2. **File Discovery**: Use `IFileDiscoveryService` to enumerate target files
3. **Progress Initialization**: Set up `IProgressTracker` with discovered file count
4. **Batch Processing**: Process files in configurable batches
5. **Result Aggregation**: Combine individual results into `BulkProcessingResult`

### Error Handling Flow

1. **Error Categorization**: Classify error into `BulkProcessingErrorType`
2. **Error Collection**: Add to `BulkProcessingError` collection
3. **Continue/Stop Decision**: Based on `MaxErrors` configuration
4. **Progress Update**: Report error to progress tracker
5. **Final Reporting**: Include all errors in final result

### Progress Reporting Flow

1. **Phase Updates**: Report current `BulkProcessingPhase`
2. **File Updates**: Report current file being processed
3. **Statistics Updates**: Update completion counts and rates
4. **Time Estimates**: Calculate remaining time based on current rate
5. **Error Reporting**: Include error counts in progress updates

## Configuration Integration

### Existing Configuration Extension

The bulk processing configuration will extend the existing `episodeidentifier.config.json` structure:

```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "recursive": true,
      "maxDepth": -1,
      "supportedExtensions": [".mkv", ".mp4", ".avi", ".mov", ".m4v", ".wmv"],
      "batchSize": 100,
      "maxErrors": -1,
      "continueOnError": true,
      "showProgress": true,
      "maxConcurrentFiles": 1
    },
    "performance": {
      "memoryCleanupBatchSize": 100,
      "progressUpdateInterval": 1000
    }
  }
}
```

## Database Integration

### Existing Database Schema Usage

Bulk processing will leverage existing database tables:

- **episode_hashes**: Store fuzzy hashes for processed files
- **identification_results**: Cache identification results
- **processing_metadata**: Track processing history

### Additional Tables (if needed)

```sql
-- Bulk processing session tracking
CREATE TABLE IF NOT EXISTS bulk_processing_sessions (
    session_id TEXT PRIMARY KEY,
    start_time DATETIME NOT NULL,
    end_time DATETIME,
    input_path TEXT NOT NULL,
    total_files INTEGER,
    successful_files INTEGER,
    failed_files INTEGER,
    status TEXT NOT NULL
);

-- Individual file processing records within sessions
CREATE TABLE IF NOT EXISTS bulk_file_processing (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    processing_time_ms INTEGER,
    file_size_bytes INTEGER,
    status TEXT NOT NULL,
    error_message TEXT,
    processed_at DATETIME NOT NULL,
    FOREIGN KEY (session_id) REFERENCES bulk_processing_sessions(session_id)
);
```

## Validation and Constraints

### Input Validation

- All file paths must be validated for existence and accessibility
- Supported extensions must be normalized (lowercase, with leading dot)
- Numeric options must be within reasonable ranges
- Directory depth limits must be enforced to prevent infinite recursion

### Resource Constraints

- Maximum batch size: 1000 files (to prevent excessive memory usage)
- Maximum concurrent files: 10 (to prevent system overload)
- Maximum directory depth: 100 levels (to prevent stack overflow)
- Minimum progress update interval: 100ms (to prevent UI flooding)

### Error Handling Constraints

- Maximum error collection: 10,000 errors (to prevent memory exhaustion)
- Error message length: 1000 characters maximum
- Exception details length: 5000 characters maximum

This data model provides comprehensive support for bulk processing operations while maintaining compatibility with existing episode identification functionality and following established patterns in the application architecture.
