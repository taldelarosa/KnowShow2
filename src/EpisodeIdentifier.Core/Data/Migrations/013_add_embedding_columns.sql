-- Migration: 013-ml-embedding-matching
-- Description: Add embedding storage and source format tracking for ML-based subtitle matching
-- Date: October 19, 2025

-- Add embedding storage column (1536 bytes = 384 floats × 4 bytes each)
-- NULL allowed for backward compatibility with existing entries
ALTER TABLE SubtitleHashes ADD COLUMN Embedding BLOB NULL;

-- Add subtitle source format tracking column
-- Used for applying format-specific confidence thresholds
-- DEFAULT 'Text' for backward compatibility with existing entries
ALTER TABLE SubtitleHashes ADD COLUMN SubtitleSourceFormat TEXT NOT NULL DEFAULT 'Text';

-- Create vectorlite virtual table for fast similarity search
-- This table will be populated by VectorSearchService.RebuildIndex()
-- HNSW parameters optimized for ~1000-10000 entries:
--   - max_elements: Maximum number of vectors (10000)
--   - ef_construction: Construction-time search depth (200, higher = more accurate index)
--   - M: Number of connections per vector (48, higher = faster search but more memory)
CREATE VIRTUAL TABLE IF NOT EXISTS vector_index USING vectorlite(
    embedding float32[384],
    hnsw(max_elements=10000, ef_construction=200, M=48)
);

-- Note: Existing entries can be backfilled with embeddings using:
-- dotnet run -- --migrate-embeddings --hash-db "path/to/database.db"
