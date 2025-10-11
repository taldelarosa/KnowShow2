# Performance Tests

## Status

### ✅ Working Tests
- **AsyncConcurrencyPerformanceTests.cs** - Tests for concurrent processing feature (9/9 passing)
  - Validates configurable maxConcurrency setting
  - Tests performance scaling from 1-8 concurrent operations
  - Simulates realistic processing delays

### ⚠️ Tests Requiring Updates
- **BulkProcessingPerformanceTests.cs** - Needs API updates
  - Uses old BulkProcessorService constructor signature
  - References removed properties (TotalDuration, Progress)
  - Needs refactoring to match current BulkProcessingResult API
  
- **SubtitleWorkflowPerformanceTests.cs** - Needs refactoring
  - References deprecated SubtitleMatcher (now EpisodeIdentificationService)
  - Needs updating to current workflow architecture

- **SubtitleProcessingBenchmarks.cs** - Needs refactoring  
  - References deprecated SubtitleMatcher
  - BenchmarkDotNet integration needs verification

## Running Tests

### Run Working Tests
```bash
dotnet test tests/performance/EpisodeIdentifier.Tests.Performance.csproj --filter "FullyQualifiedName~AsyncConcurrencyPerformanceTests"
```

### Manual Performance Testing
See `MANUAL_TESTING_RESULTS.md` for real-world performance validation results.

## Next Steps

1. Update BulkProcessingPerformanceTests to use current API
2. Refactor SubtitleWorkflow tests for new architecture
3. Verify BenchmarkDotNet integration
4. Add more real-world scenario tests

