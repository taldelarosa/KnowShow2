# KnowShow_Specd Development Guidelines


Auto-generated from all feature plans. Last updated: 2025-09-15

## Active Technologies


- C# .NET 8.0, System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
- SQLite database for hash storage, JSON configuration with hot-reload
- xUnit testing framework with TDD approach
- (008-fuzzy-hashing-plus)
- (010-async-processing-where) Configurable concurrent episode identification

## Project Structure


```
src/
  EpisodeIdentifier.Core/
    Models/
      BulkProcessingOptions.cs  # Contains MaxConcurrency property
      Configuration/            # App configuration models
    Services/                   # Core business logic
    Program.cs                  # CLI entry point
tests/
  unit/
  integration/
  contract/
specs/
  010-async-processing-where/  # Current feature documentation
    plan.md
    research.md
    data-model.md
    quickstart.md
    contracts/
```


## Commands


### Bulk Processing


- `--bulk-identify <directory>` - Process multiple files with configurable concurrency
- Reads maxConcurrency from episodeidentifier.config.json (default: 1, range: 1-100)
- Supports hot-reload of configuration during processing

## Code Style


: Follow standard conventions

## Recent Changes


- 008-fuzzy-hashing-plus: Added  +

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
