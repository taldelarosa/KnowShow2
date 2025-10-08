# KnowShow_Specd Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-10-07

## Active Technologies

- C# / .NET 8.0
- SQLite (Microsoft.Data.Sqlite 8.0.0)
- System.CommandLine (CLI framework)
- ssdeep.NET 1.0.0 (fuzzy hashing)
- xUnit (testing)

## Project Structure

```
src/
  EpisodeIdentifier.Core/
    Services/
      FuzzyHashService.cs
      SubtitleNormalizationService.cs
      ConfigurationService.cs
    Commands/
      ConfigurationCommands.cs
    Models/
tests/
  contract/
  integration/
  unit/
```

## Commands

```bash
# Identify episode with optional series/season filtering
dotnet run -- identify --input video.mkv --hash-db hashes.db [--series "SeriesName"] [--season N]

# Import subtitles
dotnet run -- import --input subtitles/ --hash-db hashes.db --recursive

# Run tests
dotnet test
```

## Code Style

- Follow standard C# conventions
- Use nullable reference types (enabled)
- Async/await for database operations
- ILogger for structured logging

## Recent Changes

- 010-hash-perf-improvements: Added optional series/season filtering to hash searches for performance optimization
- 008-fuzzy-hashing-plus: Added CTPH-only hashing system

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
