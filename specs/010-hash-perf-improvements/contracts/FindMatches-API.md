# API Contract: FuzzyHashService.FindMatches with Filtering


**Method**: `FindMatches`
**Service**: `FuzzyHashService`
**Purpose**: Search for episode matches with optional series/season filtering
**Version**: 0.10.0

## Method Signature


```csharp
public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(
    string subtitleText,
    double threshold = 0.8,
    string? seriesFilter = null,
    int? seasonFilter = null)
```


## Parameters


### Input Parameters


| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `subtitleText` | string | Yes | - | Subtitle text content to search for matches |
| `threshold` | double | No | 0.8 | Minimum confidence score (0.0-1.0) for matches |
| `seriesFilter` | string? | No | null | Optional series name for filtering (case-insensitive) |
| `seasonFilter` | int? | No | null | Optional season number for filtering (requires seriesFilter) |

### Parameter Constraints


- `subtitleText`: Cannot be null or empty
- `threshold`: Must be between 0.0 and 1.0
- `seriesFilter`: Case-insensitive, whitespace-trimmed, null/empty treated as no filter
- `seasonFilter`: Must be positive integer, cannot be provided without seriesFilter

## Return Value


Returns `Task<List<(LabelledSubtitle Subtitle, double Confidence)>>`

- Empty list if no matches found
- List of tuples containing matched subtitle and confidence score
- Sorted by confidence score (highest first)

## Error Conditions


| Condition | Exception | Message |
|-----------|-----------|---------|
| Season filter without series filter | ArgumentException | "Season filter requires series filter to be specified" |
| Null subtitle text | ArgumentException | (existing validation) |
| Invalid threshold | ArgumentException | (existing validation) |

## Backwards Compatibility


All new parameters are optional with default values. Existing call patterns remain valid:

```csharp
// Pattern 1: Minimum parameters (existing)
var matches = await service.FindMatches(subtitleText);

// Pattern 2: With threshold (existing)
var matches = await service.FindMatches(subtitleText, 0.85);

// Pattern 3: With series filter (new)
var matches = await service.FindMatches(subtitleText, 0.8, "Bones");

// Pattern 4: With series and season filter (new)
var matches = await service.FindMatches(subtitleText, 0.8, "Bones", 2);
```


## Performance Characteristics


| Scenario | Expected Behavior |
|----------|-------------------|
| No filters | Full table scan, all records compared |
| Series filter only | Index-optimized query, only matching series compared |
| Series + Season filter | Index-optimized query, only matching series/season compared |

Expected performance improvement: 10-90% faster with filtering depending on database size and filter selectivity.

## Database Query Contract


### SQL Query Pattern (No Filters)


```sql
SELECT Series, Season, Episode, OriginalText, OriginalHash,
       NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
FROM SubtitleHashes;
```


### SQL Query Pattern (Series Filter)


```sql
SELECT Series, Season, Episode, OriginalText, OriginalHash,
       NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
FROM SubtitleHashes
WHERE LOWER(Series) = LOWER(@series);
```


### SQL Query Pattern (Series + Season Filter)


```sql
SELECT Series, Season, Episode, OriginalText, OriginalHash,
       NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
FROM SubtitleHashes
WHERE LOWER(Series) = LOWER(@series)
  AND Season = @season;
```


## Test Contract Requirements


Contract tests must verify:

1. **Backwards Compatibility**: Existing calls without new parameters work unchanged
2. **Series Filtering**: Case-insensitive matching, returns only matching series
3. **Season Filtering**: Requires series, returns only matching season
4. **Parameter Validation**: Season without series throws ArgumentException
5. **Empty Results**: Non-existent series returns empty list (not error)
6. **Performance**: Filtered queries demonstrably faster than full scans

## Example Usage


### Scenario 1: No Filtering (Existing Behavior)


```csharp
var service = new FuzzyHashService(dbPath, logger, normalizationService);
var matches = await service.FindMatches(videoSubtitleText, threshold: 0.8);
// Returns: All matching episodes from entire database
```


### Scenario 2: Series Filtering


```csharp
var service = new FuzzyHashService(dbPath, logger, normalizationService);
var matches = await service.FindMatches(
    videoSubtitleText,
    threshold: 0.8,
    seriesFilter: "Bones");
// Returns: Only episodes from "Bones" series
```


### Scenario 3: Series + Season Filtering


```csharp
var service = new FuzzyHashService(dbPath, logger, normalizationService);
var matches = await service.FindMatches(
    videoSubtitleText,
    threshold: 0.8,
    seriesFilter: "Bones",
    seasonFilter: 2);
// Returns: Only episodes from "Bones" Season 2
```


### Scenario 4: Error Case - Season Without Series


```csharp
var service = new FuzzyHashService(dbPath, logger, normalizationService);

// This throws ArgumentException
await service.FindMatches(
    videoSubtitleText,
    threshold: 0.8,
    seasonFilter: 2);  // ERROR: No series specified
```


## Logging Contract


Method must log performance metrics:

```
INFO: Search completed: {ElapsedMs}ms, scanned {Scanned} records, {Comparisons} comparisons, found {Matches} matches
```


When filters applied:

```
INFO: Search filter applied: Series='{Series}', Season={Season}
```


---

*Contract version 0.10.0 - Extends existing FindMatches method with optional filtering*
