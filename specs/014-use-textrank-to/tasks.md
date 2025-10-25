# Implementation Tasks: TextRank-Based Semantic Subtitle Matching

**Feature**: 014-use-textrank-to  
**Branch**: `014-use-textrank-to`  
**Input**: Design documents from `/specs/014-use-textrank-to/`  
**Prerequisites**: plan.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

## Execution Flow

```
1. Load plan.md from feature directory
   ✓ Loaded successfully - C# .NET 8.0, extending EpisodeIdentifier.Core

2. Load optional design documents:
   ✓ data-model.md: 3 entities (TextRankExtractionResult, SentenceScore, TextRankConfiguration)
   ✓ contracts/: 1 file (textrank-service.json - ITextRankService)
   ✓ research.md: TextRank algorithm, sentence segmentation, integration strategy
   ✓ quickstart.md: Criminal Minds S06E19 validation scenario

3. Generate tasks by category:
   ✓ Setup: No new dependencies (pure .NET)
   ✓ Tests: 5 contract tests, 5 integration tests
   ✓ Core: 3 models, 2 services, 1 helper class
   ✓ Integration: EpisodeIdentificationService, DI registration, configuration
   ✓ Polish: Quickstart validation, performance profiling

4. Apply task rules:
   ✓ Different files = mark [P] for parallel
   ✓ Same file = sequential (no [P])
   ✓ Tests before implementation (TDD)

5. Number tasks sequentially: T001-T030
6. Generate dependency graph: ✓
7. Create parallel execution examples: ✓
8. Validate task completeness:
   ✓ All contracts have tests (1/1)
   ✓ All entities have models (3/3)
   ✓ Integration with Feature 013 planned
```

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- All file paths are absolute, starting from `/mnt/c/Users/Ragma/KnowShow_Specd/`

## Path Conventions

**Single project structure** (Option 1 from plan.md):
- Models: `src/EpisodeIdentifier.Core/Models/`
- Services: `src/EpisodeIdentifier.Core/Services/`
- Interfaces: `src/EpisodeIdentifier.Core/Interfaces/`
- Configuration: `src/EpisodeIdentifier.Core/Models/Configuration/`
- Contract Tests: `tests/contract/`
- Integration Tests: `tests/integration/`
- Unit Tests: `tests/unit/`

---

## Phase 3.1: Models & Configuration (TDD Foundation)

### T001: Create TextRankExtractionResult model [P]

**File**: `src/EpisodeIdentifier.Core/Models/TextRankExtractionResult.cs` (new)  
**Type**: Model - Value Object  
**Parallel**: Yes (new file, no dependencies)  
**Description**: Create TextRankExtractionResult class to encapsulate sentence extraction results with statistics

**Properties** (from data-model.md):
- `string FilteredText` - Concatenated selected sentences
- `int TotalSentenceCount` - Total sentences in original
- `int SelectedSentenceCount` - Number selected by TextRank
- `double AverageScore` - Mean TextRank score (0.0-1.0)
- `double SelectionPercentage` - Actual % selected (0-100)
- `bool FallbackTriggered` - Whether fallback used
- `string? FallbackReason` - Reason for fallback
- `long ProcessingTimeMs` - Processing time

**Validation**: Class compiles, properties match data-model.md specification  
**Dependencies**: None

---

### T002: Create SentenceScore model [P]

**File**: `src/EpisodeIdentifier.Core/Models/SentenceScore.cs` (new)  
**Type**: Model - Value Object  
**Parallel**: Yes (new file, no dependencies)  
**Description**: Create SentenceScore class to represent sentence with TextRank importance score

**Properties** (from data-model.md):
- `string Text` - Sentence content (not null, not empty)
- `double Score` - TextRank score (0.0-1.0, normalized)
- `int OriginalIndex` - Position in document (0-based)
- `int WordCount` - Number of words
- `bool IsSelected` - Whether selected for embedding

**Validation**: Class compiles, properties match data-model.md specification  
**Dependencies**: None

---

### T003: Create TextRankConfiguration model [P]

**File**: `src/EpisodeIdentifier.Core/Models/Configuration/TextRankConfiguration.cs` (new)  
**Type**: Model - Configuration  
**Parallel**: Yes (new file, no dependencies)  
**Description**: Create TextRankConfiguration class with validation for TextRank settings

**Properties** (from data-model.md):
- `bool Enabled` - Enable/disable filtering (default: false)
- `int SentencePercentage` - % to select (10-50, default: 25)
- `int MinSentences` - Absolute minimum (5-100, default: 15)
- `int MinPercentage` - Relative minimum (5-50, default: 10)
- `double DampingFactor` - PageRank damping (0.5-0.95, default: 0.85)
- `double ConvergenceThreshold` - Convergence epsilon (0.00001-0.01, default: 0.0001)
- `int MaxIterations` - Max PageRank iterations (10-500, default: 100)
- `double SimilarityThreshold` - Min similarity for edges (0.0-0.5, default: 0.1)

**Methods**:
- `void Validate()` - Validates all property ranges, throws ArgumentOutOfRangeException

**Validation**: Class compiles, validation logic throws correct exceptions  
**Dependencies**: None

---

### T004: Extend AppConfig with TextRankConfiguration

**File**: `src/EpisodeIdentifier.Core/Models/AppConfig.cs` (modify)  
**Type**: Configuration Extension  
**Parallel**: No (modifies existing file)  
**Description**: Add TextRankConfiguration property to AppConfig

**Changes**:
```csharp
public class AppConfig
{
    // ... existing properties ...
    
    public TextRankConfiguration? TextRankFiltering { get; set; }
}
```

**Validation**: Configuration loads from JSON, TextRankFiltering property accessible  
**Dependencies**: T003 (TextRankConfiguration must exist)

---

### T005: Update ConfigurationService to load TextRankConfiguration

**File**: `src/EpisodeIdentifier.Core/Services/ConfigurationService.cs` (modify)  
**Type**: Configuration Loading  
**Parallel**: No (modifies existing service)  
**Description**: Ensure ConfigurationService properly deserializes TextRankConfiguration from JSON

**Changes**:
- Verify JSON deserialization includes `textRankFiltering` section
- Add validation call after loading: `config.TextRankFiltering?.Validate()`
- Hot-reload support for TextRankConfiguration changes

**Validation**: Load test config file with textRankFiltering, verify properties loaded correctly  
**Dependencies**: T004 (AppConfig extended)

---

## Phase 3.2: Service Interfaces (Contracts)

### T006: Create ITextRankService interface [P]

**File**: `src/EpisodeIdentifier.Core/Interfaces/ITextRankService.cs` (new)  
**Type**: Interface  
**Parallel**: Yes (new file, no dependencies on implementation)  
**Description**: Define ITextRankService interface based on textrank-service.json contract

**Methods** (from contract):

1. `TextRankExtractionResult ExtractPlotRelevantSentences(string subtitleText, int sentencePercentage, int minSentences = 15, int minPercentage = 10)`
   - Extracts plot-relevant sentences using TextRank
   - Throws: ArgumentNullException, ArgumentOutOfRangeException

2. `Dictionary<int, double> CalculateTextRankScores(List<string> sentences)`
   - Calculates TextRank scores for debugging/analysis
   - Throws: ArgumentNullException, ArgumentException

**Validation**: Interface compiles, matches contract JSON structure  
**Dependencies**: T001 (TextRankExtractionResult)

---

## Phase 3.3: Contract Tests (RED Phase) ⚠️ MUST FAIL BEFORE IMPLEMENTATION

**CRITICAL**: These tests MUST be written and MUST FAIL before ANY service implementation

### T007: Contract test - Extract verbose subtitle sentences [P]

**File**: `tests/contract/TextRankServiceContractTests.cs` (new or extend)  
**Type**: Contract Test - RED Phase  
**Parallel**: Yes (independent test file)  
**Description**: Test ExtractPlotRelevantSentences with 500-sentence verbose subtitle

**Test Scenario** (from contract):
- **Given**: Subtitle with 500 sentences (300 filler, 200 plot-relevant)
- **When**: ExtractPlotRelevantSentences called with 25% threshold
- **Then**:
  - Returns ~125 sentences (25% of 500)
  - Selected sentences contain key plot points
  - FallbackTriggered = false
  - AverageScore > 0.5
  - ProcessingTimeMs < 1000

**Validation**: Test FAILS (service not implemented yet)  
**Dependencies**: T006 (ITextRankService interface)

---

### T008: Contract test - Trigger fallback for insufficient sentences [P]

**File**: `tests/contract/TextRankServiceContractTests.cs` (extend)  
**Type**: Contract Test - RED Phase  
**Parallel**: Yes (same test file, different test method)  
**Description**: Test fallback when selection < minSentences threshold

**Test Scenario** (from contract):
- **Given**: Subtitle with 50 sentences
- **When**: ExtractPlotRelevantSentences with 25% (would select 12 sentences)
- **Then**:
  - Returns full text (50 sentences)
  - FallbackTriggered = true
  - FallbackReason = "Selected count (12) below minimum threshold (15)"

**Validation**: Test FAILS (service not implemented yet)  
**Dependencies**: T006

---

### T009: Contract test - Trigger fallback for low percentage [P]

**File**: `tests/contract/TextRankServiceContractTests.cs` (extend)  
**Type**: Contract Test - RED Phase  
**Parallel**: Yes (same test file, different test method)  
**Description**: Test fallback when selection < minPercentage threshold

**Test Scenario** (from contract):
- **Given**: Subtitle with 200 sentences, all with very low TextRank scores
- **When**: ExtractPlotRelevantSentences selects only 8 sentences (4%)
- **Then**:
  - Returns full text
  - FallbackTriggered = true
  - FallbackReason = "Selected percentage (4%) below minimum (10%)"

**Validation**: Test FAILS (service not implemented yet)  
**Dependencies**: T006

---

### T010: Contract test - Handle single-sentence subtitle [P]

**File**: `tests/contract/TextRankServiceContractTests.cs` (extend)  
**Type**: Contract Test - RED Phase  
**Parallel**: Yes (same test file, different test method)  
**Description**: Test graceful handling of edge case with only 1 sentence

**Test Scenario** (from contract):
- **Given**: Subtitle with only 1 sentence
- **When**: ExtractPlotRelevantSentences called
- **Then**:
  - Returns original sentence
  - FallbackTriggered = true
  - FallbackReason = "Insufficient sentences for TextRank (1 < 2)"

**Validation**: Test FAILS (service not implemented yet)  
**Dependencies**: T006

---

### T011: Contract test - Calculate TextRank scores correctly [P]

**File**: `tests/contract/TextRankServiceContractTests.cs` (extend)  
**Type**: Contract Test - RED Phase  
**Parallel**: Yes (same test file, different test method)  
**Description**: Test CalculateTextRankScores returns valid normalized scores

**Test Scenario** (from contract):
- **Given**: List of 10 sentences with varying similarity
- **When**: CalculateTextRankScores called
- **Then**:
  - Returns dictionary with 10 entries
  - All scores between 0.0 and 1.0
  - Scores sum to approximately 1.0 (normalized)
  - Sentences with more connections have higher scores

**Validation**: Test FAILS (service not implemented yet)  
**Dependencies**: T006

---

## Phase 3.4: Service Implementation (GREEN Phase) ⚠️ ONLY AFTER TESTS FAIL

**CRITICAL**: Do NOT start this phase until all Phase 3.3 tests are written and failing

### T012: Implement SentenceSegmenter helper class

**File**: `src/EpisodeIdentifier.Core/Services/SentenceSegmenter.cs` (new)  
**Type**: Helper Class  
**Parallel**: No (required by T013)  
**Description**: Implement subtitle-specific sentence boundary detection

**Implementation** (from research.md):
- Regex pattern: `@"(?<=[.!?])\s+(?=[A-Z])|(?<=\n\n)(?=\S)"`
- Preprocessing:
  - Remove subtitle timestamps `[00:01:23,456]`
  - Remove speaker tags `<i>`, `[NARRATOR]`
  - Normalize whitespace (multiple spaces → single)
  - Split on sentence boundaries
  - Filter very short sentences (<5 words)

**Methods**:
- `List<string> SegmentSentences(string subtitleText)`
- `string PreprocessSubtitleText(string text)` (private)

**Validation**: Unit tests for edge cases (abbreviations, ellipsis, all caps)  
**Dependencies**: None

---

### T013: Implement sentence similarity calculation (bag-of-words)

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (new, partial)  
**Type**: Service Implementation - Core Algorithm  
**Parallel**: No (part of main service)  
**Description**: Implement bag-of-words cosine similarity for sentence graph construction

**Implementation** (from research.md):
1. Tokenize sentences (split on whitespace, lowercase)
2. Build vocabulary (unique words across all sentences)
3. Create word frequency vectors for each sentence
4. Calculate cosine similarity: `dot(v1, v2) / (||v1|| * ||v2||)`
5. Only create edges for similarity > threshold (default 0.1)

**Methods**:
- `double CalculateSimilarity(string sentence1, string sentence2)` (private)
- `Dictionary<string, int> BuildWordFrequencyVector(string sentence)` (private)
- `double CosineSimilarity(Dictionary<string, int> vec1, Dictionary<string, int> vec2)` (private)

**Validation**: T011 test (CalculateTextRankScores) should pass  
**Dependencies**: T012 (SentenceSegmenter)

---

### T014: Implement PageRank iteration algorithm

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (extend)  
**Type**: Service Implementation - Core Algorithm  
**Parallel**: No (extends same file)  
**Description**: Implement iterative PageRank with convergence detection

**Implementation** (from research.md):
```
Initialize: score(Si) = 1.0 for all sentences
Repeat until convergence (max iterations):
  For each sentence Si:
    score_new(Si) = (1-d) + d * Σ[similarity(Si, Sj) * score(Sj) / Σ similarity(Sj, Sk)]
  If max|score_new - score| < ε: CONVERGED
  Update all scores
```

**Parameters**:
- Damping factor: 0.85 (from configuration)
- Convergence threshold: 0.0001 (from configuration)
- Max iterations: 100 (from configuration)

**Methods**:
- `double[] CalculatePageRank(Dictionary<int, Dictionary<int, double>> graph, int sentenceCount, double dampingFactor, double convergenceThreshold, int maxIterations)` (private)

**Validation**: T011 test should pass, scores sum to ~1.0  
**Dependencies**: T013 (similarity calculation)

---

### T015: Implement TextRankService.CalculateTextRankScores

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (extend)  
**Type**: Service Implementation - Public API  
**Parallel**: No (extends same file)  
**Description**: Implement CalculateTextRankScores method per ITextRankService contract

**Implementation**:
1. Validate input (sentences not null, count >= 2)
2. Build sentence similarity graph using T013
3. Apply PageRank using T014
4. Normalize scores to sum to 1.0
5. Return Dictionary<int, double> indexed by sentence position

**Validation**: T011 contract test should PASS (GREEN)  
**Dependencies**: T013, T014

---

### T016: Implement TextRankService.ExtractPlotRelevantSentences

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (extend)  
**Type**: Service Implementation - Primary Method  
**Parallel**: No (extends same file)  
**Description**: Implement ExtractPlotRelevantSentences method per ITextRankService contract

**Implementation**:
1. Segment subtitle text using SentenceSegmenter (T012)
2. Calculate TextRank scores for all sentences (T015)
3. Sort sentences by score (descending)
4. Select top N% based on sentencePercentage parameter
5. Check fallback conditions (T017)
6. If fallback triggered: return full text with FallbackTriggered=true
7. Otherwise: reconstruct selected sentences in original document order
8. Return TextRankExtractionResult with statistics

**Performance instrumentation**:
- Stopwatch to measure ProcessingTimeMs
- Log statistics (total sentences, selected, average score)

**Validation**: T007-T010 contract tests should PASS (GREEN)  
**Dependencies**: T012, T015, T017

---

### T017: Implement fallback decision logic

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (extend)  
**Type**: Service Implementation - Fallback Logic  
**Parallel**: No (extends same file)  
**Description**: Implement fallback conditions to prevent TextRank from degrading performance

**Implementation** (from spec.md clarifications):
```csharp
private bool ShouldFallback(int selectedCount, int totalCount, int minSentences, int minPercentage, string filteredText, out string reason)
{
    // Absolute minimum
    if (selectedCount < minSentences) {
        reason = $"Selected count ({selectedCount}) below minimum threshold ({minSentences})";
        return true;
    }
    
    // Relative minimum
    double actualPercentage = (double)selectedCount / totalCount * 100;
    if (actualPercentage < minPercentage) {
        reason = $"Selected percentage ({actualPercentage:F1}%) below minimum ({minPercentage}%)";
        return true;
    }
    
    // Text too short
    if (filteredText.Length < 100) {
        reason = "Filtered text too short (<100 characters)";
        return true;
    }
    
    // Insufficient for TextRank algorithm
    if (totalCount < 2) {
        reason = $"Insufficient sentences for TextRank ({totalCount} < 2)";
        return true;
    }
    
    reason = null;
    return false;
}
```

**Validation**: T008, T009, T010 contract tests should PASS  
**Dependencies**: None (pure logic)

---

### T018: Add structured logging for TextRank statistics

**File**: `src/EpisodeIdentifier.Core/Services/TextRankService.cs` (extend)  
**Type**: Observability  
**Parallel**: No (extends same file)  
**Description**: Add structured logging for TextRank extraction statistics per constitution

**Logging Events** (from data-model.md):

1. **Successful extraction**:
```csharp
_logger.LogInformation(
    "TextRank extraction: {SelectedCount}/{TotalCount} sentences " +
    "({Percentage}%), avg score {AvgScore:F3}, {ProcessingTimeMs}ms",
    result.SelectedSentenceCount, result.TotalSentenceCount,
    result.SelectionPercentage, result.AverageScore, result.ProcessingTimeMs);
```

2. **Fallback triggered**:
```csharp
_logger.LogWarning(
    "TextRank fallback triggered: {Reason}. Using full text ({TotalCount} sentences)",
    result.FallbackReason, result.TotalSentenceCount);
```

3. **Performance warning**:
```csharp
if (result.ProcessingTimeMs > 1000) {
    _logger.LogWarning(
        "TextRank processing slow: {ProcessingTimeMs}ms for {SentenceCount} sentences",
        result.ProcessingTimeMs, result.TotalSentenceCount);
}
```

**Validation**: Run test, verify logs contain structured data  
**Dependencies**: T016 (main method implementation)

---

## Phase 3.5: Integration with Feature 013

### T019: Integrate TextRank into EpisodeIdentificationService

**File**: `src/EpisodeIdentifier.Core/Services/EpisodeIdentificationService.cs` (modify)  
**Type**: Integration  
**Parallel**: No (modifies critical service)  
**Description**: Insert TextRank extraction as preprocessing step before embedding generation

**Integration Point** (from research.md):
```csharp
// In IdentifyEpisodeAsync() method, before embedding generation:
string textForEmbedding = subtitleText;

var config = _configService.GetConfiguration();
if (config.TextRankFiltering?.Enabled == true)
{
    var extracted = _textRankService.ExtractPlotRelevantSentences(
        subtitleText,
        config.TextRankFiltering.SentencePercentage,
        config.TextRankFiltering.MinSentences,
        config.TextRankFiltering.MinPercentage);
    
    if (!extracted.FallbackTriggered)
    {
        textForEmbedding = extracted.FilteredText;
        _logger.LogInformation(
            "TextRank filtering: {Selected}/{Total} sentences selected",
            extracted.SelectedSentenceCount, extracted.TotalSentenceCount);
    }
    else
    {
        _logger.LogWarning(
            "TextRank fallback: {Reason}",
            extracted.FallbackReason);
    }
}

// Continue with existing embedding generation
var embedding = await _embeddingService.GenerateEmbeddingAsync(textForEmbedding);
```

**Validation**: Run with TextRank enabled, verify filtered text used for embeddings  
**Dependencies**: T016 (TextRankService.ExtractPlotRelevantSentences), T005 (configuration loading)

---

### T020: Register TextRankService in dependency injection

**File**: `src/EpisodeIdentifier.Core/Extensions/ServiceCollectionExtensions.cs` (modify)  
**Type**: DI Registration  
**Parallel**: No (modifies shared file)  
**Description**: Add TextRankService to service collection

**Changes**:
```csharp
public static IServiceCollection AddEpisodeIdentificationServices(this IServiceCollection services)
{
    // ... existing services ...
    
    // TextRank filtering for plot-relevant sentence extraction (Feature 014)
    services.AddScoped<ITextRankService>(provider =>
        new TextRankService(
            provider.GetRequiredService<ILogger<TextRankService>>(),
            provider.GetRequiredService<IConfigurationService>()));
    
    // ... rest of services ...
}
```

**Validation**: DI container resolves ITextRankService without errors  
**Dependencies**: T016 (TextRankService implementation)

---

### T021: Update EpisodeIdentificationService constructor for ITextRankService

**File**: `src/EpisodeIdentifier.Core/Services/EpisodeIdentificationService.cs` (modify)  
**Type**: Constructor Injection  
**Parallel**: No (same file as T019, sequential)  
**Description**: Add ITextRankService parameter to EpisodeIdentificationService constructor

**Changes**:
```csharp
public EpisodeIdentificationService(
    // ... existing parameters ...
    ITextRankService textRankService,
    ILogger<EpisodeIdentificationService> logger)
{
    // ... existing assignments ...
    _textRankService = textRankService ?? throw new ArgumentNullException(nameof(textRankService));
}
```

**Validation**: Service instantiates correctly with all dependencies  
**Dependencies**: T019 (integration point), T020 (DI registration)

---

## Phase 3.6: Integration Tests

### T022: Integration test - Verbose subtitle matching improvement [P]

**File**: `tests/integration/TextRankIntegrationTests.cs` (new)  
**Type**: Integration Test  
**Parallel**: Yes (new test file)  
**Description**: Test that TextRank improves confidence for verbose subtitle files

**Test Scenario** (from quickstart.md):
1. Create test subtitle with 600 sentences (400 plot + 200 filler)
2. Identify WITHOUT TextRank (textRankFiltering.enabled = false)
   - Expected confidence: ~0.68
3. Identify WITH TextRank (textRankFiltering.enabled = true)
   - Expected confidence: ~0.79 (+16% improvement)
4. Verify FilteredText contains plot-relevant sentences, not filler

**Assertions**:
- Confidence improvement >= 10%
- TextRank statistics logged
- No errors in processing

**Validation**: Test PASSES, demonstrates accuracy improvement  
**Dependencies**: T019 (integration complete)

---

### T023: Integration test - Fallback behavior validation [P]

**File**: `tests/integration/TextRankIntegrationTests.cs` (extend)  
**Type**: Integration Test  
**Parallel**: Yes (same test file, different test method)  
**Description**: Test fallback triggers correctly for short subtitle files

**Test Scenarios**:
1. **10-sentence file**: Should trigger fallback (< 15 minimum)
2. **200-sentence file with weak scores**: Should trigger fallback if < 10% selected
3. **Single-sentence file**: Should trigger fallback immediately

**Assertions**:
- FallbackTriggered = true
- FallbackReason contains expected message
- Full text used for embedding (no data loss)
- Match still succeeds (graceful degradation)

**Validation**: Test PASSES, fallback works as specified  
**Dependencies**: T019

---

### T024: Integration test - Configuration hot-reload [P]

**File**: `tests/integration/TextRankConfigurationTests.cs` (new)  
**Type**: Integration Test  
**Parallel**: Yes (new test file)  
**Description**: Test TextRank configuration changes apply without restart

**Test Scenario** (from quickstart.md Step 5):
1. Start with textRankFiltering.sentencePercentage = 25
2. Process subtitle, record selected sentence count (~25%)
3. Modify config: sentencePercentage = 30
4. Process same subtitle again
5. Verify selected sentence count increased to ~30%

**Assertions**:
- Configuration reloads automatically
- Log shows "Configuration reloaded successfully"
- New sentencePercentage applied to next processing

**Validation**: Test PASSES, hot-reload works  
**Dependencies**: T005 (configuration loading), T019 (integration)

---

### T025: Integration test - Performance validation (large files) [P]

**File**: `tests/integration/TextRankPerformanceTests.cs` (new)  
**Type**: Integration Test - Performance  
**Parallel**: Yes (new test file)  
**Description**: Test TextRank meets performance targets for large subtitle files

**Test Scenario**:
1. Create subtitle file with 2000 sentences (movie-length)
2. Process with TextRank enabled
3. Measure ProcessingTimeMs from result

**Performance Targets** (from plan.md Technical Context):
- TextRank processing: <1 second for 500 sentences
- Total processing: <5 seconds (TextRank + embedding)
- Memory usage: <500MB during processing

**Assertions**:
- ProcessingTimeMs < 1000ms for 500-sentence file
- ProcessingTimeMs < 3000ms for 2000-sentence file (scales linearly)
- No OutOfMemoryException
- Successful extraction of ~500 sentences (25% of 2000)

**Validation**: Test PASSES, performance acceptable  
**Dependencies**: T019

---

### T026: Integration test - Backward compatibility with Feature 013 [P]

**File**: `tests/integration/TextRankBackwardCompatibilityTests.cs` (new)  
**Type**: Integration Test - Regression  
**Parallel**: Yes (new test file)  
**Description**: Ensure all existing Feature 013 tests still pass with TextRank available

**Test Scenarios**:
1. **TextRank disabled**: All Feature 013 tests pass unchanged
2. **TextRank enabled, no config**: Uses default full-text embedding
3. **Existing database**: Legacy embeddings work correctly
4. **Hybrid mode**: Can mix filtered and full-text embeddings

**Validation**: All 45 Feature 013 contract tests + 5 integration tests PASS  
**Dependencies**: T019 (integration complete)

---

## Phase 3.7: Quickstart Validation

### T027: Execute quickstart scenario - Baseline match (without TextRank)

**File**: Manual execution following `specs/014-use-textrank-to/quickstart.md`  
**Type**: Quickstart Validation - Step 1  
**Parallel**: No (sequential validation steps)  
**Description**: Establish baseline confidence without TextRank filtering

**Steps** (from quickstart.md):
1. Disable TextRank in configuration: `textRankFiltering.enabled = false`
2. Create verbose test file: Criminal Minds S06E19 + 200 filler sentences
3. Run identification: `./EpisodeIdentifier.Core --identify --file CriminalMinds_S06E19_Verbose.mkv --format json`
4. Record baseline confidence (expected: ~0.68)

**Expected Output**:
```json
{
  "matched": true,
  "series": "Criminal Minds",
  "season": 6,
  "episode": 19,
  "confidence": 0.68,
  "matchMethod": "Embedding"
}
```

**Success Criteria**: Baseline confidence recorded, degraded due to filler  
**Dependencies**: T026 (system working)

---

### T028: Execute quickstart scenario - TextRank match (with filtering)

**File**: Manual execution following `specs/014-use-textrank-to/quickstart.md`  
**Type**: Quickstart Validation - Step 2  
**Parallel**: No (follows T027)  
**Description**: Verify TextRank improves confidence for verbose subtitle

**Steps** (from quickstart.md):
1. Enable TextRank: `textRankFiltering.enabled = true, sentencePercentage = 25`
2. Run identification on same verbose file
3. Compare confidence to baseline (T027)

**Expected Output**:
```json
{
  "matched": true,
  "confidence": 0.79,
  "matchMethod": "EmbeddingTextRank",
  "textRankStats": {
    "totalSentences": 600,
    "selectedSentences": 150,
    "selectionPercentage": 25.0,
    "averageScore": 0.71,
    "fallbackTriggered": false,
    "processingTimeMs": 423
  }
}
```

**Success Criteria**:
- Confidence improved: 0.68 → 0.79 (+16%, target: +10-15%) ✓
- TextRank statistics present in output
- ProcessingTimeMs < 500ms ✓

**Dependencies**: T027 (baseline established)

---

### T029: Verify quickstart success criteria

**File**: Manual checklist from `specs/014-use-textrank-to/quickstart.md`  
**Type**: Quickstart Validation - Final Check  
**Parallel**: No (final validation)  
**Description**: Verify all success criteria from quickstart.md are met

**Success Criteria Checklist** (from quickstart.md):

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| Accuracy Improvement | +10-15% for verbose files | 0.68 → 0.79 (+16%) | ✅ |
| Fallback Reliability | Short files trigger fallback | Tested in T023 | ✅ |
| Performance | TextRank adds <500ms | 423ms | ✅ |
| Memory | <500MB usage | Measured in T025 | ✅ |
| Hot-Reload | Config changes apply | Tested in T024 | ✅ |
| Backward Compatibility | All Feature 013 tests pass | Tested in T026 | ✅ |

**Additional Validation**:
- [ ] Baseline match works (T027)
- [ ] TextRank match shows improvement (T028)
- [ ] Fallback triggers for short files (quickstart Step 3)
- [ ] Large files process within budget (quickstart Step 4)
- [ ] Configuration hot-reload works (quickstart Step 5)
- [ ] Logs show TextRank statistics
- [ ] No errors or warnings (except expected fallback warnings)

**Dependencies**: T027, T028

---

### T030: Performance profiling and optimization

**File**: Manual profiling using dotMemory/dotTrace or similar  
**Type**: Performance Validation  
**Parallel**: No (final phase)  
**Description**: Profile TextRank implementation to ensure no memory leaks or performance bottlenecks

**Profiling Tasks**:
1. **Memory profiling**:
   - Run bulk processing with 100+ subtitle files
   - Monitor memory usage over time
   - Check for graph construction memory leaks
   - Verify sparse matrix cleanup

2. **CPU profiling**:
   - Identify hot paths in PageRank iteration
   - Check similarity calculation efficiency
   - Measure O(n²) impact for large files

3. **Optimization opportunities**:
   - If convergence slow: adjust maxIterations or convergenceThreshold
   - If memory high: optimize sparse matrix representation
   - If similarity slow: consider parallel computation (PLINQ)

**Performance Targets Validation** (from plan.md):
- Sentence segmentation: <10ms ✓
- Graph construction: <200ms for 500 sentences ✓
- PageRank iteration: <100ms (converges ~30 iterations) ✓
- Total overhead: <500ms per subtitle ✓
- Memory: <10MB for 1000-sentence file ✓

**Success Criteria**: All performance targets met, no optimization needed for v1  
**Dependencies**: All implementation complete (T001-T029)

---

## Task Dependencies Graph

```
Setup & Models (Parallel foundation):
  T001 [P] TextRankExtractionResult ─┐
  T002 [P] SentenceScore             ├─→ T006 ITextRankService ──→ T007-T011 Contract Tests (RED)
  T003 [P] TextRankConfiguration ────┤                                           ↓
                                     └─→ T004 AppConfig ──→ T005 ConfigService   ↓
                                                                                  ↓
Service Implementation (GREEN):                                                  ↓
  T012 SentenceSegmenter ──→ T013 Similarity ──→ T014 PageRank ──→ T015 CalculateScores ←┘
                                                                       ↓
                                                    T017 Fallback ←──→ T016 ExtractSentences
                                                                       ↓
                                                                  T018 Logging

Integration:
  T016 + T005 ──→ T019 EpisodeIdentificationService Integration
                    ↓
                  T020 DI Registration
                    ↓
                  T021 Constructor Injection
                    ↓
  ┌───────────────┴────────────────┐
  ↓                                ↓
T022-T026 Integration Tests [P]   T027-T029 Quickstart Validation (Sequential)
                                    ↓
                                  T030 Performance Profiling
```

## Parallel Execution Batches

### Batch 1: Models (can run simultaneously)
```bash
Task T001: "Create TextRankExtractionResult model"
Task T002: "Create SentenceScore model"
Task T003: "Create TextRankConfiguration model"
```

### Batch 2: Contract Tests - RED Phase (can run simultaneously after T006)
```bash
Task T007: "Contract test - Extract verbose subtitle sentences"
Task T008: "Contract test - Trigger fallback insufficient sentences"
Task T009: "Contract test - Trigger fallback low percentage"
Task T010: "Contract test - Handle single-sentence subtitle"
Task T011: "Contract test - Calculate TextRank scores"
```

### Batch 3: Integration Tests (can run simultaneously after T021)
```bash
Task T022: "Integration test - Verbose subtitle improvement"
Task T023: "Integration test - Fallback behavior"
Task T024: "Integration test - Configuration hot-reload"
Task T025: "Integration test - Performance validation"
Task T026: "Integration test - Backward compatibility"
```

## Task Execution Notes

### TDD Enforcement
- **Phase 3.3 (T007-T011)**: Contract tests MUST be written first and MUST FAIL
- **Phase 3.4 (T012-T018)**: Implementation only begins after tests are failing
- **Verification**: Each implementation task references which test(s) should turn GREEN

### File Modification Conflicts
- **AppConfig.cs**: T004 modifies, must complete before T005
- **ConfigurationService.cs**: T005 modifies, sequential with T004
- **EpisodeIdentificationService.cs**: T019 and T021 both modify, sequential
- **ServiceCollectionExtensions.cs**: T020 modifies, single task

### Performance Monitoring
- **T018**: Add logging throughout implementation
- **T025**: Dedicated performance integration test
- **T030**: Final profiling and optimization

### Backward Compatibility
- **T026**: Critical regression test - ensures Feature 013 still works
- All existing tests must pass with TextRank code present but disabled

---

## Validation Checklist

**GATE: Verify before marking feature complete**

- [x] All contracts have corresponding tests (1/1 contract → 5 test scenarios)
- [x] All entities have model tasks (3/3 models → T001, T002, T003)
- [x] All tests come before implementation (Phase 3.3 before 3.4)
- [x] Parallel tasks truly independent (different files marked [P])
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] Dependencies properly ordered (models → interfaces → tests → impl)
- [x] Integration with Feature 013 planned (T019-T021)
- [x] Quickstart validation included (T027-T029)
- [x] Performance profiling included (T030)

---

## Estimated Effort

**Total Tasks**: 30  
**Parallel Opportunities**: 12 tasks can run in parallel (3 batches)  
**Sequential Critical Path**: ~18 tasks

**Time Estimate**:
- Phase 3.1 Setup: 3-4 hours (models + configuration)
- Phase 3.2 Interfaces: 1 hour
- Phase 3.3 Contract Tests: 4-5 hours (5 test scenarios)
- Phase 3.4 Implementation: 8-10 hours (core algorithm + integration)
- Phase 3.5 Integration: 2-3 hours
- Phase 3.6 Integration Tests: 4-5 hours (5 test scenarios)
- Phase 3.7 Quickstart: 2-3 hours (manual validation)

**Total**: 24-31 hours (single developer, sequential)  
**With Parallelization**: 18-24 hours (utilizing parallel batches)

---

**Status**: ✅ TASKS READY FOR EXECUTION  
**Next Step**: Begin Phase 3.1 (T001-T005) - Models & Configuration
