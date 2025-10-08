# Research: Identify Season and Episode from AV1 Video via PGS Subtitle Comparison (CLI, JSON Output)


## Unresolved Clarifications


## Technology Choices


- **Language**: C# (custom code)
- **Video Processing**: Standalone utilities (e.g., ffmpeg, mkvextract)
- **Subtitle Extraction**: Standalone utilities (e.g., mkvextract, ffmpeg)
- **Subtitle Comparison**: Fuzzy hashing (e.g., ssdeep, TLSH, or custom C# logic)
- **Subtitle Storage**: Text files on local file system or mounted share
- **Fuzzy Hash Storage**: Local SQLite database for portability and simplicity

## Best Practices & Patterns


- Use CLI-only, non-interactive design for automation
- Always output JSON (success, error, ambiguous)
- Use structured error codes and messages in JSON
- Prefer open, portable formats (text, SQLite)
- Use existing, well-supported utilities for video/subtitle extraction
- Ensure all dependencies are available on target platform (Linux)

## Alternatives Considered


- Using a centralized server/database for subtitle storage (rejected for portability)
- Implementing all logic in C# (rejected for video/subtitle extraction due to complexity and reliability of existing tools)
- Using a remote database for fuzzy hashes (rejected for simplicity and offline use)

## Decisions


- Use C# for orchestration and custom logic
- Use standalone utilities for video/subtitle extraction
- Store known subtitles as text files in Subtitles=>Series=>Season
- Store fuzzy hashes in local SQLite
- Output all results as JSON

## Open Questions


- [ ] Confirm minimum match threshold for fuzzy comparison
- [ ] Confirm JSON schema for output (fields, error structure)
- [ ] Confirm handling of ambiguous/partial matches
- [ ] Confirm supported subtitle languages
- [ ] Confirm rejection/processing of non-AV1 files

---
