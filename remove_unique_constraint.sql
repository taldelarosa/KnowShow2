-- Remove UNIQUE(Series, Season, Episode) constraint to allow multiple subtitle variants
-- This migration recreates the table without the inline UNIQUE constraint

BEGIN TRANSACTION;

-- Create new table without UNIQUE constraint
CREATE TABLE SubtitleHashes_New (
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
    CleanText TEXT DEFAULT ''
);

-- Copy all data
INSERT INTO SubtitleHashes_New SELECT * FROM SubtitleHashes;

-- Verify counts match
SELECT 'Old table count: ' || COUNT(*) FROM SubtitleHashes;
SELECT 'New table count: ' || COUNT(*) FROM SubtitleHashes_New;

-- Drop old table
DROP TABLE SubtitleHashes;

-- Rename new table
ALTER TABLE SubtitleHashes_New RENAME TO SubtitleHashes;

-- Recreate indexes
CREATE INDEX idx_series_season ON SubtitleHashes(Series, Season);
CREATE INDEX idx_clean_hash ON SubtitleHashes(CleanHash);
CREATE INDEX idx_original_hash ON SubtitleHashes(OriginalHash);
CREATE INDEX idx_hash_composite ON SubtitleHashes(OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash);

COMMIT;

-- Verify schema
.schema SubtitleHashes
