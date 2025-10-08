namespace EpisodeIdentifier.Core.Models;

public class VideoFile
{
    public string FileName { get; set; } = string.Empty;
    public string EncodingType { get; set; } = string.Empty;
    public List<PGSSubtitle> EmbeddedSubtitles { get; set; } = new();

    public bool IsAV1Encoded => EncodingType.Equals("AV1", StringComparison.OrdinalIgnoreCase);
}
