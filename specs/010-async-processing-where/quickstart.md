# Quickstart: Async Processing with Configurable Concurrency

**Date**: September 15, 2025
**Feature**: 010-async-processing-where

## Overview

This quickstart validates the configurable concurrency feature for episode identification by testing various concurrency levels and verifying correct behavior.

## Prerequisites

- .NET 8.0 or higher
- Episode Identifier application built and available
- Test video files for identification
- episodeidentifier.config.json file present

## Test Scenarios

### Scenario 1: Default Single File Processing

**Objective**: Verify backward compatibility with default concurrency setting

**Setup**:
1. Ensure `episodeidentifier.config.json` has `maxConcurrency: 1` or omit the field entirely
2. Prepare directory with 5 test video files
3. Start episode identifier with bulk processing

**Commands**:
```bash
# Ensure default configuration
cat episodeidentifier.config.json | grep maxConcurrency || echo "Using default (1)"

# Run bulk identification
./episodeidentifier --bulk-identify ./test-videos --output results.json
```

**Expected Results**:
- Files processed sequentially (one at a time)
- Progress reports show `ActiveOperations: 1` at any time
- All files processed successfully
- JSON output contains all results

**Validation**:
```bash
# Check results
jq '.summary.totalFiles' results.json  # Should equal number of test files
jq '.summary.processedFiles' results.json  # Should equal totalFiles
jq '.results | length' results.json  # Should equal number of files
```

### Scenario 2: Concurrent Processing (3 Files)

**Objective**: Verify concurrent processing with moderate concurrency

**Setup**:
1. Update `episodeidentifier.config.json` to set `maxConcurrency: 3`
2. Prepare directory with 10 test video files
3. Monitor processing behavior

**Commands**:
```bash
# Update configuration
jq '.maxConcurrency = 3' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json

# Verify configuration
jq '.maxConcurrency' episodeidentifier.config.json

# Run bulk identification with monitoring
./episodeidentifier --bulk-identify ./test-videos --output results.json 2> progress.log &
PID=$!

# Monitor progress (in another terminal)
tail -f progress.log

# Wait for completion
wait $PID
```

**Expected Results**:
- Up to 3 files processed simultaneously
- Progress reports show `ActiveOperations` ≤ 3
- `QueuedFiles` count decreases as processing completes
- All files processed successfully
- Processing time reduced compared to sequential

**Validation**:
```bash
# Check for concurrent processing evidence in logs
grep "ActiveOperations: [2-3]" progress.log

# Verify all files processed
jq '.summary.processedFiles == .summary.totalFiles' results.json

# Check for any errors
jq '.errors | length' results.json  # Should be 0
```

### Scenario 3: Hot-Reload During Processing

**Objective**: Verify configuration hot-reload without interrupting active operations

**Setup**:
1. Start with `maxConcurrency: 2`
2. Begin processing large batch of files
3. Update configuration to `maxConcurrency: 5` during processing

**Commands**:
```bash
# Set initial configuration
jq '.maxConcurrency = 2' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json

# Start long-running bulk processing
./episodeidentifier --bulk-identify ./large-test-batch --output results.json 2> progress.log &
PID=$!

# Wait for processing to start
sleep 5

# Update configuration while processing
jq '.maxConcurrency = 5' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json

# Monitor behavior change
tail -f progress.log | grep "ActiveOperations"

# Wait for completion
wait $PID
```

**Expected Results**:
- Initial processing shows up to 2 concurrent operations
- After configuration change, new operations use concurrency of 5
- No interruption to active operations
- Hot-reload detected and applied

**Validation**:
```bash
# Check for configuration change evidence
grep "ActiveOperations: [3-5]" progress.log

# Ensure no processing interruption
jq '.summary.processedFiles == .summary.totalFiles' results.json
```

### Scenario 4: Error Handling with Concurrency

**Objective**: Verify individual failures don't stop concurrent operations

**Setup**:
1. Set `maxConcurrency: 4`
2. Include problematic files (corrupted, wrong format) in test batch
3. Mix with valid video files

**Commands**:
```bash
# Set concurrency
jq '.maxConcurrency = 4' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json

# Create mixed test batch
mkdir mixed-test-batch
cp ./valid-videos/*.mkv ./mixed-test-batch/
cp ./corrupted-files/*.txt ./mixed-test-batch/  # Wrong format
touch ./mixed-test-batch/empty.mkv  # Empty file

# Run processing
./episodeidentifier --bulk-identify ./mixed-test-batch --output results.json
```

**Expected Results**:
- Valid files processed successfully
- Invalid files fail gracefully
- Concurrent operations continue despite individual failures
- All results (success and failure) in JSON output

**Validation**:
```bash
# Check mixed results
jq '.summary.successfulFiles' results.json  # Should be > 0
jq '.summary.failedFiles' results.json      # Should be > 0
jq '.errors | length' results.json          # Should match failed files

# Verify specific file results
jq '.results[] | select(.status == "success") | .inputFile' results.json
jq '.results[] | select(.status == "failure") | .inputFile' results.json
```

### Scenario 5: Configuration Validation

**Objective**: Verify invalid configuration handling

**Setup**:
1. Test various invalid `maxConcurrency` values
2. Verify fallback to default behavior

**Commands**:
```bash
# Test invalid values
echo "Testing negative value"
jq '.maxConcurrency = -1' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json
./episodeidentifier --bulk-identify ./test-videos --output results-negative.json 2> log-negative.txt

echo "Testing zero value"
jq '.maxConcurrency = 0' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json
./episodeidentifier --bulk-identify ./test-videos --output results-zero.json 2> log-zero.txt

echo "Testing excessive value"
jq '.maxConcurrency = 1000' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json
./episodeidentifier --bulk-identify ./test-videos --output results-excessive.json 2> log-excessive.txt

echo "Testing non-numeric value"
jq '.maxConcurrency = "invalid"' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json
./episodeidentifier --bulk-identify ./test-videos --output results-invalid.json 2> log-invalid.txt
```

**Expected Results**:
- All invalid configurations fall back to `maxConcurrency = 1`
- Warning messages logged for invalid values
- Processing continues with single-file behavior
- No application crashes or errors

**Validation**:
```bash
# Check for warnings in logs
grep -i "warning\|invalid\|default" log-*.txt

# Verify single-file processing behavior
grep "ActiveOperations: 1" log-*.txt

# Ensure all processed successfully despite invalid config
for file in results-*.json; do
    echo "Checking $file:"
    jq '.summary.processedFiles == .summary.totalFiles' "$file"
done
```

## Performance Validation

### Processing Time Comparison

**Commands**:
```bash
# Test different concurrency levels with same file set
for concurrency in 1 2 4 8; do
    echo "Testing concurrency: $concurrency"
    jq ".maxConcurrency = $concurrency" episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json
    
    time ./episodeidentifier --bulk-identify ./performance-test-batch --output "results-$concurrency.json"
done

# Compare processing times
echo "Processing time comparison:"
for concurrency in 1 2 4 8; do
    processed=$(jq '.summary.processedFiles' "results-$concurrency.json")
    time=$(jq -r '.summary.processingTime' "results-$concurrency.json")
    echo "Concurrency $concurrency: $processed files in $time"
done
```

## Cleanup

```bash
# Restore default configuration
jq '.maxConcurrency = 1' episodeidentifier.config.json > temp.json && mv temp.json episodeidentifier.config.json

# Clean up test files
rm -rf ./test-videos ./mixed-test-batch ./performance-test-batch
rm -f results*.json log*.txt progress.log temp.json
```

## Success Criteria

This quickstart validates successful implementation when:

1. **Default Behavior**: Single-file processing works with default configuration
2. **Concurrent Processing**: Multiple files processed simultaneously up to configured limit
3. **Hot-Reload**: Configuration changes apply during processing without interruption
4. **Error Isolation**: Individual file failures don't stop other concurrent operations
5. **Configuration Validation**: Invalid configurations fall back to safe defaults
6. **Performance**: Concurrent processing reduces total processing time
7. **Output Format**: JSON results include all concurrent operation results
8. **Progress Reporting**: Progress updates reflect concurrent operation status

All scenarios should complete successfully with expected behavior and no application errors or crashes.