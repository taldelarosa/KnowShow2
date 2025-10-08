# Data Model: Hash Performance Improvements with Series/Season Filtering


**Date**: October 7, 2025
**Feature**: 010-hash-perf-improvements
**Purpose**: Define data structures and models for filtered hash searching

## Core Entities


### Search Filter Parameters


Represents optional filtering criteria provided by users to narrow database searches.

**Properties**:

- `SeriesFilter` (string?, optional): TV series name for filtering, case-insensitive matching
- `SeasonFilter` (int?, optional): Season number for filtering, must be positive integer
- `Threshold` (double, existing): Confidence threshold for matches (0.0-1.0)

**Validation Rules**:

- `SeasonFilter` cannot be provided without `SeriesFilter` (FR-011)
- `SeriesFilter` is case-insensitive, matched using SQL LOWER()
- `SeasonFilter` must be positive integer when provided
- Empty string `SeriesFilter` treated as null (no filtering)

**Usage Context**:

```csharp
// No filtering (current behavior)
var matches = await service.FindMatches(subtitleText, threshold: 0.8);

// Series filtering only
var matches = await service.FindMatches(subtitleText, threshold: 0.8, seriesFilter: "Bones");

// Series and season filtering (maximum performance)
var matches = await service.FindMatches(subtitleText, threshold: 0.8, seriesFilter: "Bones", seasonFilter: 2);
```


### Labelled Subtitle (Existing Entity - No Changes)


Existing entity representing a catalogued episode subtitle with metadata.

**Properties** (no changes required):

- `Series` (string): TV series name
- `Season` (string): Season identifier
- `Episode` (string): Episode identifier
- `SubtitleText` (string): Subtitle content
- `EpisodeName` (string?, nullable): Episode title

**Relevance**: Filter parameters match against `Series` and `Season` fields in database queries.

## Data Flow


### Filtered Search Flow


```
User Input (CLI)
    → --series "Bones" --season 2 (optional parameters)
    ↓
CommandLineParser
    → Validates season-without-series constraint
    → Passes to FuzzyHashService.FindMatches()
    ↓
FindMatches Method
    → Validates parameters (ArgumentException if invalid)
    → Builds SQL query with WHERE clause
    → Parameters: @series, @season (if provided)
    ↓
SQLite Database
    → Uses idx_series_season index for efficient filtering
    → Returns filtered SubtitleHashes records
    ↓
Hash Comparison Loop
    → Only compares against filtered records (performance gain)
    → Returns matches above threshold
    ↓
JSON Response (Existing Format)
    → Same IdentificationResult structure
    → Faster response time due to fewer comparisons
```


### Performance Comparison Flow


```

# Without Filtering (Current)

Database: SELECT all 246 records
    → Compare 246 × 4 hash variants
    → ~984 hash comparisons maximum

# With Series Filter ("Bones")

Database: SELECT WHERE Series = "Bones" → 246 records
    → Compare 246 × 4 hash variants
    → ~984 hash comparisons
    → (For Bones: same count, but isolated to one series)

# With Series + Season Filter ("Bones", Season 2)

Database: SELECT WHERE Series = "Bones" AND Season = "02" → ~20 records
    → Compare 20 × 4 hash variants
    → ~80 hash comparisons maximum
    → **~92% reduction in comparisons!**
```


## Method Signature Changes


### FuzzyHashService.FindMatches (Modified)


**Current Signature**:

```csharp
public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(
    string subtitleText,
    double threshold = 0.8)
```


**New Signature**:

```csharp
public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(
    string subtitleText,
    double threshold = 0.8,
    string? seriesFilter = null,
    int? seasonFilter = null)
```


**Changes**:

- Added `seriesFilter` parameter (nullable string, default null)
- Added `seasonFilter` parameter (nullable int, default null)
- All new parameters optional for backwards compatibility

**Validation Logic**:

```csharp
// Validate parameter combination
if (seasonFilter.HasValue && string.IsNullOrWhiteSpace(seriesFilter))
{
    throw new ArgumentException(
        "Season filter requires series filter to be specified",
        nameof(seasonFilter));
}
```


## Database Query Construction


### Dynamic WHERE Clause Building


**Current Query** (line ~376 in FuzzyHashService.cs):

```csharp
command.CommandText = @"
    SELECT Series, Season, Episode, OriginalText, OriginalHash,
           NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
    FROM SubtitleHashes;";
```


**New Query with Optional Filtering**:

```csharp
var baseQuery = @"
    SELECT Series, Season, Episode, OriginalText, OriginalHash,
           NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
    FROM SubtitleHashes";

var whereClauses = new List<string>();

if (!string.IsNullOrWhiteSpace(seriesFilter))
{
    whereClauses.Add("LOWER(Series) = LOWER(@series)");
}

if (seasonFilter.HasValue)
{
    whereClauses.Add("Season = @season");
}

if (whereClauses.Any())
{
    baseQuery += " WHERE " + string.Join(" AND ", whereClauses);
}

command.CommandText = baseQuery;

// Add parameters if filters provided
if (!string.IsNullOrWhiteSpace(seriesFilter))
{
    command.Parameters.AddWithValue("@series", seriesFilter);
}

if (seasonFilter.HasValue)
{
    // Season stored as string with zero-padding (e.g., "02", "10")
    command.Parameters.AddWithValue("@season", seasonFilter.Value.ToString("D2"));
}
```


**Index Utilization**:

- Series filter only: Uses `idx_series_season` (partial match on first column)
- Series + Season filter: Uses `idx_series_season` (exact match on both columns)
- No filters: Full table scan (current behavior)

## CLI Command Structure


### Identify Command Extension (Existing Command)


**Current Command**:

```bash
dotnet run -- identify --input <file> --hash-db <database>
```


**Extended Command**:

```bash
dotnet run -- identify --input <file> --hash-db <database> [--series <name>] [--season <number>]
```


**New Options**:

- `--series <name>`: Optional series name for filtering (case-insensitive)
- `--season <number>`: Optional season number for filtering (requires --series)

**Option Definitions** (System.CommandLine):

```csharp
var seriesOption = new Option<string?>(
    aliases: new[] { "--series", "-s" },
    description: "Filter search to specific TV series (case-insensitive)",
    getDefaultValue: () => null);

var seasonOption = new Option<int?>(
    aliases: new[] { "--season", "-n" },
    description: "Filter search to specific season (requires --series)",
    getDefaultValue: () => null);
```


## Performance Metrics Model


### Performance Log Data


Information logged to measure performance improvements.

**Metrics Captured**:

- `QueryExecutionTime` (TimeSpan): Time to execute database query
- `RecordsScanned` (int): Number of database records returned by query
- `RecordsFiltered` (int): Number of records after applying filters
- `HashComparisons` (int): Number of hash comparison operations performed
- `MatchesFound` (int): Number of matches above confidence threshold

**Logging Format**:

```csharp
_logger.LogInformation(
    "Search completed: {ElapsedMs}ms, scanned {Scanned} records, {Comparisons} comparisons, found {Matches} matches",
    stopwatch.ElapsedMilliseconds,
    recordsScanned,
    hashComparisons,
    matchesFound);
```


## Validation and Constraints


### Parameter Validation Rules


| Validation | Rule | Error Response |
|------------|------|----------------|
| Season without Series | seasonFilter != null && seriesFilter == null | ArgumentException: "Season filter requires series filter" |
| Negative Season | seasonFilter < 0 | Type validation prevents (int? cannot be negative input) |
| Empty Series String | seriesFilter == "" | Treated as null (no filtering) |
| Whitespace Series | seriesFilter == "   " | Treated as null (no filtering) |
| Non-existent Series | No records match series name | Empty result set (not an error) |
| Invalid Season Format | User enters "abc" for season | CLI validation error (type mismatch) |

### Backwards Compatibility Validation


**Existing Call Patterns** (must continue working):

```csharp
// Pattern 1: Only subtitle text
var matches = await service.FindMatches(text);

// Pattern 2: Text with threshold
var matches = await service.FindMatches(text, 0.85);

// Pattern 3: Named threshold parameter
var matches = await service.FindMatches(text, threshold: 0.9);
```


All patterns remain valid because new parameters have default null values.

## Error Handling


### Exception Types


1. **ArgumentException**: Invalid parameter combination (season without series)
   - Thrown before database query
   - Clear error message guides user to correct usage

2. **SqliteException**: Database-level errors (connection, syntax)
   - Existing error handling remains unchanged
   - Logged with full context

3. **FormatException**: Type conversion errors
   - Handled by System.CommandLine validation
   - CLI provides error before method call

### Error Messages


```csharp
// Season without series
throw new ArgumentException(
    "Season filter requires series filter to be specified. " +
    "Use --series <name> when providing --season <number>.",
    nameof(seasonFilter));
```


## Testing Considerations


### Test Scenarios


1. **No filters provided**: Verify backwards compatibility, all records scanned
2. **Series filter only**: Verify case-insensitive matching, correct filtering
3. **Series + Season filter**: Verify combined filtering, maximum performance
4. **Invalid series name**: Verify empty results (not error)
5. **Season without series**: Verify ArgumentException thrown
6. **Whitespace/empty series**: Verify treated as no filter
7. **Performance measurement**: Verify filtered queries faster than full scan

### Test Data Requirements


- Multiple series in test database (e.g., "Bones", "Breaking Bad", "The Office")
- Multiple seasons per series (e.g., Bones S01, S02, S03)
- Sufficient episodes per season for performance measurement (~20+ episodes)

---

*Data model complete. Ready for contract generation and quickstart guide.*
