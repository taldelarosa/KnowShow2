# Contract Test: Fuzzy Hash Database

## Test: Hash Insert and Lookup

- Given: Known subtitle text file
- When: Fuzzy hash is computed and inserted into SQLite DB
- Then: DB contains correct hash and metadata (series, season, episode)

## Test: Hash Match

- Given: Extracted subtitle from video
- When: Fuzzy hash is computed and looked up in DB
- Then: Closest match is returned with confidence score

## Test: No Match Found

- Given: Subtitle with no close match in DB
- When: Fuzzy hash is computed and looked up
- Then: Output indicates no match (error or low confidence)

## Test: Ambiguous Match

- Given: Subtitle with multiple close matches in DB
- When: Fuzzy hash is computed and looked up
- Then: Output indicates ambiguity in result

---
