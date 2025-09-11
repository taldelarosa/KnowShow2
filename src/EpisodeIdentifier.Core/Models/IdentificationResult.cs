namespace EpisodeIdentifier.Core.Models;

public class IdentificationResult
{
    public string? Series { get; set; }
    public string? Season { get; set; }
    public string? Episode { get; set; }
    public string? EpisodeName { get; set; }
    public double MatchConfidence { get; set; }
    public string? AmbiguityNotes { get; set; }
    public IdentificationError? Error { get; set; }
    
    /// <summary>
    /// Suggested filename for high confidence episode identifications.
    /// Format: "SeriesName - S01E01 - EpisodeName.ext"
    /// Only populated when MatchConfidence >= 0.9
    /// </summary>
    public string? SuggestedFilename { get; set; }

    /// <summary>
    /// Indicates whether the video file was actually renamed using the suggested filename.
    /// True when the --rename flag was used and the rename operation succeeded.
    /// </summary>
    public bool FileRenamed { get; set; } = false;

    /// <summary>
    /// The original filename of the video file before renaming.
    /// Only populated when FileRenamed is true.
    /// </summary>
    public string? OriginalFilename { get; set; }

    public bool IsAmbiguous => MatchConfidence < 0.9 && !string.IsNullOrEmpty(AmbiguityNotes);
    public bool HasError => Error != null;
}

public class IdentificationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static IdentificationError NoSubtitlesFound => new()
    {
        Code = "NO_SUBTITLES_FOUND",
        Message = "No PGS subtitles could be extracted from the video file."
    };

    public static IdentificationError UnsupportedFileType => new()
    {
        Code = "UNSUPPORTED_FILE_TYPE",
        Message = "The provided file is not AV1 encoded."
    };

    public static IdentificationError UnsupportedLanguage => new()
    {
        Code = "UNSUPPORTED_LANGUAGE",
        Message = "The subtitle language is not supported."
    };

    public static IdentificationError NoMatchesFound => new()
    {
        Code = "NO_MATCHES_FOUND",
        Message = "No matching episodes found in the database with sufficient confidence."
    };
}
