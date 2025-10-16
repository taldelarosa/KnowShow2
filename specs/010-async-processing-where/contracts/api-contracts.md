# API Contracts: Async Processing with Configurable Concurrency


**Date**: September 15, 2025
**Feature**: 010-async-processing-where

## Configuration Contract


### Configuration Schema Extension


**File**: `episodeidentifier.config.json`

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Episode Identifier Configuration",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "enum": ["2.0"]
    },
    "maxConcurrency": {
      "type": "integer",
      "minimum": 1,
      "maximum": 100,
      "default": 1,
      "description": "Maximum number of concurrent episode identification operations"
    },
    "matchConfidenceThreshold": {
      "type": "number",
      "minimum": 0,
      "maximum": 1
    },
    "renameConfidenceThreshold": {
      "type": "number",
      "minimum": 0,
      "maximum": 1
    },
    "fuzzyHashThreshold": {
      "type": "integer",
      "minimum": 0,
      "maximum": 100
    },
    "hashingAlgorithm": {
      "type": "string",
      "enum": ["CTPH"]
    },
    "filenamePatterns": {
      "type": "object",
      "properties": {
        "primaryPattern": { "type": "string" },
        "secondaryPattern": { "type": "string" },
        "tertiaryPattern": { "type": "string" }
      },
      "required": ["primaryPattern", "secondaryPattern", "tertiaryPattern"]
    },
    "filenameTemplate": {
      "type": "string"
    }
  },
  "required": ["version", "matchConfidenceThreshold", "renameConfidenceThreshold", "fuzzyHashThreshold", "hashingAlgorithm", "filenamePatterns", "filenameTemplate"]
}
```


## CLI Contract Extension


### Bulk Identify Command Enhancement


**Command**: `--bulk-identify <directory>`

**Behavior Changes**:

- Reads `maxConcurrency` from `episodeidentifier.config.json`
- Processes up to `maxConcurrency` files simultaneously
- Maintains existing command-line interface (no breaking changes)
- Supports hot-reload of concurrency settings during processing

**Input Contract**:

```
Required:

- directory: string (path to directory containing video files)

Optional (existing):

- --recursive: boolean (process subdirectories)
- --dry-run: boolean (simulate without making changes)
- --output: string (output file for results)

Configuration (from episodeidentifier.config.json):

- maxConcurrency: integer (1-100, default: 1)
```


**Output Contract** (unchanged):

```json
{
  "requestId": "string",
  "summary": {
    "totalFiles": "integer",
    "processedFiles": "integer",
    "successfulFiles": "integer",
    "failedFiles": "integer",
    "processingTime": "string (ISO 8601 duration)"
  },
  "results": [
    {
      "inputFile": "string",
      "status": "success|failure",
      "confidence": "number (0-1)",
      "identifiedEpisode": {
        "seriesName": "string",
        "season": "integer",
        "episode": "integer",
        "episodeName": "string"
      },
      "outputFile": "string|null",
      "error": "string|null",
      "processingStages": {
        "subtitleRipping": "success|failure|skipped",
        "hashing": "success|failure|skipped",
        "databaseLookup": "success|failure|skipped",
        "fileRenaming": "success|failure|skipped"
      }
    }
  ],
  "errors": [
    {
      "file": "string",
      "stage": "string",
      "message": "string"
    }
  ]
}
```


## Configuration Service Contract


### IAppConfigService Enhancement


**New Property**:

```csharp
public int MaxConcurrency { get; }
```


**Validation Contract**:

- Value range: 1 ≤ MaxConcurrency ≤ 100
- Default value: 1 (if not specified or invalid)
- Validation occurs during configuration loading

**Hot-Reload Contract**:

- Configuration changes trigger property update
- Existing processing operations continue with previous settings
- New processing operations use updated settings
- No interruption to active file processing

## Processing Service Contract


### BulkProcessingOptions Enhancement


**Modified Property**:

```csharp
public int MaxConcurrency { get; set; }
```


**Initialization Contract**:

- Read from IAppConfigService.MaxConcurrency instead of Environment.ProcessorCount
- Maintain validation range 1-100
- Preserve existing validation attributes

**Behavior Contract**:

- Process files concurrently up to MaxConcurrency limit
- Queue additional files when limit reached
- Maintain FIFO processing order for queued files
- Independent error handling per concurrent operation

## Progress Reporting Contract


### Concurrent Progress Updates


**Enhanced Progress Reports**:

```csharp
public class BulkProcessingProgress
{
    // Existing properties (unchanged)
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public decimal PercentComplete { get; set; }
    public string CurrentPhase { get; set; }
    public string CurrentFile { get; set; }

    // Enhanced for concurrency
    public int ActiveOperations { get; set; }  // New: current concurrent operations
    public int QueuedFiles { get; set; }       // New: files waiting in queue
}
```


**Progress Reporting Behavior**:

- Reports progress from all concurrent operations
- Updates `ActiveOperations` count as operations start/complete
- Updates `QueuedFiles` count as queue length changes
- Maintains existing progress reporting frequency and format

## Error Handling Contract


### Concurrent Error Collection


**Error Aggregation**:

- Individual file failures collected from all concurrent operations
- Error details include source operation identification
- Failed operations don't stop other concurrent operations
- All errors included in final JSON output

**Error Recovery**:

- Configuration validation errors → default to maxConcurrency = 1
- Resource exhaustion → existing backpressure handling
- Individual processing failures → continue with other operations

## Testing Contracts


### Configuration Testing


- Validate config loading with various maxConcurrency values
- Test hot-reload behavior and timing
- Verify validation and default value handling

### Concurrency Testing


- Test processing with different concurrency levels (1, 5, 10, max)
- Verify queue management and FIFO ordering
- Test concurrent operation independence (failure isolation)

### Integration Testing


- Test full workflow with concurrent processing
- Verify JSON output format with concurrent results
- Test hot-reload during active processing

## Backward Compatibility


### Existing Behavior Preservation


- Default maxConcurrency = 1 maintains single-file processing
- Existing configurations without maxConcurrency work unchanged
- All existing command-line options and behavior preserved
- JSON output format remains identical

### Migration Path


- No migration required for existing users
- Optional configuration enhancement
- Graceful degradation for invalid configurations
