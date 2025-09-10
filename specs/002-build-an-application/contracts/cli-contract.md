# Contract: CLI Input/Output for Episode Identification

## CLI Arguments

- `--input <video-file>`: Path to AV1 video file
- `--hash-db <sqlite-db>`: Path to SQLite database for fuzzy hashes
- `--language <lang>`: Preferred subtitle language (default: eng)

## Input Example

```sh
./identify-episode --input "MyShow_S01E02.mkv" --hash-db "./hashes.sqlite"
```

## Output JSON Schema

```
{
  "series": "string",
  "season": "string",
  "episode": "string",
  "matchConfidence": 0.97,
  "ambiguityNotes": "string (optional)",
  "error": {
    "code": "string (optional)",
    "message": "string (optional)"
  }
}
```

## Error Output Example

```
{
  "error": {
    "code": "NO_SUBTITLES_FOUND",
    "message": "No PGS subtitles could be extracted from the video file."
  }
}
```

## Ambiguity Output Example

```
{
  "series": "MyShow",
  "season": "01",
  "episode": null,
  "matchConfidence": 0.65,
  "ambiguityNotes": "Multiple episodes have similar subtitles. Manual review required.",
  "error": null
}
```

---
