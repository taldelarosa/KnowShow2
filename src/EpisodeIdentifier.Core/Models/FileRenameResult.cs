namespace EpisodeIdentifier.Core.Models;

public class FileRenameResult
{
    public bool Success { get; set; }
    
    public string? NewPath { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public FileRenameError? ErrorType { get; set; }
}
