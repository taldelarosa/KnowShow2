namespace EpisodeIdentifier.Core.Models;

public class LabelledSubtitle
{
    public string Series { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Episode { get; set; } = string.Empty;
    public string SubtitleText { get; set; } = string.Empty;
    public string FuzzyHash { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(SubtitleText);
}
