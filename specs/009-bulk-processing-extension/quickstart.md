# Quickstart: Bulk Processing Extension for Episode Identification


**Date**: September 13, 2025
**Feature**: 009-bulk-processing-extension
**Purpose**: Quick implementation scenarios and usage examples

## Implementation Quickstart


### Phase 1: Core Data Models (30 minutes)


Create the essential data structures in `src/EpisodeIdentifier.Core/Models/`:

```csharp
// BulkProcessingRequest.cs
public class BulkProcessingRequest
{
    public string InputPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public BulkProcessingOptions Options { get; set; } = new();
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}

// BulkProcessingOptions.cs
public class BulkProcessingOptions
{
    public bool Recursive { get; set; } = true;
    public string[] SupportedExtensions { get; set; } = { ".mkv", ".mp4", ".avi", ".mov" };
    public int BatchSize { get; set; } = 100;
    public int MaxErrors { get; set; } = -1;
    public bool ContinueOnError { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
}

// BulkProcessingResult.cs
public class BulkProcessingResult
{
    public int TotalFilesDiscovered { get; set; }
    public int FilesProcessedSuccessfully { get; set; }
    public int FilesWithErrors { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public List<FileProcessingResult> FileResults { get; set; } = new();
    public BulkProcessingStatus Status { get; set; }
}
```


### Phase 2: Service Interfaces (15 minutes)


Create interfaces in `src/EpisodeIdentifier.Core/Interfaces/`:

```csharp
// IBulkProcessor.cs
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


### Phase 3: CLI Commands (20 minutes)


Extend `src/EpisodeIdentifier.Core/Commands/` with new commands:

```csharp
// ProcessFileCommand.cs
public class ProcessFileCommand : Command
{
    public ProcessFileCommand() : base("process-file", "Process a single video file")
    {
        var fileArgument = new Argument<string>("file-path", "Path to video file");
        AddArgument(fileArgument);

        var progressOption = new Option<bool>("--progress", "Show progress information");
        AddOption(progressOption);
    }
}

// ProcessDirectoryCommand.cs
public class ProcessDirectoryCommand : Command
{
    public ProcessDirectoryCommand() : base("process-directory", "Process all video files in directory")
    {
        var dirArgument = new Argument<string>("directory-path", "Path to directory");
        AddArgument(dirArgument);

        var recursiveOption = new Option<bool>("--recursive", "Process subdirectories");
        var maxErrorsOption = new Option<int>("--max-errors", "Maximum errors before stopping");
        AddOption(recursiveOption);
        AddOption(maxErrorsOption);
    }
}
```


## Usage Quickstart


### Basic Single File Processing


```bash

# Process a single video file





dotnet run -- process-file /path/to/video.mkv --progress

# Expected output:






# Processing: /path/to/video.mkv






# ✓ Successfully identified: Show Name S01E05






# Processed 1 file successfully





```


### Basic Directory Processing


```bash

# Process all videos in a directory (non-recursive)





dotnet run -- process-directory /media/tv-shows --progress

# Expected output:






# Discovering files...






# Found 15 video files






# Processing [1/15]: show-s01e01.mkv






# Processing [2/15]: show-s01e02.mkv






# ...






# ✓ Processed 15 files successfully (12 identified, 3 errors)





```


### Recursive Directory Processing


```bash

# Process all videos recursively with error limits





dotnet run -- process-directory /media/tv-shows --recursive --max-errors 5 --progress

# Expected output:






# Discovering files recursively...






# Found 247 video files across 23 directories






# Processing [1/247]: Season 1/episode-01.mkv






# Processing [2/247]: Season 1/episode-02.mkv






# ...






# ✓ Processed 247 files (235 identified, 12 errors)






# Processing time: 00:08:32 (0.48 files/second)





```


## Configuration Quickstart


### Basic Configuration Setup


Add to `episodeidentifier.config.json`:

```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "recursive": true,
      "supportedExtensions": [".mkv", ".mp4", ".avi"],
      "batchSize": 100,
      "maxErrors": -1,
      "continueOnError": true,
      "showProgress": true
    }
  }
}
```


### Advanced Configuration


```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "recursive": true,
      "maxDepth": 10,
      "supportedExtensions": [".mkv", ".mp4", ".avi", ".mov", ".m4v", ".wmv"],
      "batchSize": 50,
      "maxErrors": 100,
      "continueOnError": true,
      "showProgress": true,
      "maxConcurrentFiles": 1
    },
    "performance": {
      "memoryCleanupBatchSize": 50,
      "progressUpdateInterval": 1000
    }
  }
}
```


## Testing Quickstart


### Unit Test Example


```csharp
[Test]
public async Task ProcessAsync_WithSingleFile_ReturnsSuccessResult()
{
    // Arrange
    var processor = new BulkProcessorService(mockServices);
    var request = new BulkProcessingRequest
    {
        InputPath = "test-video.mkv",
        IsDirectory = false
    };

    // Act
    var result = await processor.ProcessAsync(request);

    // Assert
    Assert.AreEqual(1, result.TotalFilesDiscovered);
    Assert.AreEqual(1, result.FilesProcessedSuccessfully);
    Assert.AreEqual(BulkProcessingStatus.Completed, result.Status);
}
```


### Integration Test Example


```csharp
[Test]
public async Task ProcessAsync_WithTestDirectory_ProcessesAllFiles()
{
    // Arrange
    var testDir = CreateTestDirectoryWithVideos(5);
    var request = new BulkProcessingRequest
    {
        InputPath = testDir,
        IsDirectory = true,
        Options = new BulkProcessingOptions { Recursive = false }
    };

    // Act
    var result = await bulkProcessor.ProcessAsync(request);

    // Assert
    Assert.AreEqual(5, result.TotalFilesDiscovered);
    Assert.Greater(result.FilesProcessedSuccessfully, 0);
}
```


## Development Scenarios


### Scenario 1: Add New Video Format Support


**Goal**: Support `.webm` files in bulk processing
**Time**: 5 minutes
**Steps**:

1. Update default configuration:

   ```json
   "supportedExtensions": [".mkv", ".mp4", ".avi", ".mov", ".webm"]
   ```

2. Update CLI help text to include `.webm`
3. Add test case with `.webm` file

### Scenario 2: Add Progress Callbacks


**Goal**: Custom progress reporting for UI integration
**Time**: 15 minutes
**Steps**:

1. Extend `BulkProcessingProgress` with callback support:

   ```csharp
   public Action<string>? OnFileStarted { get; set; }
   public Action<FileProcessingResult>? OnFileCompleted { get; set; }
   ```

2. Modify `BulkProcessorService` to invoke callbacks
3. Add example usage in documentation

### Scenario 3: Add Concurrency Control


**Goal**: Process multiple files simultaneously
**Time**: 30 minutes
**Steps**:

1. Add `MaxConcurrentFiles` to `BulkProcessingOptions`
2. Implement `SemaphoreSlim` in `BulkProcessorService`
3. Update progress reporting to handle concurrent operations
4. Add configuration validation for reasonable concurrency limits

## Troubleshooting Quickstart


### Common Issues


**Issue**: "Access denied" errors during directory processing
**Solution**: Check directory permissions, use `--continue-on-error` flag
**Example**:

```bash
dotnet run -- process-directory /restricted/path --continue-on-error --max-errors 50
```


**Issue**: Out of memory with very large directories
**Solution**: Reduce batch size in configuration
**Example**:

```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "batchSize": 25
    }
  }
}
```


**Issue**: Slow processing on network drives
**Solution**: Use local processing or adjust timeouts
**Example**:

```bash

# Copy to local drive first





cp -r /network/share/videos /tmp/local-videos
dotnet run -- process-directory /tmp/local-videos --recursive
```


### Debug Mode Usage


```bash

# Enable verbose logging





export ASPNETCORE_ENVIRONMENT=Development
dotnet run -- process-directory /media/test --progress --verbose

# Expected debug output:






# [DEBUG] Discovering files in: /media/test






# [DEBUG] Found file: video1.mkv (125.3 MB)






# [DEBUG] Found file: video2.mp4 (89.7 MB)






# [DEBUG] Starting batch processing (batch size: 100)






# [DEBUG] Processing file 1/2: video1.mkv






# [DEBUG] Episode identified: Show S01E01 (confidence: 0.95)





```


## Performance Optimization Quickstart


### For Large Directories (10,000+ files)


```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "batchSize": 200,
      "showProgress": false
    },
    "performance": {
      "memoryCleanupBatchSize": 200,
      "progressUpdateInterval": 5000
    }
  }
}
```


### For Network Storage


```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "batchSize": 25,
      "maxConcurrentFiles": 1
    },
    "performance": {
      "memoryCleanupBatchSize": 25,
      "progressUpdateInterval": 2000
    }
  }
}
```


### For Memory-Constrained Systems


```json
{
  "bulkProcessing": {
    "defaultOptions": {
      "batchSize": 10
    },
    "performance": {
      "memoryCleanupBatchSize": 10,
      "progressUpdateInterval": 500
    }
  }
}
```


This quickstart guide provides practical examples for implementing and using the bulk processing extension, covering the most common scenarios developers and users will encounter.
