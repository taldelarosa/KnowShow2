# Research: Async Processing with Configurable Concurrency


**Date**: September 15, 2025
**Feature**: 010-async-processing-where

## Research Summary


Research focused on integrating configurable concurrency into existing episode identification system by extending current configuration and bulk processing infrastructure.

## Key Findings


### Configuration Integration


**Decision**: Extend existing `episodeidentifier.config.json` with `maxConcurrency` property

**Rationale**:

- Leverages existing hot-reload configuration system
- Maintains consistency with current configuration patterns
- Provides seamless user experience with runtime adjustment capability
- Avoids introducing new configuration files or mechanisms

**Alternatives considered**:

- Separate concurrency config file: Rejected due to configuration fragmentation
- Command-line only configuration: Rejected due to lack of persistence and hot-reload
- Environment variables: Rejected due to no hot-reload capability

### Concurrency Implementation


**Decision**: Modify existing BulkProcessingOptions.MaxConcurrency to read from config instead of Environment.ProcessorCount

**Rationale**:

- Minimal code changes required - infrastructure already exists
- Preserves existing async processing, progress reporting, and error handling
- Maintains backward compatibility - existing tests and behavior preserved
- Leverages proven concurrent processing patterns already in use

**Alternatives considered**:

- Custom concurrent processing implementation: Rejected due to complexity and existing working solution
- External task scheduling library: Rejected due to over-engineering for current needs
- Thread pool management: Rejected as existing Task-based approach is sufficient

### Configuration Validation


**Decision**: Implement validation with range 1-100, default value 1 for backward compatibility

**Rationale**:

- Range 1-100 matches existing BulkProcessingOptions validation pattern
- Default value 1 ensures conservative behavior and no breaking changes
- Upper bound prevents resource exhaustion
- Validation follows existing patterns in the codebase

**Alternatives considered**:

- No upper limit: Rejected due to potential resource exhaustion
- Default to Environment.ProcessorCount: Rejected to avoid breaking existing single-file workflows
- Different range: 1-100 aligns with existing validation patterns

### Hot-Reload Integration


**Decision**: Utilize existing configuration service hot-reload mechanism

**Rationale**:

- No additional hot-reload infrastructure needed
- Consistent with existing configuration change behavior
- Runtime adjustment without restart already proven
- Maintains existing configuration service patterns

**Alternatives considered**:

- Custom hot-reload for concurrency: Rejected due to code duplication
- No hot-reload support: Rejected due to user experience degradation
- File system watcher: Rejected as existing service already provides this

### Error Handling Strategy


**Decision**: Extend existing JSON result aggregation to handle concurrent operation results

**Rationale**:

- Preserves existing error reporting format and user expectations
- Concurrent operations already report to common result collector
- JSON output format already handles success/failure aggregation
- No breaking changes to output format

**Alternatives considered**:

- New concurrent-specific error format: Rejected due to breaking changes
- Individual file error reporting: Rejected as batch reporting is more useful
- Real-time error streaming: Rejected due to complexity and limited value

## Technical Decisions


### Configuration Schema Extension


Add to existing `episodeidentifier.config.json`:

```json
{
  "version": "2.0",
  "maxConcurrency": 1,
  // ... existing fields
}
```


### Integration Points


1. **IAppConfigService**: Extend to include MaxConcurrency property
2. **BulkProcessingOptions**: Modify constructor/initialization to read from config
3. **Configuration validation**: Add range validation for concurrency value
4. **Hot-reload handling**: Ensure configuration changes trigger processing pool adjustment

### Testing Strategy


1. **Configuration tests**: Validate config loading, validation, and defaults
2. **Hot-reload tests**: Verify runtime configuration changes take effect
3. **Concurrency tests**: Validate different concurrency levels work correctly
4. **Integration tests**: Test full workflow with various concurrency settings
5. **Error handling tests**: Verify individual failures don't stop concurrent operations

## Implementation Approach


Minimal changes approach leveraging existing infrastructure:

1. Extend configuration model with maxConcurrency property
2. Modify bulk processing initialization to read from config instead of Environment.ProcessorCount
3. Add configuration validation
4. Update tests to cover new configuration option
5. Update documentation

## Risk Assessment


**Low Risk**:

- Uses existing proven infrastructure
- Minimal code changes
- Backward compatible
- Well-defined scope

**Mitigation**:

- Comprehensive test coverage including edge cases
- Default conservative behavior (concurrency = 1)
- Validation prevents invalid configurations
- Existing error handling prevents system instability
