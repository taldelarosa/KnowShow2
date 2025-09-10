# Legacy Code Cleanup - Removed --sub-db Parameter

## What Was Removed

### CLI Parameter

- **`--sub-db`** - "Path to root of known subtitles (Subtitles=>Series=>Season)"
- This parameter was originally designed for file-based subtitle comparison
- **Never actually used** in the current implementation

### Code Changes

1. **Program.cs** - Removed subDbOption and validation logic
2. **HandleCommand signature** - Removed DirectoryInfo subDb parameter  
3. **Documentation** - Updated all references in README.md, quickstart.md, cli-contract.md

## Why This Was Safe

### Current Architecture Uses Only SQLite

- All subtitle matching happens via **fuzzy hash comparison** in SQLite database
- The `--hash-db` parameter is the **only** database actually used
- File-based subtitle directory structure is **completely bypassed**

### No Functional Impact

- **Before cleanup**: Required dummy directory (`--sub-db "/tmp/dummy_subdb"`)
- **After cleanup**: Streamlined command (`--input video.mkv --hash-db test_hashes.db`)
- **Same results**: Perfect episode identification with 89% confidence

## Benefits

### Simplified User Experience

```bash
# Before (confusing - why do I need both?)
dotnet run -- --input video.mkv --sub-db /dummy --hash-db hashes.db

# After (clear and intuitive)
dotnet run -- --input video.mkv --hash-db hashes.db
```

### Cleaner Architecture

- Removed unused parameter validation
- Eliminated confusing legacy references
- Focused CLI on actual functionality

### Better Documentation

- Updated help text reflects actual usage
- Removed misleading parameter descriptions
- Aligned docs with implementation reality

## Testing Confirmed

✅ Episode identification still works perfectly  
✅ 89% match confidence maintained  
✅ pgsrip integration unaffected  
✅ All core functionality preserved  

The cleanup successfully removed **technical debt** without impacting any **production functionality**.
