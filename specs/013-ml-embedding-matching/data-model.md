# Data Model: ML Embedding-Based Subtitle Matching

**Feature**: 013-ml-embedding-matching  
**Date**: October 19, 2025  
**Phase**: 1 (Design & Contracts)

---

## Overview

This document defines the data models, database schema changes, and entity relationships for ML embedding-based subtitle matching. The design extends the existing `SubtitleHashes` table with embedding storage and introduces new services for embedding generation and vector search.

---

## Database Schema Changes

### SubtitleHashes Table Extension

```sql
-- Migration: Add embedding and format columns
ALTER TABLE SubtitleHashes ADD COLUMN Embedding BLOB NULL;
ALTER TABLE SubtitleHashes ADD COLUMN SubtitleFormat TEXT NOT NULL DEFAULT 'Text';

-- Updated table structure (existing columns + new columns)
CREATE TABLE IF NOT EXISTS SubtitleHashes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Series TEXT NOT NULL,
    Season TEXT NOT NULL,
    Episode TEXT NOT NULL,
    OriginalHash TEXT NOT NULL,
    NoTimecodesHash TEXT NOT NULL,
    NoHtmlHash TEXT NOT NULL,
    CleanHash TEXT NOT NULL,
    EpisodeName TEXT NULL,
    OriginalText TEXT DEFAULT '',
    NoTimecodesText TEXT DEFAULT '',
    NoHtmlText TEXT DEFAULT '',
    CleanText TEXT DEFAULT '',
    Embedding BLOB NULL,              -- NEW: 384-dimensional float32 array (1536 bytes)
    SubtitleFormat TEXT DEFAULT 'Text' -- NEW: 'Text', 'PGS', or 'VobSub'
);

-- Existing indexes remain unchanged
CREATE INDEX IF NOT EXISTS idx_series_season ON SubtitleHashes(Series, Season);
CREATE INDEX IF NOT EXISTS idx_clean_hash ON SubtitleHashes(CleanHash);
CREATE INDEX IF NOT EXISTS idx_original_hash ON SubtitleHashes(OriginalHash);
CREATE INDEX IF NOT EXISTS idx_hash_composite ON SubtitleHashes(
    OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash
);

-- New index for format filtering
CREATE INDEX IF NOT EXISTS idx_subtitle_format ON SubtitleHashes(SubtitleFormat);
```

### vectorlite Virtual Table

```sql
-- Virtual table for vector similarity search
-- Linked to SubtitleHashes via rowid
CREATE VIRTUAL TABLE IF NOT EXISTS vector_index USING vectorlite(
    embedding float32[384] cosine,
    hnsw(
        max_elements=10000,
        ef_construction=200,
        M=16,
        random_seed=42,
        allow_replace_deleted=true
    )
);

-- Populate vector_index from SubtitleHashes
-- INSERT INTO vector_index(rowid, embedding) 
-- SELECT Id, Embedding FROM SubtitleHashes WHERE Embedding IS NOT NULL;
```

---

## Core Entities

### 1. SubtitleEmbedding

**Purpose**: Represents a 384-dimensional semantic embedding of subtitle content.

```csharp
namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents a 384-dimensional embedding vector for subtitle content.
/// </summary>
public class SubtitleEmbedding
{
    /// <summary>
    /// 384-dimensional float32 array representing semantic content.
    /// </summary>
    public float[] Vector { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0-1.0) when comparing with another embedding.
    /// Null if not yet compared.
    /// </summary>
    public double? Similarity { get; init; }

    /// <summary>
    /// Source text that was embedded (typically CleanText).
    /// </summary>
    public string SourceText { get; init; }

    public SubtitleEmbedding(float[] vector, string sourceText)
    {
        if (vector == null || vector.Length != 384)
        {
            throw new ArgumentException("Embedding vector must be exactly 384 dimensions", nameof(vector));
        }

        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        Similarity = null;
    }

    /// <summary>
    /// Serialize embedding to byte array for SQLite BLOB storage.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Vector.Length * sizeof(float)];
        Buffer.BlockCopy(Vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Deserialize embedding from SQLite BLOB byte array.
    /// </summary>
    public static float[] FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 384 * sizeof(float))
        {
            throw new ArgumentException("Byte array must be exactly 1536 bytes (384 floats)", nameof(bytes));
        }

        var vector = new float[384];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    /// <summary>
    /// Calculate cosine similarity between two embeddings.
    /// Returns value in range [0.0, 1.0] where 1.0 = identical.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length != 384)
        {
            throw new ArgumentException("Embeddings must be 384 dimensions");
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < 384; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
```

### 2. SubtitleFormat

**Purpose**: Enum representing subtitle source format for analytics and threshold configuration.

```csharp
namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Subtitle format type for tracking source and applying format-specific thresholds.
/// </summary>
public enum SubtitleFormat
{
    /// <summary>
    /// Text-based subtitles (SRT, ASS, WebVTT, etc.)
    /// Highest accuracy, direct text extraction.
    /// </summary>
    Text,

    /// <summary>
    /// PGS (Presentation Graphic Stream) subtitles from Blu-ray.
    /// Requires OCR via pgsrip + Tesseract.
    /// Medium accuracy due to OCR errors.
    /// </summary>
    PGS,

    /// <summary>
    /// VobSub (DVD) subtitles (idx/sub format).
    /// Requires OCR via mkvextract + Tesseract.
    /// Lower accuracy due to OCR quality and compression artifacts.
    /// </summary>
    VobSub
}

/// <summary>
/// Extension methods for SubtitleFormat enum.
/// </summary>
public static class SubtitleFormatExtensions
{
    /// <summary>
    /// Convert enum to database string representation.
    /// </summary>
    public static string ToDbString(this SubtitleFormat format)
    {
        return format.ToString();
    }

    /// <summary>
    /// Parse database string to SubtitleFormat enum.
    /// </summary>
    public static SubtitleFormat FromDbString(string format)
    {
        return Enum.Parse<SubtitleFormat>(format, ignoreCase: true);
    }

    /// <summary>
    /// Get default matching confidence threshold for format.
    /// </summary>
    public static double GetDefaultMatchConfidence(this SubtitleFormat format)
    {
        return format switch
        {
            SubtitleFormat.Text => 0.85,
            SubtitleFormat.PGS => 0.80,
            SubtitleFormat.VobSub => 0.75,
            _ => 0.85
        };
    }
}
```

### 3. VectorSimilarityResult

**Purpose**: Search result from vector similarity query with subtitle metadata.

```csharp
namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Result from vector similarity search including subtitle metadata and similarity score.
/// </summary>
public class VectorSimilarityResult
{
    /// <summary>
    /// Database ID from SubtitleHashes table.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Series name (e.g., "Criminal Minds").
    /// </summary>
    public string Series { get; init; }

    /// <summary>
    /// Season number (e.g., "06" or "6").
    /// </summary>
    public string Season { get; init; }

    /// <summary>
    /// Episode number (e.g., "19").
    /// </summary>
    public string Episode { get; init; }

    /// <summary>
    /// Episode name if available.
    /// </summary>
    public string? EpisodeName { get; init; }

    /// <summary>
    /// Subtitle format (Text, PGS, VobSub).
    /// </summary>
    public SubtitleFormat Format { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0-1.0) between query and this result.
    /// Higher is more similar.
    /// </summary>
    public double Similarity { get; init; }

    /// <summary>
    /// Overall confidence score (0.0-1.0) combining similarity and format-specific adjustments.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Distance metric from vectorlite (1.0 - similarity for cosine distance).
    /// </summary>
    public double Distance { get; init; }

    /// <summary>
    /// Rank in search results (1 = best match).
    /// </summary>
    public int Rank { get; init; }

    public VectorSimilarityResult(
        int id,
        string series,
        string season,
        string episode,
        string? episodeName,
        SubtitleFormat format,
        double similarity,
        double confidence,
        double distance,
        int rank)
    {
        Id = id;
        Series = series ?? throw new ArgumentNullException(nameof(series));
        Season = season ?? throw new ArgumentNullException(nameof(season));
        Episode = episode ?? throw new ArgumentNullException(nameof(episode));
        EpisodeName = episodeName;
        Format = format;
        Similarity = similarity;
        Confidence = confidence;
        Distance = distance;
        Rank = rank;
    }

    /// <summary>
    /// Convert to LabelledSubtitle for compatibility with existing code.
    /// </summary>
    public LabelledSubtitle ToLabelledSubtitle()
    {
        return new LabelledSubtitle
        {
            Series = Series,
            Season = Season,
            Episode = Episode,
            EpisodeName = EpisodeName,
            // SubtitleText not available in search results
        };
    }
}
```

### 4. ModelInfo

**Purpose**: Metadata about loaded ONNX model and tokenizer.

```csharp
namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Information about the loaded embedding model and tokenizer.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Model name (e.g., "all-MiniLM-L6-v2").
    /// </summary>
    public string ModelName { get; init; }

    /// <summary>
    /// Model version or variant (e.g., "fp16", "int8").
    /// </summary>
    public string Variant { get; init; }

    /// <summary>
    /// Embedding dimension (should be 384).
    /// </summary>
    public int Dimension { get; init; }

    /// <summary>
    /// File path to ONNX model.
    /// </summary>
    public string ModelPath { get; init; }

    /// <summary>
    /// File path to tokenizer JSON.
    /// </summary>
    public string TokenizerPath { get; init; }

    /// <summary>
    /// SHA256 hash of model file for verification.
    /// </summary>
    public string? Sha256Hash { get; init; }

    /// <summary>
    /// Model file size in bytes.
    /// </summary>
    public long ModelSizeBytes { get; init; }

    /// <summary>
    /// When model was downloaded or last verified.
    /// </summary>
    public DateTime LastVerified { get; init; }

    public ModelInfo(
        string modelName,
        string variant,
        int dimension,
        string modelPath,
        string tokenizerPath,
        string? sha256Hash = null,
        long modelSizeBytes = 0,
        DateTime? lastVerified = null)
    {
        ModelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        Variant = variant ?? throw new ArgumentNullException(nameof(variant));
        Dimension = dimension;
        ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        TokenizerPath = tokenizerPath ?? throw new ArgumentNullException(nameof(tokenizerPath));
        Sha256Hash = sha256Hash;
        ModelSizeBytes = modelSizeBytes;
        LastVerified = lastVerified ?? DateTime.UtcNow;
    }
}
```

### 5. EmbeddingMatchThresholds

**Purpose**: Configuration model for embedding matching thresholds per subtitle format.

```csharp
namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Embedding-based matching thresholds for a specific subtitle format.
/// </summary>
public class EmbeddingFormatThresholds
{
    /// <summary>
    /// Minimum cosine similarity (0.0-1.0) to consider a match candidate.
    /// Higher = stricter matching.
    /// </summary>
    public double EmbedSimilarity { get; set; } = 0.85;

    /// <summary>
    /// Minimum confidence (0.0-1.0) to report a match.
    /// Should be >= EmbedSimilarity.
    /// </summary>
    public double MatchConfidence { get; set; } = 0.70;

    /// <summary>
    /// Minimum confidence (0.0-1.0) to auto-rename files.
    /// Should be >= MatchConfidence.
    /// </summary>
    public double RenameConfidence { get; set; } = 0.80;

    /// <summary>
    /// Validate threshold values are within valid ranges.
    /// </summary>
    public void Validate()
    {
        if (EmbedSimilarity < 0.0 || EmbedSimilarity > 1.0)
        {
            throw new ArgumentException("EmbedSimilarity must be between 0.0 and 1.0", nameof(EmbedSimilarity));
        }

        if (MatchConfidence < 0.0 || MatchConfidence > 1.0)
        {
            throw new ArgumentException("MatchConfidence must be between 0.0 and 1.0", nameof(MatchConfidence));
        }

        if (RenameConfidence < 0.0 || RenameConfidence > 1.0)
        {
            throw new ArgumentException("RenameConfidence must be between 0.0 and 1.0", nameof(RenameConfidence));
        }

        if (MatchConfidence < EmbedSimilarity)
        {
            throw new ArgumentException("MatchConfidence must be >= EmbedSimilarity", nameof(MatchConfidence));
        }

        if (RenameConfidence < MatchConfidence)
        {
            throw new ArgumentException("RenameConfidence must be >= MatchConfidence", nameof(RenameConfidence));
        }
    }
}

/// <summary>
/// Embedding matching thresholds configuration per subtitle format.
/// </summary>
public class EmbeddingMatchThresholds
{
    /// <summary>
    /// Thresholds for text-based subtitles (SRT, ASS, WebVTT).
    /// </summary>
    public EmbeddingFormatThresholds TextBased { get; set; } = new()
    {
        EmbedSimilarity = 0.85,
        MatchConfidence = 0.70,
        RenameConfidence = 0.80
    };

    /// <summary>
    /// Thresholds for PGS subtitles (Blu-ray).
    /// </summary>
    public EmbeddingFormatThresholds PGS { get; set; } = new()
    {
        EmbedSimilarity = 0.80,
        MatchConfidence = 0.60,
        RenameConfidence = 0.70
    };

    /// <summary>
    /// Thresholds for VobSub subtitles (DVD).
    /// </summary>
    public EmbeddingFormatThresholds VobSub { get; set; } = new()
    {
        EmbedSimilarity = 0.75,
        MatchConfidence = 0.50,
        RenameConfidence = 0.60
    };

    /// <summary>
    /// Get thresholds for a specific subtitle format.
    /// </summary>
    public EmbeddingFormatThresholds GetThresholds(SubtitleFormat format)
    {
        return format switch
        {
            SubtitleFormat.Text => TextBased,
            SubtitleFormat.PGS => PGS,
            SubtitleFormat.VobSub => VobSub,
            _ => TextBased
        };
    }

    /// <summary>
    /// Validate all threshold configurations.
    /// </summary>
    public void Validate()
    {
        TextBased.Validate();
        PGS.Validate();
        VobSub.Validate();
    }
}
```

---

## Entity Relationships

```
┌────────────────────────────────┐
│   SubtitleHashes (SQLite)      │
│  ────────────────────────────  │
│  Id (PK)                       │
│  Series, Season, Episode       │
│  CleanText                     │
│  Embedding (BLOB)         ────┐│
│  SubtitleFormat (Text/PGS/     ││
│                  VobSub)       ││
└────────────────────────────────┘│
                                  │
         ┌────────────────────────┘
         │
         ▼
┌────────────────────────────────┐
│  vector_index (vectorlite)     │
│  ────────────────────────────  │
│  rowid (FK to SubtitleHashes)  │
│  embedding (float32[384])      │
│  [HNSW index for fast search]  │
└────────────────────────────────┘
         │
         │ knn_search()
         ▼
┌────────────────────────────────┐
│  VectorSimilarityResult        │
│  ────────────────────────────  │
│  Id, Series, Season, Episode   │
│  Similarity (cosine)           │
│  Confidence (adjusted)         │
│  Format                        │
└────────────────────────────────┘
```

---

## Migration Strategy

### Phase 1: Schema Update

```csharp
public void MigrateSchema_013(SqliteConnection connection)
{
    using var command = connection.CreateCommand();

    // Add Embedding column
    command.CommandText = @"
        ALTER TABLE SubtitleHashes 
        ADD COLUMN Embedding BLOB NULL;";
    ExecuteIfColumnNotExists(command, "SubtitleHashes", "Embedding");

    // Add SubtitleFormat column
    command.CommandText = @"
        ALTER TABLE SubtitleHashes 
        ADD COLUMN SubtitleFormat TEXT NOT NULL DEFAULT 'Text';";
    ExecuteIfColumnNotExists(command, "SubtitleHashes", "SubtitleFormat");

    // Create index on SubtitleFormat
    command.CommandText = @"
        CREATE INDEX IF NOT EXISTS idx_subtitle_format 
        ON SubtitleHashes(SubtitleFormat);";
    command.ExecuteNonQuery();

    _logger.LogInformation("Schema migration 013 complete");
}
```

### Phase 2: Populate Embeddings

```csharp
public async Task PopulateEmbeddings(int maxConcurrency = 4)
{
    // Get all records without embeddings
    var records = await GetRecordsWithoutEmbeddings();
    
    _logger.LogInformation("Populating embeddings for {Count} records", records.Count);
    
    // Process in parallel batches (see research.md for full implementation)
    await ProcessInBatches(records, maxConcurrency);
    
    _logger.LogInformation("Embedding population complete");
}
```

### Phase 3: Create vectorlite Index

```csharp
public void CreateVectorIndex(SqliteConnection connection)
{
    using var command = connection.CreateCommand();

    // Load vectorlite extension
    LoadVectorliteExtension(connection);

    // Create virtual table
    command.CommandText = @"
        CREATE VIRTUAL TABLE IF NOT EXISTS vector_index 
        USING vectorlite(
            embedding float32[384] cosine,
            hnsw(
                max_elements=10000,
                ef_construction=200,
                M=16,
                random_seed=42,
                allow_replace_deleted=true
            )
        );";
    command.ExecuteNonQuery();

    // Populate from SubtitleHashes
    command.CommandText = @"
        INSERT INTO vector_index(rowid, embedding)
        SELECT Id, Embedding 
        FROM SubtitleHashes 
        WHERE Embedding IS NOT NULL;";
    command.ExecuteNonQuery();

    _logger.LogInformation("Vector index created and populated");
}
```

---

## Configuration Schema

### Updated episodeidentifier.config.json

```json
{
  "embeddingModel": {
    "autoDownload": true,
    "modelPath": null,
    "tokenizerPath": null,
    "sha256": "abc123...",
    "cacheDir": null
  },
  "matchingStrategy": "embedding",
  "embeddingFallback": true,
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
  },
  "maxConcurrency": 4
}
```

---

## Data Validation Rules

### Embedding Validation

1. **Dimension**: Must be exactly 384 floats (1536 bytes)
2. **Format**: float32 (4 bytes per element)
3. **Normalization**: Vector magnitude should be ~1.0 (normalized by model)
4. **Non-null**: Required for vector search operations

### Format Validation

1. **Values**: Must be "Text", "PGS", or "VobSub"
2. **Case-insensitive**: Stored as proper case in database
3. **Default**: "Text" for existing records
4. **Required**: NOT NULL constraint

### Threshold Validation

1. **Range**: All thresholds must be 0.0-1.0
2. **Ordering**: embedSimilarity ≤ matchConfidence ≤ renameConfidence
3. **Format-specific**: Each format has independent thresholds
4. **Validation**: Enforced at configuration load time

---

## Performance Considerations

### Storage

- **Embedding Size**: 1536 bytes per record (384 × 4 bytes)
- **Database Growth**: 300 records = ~450KB, 1000 records = ~1.5MB
- **Index Overhead**: vectorlite HNSW index = ~2-3× embedding size

### Query Performance

- **Vector Search**: O(log N) with HNSW index
- **Target**: <2 seconds for 1000 entries
- **Tuning**: Adjust `ef` parameter at query time for speed/accuracy tradeoff

### Memory Usage

- **ONNX Session**: ~100MB per instance
- **vectorlite Index**: Loaded in memory
- **Total**: <500MB during batch processing

---

**Data Model Complete**: Ready for contract generation and quickstart documentation.
