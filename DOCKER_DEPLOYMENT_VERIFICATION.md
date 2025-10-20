# Docker Deployment Verification for Feature 013

**Date:** 2025-10-19  
**Feature:** ML Embedding-Based Matching (013-ml-embedding-matching)

## Overview

This document verifies that all ML components and dependencies added in Feature 013 are properly configured for Docker deployment.

## Docker Environment Checklist

### ✅ System Dependencies (Dockerfile)

All required system packages are installed in the Docker image:

| Package | Purpose | Status |
|---------|---------|--------|
| `ffmpeg` | Video/audio processing | ✅ Present |
| `mkvtoolnix` (mkvextract) | MKV container manipulation | ✅ Present |
| `tesseract-ocr` + language packs | OCR for PGS/VobSub subtitles | ✅ Present |
| `vobsub2srt` | **NEW** - DVD subtitle OCR conversion | ✅ **Added in this commit** |
| `python3` + `pip` | Python runtime for pgsrip | ✅ Present |
| `pgsrip` | PGS subtitle extraction | ✅ Present |
| `sqlite3` | Database operations | ✅ Present |

### ✅ Native Binaries (vectorlite Extension)

**Issue Discovered:** Vectorlite binaries were added during runtime error fixes (commit acaf9fc) but not configured for MSBuild publish, meaning they wouldn't be included in Docker builds.

**Resolution:** Added `<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>` to .csproj ItemGroup

**Files Configured:**

- `src/EpisodeIdentifier.Core/native/vectorlite.so` (3.3 MB) - Linux x64
- `src/EpisodeIdentifier.Core/native/vectorlite.dll` (1.5 MB) - Windows x64

**Verification:**

```bash
# Build verification
$ ls -lh src/EpisodeIdentifier.Core/bin/Release/net8.0/vectorlite*
-rwxrwxrwx 1.5M vectorlite.dll
-rwxrwxrwx 3.3M vectorlite.so

# Publish verification
$ dotnet publish --configuration Release -o /tmp/test
$ ls -lh /tmp/test/vectorlite*
-rwxrwxrwx 1.5M vectorlite.dll
-rwxrwxrwx 3.3M vectorlite.so
```

**Status:** ✅ Both binaries copy correctly to publish output

### ✅ .NET NuGet Packages

All ML-related packages are properly referenced:

| Package | Version | Purpose | Status |
|---------|---------|---------|--------|
| `Microsoft.ML.OnnxRuntime` | 1.16.3 | ONNX model inference | ✅ Present |
| `Microsoft.ML.Tokenizers` | 1.0.2 | BPE tokenization (sentence transformers) | ✅ Present |

**Note:** During troubleshooting, accidentally changed Tokenizers version to `0.21.0-preview.23511.1` which caused build errors. Reverted to `1.0.2`.

### ✅ Runtime Model Downloads

The all-MiniLM-L6-v2 ONNX model (~86MB) is **NOT** included in the Docker image. It downloads automatically on first use via `ModelManager` service:

- Download URL: `https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx`
- Cache Location: `/root/.episodeidentifier/models/all-MiniLM-L6-v2/` (in Docker container)
- Tokenizer Vocab: `/root/.episodeidentifier/models/all-MiniLM-L6-v2/vocab.txt`

**Docker Implications:**

- First run will require internet access to download model
- Subsequent runs use cached model from volume mount or container state
- Consider pre-downloading in Docker build stage for offline deployments (future enhancement)

### ✅ Database Schema

SQLite database includes vector extension support:

| Component | Purpose | Status |
|-----------|---------|--------|
| `Embedding` BLOB column | Stores 384-dim float32 vectors | ✅ Present |
| `vector_index` virtual table | HNSW index for fast similarity search | ✅ Present |
| `vectorlite` extension | SQLite vector distance functions | ✅ Binaries configured for Docker |

### ✅ Configuration Files

The `episodeidentifier.config.json` includes ML-related settings:

```json
{
  "matchingStrategy": "embedding",  // or "fuzzy", "hybrid"
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
  }
}
```

**Docker Volume Mount:** Configuration must be mounted as a volume for production use.

## Changes Made

### 1. Dockerfile

**Added vobsub2srt package:**

```dockerfile
RUN apt-get update && apt-get install -y \
    ...
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-jpn \
    # VobSub to SRT converter for DVD subtitles
    vobsub2srt \
    ...
```

### 2. EpisodeIdentifier.Core.csproj

**Added CopyToPublishDirectory for vectorlite binaries:**

```xml
<!-- Copy vectorlite native binaries to output directory -->
<ItemGroup>
  <None Include="native/vectorlite.so">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <Link>vectorlite.so</Link>
  </None>
  <None Include="native/vectorlite.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <Link>vectorlite.dll</Link>
  </None>
</ItemGroup>
```

**Why this matters:** Docker uses multi-stage build with `dotnet publish`. Without `CopyToPublishDirectory`, the vectorlite binaries wouldn't exist in the final Docker image, causing runtime failures.

## Runtime Path Discovery

`VectorSearchService.GetVectorliteExtensionPath()` checks multiple locations:

1. `{baseDir}/vectorlite.so` ← **Primary location** (where publish copies it)
2. `{baseDir}/external/vectorlite/vectorlite-linux-x64.so` ← Fallback
3. `{baseDir}/native/vectorlite.so` ← Development path

The publish configuration ensures binaries are in location #1 for Docker deployment.

## Testing Recommendations

### Pre-Deployment Tests

1. **Build Docker Image:**
   ```bash
   docker build -t episodeidentifier:test .
   ```

2. **Verify vectorlite binaries in image:**
   ```bash
   docker run --rm episodeidentifier:test ls -la /app/vectorlite*
   ```
   Expected output: `vectorlite.so` (3.3MB)

3. **Test ML embedding generation:**
   ```bash
   docker run --rm -v $(pwd)/episodeidentifier.config.json:/app/episodeidentifier.config.json \
     episodeidentifier:test --migrate-embeddings
   ```

4. **Test episode identification with embeddings:**
   ```bash
   docker run --rm \
     -v $(pwd)/episodeidentifier.config.json:/app/episodeidentifier.config.json \
     -v $(pwd)/test_files:/media \
     episodeidentifier:test --identify /media/test_episode.mkv
   ```

### Expected Behaviors

- ✅ First run downloads ONNX model (~86MB) to `/root/.episodeidentifier/models/`
- ✅ VectorSearchService loads vectorlite extension from `/app/vectorlite.so`
- ✅ EmbeddingService generates 384-dim embeddings via ONNX Runtime
- ✅ VobSub OCR uses `vobsub2srt` for DVD subtitle conversion
- ✅ PGS OCR uses `pgsrip` + `tesseract` for image-based subtitle conversion

## Known Limitations

1. **Model Download on First Run:** The ONNX model (~86MB) downloads on first use. Offline deployments need pre-cached model in Docker image (future enhancement).

2. **Platform-Specific Binaries:** Only `vectorlite.so` (Linux) is used in Docker. The `vectorlite.dll` (Windows) is copied but unused.

3. **Model Cache Persistence:** Without volume mount for `/root/.episodeidentifier/`, the model re-downloads every container restart.

## Deployment Checklist

Before merging Feature 013:

- [x] vobsub2srt added to Dockerfile
- [x] vectorlite binaries configured for publish
- [x] Build succeeds with 0 errors
- [x] Publish includes vectorlite.so
- [ ] Docker image build test
- [ ] Docker runtime test with ML embedding matching
- [ ] Verify Criminal Minds S06E19 identification in Docker

## Conclusion

All ML components for Feature 013 are now properly configured for Docker deployment:

1. ✅ **System dependencies** (vobsub2srt added)
2. ✅ **Native binaries** (vectorlite configured for publish)
3. ✅ **.NET packages** (OnnxRuntime, Tokenizers)
4. ✅ **Runtime model downloads** (ModelManager handles ONNX model)
5. ✅ **Configuration** (embeddingThresholds in config.json)

The deployment blocker identified by the user ("vectorlite should be there from the start") has been resolved.

**Status:** Ready for Docker build testing and final deployment validation.
