# Bulk Processing Extension Documentation

## Overview

The Bulk Processing Extension provides enterprise-grade batch processing capabilities for the Episode Identifier system. It enables efficient processing of large numbers of media files with comprehensive error handling, progress reporting, and configuration management.

## Features

### Core Capabilities

- **Batch Processing**: Efficient processing of files in configurable batches
- **Concurrent Processing**: Multi-threaded execution with configurable concurrency limits
- **Progress Reporting**: Real-time progress updates with detailed statistics
- **Error Handling**: Comprehensive error categorization and retry mechanisms
- **Memory Management**: Automatic garbage collection and memory monitoring
- **Configuration Integration**: Hot-reloadable configuration with validation

### Performance Characteristics

- **Throughput**: 5-15 files/second depending on file size and system resources
- **Memory Usage**: < 200MB memory growth for 1000+ files
- **Scalability**: Linear performance scaling with increased concurrency
- **Error Recovery**: Automatic retry with exponential backoff

## Architecture

### Core Components

```
BulkProcessorService
├── FileDiscoveryService     # File enumeration and filtering
├── ProgressTracker         # Progress monitoring and reporting
├── ConsoleProgressReporter # Interactive console output
└── ConfigurationService    # Configuration management
```

### Data Flow

```
Input Paths → File Discovery → Batch Creation → Processing → Results
     ↓              ↓              ↓           ↓         ↓
Configuration → Validation → Progress → Error → Reporting
```

## Configuration

### JSON Configuration

```json
{
  "bulkProcessing": {
    "defaultBatchSize": 100,
    "defaultMaxConcurrency": 4,
    "defaultProgressReportingInterval": 1000,
    "defaultForceGarbageCollection": true,
    "defaultCreateBackups": false,
    "defaultContinueOnError": true,
    "defaultMaxErrorsBeforeAbort": null,
    "defaultFileProcessingTimeout": "00:05:00",
    "defaultFileExtensions": [".mkv", ".mp4", ".avi"],
    "maxBatchSize": 10000,
    "maxConcurrency": 32,
    "enableBatchStatistics": false,
    "enableMemoryMonitoring": true
  }
}
```

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `defaultBatchSize` | int | 100 | Default number of files per batch |
| `defaultMaxConcurrency` | int | CPU cores | Default concurrent processing limit |
| `defaultProgressReportingInterval` | int | 1000ms | Progress reporting frequency |
| `defaultForceGarbageCollection` | bool | true | Enable garbage collection between batches |
| `defaultCreateBackups` | bool | false | Create backups before processing |
| `defaultContinueOnError` | bool | true | Continue processing when files fail |
| `defaultMaxErrorsBeforeAbort` | int? | null | Maximum errors before aborting (null = no limit) |
| `defaultFileProcessingTimeout` | TimeSpan | 5 minutes | Timeout per file |
| `defaultFileExtensions` | string[] | [] | Default file extensions to process |
| `maxBatchSize` | int | 10000 | Maximum allowed batch size |
| `maxConcurrency` | int | CPU cores × 8 | Maximum allowed concurrency |
| `enableBatchStatistics` | bool | false | Enable detailed batch statistics |
| `enableMemoryMonitoring` | bool | true | Enable memory usage monitoring |

### Validation Rules

- `defaultBatchSize`: 1-10,000, must not exceed `maxBatchSize`
- `defaultMaxConcurrency`: 1-100, must not exceed `maxConcurrency`
- `defaultProgressReportingInterval`: 100-60,000 milliseconds
- `defaultFileProcessingTimeout`: 1 second - 1 hour (if specified)
- `maxBatchSize`: 1-50,000
- `maxConcurrency`: 1-500

## Usage Examples

### Basic Usage

```csharp
var bulkProcessor = new BulkProcessorService(logger, fileSystem);

var request = new BulkProcessingRequest
{
    InputPaths = new[] { "/path/to/videos" },
    Options = new BulkProcessingOptions
    {
        BatchSize = 50,
        MaxConcurrency = 4,
        ContinueOnError = true
    }
};

var result = await bulkProcessor.ProcessAsync(request);
```

### With Progress Reporting

```csharp
var progress = new Progress<BulkProcessingProgress>(p =>
{
    Console.WriteLine($"Processed: {p.ProcessedFiles}/{p.TotalFiles} " +
                     $"({p.PercentComplete:F1}%)");
});

var request = new BulkProcessingRequest
{
    InputPaths = filePaths,
    Options = options,
    Progress = progress
};

var result = await bulkProcessor.ProcessAsync(request);
```

### With Configuration Integration

```csharp
var configService = new ConfigurationService(logger, fileSystem);
var config = await configService.LoadConfiguration();

var options = new BulkProcessingOptions();
if (config.Configuration?.BulkProcessing != null)
{
    var bulkConfig = config.Configuration.BulkProcessing;
    options.BatchSize = bulkConfig.DefaultBatchSize;
    options.MaxConcurrency = bulkConfig.DefaultMaxConcurrency;
    options.ProgressReportingInterval = bulkConfig.DefaultProgressReportingInterval;
}
```

### Error Handling

```csharp
var request = new BulkProcessingRequest
{
    InputPaths = filePaths,
    Options = new BulkProcessingOptions
    {
        ContinueOnError = true,
        RetryAttempts = 2,
        RetryDelayMs = 1000,
        MaxErrorsBeforeAbort = 10
    }
};

var result = await bulkProcessor.ProcessAsync(request);

if (result.Status == BulkProcessingStatus.CompletedWithErrors)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error.FilePath} - {error.ErrorMessage}");
    }
}
```

## Models

### BulkProcessingRequest

Primary input for bulk processing operations.

```csharp
public class BulkProcessingRequest
{
    public IEnumerable<string> InputPaths { get; set; } = new List<string>();
    public BulkProcessingOptions Options { get; set; } = new();
    public IProgress<BulkProcessingProgress>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; } = default;
}
```

### BulkProcessingOptions

Configuration options for processing behavior.

```csharp
public class BulkProcessingOptions
{
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public bool ContinueOnError { get; set; } = true;
    public bool CreateBackups { get; set; } = false;
    public int ProgressReportingInterval { get; set; } = 1000;
    public bool ForceGarbageCollection { get; set; } = true;
    public int RetryAttempts { get; set; } = 0;
    public int RetryDelayMs { get; set; } = 1000;
    public int? MaxErrorsBeforeAbort { get; set; } = null;
    public TimeSpan? FileProcessingTimeout { get; set; } = null;
    public IEnumerable<string> IncludePatterns { get; set; } = new List<string>();
    public IEnumerable<string> ExcludePatterns { get; set; } = new List<string>();
    public bool RecursiveSearch { get; set; } = true;
}
```

### BulkProcessingResult

Result container with processing statistics and error information.

```csharp
public class BulkProcessingResult
{
    public BulkProcessingStatus Status { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public List<BulkProcessingError> Errors { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Dictionary<string, object> Statistics { get; set; } = new();
}
```

### BulkProcessingProgress

Progress reporting model with detailed status information.

```csharp
public class BulkProcessingProgress
{
    public BulkProcessingStatus Status { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public decimal PercentComplete { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public TimeSpan Elapsed { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
    public string Details { get; set; } = string.Empty;
    public Dictionary<string, object> Statistics { get; set; } = new();
}
```

## Error Handling

### Error Categories

The system categorizes errors for appropriate handling:

- **ValidationError**: Input validation failures
- **FileSystemError**: File access and I/O issues  
- **ProcessingError**: Core processing failures
- **TimeoutError**: Operation timeout exceeded
- **CancellationError**: Operation cancelled by user
- **ConfigurationError**: Configuration-related issues
- **UnknownError**: Uncategorized errors

### Retry Mechanism

```csharp
// Automatic retry with exponential backoff
var options = new BulkProcessingOptions
{
    RetryAttempts = 3,
    RetryDelayMs = 1000, // Base delay
    ContinueOnError = true
};

// Retry delays: 1000ms, 2000ms, 4000ms (exponential backoff)
```

### Error Limits

```csharp
// Stop processing after 10 errors
var options = new BulkProcessingOptions
{
    MaxErrorsBeforeAbort = 10,
    ContinueOnError = true
};
```

## Performance Tuning

### Batch Size Optimization

- **Small batches (10-50)**: Better for mixed file sizes, more frequent progress updates
- **Medium batches (50-200)**: Balanced performance and memory usage
- **Large batches (200-1000)**: Maximum throughput for uniform file sizes

### Concurrency Guidelines

- **CPU-bound**: Set to CPU core count
- **I/O-bound**: Set to 2-4× CPU core count  
- **Network storage**: Lower concurrency (2-4) to avoid overwhelming network
- **SSD storage**: Higher concurrency acceptable
- **HDD storage**: Lower concurrency (2-6) to minimize seek time

### Memory Management

```csharp
var options = new BulkProcessingOptions
{
    ForceGarbageCollection = true, // Enable for large batches
    BatchSize = 100, // Smaller batches for memory pressure
    MaxConcurrency = 2 // Reduce concurrency if memory limited
};
```

## Monitoring and Diagnostics

### Progress Reporting

```csharp
var reporter = new ConsoleProgressReporter();
var progress = new Progress<BulkProcessingProgress>(reporter.Report);

// Provides real-time console output with:
// - Progress bar
// - Files processed count
// - Success/failure rates
// - Time elapsed/remaining
// - Current file being processed
```

### Logging Integration

The system uses structured logging with Microsoft.Extensions.Logging:

```csharp
// Log levels used:
// - Information: Normal progress and completion
// - Warning: Recoverable errors and retries  
// - Error: Unrecoverable errors
// - Debug: Detailed operation traces
```

### Statistics Collection

```csharp
// Available in result.Statistics:
// - TotalProcessingTime
// - AverageFileProcessingTime
// - ThroughputFilesPerSecond
// - MemoryUsageBytes (if monitoring enabled)
// - BatchCompletionTimes
// - ErrorsByCategory
// - RetryStatistics
```

## Testing

### Unit Tests

- Model validation and property behavior
- Service method functionality
- Error handling scenarios
- Configuration validation

### Integration Tests  

- End-to-end processing workflows
- Configuration loading and validation
- Progress reporting integration
- Error handling with real file operations

### Performance Tests

- Throughput measurement across batch sizes
- Memory usage monitoring
- Concurrency scaling validation
- Large-scale processing scenarios

## Best Practices

### File Organization

```csharp
// Organize input by size for better batching
var smallFiles = files.Where(f => GetFileSize(f) < 1GB);
var largeFiles = files.Where(f => GetFileSize(f) >= 1GB);

// Process separately with different batch sizes
```

### Error Handling Strategy

```csharp
// Use appropriate error handling for scenario
var interactiveOptions = new BulkProcessingOptions
{
    ContinueOnError = true,
    RetryAttempts = 2,
    MaxErrorsBeforeAbort = 10
};

var automatedOptions = new BulkProcessingOptions  
{
    ContinueOnError = false, // Fail fast for automated scenarios
    RetryAttempts = 0
};
```

### Resource Management

```csharp
// For long-running operations
var options = new BulkProcessingOptions
{
    ForceGarbageCollection = true,
    ProgressReportingInterval = 5000, // Less frequent updates
    BatchSize = 50 // Smaller batches for memory control
};
```

### Configuration Management

```csharp
// Load configuration once and reuse
var configService = new ConfigurationService(logger);
var config = await configService.LoadConfiguration();

// Monitor for changes in long-running applications
var reloaded = await configService.ReloadIfChanged();
if (reloaded)
{
    // Update processing options
}
```

## Troubleshooting

### Common Issues

**High Memory Usage**

- Reduce batch size
- Enable garbage collection
- Reduce concurrency
- Check for file handle leaks

**Poor Performance**  

- Adjust batch size for file size distribution
- Optimize concurrency for storage type
- Check network bandwidth for remote files
- Monitor CPU utilization

**Frequent Errors**

- Check file permissions
- Verify file extensions configuration
- Review timeout settings
- Check available disk space

**Configuration Issues**

- Validate JSON syntax
- Check property names and types
- Verify constraint satisfaction
- Review validation error messages

### Diagnostic Information

```csharp
// Enable detailed logging
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Check configuration validation
var configResult = await configService.LoadConfiguration();
if (!configResult.IsValid)
{
    foreach (var error in configResult.Errors)
        Console.WriteLine($"Config Error: {error}");
}

// Monitor progress details
var progress = new Progress<BulkProcessingProgress>(p =>
{
    Console.WriteLine($"Details: {p.Details}");
    foreach (var stat in p.Statistics)
        Console.WriteLine($"{stat.Key}: {stat.Value}");
});
```

## Version History

### v1.0.0 (2025-09-13)

- Initial implementation of bulk processing extension
- Core batch processing with concurrency support
- Progress reporting and error handling
- Configuration integration with hot-reload
- Comprehensive test coverage (339 tests)
- Performance optimization and memory management

## Support

For questions, issues, or contributions:

- Review unit and integration tests for usage examples
- Check performance tests for scalability guidance
- Refer to configuration validation for setup issues
- Monitor structured logs for runtime diagnostics
