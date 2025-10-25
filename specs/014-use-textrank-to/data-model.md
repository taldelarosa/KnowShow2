# Phase 1: Data Model - TextRank-Based Semantic Subtitle Matching

**Feature**: TextRank sentence extraction for plot-relevant subtitle matching
**Date**: 2025-10-24
**Status**: Complete

## Entity Definitions

### 1. TextRankExtractionResult

**Purpose**: Encapsulates the result of TextRank sentence extraction with statistics for logging and debugging.

**Properties**:

| Property | Type | Description | Validation |
|----------|------|-------------|------------|
| FilteredText | string | Concatenated selected sentences in original document order | Not null |
| TotalSentenceCount | int | Total number of sentences in original text | >= 0 |
| SelectedSentenceCount | int | Number of sentences selected by TextRank | >= 0, <= TotalSentenceCount |
| AverageScore | double | Mean TextRank score of selected sentences | 0.0 - 1.0 |
| SelectionPercentage | double | Actual percentage selected (may differ from target due to rounding) | 0.0 - 100.0 |
| FallbackTriggered | bool | Whether fallback to full-text was used | - |
| FallbackReason | string? | Reason for fallback (if applicable) | null or non-empty |
| ProcessingTimeMs | long | Time taken for TextRank extraction | >= 0 |

**Relationships**: None (value object returned by ITextRankService)

**Example**:

```csharp
var result = new TextRankExtractionResult
{
    FilteredText = "Detective finds clue. Suspect confesses. Case solved.",
    TotalSentenceCount = 500,
    SelectedSentenceCount = 125,
    AverageScore = 0.73,
    SelectionPercentage = 25.0,
    FallbackTriggered = false,
    FallbackReason = null,
    ProcessingTimeMs = 342
};
```

### 2. SentenceScore

**Purpose**: Represents a single sentence with its calculated TextRank importance score for debugging and analysis.

**Properties**:

| Property | Type | Description | Validation |
|----------|------|-------------|------------|
| Text | string | Sentence content | Not null, not empty |
| Score | double | TextRank importance score | 0.0 - 1.0 (normalized) |
| OriginalIndex | int | Position in original document (0-based) | >= 0 |
| WordCount | int | Number of words in sentence | >= 0 |
| IsSelected | bool | Whether sentence was selected for embedding | - |

**Relationships**: Part of TextRankExtractionResult (collection of top sentences)

**Example**:

```csharp
var sentence = new SentenceScore
{
    Text = "The detective discovered a crucial piece of evidence.",
    Score = 0.87,
    OriginalIndex = 42,
    WordCount = 9,
    IsSelected = true
};
```

### 3. TextRankConfiguration

**Purpose**: Configuration settings for TextRank sentence extraction behavior (part of AppConfig).

**Properties**:

| Property | Type | Description | Validation | Default |
|----------|------|-------------|------------|---------|
| Enabled | bool | Enable/disable TextRank filtering | - | false |
| SentencePercentage | int | Percentage of sentences to select | 10-50 (inclusive) | 25 |
| MinSentences | int | Absolute minimum sentences to accept | 5-100 | 15 |
| MinPercentage | int | Minimum percentage of original to accept | 5-50, < SentencePercentage | 10 |
| DampingFactor | double | PageRank damping factor | 0.5-0.95 | 0.85 |
| ConvergenceThreshold | double | Convergence epsilon for PageRank | 0.00001-0.01 | 0.0001 |
| MaxIterations | int | Maximum PageRank iterations | 10-500 | 100 |
| SimilarityThreshold | double | Minimum sentence similarity for edge creation | 0.0-0.5 | 0.1 |

**Relationships**: Nested within AppConfig, loaded by IConfigurationService

**Example**:

```json
{
  "textRankFiltering": {
    "enabled": true,
    "sentencePercentage": 25,
    "minSentences": 15,
    "minPercentage": 10,
    "dampingFactor": 0.85,
    "convergenceThreshold": 0.0001,
    "maxIterations": 100,
    "similarityThreshold": 0.1
  }
}
```

### 4. SentenceGraph (Internal)

**Purpose**: Internal representation of sentence similarity graph for PageRank calculation (not exposed in API).

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| Sentences | List\<string\> | List of all sentences |
| AdjacencyMatrix | Dictionary<int, Dictionary<int, double>> | Sparse matrix of similarity scores |
| Scores | double[] | Current TextRank scores for each sentence |

**Note**: This is an internal implementation detail of TextRankService and not exposed via contracts.

## Database Schema Changes

**No schema changes required** - TextRank operates as a preprocessing step before embedding generation. The filtered text is embedded and stored in the existing `Embedding` BLOB column from Feature 013.

**Rationale**:

- Backward compatible: existing full-text embeddings remain valid
- Lazy migration: embeddings regenerate on-demand when TextRank is enabled
- Storage efficient: no need to store both full-text and filtered embeddings
- Transparent: matching logic unchanged (uses cosine similarity on whatever embedding exists)

## Configuration Schema Extensions

### AppConfig Extension

```csharp
public class AppConfig
{
    // ... existing properties ...
    
    public TextRankConfiguration? TextRankFiltering { get; set; }
}

public class TextRankConfiguration
{
    public bool Enabled { get; set; } = false;
    public int SentencePercentage { get; set; } = 25;
    public int MinSentences { get; set; } = 15;
    public int MinPercentage { get; set; } = 10;
    public double DampingFactor { get; set; } = 0.85;
    public double ConvergenceThreshold { get; set; } = 0.0001;
    public int MaxIterations { get; set; } = 100;
    public double SimilarityThreshold { get; set; } = 0.1;
    
    public void Validate()
    {
        if (SentencePercentage < 10 || SentencePercentage > 50)
            throw new ArgumentOutOfRangeException(nameof(SentencePercentage), 
                "Must be between 10 and 50");
        
        if (MinSentences < 5 || MinSentences > 100)
            throw new ArgumentOutOfRangeException(nameof(MinSentences), 
                "Must be between 5 and 100");
        
        if (MinPercentage < 5 || MinPercentage > 50 || MinPercentage >= SentencePercentage)
            throw new ArgumentOutOfRangeException(nameof(MinPercentage), 
                "Must be between 5 and 50, and less than SentencePercentage");
        
        if (DampingFactor < 0.5 || DampingFactor > 0.95)
            throw new ArgumentOutOfRangeException(nameof(DampingFactor), 
                "Must be between 0.5 and 0.95");
    }
}
```

### JSON Configuration Example

```json
{
  "databasePath": "./hashes.db",
  "logLevel": "Information",
  "matchingStrategy": "embedding",
  "embeddingThresholds": {
    "textBased": {
      "embedSimilarity": 0.85,
      "matchConfidence": 0.70,
      "renameConfidence": 0.80
    },
    "pgs": {
      "embedSimilarity": 0.80,
      "matchConfidence": 0.60,
      "renameConfidence": 0.70
    },
    "vobSub": {
      "embedSimilarity": 0.75,
      "matchConfidence": 0.50,
      "renameConfidence": 0.60
    }
  },
  "textRankFiltering": {
    "enabled": true,
    "sentencePercentage": 25,
    "minSentences": 15,
    "minPercentage": 10,
    "dampingFactor": 0.85,
    "convergenceThreshold": 0.0001,
    "maxIterations": 100,
    "similarityThreshold": 0.1
  }
}
```

## Data Flow

### TextRank Processing Pipeline

```
1. SubtitleExtractor extracts subtitle text → CleanText
2. IF TextRankConfiguration.Enabled:
   a. TextRankService.ExtractPlotRelevantSentences(CleanText)
      → Segment sentences
      → Build similarity graph
      → Calculate PageRank scores
      → Select top N% sentences
      → Check fallback conditions
      → Return TextRankExtractionResult
   b. IF FallbackTriggered:
      → Log warning + reason
      → Use original CleanText
   c. ELSE:
      → Use FilteredText from result
      → Log extraction statistics
3. EmbeddingService.GenerateEmbeddingAsync(text) → 384-dim vector
4. VectorSearchService.SearchSimilarEmbeddingsAsync(vector) → matches
5. Return IdentificationResult with match confidence
```

### Fallback Decision Logic

```csharp
public bool ShouldFallbackToFullText(
    int selectedCount, 
    int totalCount, 
    TextRankConfiguration config)
{
    // Absolute minimum
    if (selectedCount < config.MinSentences)
        return true;
    
    // Relative minimum
    double actualPercentage = (double)selectedCount / totalCount * 100;
    if (actualPercentage < config.MinPercentage)
        return true;
    
    // Text too short (< 100 characters)
    if (filteredText.Length < 100)
        return true;
    
    return false;
}
```

## Logging & Observability

### Structured Logging Events

```csharp
// Successful extraction
logger.LogInformation(
    "TextRank extraction: {SelectedCount}/{TotalCount} sentences " +
    "({Percentage}%), avg score {AvgScore:F3}, {ProcessingTimeMs}ms",
    result.SelectedSentenceCount,
    result.TotalSentenceCount,
    result.SelectionPercentage,
    result.AverageScore,
    result.ProcessingTimeMs);

// Fallback triggered
logger.LogWarning(
    "TextRank fallback triggered: {Reason}. " +
    "Using full text ({TotalCount} sentences)",
    result.FallbackReason,
    result.TotalSentenceCount);

// Performance warning
if (result.ProcessingTimeMs > 1000)
{
    logger.LogWarning(
        "TextRank processing slow: {ProcessingTimeMs}ms for {SentenceCount} sentences",
        result.ProcessingTimeMs,
        result.TotalSentenceCount);
}
```

## Validation Rules Summary

| Rule | Validation | Error Message |
|------|------------|---------------|
| SentencePercentage range | 10 <= x <= 50 | "SentencePercentage must be between 10 and 50" |
| MinSentences range | 5 <= x <= 100 | "MinSentences must be between 5 and 100" |
| MinPercentage range | 5 <= x <= 50 | "MinPercentage must be between 5 and 50" |
| MinPercentage < SentencePercentage | MinPercentage < SentencePercentage | "MinPercentage must be less than SentencePercentage" |
| DampingFactor range | 0.5 <= x <= 0.95 | "DampingFactor must be between 0.5 and 0.95" |
| ConvergenceThreshold range | 0.00001 <= x <= 0.01 | "ConvergenceThreshold must be between 0.00001 and 0.01" |
| MaxIterations range | 10 <= x <= 500 | "MaxIterations must be between 10 and 500" |

## Next Steps

Proceed to contract generation for ITextRankService and related services.
