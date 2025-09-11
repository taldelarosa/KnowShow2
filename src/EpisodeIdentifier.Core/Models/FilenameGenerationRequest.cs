using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models;

public class FilenameGenerationRequest
{
    [Required]
    public string Series { get; set; } = string.Empty;
    
    [Required]
    public string Season { get; set; } = string.Empty;
    
    [Required] 
    public string Episode { get; set; } = string.Empty;
    
    public string? EpisodeName { get; set; }
    
    [Required]
    public string FileExtension { get; set; } = string.Empty;
    
    [Range(0.0, 1.0)]
    public double MatchConfidence { get; set; }
}
