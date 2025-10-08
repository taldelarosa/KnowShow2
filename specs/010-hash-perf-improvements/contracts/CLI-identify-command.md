# CLI Contract: Identify Command with Filtering

**Command**: `identify`
**Purpose**: Identify episode from video file with optional series/season filtering
**Version**: 0.10.0

## Command Syntax

```bash
dotnet run -- identify --input <file> --hash-db <database> [--series <name>] [--season <number>] [--threshold <value>] [--rename] [--verbose]
```

## New Options (Feature 010)

### --series Option

**Syntax**: `--series <name>` or `-s <name>`

**Description**: Filter search to specific TV series (case-insensitive)

**Type**: String (optional)

**Example**:
```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones"
```

**Behavior**:
- Case-insensitive matching against database Series column
- Trims whitespace from input
- Empty/whitespace-only values treated as no filter
- Non-existent series names return empty results (not an error)

### --season Option

**Syntax**: `--season <number>` or `-n <number>`

**Description**: Filter search to specific season number (requires --series)

**Type**: Integer (optional)

**Example**:
```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones" --season 2
```

**Behavior**:
- Must be positive integer
- Requires --series to be specified
- Season numbers formatted with zero-padding for database matching (e.g., "02")
- Invalid without --series (returns error)

## Existing Options (Unchanged)

- `--input <file>`: Video file to identify (required)
- `--hash-db <database>`: Database path (required)
- `--threshold <value>`: Confidence threshold (optional, default 0.8)
- `--rename`: Rename file based on identification (optional, default false)
- `--verbose`: Enable verbose logging (optional, default false)

## Parameter Validation

| Validation | Error Message | Exit Code |
|------------|---------------|-----------|
| --season without --series | "Season parameter requires series parameter" | 1 |
| --season negative/zero | "Option '--season' expects a valid positive integer" | 1 |
| --series empty string | Treated as no filter (no error) | - |
| Missing required --input | "Option '--input' is required" | 1 |
| Missing required --hash-db | "Option '--hash-db' is required" | 1 |

## Output Format (JSON)

Output format remains unchanged from existing implementation:

```json
{
  "series": "Bones",
  "season": "02",
  "episode": "13",
  "episodeName": "The Girl in the Gator",
  "matchConfidence": 0.60,
  "suggestedFilename": "Bones - S02E13 - The Girl in the Gator.mkv"
}
```

## Exit Codes

| Exit Code | Meaning |
|-----------|---------|
| 0 | Success - identification completed (match found or not) |
| 1 | Error - invalid parameters, validation failure |
| 2 | Error - file not found, database not accessible |

## Usage Examples

### Example 1: No Filtering (Existing Behavior)

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db
```

**Expected**: Searches entire database, returns best match

### Example 2: Series Filter Only

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones"
```

**Expected**: Searches only Bones episodes, faster than full database search

### Example 3: Series + Season Filter (Maximum Performance)

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones" --season 2
```

**Expected**: Searches only Bones Season 2, maximum performance optimization

### Example 4: Case-Insensitive Series Matching

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "BONES"
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "bones"
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones"
```

**Expected**: All three commands produce identical results

### Example 5: Error - Season Without Series

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --season 2
```

**Expected Output**:
```
Error: Season parameter requires series parameter
```

**Exit Code**: 1

### Example 6: Non-Existent Series

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "NonExistentShow"
```

**Expected Output**:
```json
{
  "series": null,
  "season": null,
  "episode": null,
  "matchConfidence": 0,
  "error": "No matching episodes found in database"
}
```

**Exit Code**: 0 (not an error, just no matches)

### Example 7: With Rename Flag (Existing Feature)

```bash
dotnet run -- identify --input video.mkv --hash-db hashes.db --series "Bones" --season 2 --rename
```

**Expected**: Identifies episode from Bones Season 2, renames file if high confidence

## Performance Characteristics

| Command Pattern | Expected Search Time |
|-----------------|---------------------|
| No filter | Baseline (e.g., 1000ms for 1000 episodes) |
| --series filter | ~10-50% faster depending on database size |
| --series + --season | ~80-95% faster depending on episodes per season |

Actual performance depends on database size, number of series, and episodes per season.

## Logging Output

### With Verbose Flag

```
INFO: Search filter applied: Series='Bones', Season=2
DEBUG: Input video subtitle hashes:
DEBUG:   CleanHash = 3:hCMqz+X...
INFO: Database query completed in 15ms, returned 20 records
INFO: Search completed: 15ms, scanned 20 records, 60 comparisons, found 1 matches
INFO: Bones S02E13 confidence: 60.00%
```

### Without Verbose Flag

```
INFO: Search completed: 15ms, scanned 20 records, found 1 matches
```

## Help Text

```
Options:
  --input, -i <file>         Video file to identify (required)
  --hash-db <database>       Path to hash database (required)
  --series, -s <name>        Filter search to specific series (case-insensitive)
  --season, -n <number>      Filter search to specific season (requires --series)
  --threshold <value>        Confidence threshold for matches (default: 0.8)
  --rename                   Rename file based on identification result
  --verbose, -v              Enable verbose logging
  --help                     Display help information
```

## Backwards Compatibility

All existing command patterns continue to work without modification:

```bash
# Pattern 1: Minimum required parameters
dotnet run -- identify --input video.mkv --hash-db hashes.db

# Pattern 2: With threshold
dotnet run -- identify --input video.mkv --hash-db hashes.db --threshold 0.9

# Pattern 3: With rename
dotnet run -- identify --input video.mkv --hash-db hashes.db --rename
```

All produce identical results to previous versions.

## Test Contract Requirements

CLI contract tests must verify:

1. **New options parse correctly**: --series and --season accepted
2. **Parameter validation**: Season without series rejected with error
3. **Case-insensitive series**: "bones", "BONES", "Bones" all work identically
4. **JSON output format**: Output structure unchanged from previous version
5. **Exit codes**: Correct exit codes for success, validation errors, runtime errors
6. **Backwards compatibility**: Old command patterns work unchanged
7. **Help text**: --help displays new options correctly

---

*CLI Contract version 0.10.0 - Extends existing identify command with optional filtering*
