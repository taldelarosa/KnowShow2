# CTPH Hashing Service Contract

## Interface: ICTPhHashingService

### ComputeFuzzyHash()

**Signature**: `Task<string> ComputeFuzzyHash(string filePath)`

**Returns**: CTPH hash string (e.g., "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C")

**Error Cases**:
- `FileNotFoundException`: File does not exist
- `UnauthorizedAccessException`: Cannot read file
- `HashingException`: CTPH computation failed

### CompareFuzzyHashes()

**Signature**: `int CompareFuzzyHashes(string hash1, string hash2)`

**Returns**: Similarity score (0-100)
- 0: Completely different
- 100: Identical files
- 75+: High similarity (typical match threshold)

**Error Cases**:
- `ArgumentException`: Invalid hash format
- `HashingException`: Comparison failed

### CompareFiles()

**Signature**: `Task<FuzzyHashResult> CompareFiles(string file1Path, string file2Path)`

**Returns**:
```json
{
  "hash1": "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C",
  "hash2": "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C",
  "similarityScore": 85,
  "isMatch": true,
  "comparisonTime": "00:00:00.0234567"
}
```

### GetSimilarityThreshold()

**Signature**: `int GetSimilarityThreshold()`

**Returns**: Currently configured threshold (from configuration)

**Behavior**: Reads from IConfigurationService.GetCurrentConfiguration().FuzzyHashThreshold