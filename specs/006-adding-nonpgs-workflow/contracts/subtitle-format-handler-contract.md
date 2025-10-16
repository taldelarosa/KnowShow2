# SubtitleFormatHandler Contract


## Interface: ISubtitleFormatHandler


### SupportedFormat


**Purpose**: Identify which subtitle format this handler supports.

**Signature**:

```csharp
SubtitleFormat SupportedFormat { get; }
```


**Output**:

- SubtitleFormat enum value for this handler
- Must be consistent across all calls

---

### CanHandle


**Purpose**: Determine if this handler can process the given track format.

**Signature**:

```csharp
bool CanHandle(SubtitleFormat format)
```


**Input**:

- `format`: Subtitle format to check

**Output**:

- `true` if handler supports the format
- `false` otherwise

**Contract Tests**:

1. Returns true for SupportedFormat
2. Returns false for other formats
3. Consistent results across multiple calls

---

### ParseSubtitleText


**Purpose**: Parse raw subtitle file content into clean dialogue text.

**Signature**:

```csharp
Task&lt;string&gt; ParseSubtitleTextAsync(
    Stream subtitleData,
    string encoding = "UTF-8")
```


**Input**:

- `subtitleData`: Raw subtitle file content stream
- `encoding`: Character encoding (default UTF-8)

**Output**:

- Cleaned dialogue text with timestamps and formatting removed
- Empty string if no dialogue found
- Never returns null

**Errors**:

- `ArgumentNullException`: subtitleData is null
- `NotSupportedException`: Unsupported encoding
- `InvalidDataException`: Malformed subtitle data

**Contract Tests**:

1. Valid SRT content returns clean dialogue text
2. Valid ASS content returns dialogue events only
3. Valid VTT content returns cue text only
4. Empty content returns empty string
5. Invalid encoding throws NotSupportedException
6. Malformed data throws InvalidDataException

## Specific Format Handler Contracts


### SrtFormatHandler


**Pattern Recognition**:

- Sequence number lines (integers)
- Timestamp lines (HH:MM:SS,mmm --> HH:MM:SS,mmm)
- Text content lines
- Blank line separators

**Parsing Rules**:

- Extract only text content lines
- Remove HTML-like tags (&lt;b&gt;, &lt;i&gt;, etc.)
- Preserve line breaks within subtitles
- Skip malformed sequences

### AssFormatHandler


**Pattern Recognition**:

- [Script Info] section
- [V4+ Styles] section
- [Events] section with Format: and Dialogue: lines

**Parsing Rules**:

- Process only Dialogue lines from Events section
- Extract Text field (last comma-separated value)
- Remove ASS override codes (\\N, \\h, etc.)
- Handle both ASS and SSA variants

### VttFormatHandler


**Pattern Recognition**:

- WEBVTT header line
- Optional NOTE blocks
- Cue blocks with timestamps
- Cue text content

**Parsing Rules**:

- Skip WEBVTT header and NOTE blocks
- Extract text from cue blocks only
- Remove cue settings (&lt;v speaker&gt;, etc.)
- Handle nested timestamps
