using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models;

public class FileRenameRequest
{
    [Required]
    public string OriginalPath { get; set; } = string.Empty;

    [Required]
    public string SuggestedFilename { get; set; } = string.Empty;

    public bool ForceOverwrite { get; set; } = false;
}
