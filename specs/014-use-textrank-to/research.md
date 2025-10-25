# Phase 0: Research - TextRank-Based Semantic Subtitle Matching

**Feature**: TextRank-based sentence extraction for plot-relevant subtitle matching
**Date**: 2025-10-24
**Status**: Complete

## Research Questions & Decisions

### 1. TextRank Algorithm Implementation

**Decision**: Implement PageRank-based graph ranking adapted for sentence extraction

**Rationale**:
- TextRank is a well-established unsupervised algorithm for extractive summarization
- Based on PageRank: nodes are sentences, edges are similarity weights
- No training data required - works on any subtitle content
- Proven effective for identifying salient sentences in documents

**Implementation Approach**:
1. Parse subtitle text into individual sentences (sentence segmentation)
2. Build similarity graph: each sentence is a node
3. Calculate edge weights using cosine similarity of word vectors (bag-of-words representation)
4. Apply iterative PageRank with damping factor (d=0.85 standard)
5. Rank sentences by final TextRank score
6. Select top N% by score for embedding generation

**Alternatives Considered**:
- **LexRank**: Very similar to TextRank but uses TF-IDF; more complex, similar results
- **LSA (Latent Semantic Analysis)**: Requires SVD decomposition; computationally expensive
- **Simple TF-IDF ranking**: Doesn't capture sentence relationships; inferior quality
- **Embedding-based clustering**: Would require generating embeddings for ALL sentences first (defeats purpose of filtering)

**References**:
- Mihalcea & Tarau (2004): TextRank: Bringing Order into Texts
- PageRank algorithm: iterative convergence until delta < threshold

### 2. Sentence Segmentation

**Decision**: Use regex-based segmentation with subtitle-specific preprocessing

**Rationale**:
- Subtitles have simpler structure than prose (shorter sentences, less complex punctuation)
- Regex sufficient for subtitle domain (no need for NLP libraries like Stanford CoreNLP)
- Zero additional dependencies (.NET built-in Regex)
- Fast performance (<1ms for typical subtitle file)

**Implementation Details**:
```csharp
// Sentence boundary pattern
var pattern = @"(?<=[.!?])\s+(?=[A-Z])|(?<=\n\n)(?=\S)";

// Preprocessing steps:
1. Remove subtitle timestamps ([00:01:23,456])
2. Remove speaker tags (<i>, [NARRATOR])
3. Normalize whitespace (multiple spaces → single space)
4. Split on sentence boundaries
5. Filter out very short sentences (<5 words)
```

**Edge Cases Handled**:
- Abbreviations (Mr., Dr., etc.) - don't split
- Ellipsis (...) - preserve as single sentence
- Quoted dialogue - keep quotes with sentence
- All caps text - normalize for comparison

**Alternatives Considered**:
- **NLP Library (Stanford CoreNLP, spaCy)**: Overkill for subtitles; adds large dependency
- **ML-based segmentation**: Requires training data; unnecessary complexity
- **Split on periods only**: Too naive; breaks on abbreviations

### 3. Sentence Similarity Calculation

**Decision**: Cosine similarity on bag-of-words representation with stopword filtering

**Rationale**:
- Simple and fast (no embeddings needed for graph construction)
- Captures lexical overlap between sentences
- Stopword removal focuses on content words
- Sufficient for identifying redundant/related sentences

**Implementation**:
```csharp
// Bag-of-words similarity
1. Tokenize sentences (split on whitespace, lowercase)
2. Remove stopwords (common words: the, a, is, etc.)
3. Build vocabulary (unique words across all sentences)
4. Create word frequency vectors for each sentence
5. Calculate cosine similarity: dot(v1, v2) / (||v1|| * ||v2||)
```

**Threshold for Edge Creation**:
- Only create edges for sentence pairs with similarity > 0.1
- Sparse graph reduces computation in PageRank iteration
- Low-similarity sentences don't influence each other's scores

**Alternatives Considered**:
- **Embedding-based similarity**: Defeats purpose (requires embedding all sentences upfront)
- **Jaccard similarity**: Less accurate than cosine for sentence comparison
- **Edit distance**: Too slow for O(n²) comparisons in large files

### 4. TextRank Convergence & Parameters

**Decision**: Iterative PageRank with damping factor d=0.85, convergence threshold ε=0.0001

**Rationale**:
- Standard PageRank parameters proven effective in TextRank literature
- Damping factor 0.85 balances local vs. global sentence importance
- Convergence typically achieved in 20-30 iterations (<100ms)

**Algorithm**:
```
Initialize: score(Si) = 1.0 for all sentences Si
Repeat until convergence (max 100 iterations):
  For each sentence Si:
    score_new(Si) = (1-d) + d * Σ[similarity(Si, Sj) * score(Sj) / Σ similarity(Sj, Sk)]
                                   j∈neighbors(Si)
  If max|score_new - score| < ε: CONVERGED
  Update all scores
```

**Performance Optimization**:
- Sparse matrix representation (only store non-zero similarities)
- Early stopping (most graphs converge in <30 iterations)
- Cache normalized similarity sums

**Alternatives Considered**:
- **Fixed iteration count**: Less precise; may under/over-iterate
- **Different damping factors (0.5, 0.9)**: 0.85 is standard and well-tested
- **Single-iteration approximation**: Poor quality results

### 5. Sentence Selection Strategy

**Decision**: Select top 25% of sentences by TextRank score (configurable 10-50%)

**Rationale**:
- 25% balances information retention with noise reduction
- Captures key plot points without excessive filler
- Research shows 20-30% extraction ratio optimal for summarization
- User-configurable for tuning per use case

**Selection Process**:
1. Sort sentences by TextRank score (descending)
2. Select top N% based on configuration
3. Reconstruct in original document order (preserve narrative flow)
4. Concatenate selected sentences for embedding generation

**Fallback Conditions** (use full text):
- Selected sentences < 15 (absolute minimum)
- Selected sentences < 10% of original count (relative minimum)
- Total selected text < 100 characters (too short)

**Alternatives Considered**:
- **Fixed sentence count (e.g., top 50 sentences)**: Doesn't scale with file size
- **Score threshold (e.g., score > 0.5)**: Variable extraction ratio; unpredictable
- **Cluster-based selection**: More complex; similar results

### 6. Integration with Feature 013 Embedding Pipeline

**Decision**: Insert TextRank extraction as preprocessing step before embedding generation

**Rationale**:
- Minimal changes to existing embedding workflow
- Reuses existing EmbeddingService, VectorSearchService, database schema
- No migration required (embeddings stored in same Embedding column)
- Transparent to downstream matching logic

**Integration Point**:
```csharp
// In EpisodeIdentificationService.IdentifyEpisodeAsync():
string textForEmbedding = subtitleText;

if (config.EnableTextRankFiltering) {
    var extracted = textRankService.ExtractPlotRelevantSentences(
        subtitleText, 
        config.TextRankSentencePercentage);
    
    if (extracted.SentenceCount >= minThreshold) {
        textForEmbedding = extracted.FilteredText;
        logger.LogInformation($"TextRank: {extracted.SelectedCount}/{extracted.TotalCount} sentences");
    } else {
        logger.LogWarning("TextRank yielded insufficient sentences, using full text");
    }
}

// Continue with existing embedding generation
var embedding = await embeddingService.GenerateEmbeddingAsync(textForEmbedding);
```

**Configuration**:
```json
{
  "enableTextRankFiltering": true,
  "textRankSentencePercentage": 25,
  "textRankMinSentences": 15,
  "textRankMinPercentage": 10
}
```

### 7. Configuration Schema Extensions

**Decision**: Add TextRank settings to existing episodeidentifier.config.json

**New Configuration Properties**:
```json
{
  "textRankFiltering": {
    "enabled": true,
    "sentencePercentage": 25,
    "minSentences": 15,
    "minPercentage": 10,
    "dampingFactor": 0.85,
    "convergenceThreshold": 0.0001,
    "maxIterations": 100
  }
}
```

**Validation Rules**:
- sentencePercentage: 10-50 (inclusive)
- minSentences: 5-100 (must be reasonable)
- minPercentage: 5-50 (must be < sentencePercentage)
- dampingFactor: 0.5-0.95 (PageRank standard range)

**Hot-Reload Support**:
- Reuses existing IConfigurationService watch mechanism
- Changes apply to next subtitle processing operation
- No restart required

### 8. Performance Targets & Validation

**Performance Goals**:
- Sentence segmentation: <10ms for 500-line subtitle
- Graph construction: <200ms (O(n²) similarity calculations)
- PageRank iteration: <100ms (converges in ~30 iterations)
- Total TextRank overhead: <500ms per subtitle file
- Memory overhead: <10MB for 1000-sentence file

**Validation Approach**:
- Stopwatch instrumentation for each phase
- Log performance metrics at INFO level
- Integration test with large subtitle file (2000+ lines)
- Memory profiler to detect leaks in graph structures

**Optimization Strategies if Needed**:
- Parallel similarity calculations (use PLINQ)
- Sentence caching (reuse embeddings for repeated processing)
- Sparse matrix optimizations (only non-zero edges)

## Implementation Dependencies

### New Services Required

1. **ITextRankService** (interface)
   - `ExtractPlotRelevantSentences(string text, int percentage)` → ExtractionResult
   - `CalculateTextRankScores(List<Sentence> sentences)` → Dictionary<Sentence, double>

2. **TextRankService** (implementation)
   - Sentence segmentation logic
   - Similarity graph construction
   - PageRank iteration
   - Sentence selection and ordering

3. **SentenceSegmenter** (helper class)
   - Regex-based sentence boundary detection
   - Subtitle preprocessing (timestamps, tags)
   - Edge case handling

### New Models Required

1. **TextRankExtractionResult**
   - `string FilteredText` - concatenated selected sentences
   - `int TotalSentenceCount` - original count
   - `int SelectedSentenceCount` - filtered count
   - `double AverageScore` - mean TextRank score
   - `List<SentenceScore> TopSentences` - for debugging

2. **SentenceScore**
   - `string Text` - sentence content
   - `double Score` - TextRank score
   - `int OriginalIndex` - position in document

3. **TextRankConfiguration** (part of AppConfig)
   - Properties as defined in section 7

### Dependencies on Existing Code

- **EpisodeIdentificationService**: Integrate TextRank preprocessing
- **IConfigurationService**: Extend with TextRank config
- **IEmbeddingService**: Unchanged (receives filtered text)
- **IVectorSearchService**: Unchanged (uses filtered embeddings)
- **Database schema**: Unchanged (reuses Embedding column)

## Testing Strategy

### Contract Tests (RED phase)

1. **TextRankServiceContractTests**
   - Extract sentences from known subtitle sample
   - Verify top-ranked sentences contain key plot points
   - Validate fallback when insufficient sentences
   - Confirm sentence count matches percentage

### Integration Tests

1. **TextRankIntegrationTests**
   - Process verbose subtitle file (300 filler + 200 plot)
   - Verify filtered embedding differs from full-text embedding
   - Confirm match confidence improves for verbose files
   - Test fallback to full-text for very short files

2. **ConfigurationIntegrationTests**
   - Toggle enableTextRankFiltering on/off
   - Modify sentencePercentage and verify selection
   - Test hot-reload of TextRank configuration

### Unit Tests

1. **SentenceSegmenterTests**
   - Split sentences correctly
   - Handle abbreviations without splitting
   - Preserve sentence order
   - Remove timestamps and tags

2. **TextRankAlgorithmTests**
   - Calculate similarity scores correctly
   - PageRank converges within max iterations
   - Score normalization (sum to 1.0)
   - Handle single-sentence edge case

## Success Criteria

1. **Accuracy Improvement**: Verbose subtitle matching confidence increases by 10-20%
2. **Performance**: TextRank adds <500ms overhead per subtitle file
3. **Backward Compatibility**: All existing tests pass with filtering disabled
4. **Fallback Reliability**: System never fails due to TextRank (graceful fallback to full-text)
5. **Configuration Flexibility**: Users can tune extraction ratio without code changes

## Open Questions / Risks

1. **Question**: Will 25% sentence retention be sufficient for all content types?
   - **Mitigation**: Make configurable (10-50%), log statistics for tuning

2. **Question**: How to handle multilingual subtitles (non-English)?
   - **Mitigation**: Bag-of-words approach is language-agnostic (no stopwords in v1)

3. **Risk**: Very short subtitles (<50 sentences) may not benefit from TextRank
   - **Mitigation**: Fallback thresholds (minSentences, minPercentage)

4. **Risk**: Graph construction O(n²) complexity for very large files (10,000+ sentences)
   - **Mitigation**: Rare case (most TV episodes <1000 sentences); optimize if needed

## Next Steps

Proceed to Phase 1: Design contracts, data models, and quickstart validation scenario.
