namespace EpisodeIdentifier.Core.Models;

public class PGSSubtitle
{
    public string Language { get; set; } = string.Empty;
    public SubtitleTiming Timing { get; set; } = new();
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class SubtitleTiming
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}
