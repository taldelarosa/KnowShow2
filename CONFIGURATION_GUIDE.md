# Configuration Guide


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
EpisodeIdentifier.Core supports JSON-based configuration for maximum flexibility. The configuration file `episodeidentifier.config.json` is automatically created with default values if not present.

## Configuration File Location


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
The configuration file should be placed in the same directory as the executable:

- `episodeidentifier.config.json`

## Configuration Options


<<<<<<< HEAD

### Match Confidence Thresholds


=======

### Match Confidence Thresholds


>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "matchConfidenceThreshold": 0.8,
  "renameConfidenceThreshold": 0.85
}
```


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

- **matchConfidenceThreshold**: Minimum confidence (0.0-1.0) required for episode identification
    - Default: `0.8` (80%)
    - Lower values allow more fuzzy matches but may increase false positives
    - Higher values require more precise matches but may miss valid episodes

- **renameConfidenceThreshold**: Minimum confidence required for automatic file renaming
    - Default: `0.85` (85%)
    - Should typically be higher than matchConfidenceThreshold
    - Used when `--rename` flag is specified

### Filename Parsing Patterns


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "filenamePatterns": {
    "primaryPattern": "^(.+?)\\s+S(\\d+)E(\\d+)(?:[\\s\\.\\-]+(.+?))?$",
<<<<<<< HEAD
    "secondaryPattern": "^(.+?)\\s+(\\d+)x(\\d+)(?:[\\s\\.\\-]+(.+?))?$",
=======
    "secondaryPattern": "^(.+?)\\s+(\\d+)x(\\d+)(?:[\\s\\.\\-]+(.+?))?$",
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
    "tertiaryPattern": "^(.+?)\\.S(\\d+)\\.E(\\d+)(?:\\.(.+?))?$"
  }
}
```


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
These regex patterns control how series information is extracted from subtitle filenames:

- **primaryPattern**: Default format "Series S##E## Episode Name"
    - Matches: "Bones S12E01 The Final Chapter"
    - Groups: (1) Series, (2) Season, (3) Episode, (4) Episode Name

- **secondaryPattern**: Alternative format "Series ##x## Episode Name"
    - Matches: "Bones 12x01 The Final Chapter"
    - Groups: (1) Series, (2) Season, (3) Episode, (4) Episode Name

- **tertiaryPattern**: Dot-separated format "Series.S##.E##.Episode Name"
    - Matches: "Bones.S12.E01.The Final Chapter"
    - Groups: (1) Series, (2) Season, (3) Episode, (4) Episode Name

### Filename Template


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
}
```


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
Controls the format of renamed files. Available placeholders:

- `{SeriesName}`: The identified series name
- `{Season}`: Season number (can use format specifiers like `:D2` for zero-padding)
- `{Episode}`: Episode number (can use format specifiers like `:D2` for zero-padding)
- `{EpisodeName}`: The episode title (if available)
- `{FileExtension}`: Original file extension (.mkv, .mp4, etc.)

**Note**: The filename template feature is defined but not yet fully implemented. Currently uses hard-coded format.

## Example Configurations


<<<<<<< HEAD

### Conservative (High Accuracy)


=======

### Conservative (High Accuracy)


>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "matchConfidenceThreshold": 0.9,
  "renameConfidenceThreshold": 0.95
}
```


<<<<<<< HEAD

### Permissive (More Matches)


=======

### Permissive (More Matches)


>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "matchConfidenceThreshold": 0.7,
  "renameConfidenceThreshold": 0.8
}
```


<<<<<<< HEAD

### Custom Patterns for Different Naming Conventions


=======

### Custom Patterns for Different Naming Conventions


>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78

```json
{
  "filenamePatterns": {
    "primaryPattern": "^([^-]+)-\\s*Season\\s*(\\d+)\\s*Episode\\s*(\\d+)(?:\\s*-\\s*(.+?))?$",
    "secondaryPattern": "^(.+?)\\s+(\\d{4})\\s+(\\d{2})\\s+(\\d{2})(?:\\s+(.+?))?$",
    "tertiaryPattern": "^(.+?)_S(\\d+)E(\\d+)_(.+?)$"
  }
}
```


<<<<<<< HEAD

## Configuration Validation


=======

## Configuration Validation


>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
The application will log warnings if:

- Configuration file cannot be parsed
- Invalid confidence values (outside 0.0-1.0 range)
- Invalid regex patterns that cannot compile

In case of errors, the application falls back to default values and continues operation.

## Runtime Configuration Loading


<<<<<<< HEAD

=======
>>>>>>> 81fec204ed68e2301ca62c560a507572f1abad78
Configuration is loaded once at application startup. To apply changes:

1. Modify the `episodeidentifier.config.json` file
2. Restart the application

The current configuration values are logged at DEBUG level during startup for verification.
