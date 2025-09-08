# TextSubtitleExtractor Contract

## Interface: ITextSubtitleExtractor

### DetectTextSubtitleTracks
**Purpose**: Detect all text-based subtitle tracks in a video file.

**Signature**:
```csharp
Task&lt;List&lt;TextSubtitleTrack&gt;&gt; DetectTextSubtitleTracksAsync(string videoFilePath)
```

**Input**:
- `videoFilePath`: Absolute path to video file

**Output**:
- List of TextSubtitleTrack objects representing detected tracks
- Empty list if no text tracks found
- Never returns null

**Errors**:
- `FileNotFoundException`: Video file does not exist
- `ArgumentException`: Invalid file path
- `InvalidOperationException`: Video file is corrupted or unreadable

**Contract Tests**:
1. Valid video with SRT tracks returns non-empty list
2. Valid video with no text tracks returns empty list
3. Non-existent file throws FileNotFoundException
4. Null/empty path throws ArgumentException

---

### ExtractTextSubtitleContent
**Purpose**: Extract and clean text content from a specific subtitle track.

**Signature**:
```csharp
Task&lt;TextSubtitleContent&gt; ExtractTextSubtitleContentAsync(
    string videoFilePath, 
    TextSubtitleTrack track)
```

**Input**:
- `videoFilePath`: Absolute path to video file
- `track`: Subtitle track to extract

**Output**:
- TextSubtitleContent with extracted and cleaned text
- Never returns null

**Errors**:
- `FileNotFoundException`: Video file does not exist
- `ArgumentException`: Invalid track or file path
- `NotSupportedException`: Unsupported subtitle format
- `InvalidDataException`: Subtitle track is corrupted

**Contract Tests**:
1. Valid SRT track returns content with text and metadata
2. Valid ASS track returns content with dialogue text only
3. Valid VTT track returns content with cue text
4. Invalid track index throws ArgumentException
5. Corrupted subtitle data throws InvalidDataException

---

### TryExtractAllTextSubtitles
**Purpose**: Attempt to extract text from all tracks until successful match or exhaustion.

**Signature**:
```csharp
Task&lt;SubtitleProcessingResult&gt; TryExtractAllTextSubtitlesAsync(
    string videoFilePath, 
    CancellationToken cancellationToken = default)
```

**Input**:
- `videoFilePath`: Absolute path to video file
- `cancellationToken`: Optional cancellation support

**Output**:
- SubtitleProcessingResult with match results and metadata
- Never returns null

**Errors**:
- `FileNotFoundException`: Video file does not exist
- `OperationCanceledException`: Operation was cancelled
- `TimeoutException`: Processing exceeded reasonable time limit

**Contract Tests**:
1. Video with matching subtitles returns successful result
2. Video with non-matching subtitles returns unsuccessful result with all tracks attempted
3. Video with no text tracks returns result indicating no tracks processed
4. Cancellation token cancels operation appropriately
5. Very large subtitle files complete within timeout
