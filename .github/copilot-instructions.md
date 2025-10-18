# KnowShow_Specd Development Guidelines


Auto-generated from all feature plans. Last updated: 2025-09-15

## Active Technologies


- C# .NET 8.0, System.CommandLine, Microsoft.Extensions.Logging, System.Text.Json
- SQLite database for hash storage, JSON configuration with hot-reload
- xUnit testing framework with TDD approach
- (008-fuzzy-hashing-plus) CTPH fuzzy hashing for subtitle matching
- (010-async-processing-where) Configurable concurrent episode identification
- (012-process-dvd-subtitle) DVD subtitle (VobSub) OCR support with mkvextract + Tesseract
  - VobSubExtractor: Extracts .idx/.sub files from MKV containers
  - VobSubOcrService: Performs OCR on VobSub subtitle images
  - Subtitle priority: Text > PGS > DVD

## Project Structure


```
src/
  EpisodeIdentifier.Core/
    Models/
      BulkProcessingOptions.cs          # Contains MaxConcurrency property
      VobSubExtractionResult.cs         # DVD subtitle extraction results
      VobSubOcrResult.cs                # DVD subtitle OCR results
      Configuration/                    # App configuration models
    Services/                           # Core business logic
      VobSubExtractor.cs                # DVD subtitle extraction service
      VobSubOcrService.cs               # DVD subtitle OCR service
    Interfaces/
      IVobSubExtractor.cs               # DVD subtitle extraction contract
      IVobSubOcrService.cs              # DVD subtitle OCR contract
    Program.cs                          # CLI entry point
tests/
  unit/
  integration/
  contract/
    VobSubExtractorContractTests.cs     # VobSub extractor tests
    VobSubOcrServiceContractTests.cs    # VobSub OCR tests
specs/
  010-async-processing-where/  # Previous feature documentation
    plan.md
    research.md
    data-model.md
    quickstart.md
    contracts/
  012-process-dvd-subtitle/    # Current feature: DVD subtitle OCR
    spec.md                     # Feature specification
    plan.md                     # Implementation plan
    research.md                 # Phase 0: Technical decisions
    data-model.md               # Phase 1: VobSubExtractionResult, VobSubOcrResult
    quickstart.md               # Manual testing guide
    contracts/
      vobsub-extractor.json     # IVobSubExtractor contract
      vobsub-ocr.json           # IVobSubOcrService contract
```


## Commands


### Bulk Processing


- `--bulk-identify <directory>` - Process multiple files with configurable concurrency
- Reads maxConcurrency from episodeidentifier.config.json (default: 1, range: 1-100)
- Supports hot-reload of configuration during processing

### Subtitle Processing


- Text subtitles (SRT, ASS, WebVTT): Processed directly (Priority 1)
- PGS subtitles: OCR using pgsrip + Tesseract (Priority 2)
- DVD subtitles (VobSub): Conversion using vobsub2srt tool (Priority 3)
- Requires: mkvextract (mkvtoolnix), vobsub2srt, tesseract-ocr for DVD subtitle support

## Code Style


: Follow standard conventions

## Recent Changes


- 008-fuzzy-hashing-plus: Added CTPH fuzzy hashing for subtitle matching
- 010-async-processing-where: Implemented configurable concurrent episode identification
- 012-process-dvd-subtitle: Added DVD subtitle (VobSub) OCR support with VobSubExtractor and VobSubOcrService

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
