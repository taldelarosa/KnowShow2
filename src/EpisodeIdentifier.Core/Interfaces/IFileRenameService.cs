using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service interface for performing safe file rename operations with error handling and validation.
/// </summary>
public interface IFileRenameService
{
    /// <summary>
    /// Renames a file to the suggested filename with comprehensive error handling.
    /// </summary>
    /// <param name="request">File rename request with original path and target filename</param>
    /// <returns>Result indicating success/failure with error details</returns>
    Task<FileRenameResult> RenameFileAsync(FileRenameRequest request);

    /// <summary>
    /// Validates whether a file can be renamed (exists, not locked, has permissions).
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if file can be renamed, false otherwise</returns>
    bool CanRenameFile(string filePath);

    /// <summary>
    /// Generates the full target path by combining the original file's directory
    /// with the suggested filename.
    /// </summary>
    /// <param name="originalPath">Original file path</param>
    /// <param name="suggestedFilename">Suggested filename (name only, not full path)</param>
    /// <returns>Full path to the target location</returns>
    string GetTargetPath(string originalPath, string suggestedFilename);
}
