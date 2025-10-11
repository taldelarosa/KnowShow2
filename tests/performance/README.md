# Performance Tests

## Status

### ✅ Working Tests
- **AsyncConcurrencyPerformanceTests.cs** - Tests for concurrent processing feature (9/9 passing)
  - Validates configurable maxConcurrency setting
  - Tests performance scaling from 1-8 concurrent operations
  - Simulates realistic processing delays

- **BulkProcessingPerformanceTests.cs** - Bulk processing performance tests (✅ Fixed!)
  - Tests small, medium, and large batch processing
  - Validates concurrency scaling and batch size optimization
  - Tests memory management and error handling
  - Tests progress reporting overhead

- **SubtitleProcessingBenchmarks.cs** - BenchmarkDotNet performance benchmarks (✅ Fixed!)
  - Benchmarks episode identification from subtitle text
  - Benchmarks video format validation
  - Benchmarks text subtitle extraction
  - Benchmarks PGS subtitle extraction and conversion
  - Uses in-memory database for consistent testing

### ⚠️ Tests Requiring Updates
- **SubtitleWorkflowPerformanceTests.cs** - Needs refactoring
  - References deprecated SubtitleMatcher (now EpisodeIdentificationService)
  - Needs updating to current workflow architecture

## Running Tests

### Run All Working Tests
```bash
dotnet test tests/performance/EpisodeIdentifier.Tests.Performance.csproj --filter "FullyQualifiedName~AsyncConcurrencyPerformanceTests|FullyQualifiedName~BulkProcessingPerformanceTests"
```

### Run Individual Test Suites
```bash
# Async concurrency tests
dotnet test tests/performance/EpisodeIdentifier.Tests.Performance.csproj --filter "FullyQualifiedName~AsyncConcurrencyPerformanceTests"

# Bulk processing tests
dotnet test tests/performance/EpisodeIdentifier.Tests.Performance.csproj --filter "FullyQualifiedName~BulkProcessingPerformanceTests"
```

### Manual Performance Testing
See `MANUAL_TESTING_RESULTS.md` for real-world performance validation results.

## Completed Tasks

1. ✅ **Update BulkProcessingPerformanceTests to use current API** - Fixed all API mismatches
2. ✅ **Update SubtitleProcessingBenchmarks to use current API** - Fixed deprecated service references
3. ✅ **Verify BenchmarkDotNet integration** - Working correctly with 7 benchmarks

## Next Steps

1. ⚠️ **Refactor SubtitleWorkflowPerformanceTests** (Low Priority) - Update deprecated SubtitleMatcher references
2. 🎯 **Add more real-world scenario tests** - Consider adding:
   - Network vs local storage performance comparisons
   - Large batch processing (100+ files)
   - Memory pressure scenarios
   - Different video formats and codecs
3. 📊 **Baseline Performance Metrics** - Establish baseline benchmarks for:
   - Episode identification speed
   - Concurrent processing throughput
   - Memory usage patterns
4. 🔍 **Performance Regression Testing** - Set up automated performance tracking


