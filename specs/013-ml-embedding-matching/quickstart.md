# Quickstart Guide: ML Embedding-Based Subtitle Matching

**Feature**: 013-ml-embedding-matching  
**Date**: October 19, 2025  
**Purpose**: Manual validation of embedding-based subtitle matching

---

## Prerequisites

- .NET 8.0 SDK installed
- Build completed: `dotnet build` in `/src/EpisodeIdentifier.Core`
- Test database with Criminal Minds entries
- Criminal Minds S06E19 VobSub file for validation

---

## Test Data Setup

### 1. Prepare Test Database

```bash
cd /mnt/c/Users/Ragma/KnowShow_Specd

# Use existing production database or create test database
DB_PATH="test_embeddings.db"
rm -f "$DB_PATH"

# Build application
cd src/EpisodeIdentifier.Core
dotnet build
```

### 2. Store Known Text Subtitle (Reference)

```bash
# Store Criminal Minds S06E19 text subtitle as reference
dotnet run -- --store \
  --input "/path/to/criminal_minds/S06E19.srt" \
  --series "Criminal Minds" \
  --season 6 \
  --episode 19 \
  --hash-db "../../$DB_PATH"

# Expected output:
# Subtitle stored successfully for Criminal Minds S06E19
# Format: Text
# Embedding generated: 384 dimensions
```

### 3. Verify Embedding Generated

```bash
# Check database has embedding
sqlite3 "$DB_PATH" "SELECT Id, Series, Season, Episode, SubtitleFormat, length(Embedding) as EmbedSize FROM SubtitleHashes;"

# Expected output:
# 1|Criminal Minds|06|19|Text|1536
# (1536 bytes = 384 floats × 4 bytes each)
```

---

## User Story Validation

### US-1: Match VobSub OCR to Text Database

**Goal**: VobSub subtitle for S06E19 matches stored text subtitle with >85% confidence

```bash
# Extract and identify VobSub subtitle
dotnet run -- --identify \
  --input "/path/to/criminal_minds/S06E19_vobsub.mkv" \
  --hash-db "../../$DB_PATH"

# Expected output (embedding-based):
# Processing VobSub subtitle (OCR extraction)...
# Generating embedding...
# Searching vector index...
# 
# Match found!
# Series: Criminal Minds
# Season: 06
# Episode: 19
# Format: Text (matched against VobSub)
# Similarity: 0.89 (cosine)
# Confidence: 87.5%
# Method: Embedding-based matching
```

**Success Criteria**:
- ✓ VobSub is extracted and OCR'd correctly
- ✓ Embedding is generated from OCR text
- ✓ Match found with confidence >85%
- ✓ Matches correct episode (S06E19)
- ✓ Reports "Embedding-based matching" method

### US-2: Migrate Existing Database

**Goal**: All existing entries get embeddings generated automatically

```bash
# Add more text subtitles without embeddings (simulate old database)
for ep in 01 02 03 04 05; do
  dotnet run -- --store \
    --input "/path/to/criminal_minds/S06E${ep}.srt" \
    --series "Criminal Minds" \
    --season 6 \
    --episode $ep \
    --hash-db "../../$DB_PATH" \
    --skip-embedding  # Simulate old entries
done

# Trigger migration (automatic on first run with new version)
dotnet run -- --migrate-embeddings \
  --hash-db "../../$DB_PATH" \
  --max-concurrency 4

# Expected output:
# Starting embedding migration...
# Found 5 records without embeddings
# Migration progress: 20% (1/5)
# Migration progress: 40% (2/5)
# Migration progress: 60% (3/5)
# Migration progress: 80% (4/5)
# Migration progress: 100% (5/5)
# Migration complete: 5 success, 0 failed
# Total time: ~25 seconds (5 × 5s each with 4 workers)
```

**Success Criteria**:
- ✓ Migration detects entries without embeddings
- ✓ Progress reporting shows percentage complete
- ✓ All entries get embeddings generated
- ✓ Migration time <30 seconds for 5 entries
- ✓ Database integrity maintained (no data loss)

### US-3: Configure Matching Thresholds

**Goal**: Threshold changes affect matching behavior

```bash
# Create test config with different thresholds
cat > episodeidentifier.config.json <<EOF
{
  "matchingStrategy": "embedding",
  "embeddingThresholds": {
    "vobSub": {
      "embedSimilarity": 0.95,
      "matchConfidence": 0.90,
      "renameConfidence": 0.95
    }
  }
}
EOF

# Run identification with strict thresholds
dotnet run -- --identify \
  --input "/path/to/criminal_minds/S06E19_vobsub.mkv" \
  --hash-db "../../$DB_PATH"

# Expected output (no match due to high threshold):
# Processing VobSub subtitle...
# Generating embedding...
# Searching vector index...
# No match found above threshold (embedSimilarity: 0.95)
# Best match: Criminal Minds S06E19 (similarity: 0.89)

# Reset to default thresholds
cat > episodeidentifier.config.json <<EOF
{
  "matchingStrategy": "embedding",
  "embeddingThresholds": {
    "vobSub": {
      "embedSimilarity": 0.75,
      "matchConfidence": 0.50,
      "renameConfidence": 0.60
    }
  }
}
EOF

# Run again with permissive thresholds
dotnet run -- --identify \
  --input "/path/to/criminal_minds/S06E19_vobsub.mkv" \
  --hash-db "../../$DB_PATH"

# Expected output (match found):
# Match found!
# Confidence: 87.5%
```

**Success Criteria**:
- ✓ High threshold rejects matches below threshold
- ✓ Low threshold accepts more matches
- ✓ Configuration hot-reloads without restart
- ✓ Invalid thresholds (>1.0 or <0.0) show error
- ✓ Per-format thresholds apply correctly

### US-4: Maintain CLI Compatibility

**Goal**: Existing CLI commands work unchanged

```bash
# Test --identify command (unchanged interface)
dotnet run -- --identify \
  --input "/path/to/file.mkv" \
  --hash-db "../../$DB_PATH"

# Test --store command (unchanged interface)
dotnet run -- --store \
  --input "/path/to/file.srt" \
  --series "Test Series" \
  --season 1 \
  --episode 1 \
  --hash-db "../../$DB_PATH"

# Test --bulk-identify command (unchanged interface)
dotnet run -- --bulk-identify \
  --input "/path/to/directory" \
  --hash-db "../../$DB_PATH"

# Test --help output (should show same commands)
dotnet run -- --help
```

**Success Criteria**:
- ✓ All existing commands work as before
- ✓ Output format remains JSON compatible
- ✓ --help shows same command structure
- ✓ No breaking changes to CLI interface
- ✓ Performance meets existing benchmarks

---

## Feature Validation Steps

### Step 1: Model Download

```bash
# First run should download model automatically
rm -rf ~/.config/EpisodeIdentifier/models/  # Clear cache

dotnet run -- --identify \
  --input "/path/to/file.mkv" \
  --hash-db "../../$DB_PATH"

# Expected output:
# Checking embedding model...
# Model not found in cache
# Downloading all-MiniLM-L6-v2 model (45MB)...
# Download progress: 10%
# Download progress: 20%
# ...
# Download progress: 100%
# Verifying SHA256 hash...
# Model download complete
# Model loaded successfully
# Processing file...
```

**Verification**:
```bash
# Check model cached locally
ls -lh ~/.config/EpisodeIdentifier/models/

# Expected output:
# all-MiniLM-L6-v2-fp16.onnx  (45MB)
# tokenizer.json               (470KB)
```

### Step 2: Embedding Generation

```bash
# Test embedding generation directly
sqlite3 "$DB_PATH" "SELECT CleanText FROM SubtitleHashes WHERE Id=1 LIMIT 1;" > test_text.txt

# Store new subtitle and verify embedding
dotnet run -- --store \
  --input test_text.txt \
  --series "Test" \
  --season 1 \
  --episode 1 \
  --hash-db "../../$DB_PATH" \
  --verbose

# Expected verbose output:
# Normalizing subtitle text...
# Generating embedding (384 dimensions)...
# Tokenization: 123 tokens
# ONNX inference time: 1.2s
# Mean pooling: 384 dimensions
# Embedding stored successfully
```

### Step 3: Vector Search

```bash
# Test vector search with known embedding
sqlite3 "$DB_PATH" <<EOF
-- Check vector_index table exists
SELECT name FROM sqlite_master WHERE type='table' AND name='vector_index';

-- Count indexed vectors
SELECT COUNT(*) FROM vector_index;

-- Test vectorlite info
SELECT vectorlite_info();
EOF

# Expected output:
# vector_index
# 6
# vectorlite v0.2.0 [SSE, AVX enabled]
```

### Step 4: Format-Specific Matching

```bash
# Store same episode in different formats
dotnet run -- --store \
  --input "/path/to/S06E19.srt" \
  --series "Criminal Minds" --season 6 --episode 19 \
  --hash-db "../../$DB_PATH"

dotnet run -- --store \
  --input "/path/to/S06E19.sup" \
  --series "Criminal Minds" --season 6 --episode 19 \
  --hash-db "../../$DB_PATH"

dotnet run -- --store \
  --input "/path/to/S06E19_vobsub.mkv" \
  --series "Criminal Minds" --season 6 --episode 19 \
  --hash-db "../../$DB_PATH"

# Verify all formats stored with embeddings
sqlite3 "$DB_PATH" "SELECT SubtitleFormat, COUNT(*) FROM SubtitleHashes GROUP BY SubtitleFormat;"

# Expected output:
# Text|1
# PGS|1
# VobSub|1

# Test cross-format matching (VobSub query → Text match)
dotnet run -- --identify \
  --input "/path/to/unknown_vobsub.mkv" \
  --hash-db "../../$DB_PATH"

# Should match against Text entry with high confidence
```

### Step 5: Performance Validation

```bash
# Benchmark embedding generation
time dotnet run -- --store \
  --input "/path/to/large_subtitle.srt" \
  --series "Test" --season 1 --episode 1 \
  --hash-db "../../$DB_PATH"

# Expected: <5 seconds total

# Benchmark vector search with 1000 entries
# (Requires populated database)
time dotnet run -- --identify \
  --input "/path/to/query_file.mkv" \
  --hash-db "large_database.db"  # 1000+ entries

# Expected: <2 seconds total
```

**Performance Targets**:
- Embedding generation: <5 seconds per subtitle
- Vector search: <2 seconds for 1000 entries
- Migration: <7 minutes for 300 entries (4 workers)
- Memory usage: <500MB during batch processing

---

## Troubleshooting

### Model Download Fails

```bash
# Check network connectivity
curl -I https://huggingface.co/

# Manual download fallback
wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_fp16.onnx
mkdir -p ~/.config/EpisodeIdentifier/models/
mv model_fp16.onnx ~/.config/EpisodeIdentifier/models/all-MiniLM-L6-v2-fp16.onnx

# Retry identification
dotnet run -- --identify --input "/path/to/file.mkv" --hash-db "../../$DB_PATH"
```

### vectorlite Extension Not Found

```bash
# Check extension file exists
ls -la vectorlite.so  # Linux
ls -la vectorlite.dll  # Windows

# Test extension loading
sqlite3 "$DB_PATH" <<EOF
.load ./vectorlite.so
SELECT vectorlite_info();
EOF

# If error, download from GitHub releases
wget https://github.com/1yefuwang1/vectorlite/releases/download/v0.2.0/vectorlite-linux-x64.so
mv vectorlite-linux-x64.so vectorlite.so
```

### Low Match Confidence

```bash
# Check embedding quality
sqlite3 "$DB_PATH" "SELECT Id, length(Embedding) FROM SubtitleHashes WHERE Embedding IS NULL;"

# If embeddings missing, run migration
dotnet run -- --migrate-embeddings --hash-db "../../$DB_PATH"

# Adjust thresholds for OCR formats
cat > episodeidentifier.config.json <<EOF
{
  "embeddingThresholds": {
    "vobSub": {
      "embedSimilarity": 0.70,
      "matchConfidence": 0.45
    }
  }
}
EOF
```

---

## Cleanup

```bash
# Remove test database
rm -f test_embeddings.db

# Clear model cache (optional)
rm -rf ~/.config/EpisodeIdentifier/models/

# Reset configuration
rm -f episodeidentifier.config.json
```

---

## Success Checklist

### Criminal Minds S06E19 Validation

- [ ] Text subtitle stored successfully (Format: Text)
- [ ] Embedding generated (1536 bytes / 384 dimensions)
- [ ] VobSub file extracted and OCR'd
- [ ] VobSub embedding generated
- [ ] Vector search finds text subtitle entry
- [ ] Confidence score >85%
- [ ] Correct episode identified (S06E19)
- [ ] Embedding-based method logged

### Migration Validation

- [ ] 5 subtitles stored without embeddings
- [ ] Migration command detects 5 entries
- [ ] Progress logging shows 20%, 40%, 60%, 80%, 100%
- [ ] All embeddings generated successfully
- [ ] Migration time <30 seconds
- [ ] No database errors or data loss

### Configuration Validation

- [ ] High threshold (0.95) rejects match
- [ ] Low threshold (0.75) accepts match
- [ ] Configuration hot-reload works
- [ ] Invalid thresholds show error message
- [ ] Per-format thresholds apply correctly

### CLI Compatibility

- [ ] --identify command works unchanged
- [ ] --store command works unchanged
- [ ] --bulk-identify command works unchanged
- [ ] --help shows same commands
- [ ] JSON output format unchanged

### Performance

- [ ] Embedding generation: <5 seconds
- [ ] Vector search: <2 seconds (1000 entries)
- [ ] Memory usage: <500MB
- [ ] Model download: <60 seconds

---

**Quickstart Complete**: All user stories validated manually. Ready for automated testing.
