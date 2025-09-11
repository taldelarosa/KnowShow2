# CLI Contract: File Renaming Recommendations

## Overview
Command line interface contract for the enhanced episode identification system with file renaming recommendations.

## Extended Command Structure

### Identification with Filename Suggestion
```bash
dotnet run -- --input video.mkv --hash-db hashes.db
```

**Response** (Enhanced JSON):
```json
{
  "series": "Breaking Bad",
  "season": "01", 
  "episode": "05",
  "matchConfidence": 0.95,
  "ambiguityNotes": null,
  "error": null,
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv"
}
```

### Identification with Automatic Rename
```bash
dotnet run -- --input video.mkv --hash-db hashes.db --rename
```

**Response** (With Rename Confirmation):
```json
{
  "series": "Breaking Bad",
  "season": "01", 
  "episode": "05",
  "matchConfidence": 0.95,
  "ambiguityNotes": null,
  "error": null,
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv",
  "fileRenamed": true,
  "originalFilename": "video.mkv"
}
```

## New CLI Parameters

### --rename Flag
- **Type**: Boolean flag
- **Required**: No
- **Default**: false
- **Description**: Automatically rename the input file to the suggested filename
- **Behavior**: Only active when identification confidence ≥ 90%

```bash
# Basic usage
--rename

# Combined with existing options
--input video.mkv --hash-db hashes.db --rename --language eng
```

## Response Contract Changes

### Enhanced Success Response
```json
{
  "series": "string",
  "season": "string", 
  "episode": "string",
  "matchConfidence": "number (0.0-1.0)",
  "ambiguityNotes": "string|null",
  "error": "object|null",
  "suggestedFilename": "string|null",    // NEW: Only present for high confidence
  "fileRenamed": "boolean|null",         // NEW: Only present when --rename used
  "originalFilename": "string|null"      // NEW: Only present when --rename used
}
```

### Low Confidence Response (No Change)
```json
{
  "series": "Example Show",
  "season": "01", 
  "episode": "02",
  "matchConfidence": 0.75,
  "ambiguityNotes": "Multiple possible matches found",
  "error": null
  // suggestedFilename NOT included (confidence < 90%)
}
```

### Error Responses (Enhanced)
```json
{
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Could not rename file: Permission denied"
  },
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv"
}
```

## Filename Generation Rules

### Standard Format
```
{SeriesName} - S{Season}E{Episode} - {EpisodeName}.{Extension}
```

### Examples
- `"Breaking Bad - S01E05 - Gray Matter.mkv"`
- `"The Office - S02E01 - The Dundies.mp4"`
- `"Lost - S04E13 - There's No Place Like Home.avi"`

### Fallback Format (No Episode Name)
```
{SeriesName} - S{Season}E{Episode}.{Extension}
```

### Examples
- `"Breaking Bad - S01E05.mkv"`
- `"The Office - S02E01.mp4"`

### Windows Sanitization
- Replace `< > : " | ? * \` with single space
- Trim multiple spaces to single space
- Ensure total length ≤ 260 characters

### Sanitization Examples
```
Input:  "Show: Name" - S01E01 - "Episode Title".mkv
Output: "Show  Name - S01E01 - Episode Title.mkv"

Input:  "Very Long Series Name With Many Words" - S01E01 - "Very Long Episode Title That Exceeds Limits".mkv
Output: "Very Long Series Name With Many Words - S01E01 - Very Long Episode Title That Ex.mkv"
```

## Confidence Threshold Behavior

### High Confidence (≥ 90%)
- Include `suggestedFilename` in response
- Execute rename operation if `--rename` flag present
- Return rename status in response

### Low Confidence (< 90%)
- Exclude `suggestedFilename` from response  
- Ignore `--rename` flag (no file operations)
- Standard response format maintained

## Error Scenarios

### File Rename Errors
```json
{
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Target file already exists: Breaking Bad - S01E05 - Gray Matter.mkv"
  },
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv",
  "fileRenamed": false,
  "originalFilename": "video.mkv"
}
```

### Permission Errors
```json
{
  "error": {
    "code": "FILE_RENAME_FAILED", 
    "message": "Permission denied: Cannot write to directory"
  },
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv",
  "fileRenamed": false,
  "originalFilename": "video.mkv"
}
```

### File Not Found Errors
```json
{
  "error": {
    "code": "FILE_RENAME_FAILED",
    "message": "Source file not found: video.mkv"
  },
  "suggestedFilename": "Breaking Bad - S01E05 - Gray Matter.mkv"
}
```

## Backward Compatibility

### Existing Behavior Preserved
- All existing CLI parameters work unchanged
- Existing JSON response format maintained
- New fields are additive only
- No breaking changes to current workflows

### Migration Strategy
- Clients can ignore new fields if not needed
- `suggestedFilename` field is optional
- `--rename` flag is optional (defaults to false)
- Error handling maintains existing patterns

## Testing Scenarios

### High Confidence Identification
```bash
# Test filename suggestion
./test-input.mkv → "Test Show - S01E01 - Pilot.mkv"

# Test automatic rename
./random-filename.mkv → "Test Show - S01E01 - Pilot.mkv" (file renamed)
```

### Low Confidence Identification  
```bash
# No filename suggestion provided
./unclear-video.mkv → Standard response, no suggestedFilename field
```

### Character Sanitization
```bash
# Windows invalid characters
"Show: Title" → "Show  Title"
"Episode \"Title\"" → "Episode  Title "
```

### Length Limits
```bash
# Long names truncated
"Very Long Series Name... - S01E01 - Very Long Episode Title..." 
→ "Very Long Series Name... - S01E01 - Very Long Episode Tit.mkv"
```

### Error Conditions
```bash
# File already exists
./video.mkv + target exists → Error with suggestion still provided

# Permission denied
./video.mkv + no write permission → Error with suggestion still provided

# Invalid filename generated
Series with all invalid chars → Error in filename generation
```
