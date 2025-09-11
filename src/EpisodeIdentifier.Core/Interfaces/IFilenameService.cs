using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service interface for generating Windows-compatible filenames from episode identification data.
/// </summary>
public interface IFilenameService
{
    /// <summary>
    /// Generates a suggested filename for high confidence episode identifications.
    /// Format: "SeriesName - S01E01 - EpisodeName.ext"
    /// </summary>
    /// <param name="request">Filename generation request with episode data</param>
    /// <returns>Result containing suggested filename or validation errors</returns>
    FilenameGenerationResult GenerateFilename(FilenameGenerationRequest request);

    /// <summary>
    /// Sanitizes a string for Windows filesystem compatibility.
    /// Replaces invalid characters (< > : " | ? * \) with single spaces.
    /// </summary>
    /// <param name="input">Input string to sanitize</param>
    /// <returns>Sanitized string safe for Windows filenames</returns>
    string SanitizeForWindows(string input);

    /// <summary>
    /// Validates whether a filename is compatible with Windows filesystem.
    /// </summary>
    /// <param name="filename">Filename to validate</param>
    /// <returns>True if valid for Windows, false otherwise</returns>
    bool IsValidWindowsFilename(string filename);

    /// <summary>
    /// Truncates a filename to fit within the specified length limit.
    /// Preserves file extension and essential format structure.
    /// </summary>
    /// <param name="filename">Filename to truncate</param>
    /// <param name="maxLength">Maximum allowed length (default: 260 for Windows)</param>
    /// <returns>Truncated filename within the length limit</returns>
    string TruncateToLimit(string filename, int maxLength = 260);
}
