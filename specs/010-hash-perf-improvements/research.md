# Research: Hash Performance Improvements with Series/Season Filtering


**Date**: October 7, 2025
**Feature**: 010-hash-perf-improvements
**Purpose**: Research technical decisions for adding optional series/season filtering to database searches

## Database Query Filtering Research


### Decision: Add WHERE clause filters to existing SQL query in FindMatches method


**Rationale**:

- SQLite supports efficient filtering with existing indexes on Series and Season columns
- Minimal code changes required to existing FuzzyHashService.FindMatches method
- WHERE clause filtering happens at database level for maximum performance
- Optional parameters pattern already used in C# (nullable strings, default parameters)

**Alternatives considered**:

- **In-memory filtering after full query**: Would load entire database then filter in C#, defeating performance purpose
- **Separate filtered FindMatches method**: Would duplicate logic, violate DRY principle
- **Stored procedures**: Overkill for simple WHERE clause, adds complexity

### Current Query Structure Analysis


```csharp
// Current query in FuzzyHashService.FindMatches (line 376)
command.CommandText = @"
    SELECT Series, Season, Episode, OriginalText, OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
    FROM SubtitleHashes;";
```


**Proposed modification**:

```csharp
// Build query with optional filters
var query = "SELECT Series, Season, Episode, OriginalText, OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName FROM SubtitleHashes";
var whereClauses = new List<string>();

if (!string.IsNullOrEmpty(seriesFilter))
{
    whereClauses.Add("LOWER(Series) = LOWER(@series)");
}

if (seasonFilter.HasValue)
{
    whereClauses.Add("Season = @season");
}

if (whereClauses.Any())
{
    query += " WHERE " + string.Join(" AND ", whereClauses);
}
```


### Index Utilization


**Current indexes** (from FuzzyHashService.cs line 258):

- `idx_series_season` on `(Series, Season)` - **PERFECT for our use case!**
- `idx_unique_episode` on `(Series, Season, Episode)`
- `idx_clean_hash` on `CleanHash`
- `idx_original_hash` on `OriginalHash`

**Performance impact**:

- Series-only filter: Uses `idx_series_season`, dramatically reduces scan
- Series+Season filter: Uses `idx_series_season`, maximum efficiency
- No filter: Full table scan (current behavior)

## CLI Parameter Design Research


### Decision: Add optional --series and --season parameters to existing identify command


**Rationale**:

- Follows existing System.CommandLine patterns in the codebase
- Optional parameters integrate cleanly with existing command structure
- Users familiar with current CLI won't need to learn new patterns

**Alternatives considered**:

- **Environment variables**: Less discoverable, harder to use in scripts
- **Configuration file**: Overkill for per-query parameters
- **Separate filtered-identify command**: Confusing UX, duplicates functionality

### Command Line Pattern


```bash

# Current usage (backwards compatible)

dotnet run -- identify --input video.mkv --hash-db hashes.db

# New usage with series filter

dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones"

# New usage with series and season filter (maximum performance)

dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones" --season 2
```


### Parameter Validation Strategy


**Season without series validation**:

- Per FR-011: Must return error message when season provided without series
- Validation happens before database query
- Clear error message: "Season parameter requires series parameter"

**Series name validation**:

- Case-insensitive matching (per FR-006)
- Use `LOWER()` in SQL for consistent comparison
- Empty results if series doesn't exist (not an error condition)

## Method Signature Enhancement Research


### Decision: Add optional parameters to FindMatches method signature


**Current signature**:

```csharp
public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(
    string subtitleText,
    double threshold = 0.8)
```


**Proposed signature**:

```csharp
public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(
    string subtitleText,
    double threshold = 0.8,
    string? seriesFilter = null,
    int? seasonFilter = null)
```


**Rationale**:

- Maintains backwards compatibility (all new parameters are optional)
- Follows C# nullable reference types pattern (enabled in project)
- Default null values clearly indicate "no filtering"

**Alternatives considered**:

- **Separate FilteredFindMatches method**: Code duplication, confusing API
- **Options object parameter**: Overkill for 2 simple parameters
- **Builder pattern**: Too complex for simple filtering use case

## Performance Measurement Strategy


### Decision: Add logging to measure and compare query performance


**Rationale**:

- Existing ILogger infrastructure already in place
- Stopwatch class for precise timing
- Logs provide measurable evidence of performance improvement

**Implementation approach**:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// Execute query
sw.Stop();
_logger.LogInformation("Database query completed in {ElapsedMs}ms, returned {Count} records",
    sw.ElapsedMilliseconds, recordCount);
```


**Metrics to track**:

- Query execution time (milliseconds)
- Number of records scanned
- Number of records matching filters
- Hash comparison count

## Error Handling Design


### Decision: Use ArgumentException for invalid parameter combinations


**Rationale**:

- Standard .NET exception for invalid arguments
- Consistent with existing validation pattern in codebase (see StoreHash validation)
- Clear error messages guide users to correct usage

**Error scenarios**:

1. Season provided without series → ArgumentException with clear message
2. Invalid season number (negative, zero) → Caught by int validation naturally
3. Series doesn't exist in database → Empty result set (not an error)

## Backwards Compatibility Analysis


### Decision: All changes are additive only, no breaking changes


**Verification**:

- New parameters have default values (null)
- Existing code calling FindMatches(text, threshold) works unchanged
- No schema changes required (filters use existing Series/Season columns)
- No configuration changes required

**Testing strategy**:

- Run existing tests without modification
- All should pass (backwards compatibility validated)
- Add new tests for filtering functionality

## Summary of Technical Decisions


| Aspect | Decision | Key Benefit |
|--------|----------|-------------|
| Query Filtering | SQL WHERE clause with optional parameters | Database-level performance |
| Index Usage | Leverage existing idx_series_season | No schema changes needed |
| CLI Parameters | Optional --series and --season flags | Familiar UX pattern |
| Method Signature | Optional nullable parameters | Backwards compatible |
| Validation | ArgumentException for season-without-series | Clear error messages |
| Performance | Logging with Stopwatch | Measurable improvements |
| Error Handling | Standard .NET exceptions | Consistent with codebase |

---

*All technical unknowns resolved. Ready for Phase 1: Design*
