# Contract Test: CLI Output Contract

## Test: Successful Identification

- Given: AV1 video file with PGS subtitles matching known subtitle
- When: CLI tool is run with required arguments
- Then: Output JSON contains correct series, season, episode, matchConfidence > threshold, error = null

## Test: No Subtitles Found

- Given: AV1 video file with no PGS subtitles
- When: CLI tool is run
- Then: Output JSON contains error.code = NO_SUBTITLES_FOUND

## Test: Unsupported File Type

- Given: Non-AV1 video file
- When: CLI tool is run
- Then: Output JSON contains error.code = UNSUPPORTED_FILE_TYPE

## Test: Ambiguous Match

- Given: AV1 video file with subtitles matching multiple known episodes
- When: CLI tool is run
- Then: Output JSON contains ambiguityNotes and matchConfidence < threshold

## Test: Language Mismatch

- Given: AV1 video file with PGS subtitles in unsupported language
- When: CLI tool is run
- Then: Output JSON contains error.code = UNSUPPORTED_LANGUAGE

---
