namespace EpisodeIdentifier.Core.Models;

public class SubtitleTrackInfo
{
    public int Index { get; set; }
    public string CodecName { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Title { get; set; }
}
