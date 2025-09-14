# Contract: IBulkProcessor Service






**Date**: September 13, 2025
**Feature**: 009-bulk-processing-extension
**Contract**: IBulkProcessor
**Purpose**: Define the primary service contract for bulk processing operations

## Interface Definition






```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Core.Services
{
    /// <summary>
    /// Primary service for bulk processing video files for episode identification.
    /// Handles both individual files and directory traversal with progress reporting.
    /// </summary>
    public interface IBulkProcessor
    {
        /// <summary>
        /// Process a single file or directory for episode identification.
        /// </summary>
        /// <param name="request">Bulk processing request with input path and options</param>
        /// <param name="progress">Optional progress reporter for long-running operations</param>
        /// <returns>Complete results of the bulk processing operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="ArgumentException">Thrown when request contains invalid data</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when input directory does not exist</exception>
        /// <exception cref="FileNotFoundException">Thrown when input file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to input path is denied</exception>
        Task<BulkProcessingResult> ProcessAsync(
            BulkProcessingRequest request,
            IProgress<BulkProcessingProgress>? progress = null);

        /// <summary>
        /// Discover all video files in the specified path without processing them.
        /// Useful for estimating processing time and validating input paths.
        /// </summary>
        /// <param name="path">File or directory path to discover</param>
        /// <param name="options">Discovery options (recursive, extensions, etc.)</param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <returns>Enumerable of file paths discovered</returns>
        /// <exception cref="ArgumentNullException">Thrown when path or options is null</exception>
        /// <exception cref="ArgumentException">Thrown when path is empty or invalid</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when directory does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to path is denied</exception>
        Task<IEnumerable<string>> DiscoverFilesAsync(
            string path,
            BulkProcessingOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate a bulk processing request without executing it.
        /// Checks path existence, permissions, and option validity.
        /// </summary>
        /// <param name="request">Request to validate</param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <returns>Validation result with any issues found</returns>
        Task<BulkProcessingValidationResult> ValidateRequestAsync(
            BulkProcessingRequest request,
            CancellationToken cancellationToken = default);
    }
}
```






## Behavioral Contracts






### ProcessAsync Method






**Preconditions**:

- `request` parameter must not be null
- `request.InputPath` must be non-empty and point to existing file or directory
- `request.Options` must contain valid configuration values
- If processing a directory, must have read permissions on directory
- If processing a file, file must exist and have supported extension

**Postconditions**:

- Returns complete `BulkProcessingResult` with processing statistics
- All discovered files are either processed successfully or recorded as errors
- Progress reporter (if provided) receives regular updates during operation
- Operation respects cancellation token from request
- No partial state left behind if operation is cancelled
- All file handles and resources are properly disposed

**Invariants**:

- `FilesProcessedSuccessfully + FilesWithErrors + FilesSkipped == TotalFilesDiscovered`
- `FileResults.Count == TotalFilesDiscovered`
- All errors are captured in `Errors` collection
- Processing time is accurately measured and reported

**Error Handling**:

- Individual file processing errors do not stop the entire operation (when `ContinueOnError = true`)
- System-level errors (out of memory, disk full) may cause operation to fail completely
- All errors are categorized and reported with sufficient detail for troubleshooting
- Partial results are returned even if operation fails or is cancelled

**Performance Guarantees**:

- Memory usage remains bounded regardless of number of files processed
- Progress updates occur at reasonable intervals (not more than once per 100ms)
- File enumeration uses streaming approach to avoid loading all paths into memory
- Batch processing prevents resource exhaustion during very large operations

### DiscoverFilesAsync Method






**Preconditions**:

- `path` parameter must be non-empty and point to existing file or directory
- `options` parameter must not be null and contain valid values
- Must have read permissions on the specified path

**Postconditions**:

- Returns all video files found in the specified path
- Files are returned in deterministic order (alphabetical by full path)
- Only files with extensions matching `options.SupportedExtensions` are included
- Respects `options.Recursive` and `options.MaxDepth` settings
- Operation can be cancelled via `cancellationToken`

**Invariants**:

- All returned paths point to existing files
- All returned files have extensions in the supported list
- No duplicate paths are returned
- Directory traversal respects depth limits

**Performance Guarantees**:

- Uses streaming enumeration to avoid memory issues with large directories
- Cancellation is checked regularly during enumeration
- File system calls are minimized through efficient enumeration patterns

### ValidateRequestAsync Method






**Preconditions**:

- `request` parameter must not be null

**Postconditions**:

- Returns comprehensive validation result
- All validation issues are reported with clear descriptions
- Validation does not modify the request object
- Validation operation is fast (typically under 100ms)

**Invariants**:

- Validation result accurately reflects request validity
- All potential runtime issues are identified where possible
- Validation logic matches actual processing requirements

## Usage Examples






### Basic File Processing






```csharp
var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
var request = new BulkProcessingRequest
{
    InputPath = "/path/to/video.mkv",
    IsDirectory = false,
    Options = new BulkProcessingOptions
    {
        ShowProgress = true,
        ContinueOnError = true
    }
};

var result = await processor.ProcessAsync(request);
Console.WriteLine($"Processed {result.FilesProcessedSuccessfully} files successfully");
```






### Directory Processing with Progress






```csharp
var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
var progress = new Progress<BulkProcessingProgress>(p =>
{
    Console.WriteLine($"Processing {p.CurrentFile} ({p.FilesCompleted}/{p.TotalFiles})");
    Console.WriteLine($"Progress: {p.PercentComplete:F1}% - ETA: {p.EstimatedRemaining}");
});

var request = new BulkProcessingRequest
{
    InputPath = "/media/tv-shows",
    IsDirectory = true,
    Options = new BulkProcessingOptions
    {
        Recursive = true,
        MaxDepth = 5,
        BatchSize = 50,
        MaxErrors = 10
    }
};

var result = await processor.ProcessAsync(request, progress);
```






### File Discovery






```csharp
var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
var options = new BulkProcessingOptions
{
    Recursive = true,
    SupportedExtensions = new[] { ".mkv", ".mp4" }
};

var files = await processor.DiscoverFilesAsync("/media/movies", options);
Console.WriteLine($"Found {files.Count()} video files");
```






### Request Validation






```csharp
var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
var request = new BulkProcessingRequest
{
    InputPath = "/some/path",
    IsDirectory = true,
    Options = new BulkProcessingOptions { MaxDepth = -5 } // Invalid
};

var validation = await processor.ValidateRequestAsync(request);
if (!validation.IsValid)
{
    foreach (var issue in validation.Issues)
    {
        Console.WriteLine($"Validation Error: {issue.Message}");
    }
}
```






## Integration Points






### Dependency Injection Registration






```csharp
services.AddScoped<IBulkProcessor, BulkProcessorService>();
```






### Configuration Dependencies






- Requires `IConfiguration` for bulk processing settings
- Uses `episodeidentifier.config.json` for default options
- Respects existing configuration patterns

### Service Dependencies






- `IEpisodeIdentificationService`: For actual episode identification
- `IFileDiscoveryService`: For file enumeration and validation
- `IProgressTracker`: For progress reporting and statistics
- `ILogger<IBulkProcessor>`: For structured logging

### Database Dependencies






- Uses existing database connection for storing results
- Leverages existing hash storage and caching mechanisms
- May create additional tables for bulk processing history

## Testing Strategy






### Unit Tests






- Mock all dependencies to test business logic in isolation
- Test all error conditions and edge cases
- Verify progress reporting accuracy
- Test cancellation handling

### Integration Tests






- Test with real file system and database
- Verify performance characteristics with large datasets
- Test cross-platform file system behavior
- Validate memory usage patterns

### Contract Tests






- Verify interface contracts are fulfilled by implementation
- Test pre/post-conditions and invariants
- Validate error handling requirements
- Ensure backward compatibility

### Performance Tests






- Test with directories containing 10,000+ files
- Measure memory usage during large operations
- Verify progress reporting doesn't impact performance
- Test concurrent processing capabilities

## Error Scenarios






### File System Errors






- **Access Denied**: Log error, skip file, continue processing
- **File Not Found**: Skip file if discovered but deleted during processing
- **Corrupted File**: Log error, categorize as corrupted, continue processing
- **Network Drive Issues**: Timeout handling, retry logic for temporary failures

### Configuration Errors






- **Invalid Options**: Fail fast with descriptive error message
- **Missing Dependencies**: Fail during service construction, not runtime
- **Resource Limits**: Graceful degradation when system limits reached

### System Errors






- **Out of Memory**: Reduce batch sizes, force garbage collection, continue if possible
- **Disk Full**: Fail gracefully with clear error message
- **Thread Pool Exhaustion**: Reduce concurrency, continue with single-threaded processing

## Backward Compatibility






### Existing Service Integration






- Does not modify existing `IEpisodeIdentificationService` interface
- Extends existing functionality without breaking changes
- Maintains existing database schema compatibility
- Preserves existing configuration file structure

### CLI Compatibility






- Adds new commands without modifying existing ones
- Maintains existing command behavior and output formats
- New options use different parameter names to avoid conflicts
