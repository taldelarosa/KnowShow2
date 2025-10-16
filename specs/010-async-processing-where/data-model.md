# Data Model: Async Processing with Configurable Concurrency


**Date**: September 15, 2025
**Feature**: 010-async-processing-where

## Entity Overview


This feature extends existing entities rather than creating new ones, focusing on configuration model enhancement and leveraging existing processing infrastructure.

## Extended Entities


### ConcurrencyConfiguration (Extension of existing configuration)


**Purpose**: Stores configurable concurrency settings within existing application configuration

**Fields**:

- `maxConcurrency`: int (1-100, default: 1)
    - Maximum number of concurrent episode identification operations
    - Range validation prevents resource exhaustion
    - Default value maintains backward compatibility

**Validation Rules**:

- Range: 1 ≤ maxConcurrency ≤ 100
- Required: false (defaults to 1 if not specified)
- Type: positive integer

**Relationships**:

- Embedded within existing `AppConfiguration` model
- Used by `BulkProcessingOptions` during initialization
- Subject to hot-reload configuration monitoring

**State Transitions**:

- Configuration loading: default → validated value
- Hot-reload trigger: old value → new validated value → processing pool adjustment
- Validation failure: invalid value → default value (1) with warning

### EpisodeIdentificationJob (Existing entity - usage clarification)


**Purpose**: Represents individual file processing operation within concurrent execution pool

**Key Fields (existing)**:

- File path and metadata
- Processing stage status (ripping, hashing, DB checking, renaming)
- Result status (success, failure, error details)
- Progress information

**Concurrency Considerations**:

- Multiple instances run simultaneously up to maxConcurrency limit
- Independent execution - failure of one doesn't affect others
- Results aggregated into common JSON output at completion

### ProcessingQueue (Conceptual - implemented within existing BulkProcessing)


**Purpose**: Manages files awaiting processing when demand exceeds concurrency limit

**Behavior**:

- FIFO queue for pending files
- Automatic dequeue when concurrent slot becomes available
- Thread-safe operations for concurrent access

**Implementation**: Built into existing async processing infrastructure

### ProcessingPool (Conceptual - implemented within existing BulkProcessing)


**Purpose**: Manages active concurrent operations and resource allocation

**Key Responsibilities**:

- Maintain count of active operations ≤ maxConcurrency
- Coordinate resource allocation for subtitle ripping and hashing
- Handle completion events and queue management
- Provide progress reporting aggregation

## Configuration Schema Extension


### Existing episodeidentifier.config.json Enhancement


```json
{
  "version": "2.0",
  "maxConcurrency": 1,
  "matchConfidenceThreshold": 0.6,
  "renameConfidenceThreshold": 0.7,
  "fuzzyHashThreshold": 75,
  "hashingAlgorithm": "CTPH",
  "filenamePatterns": {
    "primaryPattern": "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$",
    "secondaryPattern": "^(?<SeriesName>.+?)\\s(?<Season>\\d+)x(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$",
    "tertiaryPattern": "^(?<SeriesName>.+?)\\.S(?<Season>\\d+)\\.E(?<Episode>\\d+)(?:\\.(?<EpisodeName>.+?))?$"
  },
  "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
}
```


**New Field**: `maxConcurrency`

- Type: integer
- Range: 1-100
- Default: 1
- Description: Maximum concurrent episode identification operations

## Data Flow


### Configuration Flow


1. Application start/hot-reload trigger
2. Load episodeidentifier.config.json
3. Validate maxConcurrency value (default to 1 if invalid)
4. Update BulkProcessingOptions.MaxConcurrency
5. Apply to active processing (if running)

### Processing Flow


1. User initiates bulk processing
2. System reads maxConcurrency from configuration
3. Creates processing pool with specified concurrency limit
4. Queues files for processing
5. Processes up to maxConcurrency files simultaneously
6. Maintains queue for additional files
7. Aggregates results from all concurrent operations
8. Outputs comprehensive JSON results

### Hot-Reload Flow


1. Configuration file change detected
2. New maxConcurrency value loaded and validated
3. Active processing pool adjusted (if processing active)
4. Queue processing continues with new limit
5. No interruption to active operations

## Error Handling


### Configuration Errors


- Invalid maxConcurrency value → default to 1, log warning
- Missing maxConcurrency field → default to 1
- Malformed configuration → use all defaults, log error

### Processing Errors


- Individual file failures → continue processing others, collect in results
- Resource exhaustion → existing error handling with backpressure
- Database connectivity → retry logic for concurrent operations

## Validation Rules Summary


1. **maxConcurrency Validation**:
   - Must be positive integer
   - Must be within range 1-100
   - Invalid values default to 1

2. **Resource Validation**:
   - Respect system limits during concurrent operations
   - Handle resource contention gracefully

3. **Configuration Validation**:
   - Backward compatible with existing configurations
   - Graceful degradation for missing or invalid settings

## Integration Points


### Existing Services Integration


- **IAppConfigService**: Extended to include maxConcurrency property
- **BulkProcessingService**: Modified to use config-based concurrency
- **Configuration validation**: Enhanced with concurrency range checking
- **Hot-reload service**: Monitors maxConcurrency changes

### Database Coordination


- Shared SQLite connection pool for concurrent hash lookups
- Connection limiting to prevent database lock contention
- Efficient query batching for concurrent operations

### File System Coordination


- Directory-level locking for concurrent rename operations
- Atomic file operations to prevent conflicts
- Temporary file handling for concurrent subtitle extraction
