namespace EpisodeIdentifier.Core.Models;

public class FilenameGenerationResult
{
    public string? SuggestedFilename { get; set; }

    public bool IsValid { get; set; }

    public string? ValidationError { get; set; }

    public int TotalLength { get; set; }

    public bool WasTruncated { get; set; }

    public List<string> SanitizedCharacters { get; set; } = new();
}
