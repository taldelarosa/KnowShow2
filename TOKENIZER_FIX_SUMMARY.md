# Tokenizer and Configuration Fix Summary

## Issues Identified

### 1. Tokenizer Loading Error
**Error:** `The special token '[UNK]' is not in the vocabulary`

**Root Cause:** The code was attempting to load `tokenizer.json` (Hugging Face format) using `BertTokenizer.CreateAsync()`, which expects a `vocab.txt` file in BERT/WordPiece format.

**Fix:**
- Updated `EmbeddingModelConfiguration.TokenizerUrl` to download `vocab.txt` instead of `tokenizer.json`
  ```csharp
  // Before:
  public string TokenizerUrl { get; set; } = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json";
  
  // After:
  public string TokenizerUrl { get; set; } = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";
  ```

- Updated `EmbeddingService.EnsureModelInitializedAsync()` to properly use `BertTokenizer.CreateAsync()`:
  ```csharp
  // all-MiniLM-L6-v2 uses WordPiece tokenization - load from vocab.txt
  _tokenizer = await BertTokenizer.CreateAsync(modelInfo.TokenizerPath);
  ```

### 2. Configuration Validation Error
**Error:** `FuzzyHashSimilarity must be greater than 0` (validation failed for TextBased, PGS, and VobSub)

**Root Cause:** The `SubtitleTypeThresholds` class initialized all properties with default values (0), which failed validation.

**Fix:** Added proper default values to all threshold properties:

```csharp
public class SubtitleTypeThresholds
{
    [Range(0.0, 1.0)]
    public decimal MatchConfidence { get; set; } = 0.7m;  // Was: 0
    
    [Range(0.0, 1.0)]
    public decimal RenameConfidence { get; set; } = 0.8m;  // Was: 0
    
    [Range(0, 100)]
    public int FuzzyHashSimilarity { get; set; } = 70;  // Was: 0
}

public class MatchingThresholds
{
    // Text-based (highest accuracy)
    public SubtitleTypeThresholds TextBased { get; set; } = new() 
    {
        MatchConfidence = 0.7m,
        RenameConfidence = 0.8m,
        FuzzyHashSimilarity = 70
    };

    // PGS (medium accuracy, requires OCR)
    public SubtitleTypeThresholds PGS { get; set; } = new()
    {
        MatchConfidence = 0.6m,
        RenameConfidence = 0.7m,
        FuzzyHashSimilarity = 60
    };

    // VobSub (lower accuracy, requires OCR)
    public SubtitleTypeThresholds VobSub { get; set; } = new()
    {
        MatchConfidence = 0.5m,
        RenameConfidence = 0.6m,
        FuzzyHashSimilarity = 50
    };
}
```

## Files Changed

1. **src/EpisodeIdentifier.Core/Models/Configuration/EmbeddingModelConfiguration.cs**
   - Changed TokenizerUrl from `tokenizer.json` to `vocab.txt`

2. **src/EpisodeIdentifier.Core/Services/EmbeddingService.cs**
   - Updated tokenizer loading to use `BertTokenizer.CreateAsync()` with vocab.txt
   - Changed `EnsureModelInitialized()` to `EnsureModelInitializedAsync()` for proper async handling

3. **src/EpisodeIdentifier.Core/Models/Configuration/MatchingThresholds.cs**
   - Added default values to `SubtitleTypeThresholds` properties
   - Added proper initialization with type-specific defaults for `MatchingThresholds`

## Testing

Build completed successfully with no errors:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:22.44
```

## Expected Behavior

After these fixes:

1. **Model Download:** On first run, the system will now download:
   - `model.onnx` (~86MB) - ONNX model file
   - `vocab.txt` (~226KB) - BERT vocabulary file for WordPiece tokenization

2. **Configuration:** Auto-generated configuration files will now have valid default values:
   ```json
   "matchingThresholds": {
     "textBased": {
       "matchConfidence": 0.7,
       "renameConfidence": 0.8,
       "fuzzyHashSimilarity": 70
     },
     "pgs": {
       "matchConfidence": 0.6,
       "renameConfidence": 0.7,
       "fuzzyHashSimilarity": 60
     },
     "vobSub": {
       "matchConfidence": 0.5,
       "renameConfidence": 0.6,
       "fuzzyHashSimilarity": 50
     }
   }
   ```

3. **Embedding Generation:** The embedding service will successfully:
   - Initialize the ONNX Runtime session
   - Load the BERT tokenizer from vocab.txt
   - Generate 384-dimensional embeddings for subtitle content

## Next Steps

1. Delete cached model files to force re-download with correct tokenizer:
   ```bash
   rm -rf ~/.episodeidentifier/models/all-MiniLM-L6-v2/
   ```

2. Delete the invalid configuration file:
   ```bash
   rm episodeidentifier.config.json
   ```

3. Rebuild Docker image with these fixes:
   ```bash
   docker build -t knowshow-episodeidentifier:latest .
   ```

4. Re-run the bulk store command:
   ```bash
   ./EpisodeIdentifier.Core --bulk-store /data/subtitles/Criminal\ Minds/ --series "Criminal Minds"
   ```

## Technical Notes

- The all-MiniLM-L6-v2 model uses WordPiece tokenization (BERT-style), not the newer Hugging Face tokenizer format
- The `vocab.txt` file contains 30,522 tokens including special tokens: `[PAD]`, `[UNK]`, `[CLS]`, `[SEP]`, `[MASK]`
- Microsoft.ML.Tokenizers provides `BertTokenizer` specifically for WordPiece vocabularies
- The `tokenizer.json` format would require a different tokenizer loader (not currently supported by Microsoft.ML.Tokenizers 0.22.0)
