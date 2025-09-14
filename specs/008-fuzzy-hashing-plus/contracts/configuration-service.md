# Configuration Service Contract








## Interface: IConfigurationService








### LoadConfiguration()








**Signature**: `Task<ConfigurationResult> LoadConfiguration()`

**Returns**:

```json
{
  "isValid": true,
  "configuration": {
    "version": "2.0",
    "matchConfidenceThreshold": 0.8,
    "renameConfidenceThreshold": 0.85,
    "fuzzyHashThreshold": 75,
    "hashingAlgorithm": "CTPH",
    "filenamePatterns": {
      "primaryPattern": "^(.+?)\\sS(\\d+)E(\\d+)(?:[\\s\\.\\-]+(.+?))?$",
      "secondaryPattern": "^(.+?)\\s(\\d+)x(\\d+)(?:[\\s\\.\\-]+(.+?))?$",
      "tertiaryPattern": "^(.+?)\\.S(\\d+)\\.E(\\d+)(?:\\.(.+?))?$"
    },
    "filenameTemplate": "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
  },
  "errors": []
}
```








**Error Cases**:

```json
{
  "isValid": false,
  "configuration": null,
  "errors": [
    "Configuration file not found at path: /path/to/config.json",
    "Invalid threshold values: renameConfidenceThreshold must be >= matchConfidenceThreshold"
  ]
}
```








### ReloadIfChanged()








**Signature**: `Task<bool> ReloadIfChanged()`

**Returns**: Boolean indicating if configuration was reloaded

**Behavior**:

- Check file modification time
- Return `false` if no changes detected
- Return `true` if successfully reloaded
- Throw `ConfigurationException` if reload fails

### ValidateConfiguration()








**Signature**: `ValidationResult ValidateConfiguration(Configuration config)`

**Returns**:

```json
{
  "isValid": true,
  "errors": []
}
```








**Validation Errors**:

```json
{
  "isValid": false,
  "errors": [
    "Version must be a valid semantic version",
    "MatchConfidenceThreshold must be between 0.0 and 1.0",
    "FuzzyHashThreshold required when HashingAlgorithm is CTPH"
  ]
}
```







