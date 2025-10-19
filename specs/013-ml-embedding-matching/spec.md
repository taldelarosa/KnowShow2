# Feature Specification: ML Embedding-Based Subtitle Matching

**Feature ID**: 013-ml-embedding-matching  
**Status**: Planning  
**Priority**: High  
**Created**: 2025-10-19

---

## Overview

Replace CTPH fuzzy hashing with ML embedding-based semantic similarity matching to improve cross-format subtitle identification, particularly for OCR-based subtitles (VobSub, PGS) that suffer from character recognition errors.

### Problem Statement

Current CTPH fuzzy hashing shows 0% similarity between VobSub OCR subtitles and text subtitles despite 99.3% content similarity due to:
- Character confusion (i/l, O/0)
- Word spacing issues in OCR
- Minor formatting differences

This prevents accurate episode identification across different subtitle formats for the same content.

### Success Metrics

- VobSub OCR for Criminal Minds S06E19 matches existing text subtitle entries with >85% confidence
- No performance regression for text subtitle matching (<5s embedding generation, <2s search across 1000 entries)
- All existing tests pass with embedding-based matching
- Successful migration path for existing hash-based database

---

## User Stories

### US-1: Match OCR Subtitles to Text Database
**As a** user with VobSub OCR subtitles  
**I want** the system to match them against text subtitle database entries  
**So that** I can identify episodes regardless of subtitle format

**Acceptance Criteria**:
- VobSub subtitles match text subtitles with >85% confidence for same episode
- System reports confidence score per subtitle format
- Match results include format type in output

### US-2: Migrate Existing Database
**As a** user with an existing hash-based database  
**I want** the system to automatically generate embeddings for existing entries  
**So that** I don't lose my stored subtitle data

**Acceptance Criteria**:
- Database migration runs automatically on first launch with new version
- All existing CleanText entries get embeddings generated
- Migration is non-destructive (keeps existing hash data)
- Migration progress is logged

### US-3: Configure Matching Thresholds
**As a** power user  
**I want** to configure similarity thresholds per subtitle format  
**So that** I can tune matching accuracy for my content

**Acceptance Criteria**:
- Configuration file supports embedding similarity thresholds per format (Text, PGS, VobSub)
- Invalid threshold values (outside 0.0-1.0) are rejected with clear error
- Hot-reload of configuration updates thresholds without restart

### US-4: Maintain CLI Compatibility
**As a** current user of the CLI  
**I want** the existing commands to work unchanged  
**So that** my scripts and workflows continue to function

**Acceptance Criteria**:
- --identify, --store, --bulk-identify commands work as before
- Output format remains compatible
- Performance meets existing benchmarks

---

## Requirements *(mandatory)*

### Functional Requirements

1. **FR-1**: Replace CTPH fuzzy hash similarity with cosine similarity of semantic embeddings
2. **FR-2**: Generate 384-dimensional embeddings from CleanText using all-MiniLM-L6-v2 or similar
3. **FR-3**: Store embeddings as BLOB in SQLite SubtitleHashes table
4. **FR-4**: Implement vector similarity search using vectorlite extension
5. **FR-5**: Support configurable similarity thresholds per subtitle format (Text, PGS, VobSub)
6. **FR-6**: Add SubtitleFormat column to track format type
7. **FR-7**: Provide automatic migration for existing database entries
8. **FR-8**: Maintain backward compatibility with existing CLI interface
9. **FR-9**: Bundle or auto-download embedding model on first run
10. **FR-10**: Log embedding generation and search operations with timing metrics

### Non-Functional Requirements

1. **NFR-1**: Cross-platform compatibility (Windows/Linux, .NET 8.0)
2. **NFR-2**: Embedding generation: <5 seconds per subtitle
3. **NFR-3**: Vector search: <2 seconds across 1000 entries
4. **NFR-4**: Model size: <100MB
5. **NFR-5**: Memory usage: <500MB during embedding generation
6. **NFR-6**: No external API dependencies (fully offline operation)
7. **NFR-7**: Minimal external dependencies (avoid Python interop if possible)

### Key Entities *(include if feature involves data)*

- **SubtitleEmbedding**: 384-dimensional float vector representing semantic content of CleanText
- **SubtitleFormat**: Enum (Text, PGS, VobSub) indicating source subtitle type
- **EmbeddingModel**: ONNX model for generating sentence embeddings, with tokenizer
- **VectorSimilarityResult**: Search result with subtitle metadata, cosine similarity score, and format
- **EmbeddingMatchThresholds**: Configuration per format (embedSimilarity, matchConfidence, renameConfidence)

---

## Technical Approach

### Technology Stack

- **ML Runtime**: Microsoft.ML.OnnxRuntime for cross-platform ONNX model execution
- **Vector Storage**: vectorlite SQLite extension (https://github.com/1yefuwang1/vectorlite)
- **Embedding Model**: all-MiniLM-L6-v2 (ONNX format, ~90MB)
- **Tokenizer**: BPE tokenizer bundled with model

### Database Schema Changes

```sql
-- Add embedding support to SubtitleHashes table
ALTER TABLE SubtitleHashes ADD COLUMN Embedding BLOB NULL;
ALTER TABLE SubtitleHashes ADD COLUMN SubtitleFormat TEXT NOT NULL DEFAULT 'Text';

-- Create vector index using vectorlite
-- vectorlite_create_index('SubtitleHashes', 'Embedding', 'cosine', 384)
```

### Architecture Components

1. **EmbeddingService**: Generate embeddings from text using ONNX model
2. **VectorSearchService**: Query embeddings using vectorlite cosine similarity
3. **ModelManager**: Download, cache, and load ONNX model on first run
4. **DatabaseMigration**: Generate embeddings for existing CleanText entries

### Configuration Schema

```json
{
  "embeddingModel": {
    "modelPath": "models/all-MiniLM-L6-v2.onnx",
    "autoDownload": true,
    "downloadUrl": "https://..."
  },
  "matchingThresholds": {
    "textBased": {
      "embedSimilarity": 0.85,
      "matchConfidence": 0.7,
      "renameConfidence": 0.8
    },
    "pgs": {
      "embedSimilarity": 0.80,
      "matchConfidence": 0.6,
      "renameConfidence": 0.7
    },
    "vobSub": {
      "embedSimilarity": 0.75,
      "matchConfidence": 0.5,
      "renameConfidence": 0.6
    }
  }
}
```

---

## Constraints

1. Keep SQLite as database (use vectorlite extension)
2. Cross-platform .NET 8.0 implementation
3. No external API calls (fully offline)
4. Minimal dependencies (avoid Python interop)
5. Model bundled or auto-downloaded (<100MB)
6. Maintain existing CLI interface
7. Pass all existing tests

---

## Dependencies

- **New NuGet Packages**: 
  - Microsoft.ML.OnnxRuntime (~40MB)
  - Microsoft.ML.Tokenizers (for BPE tokenization)
- **SQLite Extension**: vectorlite (needs to be loaded at runtime)
- **External Tools**: None (model downloaded via HTTP if not bundled)
- **Feature Dependencies**: Builds on 012-process-dvd-subtitle for VobSub support

---

## Testing Strategy

### Contract Tests
- EmbeddingService generates 384-dimensional vectors from text
- VectorSearchService returns sorted results by cosine similarity
- ModelManager downloads and caches model correctly

### Integration Tests
- Database migration generates embeddings for all existing entries
- Vector search finds correct episode across formats
- Configuration threshold changes affect matching behavior

### End-to-End Tests
- Criminal Minds S06E19 VobSub matches text entry with >85% confidence
- Bulk processing with embeddings matches performance targets
- Mixed format database (Text + PGS + VobSub) returns correct matches

### Performance Tests
- Embedding generation: <5s per subtitle
- Vector search: <2s for 1000 entries
- Memory usage: <500MB during batch processing

---

## Rollout Plan

### Phase 1: Infrastructure (Embedding Generation)
- Add EmbeddingService with ONNX model loading
- Add Embedding column to database
- Implement embedding generation from CleanText

### Phase 2: Vector Search
- Integrate vectorlite extension
- Implement VectorSearchService
- Add SubtitleFormat column and tracking

### Phase 3: Migration & Configuration
- Implement database migration for existing entries
- Add embedding-specific configuration
- Update threshold handling per format

### Phase 4: Integration & Testing
- Update identification workflow to use embeddings
- Run Criminal Minds S06E19 validation test
- Performance benchmarking

### Phase 5: Documentation & Deployment
- Update configuration guide
- Document model management
- Release notes and migration guide

---

## Open Questions

1. **Q**: Should we keep fuzzy hashing as fallback option or fully replace?  
   **A**: TBD - May want to keep for backward compatibility testing

2. **Q**: What's the best ONNX model source for all-MiniLM-L6-v2?  
   **A**: TBD - Research Hugging Face ONNX exports vs Optimum

3. **Q**: How to handle vectorlite extension loading on different platforms?  
   **A**: TBD - Research SQLitePCLRaw provider approach

4. **Q**: Should embedding generation be async with progress reporting?  
   **A**: TBD - Probably yes for large databases

---

## Related Documentation

- [Feature 008: Fuzzy Hashing Plus](../008-fuzzy-hashing-plus/spec.md)
- [Feature 012: Process DVD Subtitle](../012-process-dvd-subtitle/spec.md)
- [Configuration Guide](../../CONFIGURATION_GUIDE.md)
- [vectorlite Documentation](https://github.com/1yefuwang1/vectorlite)

---

**Version**: 1.0  
**Last Updated**: 2025-10-19  
**Authors**: Development Team
