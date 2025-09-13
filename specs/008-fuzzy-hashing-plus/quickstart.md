# Quickstart: Fuzzy Hashing Plus Configuration System

## Prerequisites

- .NET 8.0 SDK installed
- Episode files for testing
- Valid episode identification database

## Test Scenario: Configure and Test Fuzzy Hashing

### Step 1: Create Configuration File

Create `episodeidentifier.config.json`:

```json
{
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
}
```

### Step 2: Validate Configuration

```bash
dotnet run -- --config-validate
```

**Expected Output**:
```
✓ Configuration loaded successfully
✓ All threshold values are valid
✓ Filename patterns compiled successfully  
✓ CTPH hashing library available
Configuration validation: PASSED
```

### Step 3: Test Fuzzy Hash Comparison

```bash
dotnet run -- --hash-test file1.mkv file2.mkv
```

**Expected Output**:
```
Computing CTPH hashes...
File1 hash: 3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C
File2 hash: 3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C
Similarity score: 85
Threshold: 75
Result: MATCH (similarity exceeds threshold)
Comparison time: 23ms
```

### Step 4: Test Configuration Hot-Reloading

1. **Start file processing**:
   ```bash
   dotnet run -- --process /path/to/episodes/
   ```

2. **In another terminal, modify config**:
   ```bash
   # Change fuzzyHashThreshold from 75 to 80
   sed -i 's/"fuzzyHashThreshold": 75/"fuzzyHashThreshold": 80/' episodeidentifier.config.json
   ```

3. **Verify reload in processing output**:
   ```
   [12:34:56] Processing: Episode.S01E01.mkv
   [12:34:57] Config file changed - reloading...
   [12:34:57] ✓ Configuration reloaded (new threshold: 80)
   [12:34:57] Processing: Episode.S01E02.mkv (using new config)
   ```

### Step 5: Test Backward Compatibility

```bash
# Test with legacy config (no fuzzy hash settings)
cp episodeidentifier.config.json episodeidentifier.config.legacy.json
dotnet run -- --config-file episodeidentifier.config.legacy.json --process /path/to/episodes/
```

**Expected Output**:
```
⚠ Legacy configuration detected (version not specified)
✓ Using MD5/SHA1 hashing (backward compatibility mode)
✓ Processing files with legacy algorithm
```

## Success Criteria

- [ ] Configuration loads without errors
- [ ] CTPH hashing produces similarity scores
- [ ] Hot-reloading detects config changes within one file processing cycle
- [ ] Legacy configurations continue to work
- [ ] All validation errors provide clear, actionable messages

## Troubleshooting

### "CTPH library not found"
- Ensure ssdeep-dotnet package is installed
- Check .NET 8.0 compatibility

### "Configuration validation failed"  
- Verify JSON syntax
- Check threshold ranges (0.0-1.0 for confidence, 0-100 for fuzzy hash)
- Ensure required fields are present

### "Config reload not detected"
- Verify file modification timestamp changes
- Check file permissions
- Ensure FileSystemWatcher has access to config directory