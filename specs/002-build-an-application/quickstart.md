# Quickstart: Identify Season and Episode from AV1 Video via PGS Subtitle Comparison (CLI, JSON Output)

## Prerequisites

- Linux environment
- C# runtime (dotnet)
- Standalone utilities: ffmpeg, mkvextract, sqlite3, fuzzy hash tool (e.g., ssdeep)
- Known subtitles stored as text files in Subtitles=>Series=>Season
- SQLite database for fuzzy hashes

## Steps

1. Place AV1 video file in working directory
2. Run CLI tool: `dotnet run -- --input <video-file> --hash-db <sqlite-db>`
3. Tool extracts PGS subtitles using pgsrip (enhanced) or FFmpeg (fallback)
4. Tool computes fuzzy hash of extracted subtitles
5. Tool compares hash to known hashes in SQLite DB
6. Tool outputs JSON with identified Series, Season, Episode, and confidence
7. If error or ambiguity, outputs JSON error/ambiguity structure

## Example

```sh
# Example command
./identify-episode --input "MyShow_S01E02.mkv" --hash-db "./hashes.sqlite"
```

## Output

- JSON object with fields: series, season, episode, matchConfidence, ambiguityNotes, error

---
