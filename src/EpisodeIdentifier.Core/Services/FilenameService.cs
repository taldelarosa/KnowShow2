using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for generating Windows-compatible filenames from episode identification data.
/// Implements comprehensive filename sanitization and validation.
/// </summary>
public class FilenameService : IFilenameService
{
    private readonly char[] _invalidWindowsChars = { '<', '>', ':', '"', '|', '?', '*', '\\', '/' };
    private readonly string[] _reservedWindowsNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    /// <inheritdoc/>
    public FilenameGenerationResult GenerateFilename(FilenameGenerationRequest request)
    {
        var result = new FilenameGenerationResult
        {
            SanitizedCharacters = new List<string>()
        };

        // Validate input request
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            result.IsValid = false;
            result.ValidationError = validationError;
            return result;
        }

        // Check confidence threshold
        if (request.MatchConfidence < 0.9)
        {
            result.IsValid = false;
            result.ValidationError = $"Match confidence {request.MatchConfidence:F2} is below required threshold of 0.9";
            return result;
        }

        // Generate base filename components
        var sanitizedSeries = SanitizeForWindows(request.Series);
        var seasonEpisode = $"S{request.Season.PadLeft(2, '0')}E{request.Episode.PadLeft(2, '0')}";
        
        // Track sanitized characters
        if (sanitizedSeries != request.Series)
        {
            result.SanitizedCharacters.Add($"Series: '{request.Series}' → '{sanitizedSeries}'");
        }

        string filename;
        if (!string.IsNullOrWhiteSpace(request.EpisodeName))
        {
            var sanitizedEpisodeName = SanitizeForWindows(request.EpisodeName);
            if (sanitizedEpisodeName != request.EpisodeName)
            {
                result.SanitizedCharacters.Add($"Episode: '{request.EpisodeName}' → '{sanitizedEpisodeName}'");
            }
            filename = $"{sanitizedSeries} - {seasonEpisode} - {sanitizedEpisodeName}{request.FileExtension}";
        }
        else
        {
            filename = $"{sanitizedSeries} - {seasonEpisode}{request.FileExtension}";
        }

        // Handle length truncation if necessary
        var maxLength = request.MaxLength ?? 260;
        if (filename.Length > maxLength)
        {
            filename = TruncateToLimit(filename, maxLength);
            result.WasTruncated = true;
        }

        result.SuggestedFilename = filename;
        result.TotalLength = filename.Length;
        result.IsValid = IsValidWindowsFilename(filename);

        if (!result.IsValid)
        {
            result.ValidationError = "Generated filename failed Windows validation";
        }

        return result;
    }

    /// <inheritdoc/>
    public string SanitizeForWindows(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Replace invalid characters with single space
        var sanitized = input;
        foreach (var invalidChar in _invalidWindowsChars)
        {
            sanitized = sanitized.Replace(invalidChar, ' ');
        }

        // Collapse multiple consecutive spaces to single space
        sanitized = Regex.Replace(sanitized, @"\s+", " ");

        // Trim leading and trailing spaces
        sanitized = sanitized.Trim();

        return sanitized;
    }

    /// <inheritdoc/>
    public bool IsValidWindowsFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Check length limit
        if (filename.Length > 260)
            return false;

        // Check for invalid characters
        if (filename.IndexOfAny(_invalidWindowsChars) >= 0)
            return false;

        // Check for reserved names
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        if (_reservedWindowsNames.Contains(nameWithoutExtension.ToUpperInvariant()))
            return false;

        // Check for valid file extension
        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
            return false;

        // Check for trailing periods or spaces (not allowed in Windows)
        if (filename.EndsWith('.') || filename.EndsWith(' '))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public string TruncateToLimit(string filename, int maxLength = 260)
    {
        if (filename.Length <= maxLength)
            return filename;

        var extension = Path.GetExtension(filename);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        // Reserve space for extension
        var availableLength = maxLength - extension.Length;

        // Try to parse the filename format: "Series - S01E01 - Episode"
        var parts = nameWithoutExtension.Split(new[] { " - " }, StringSplitOptions.None);
        
        if (parts.Length >= 2)
        {
            var series = parts[0];
            var seasonEpisode = parts[1]; // Should be like "S01E01"
            var episode = parts.Length > 2 ? string.Join(" - ", parts.Skip(2)) : "";

            // Always preserve series and season/episode info
            var coreInfo = $"{series} - {seasonEpisode}";
            
            if (coreInfo.Length >= availableLength)
            {
                // If even core info is too long, truncate series
                var maxSeriesLength = availableLength - seasonEpisode.Length - 3; // " - " = 3 chars
                if (maxSeriesLength > 0)
                {
                    series = series.Length > maxSeriesLength ? series.Substring(0, maxSeriesLength).TrimEnd() : series;
                    return $"{series} - {seasonEpisode}{extension}";
                }
                else
                {
                    // Extreme case: just use season/episode
                    return seasonEpisode.Length <= availableLength ? $"{seasonEpisode}{extension}" : filename.Substring(0, maxLength);
                }
            }

            // Calculate remaining space for episode name
            var remainingLength = availableLength - coreInfo.Length;
            
            if (!string.IsNullOrEmpty(episode) && remainingLength > 3) // Need at least " - " + 1 char
            {
                remainingLength -= 3; // Account for " - "
                if (episode.Length > remainingLength)
                {
                    episode = episode.Substring(0, remainingLength).TrimEnd();
                }
                return $"{series} - {seasonEpisode} - {episode}{extension}";
            }
            else
            {
                return $"{series} - {seasonEpisode}{extension}";
            }
        }

        // Fallback: simple truncation
        var truncatedName = nameWithoutExtension.Substring(0, availableLength).TrimEnd();
        return $"{truncatedName}{extension}";
    }

    private string? ValidateRequest(FilenameGenerationRequest request)
    {
        if (request == null)
            return "Request cannot be null";

        if (string.IsNullOrWhiteSpace(request.Series))
            return "Series name is required";

        if (string.IsNullOrWhiteSpace(request.Season))
            return "Season is required";

        if (string.IsNullOrWhiteSpace(request.Episode))
            return "Episode is required";

        if (string.IsNullOrWhiteSpace(request.FileExtension))
            return "File extension is required";

        if (!request.FileExtension.StartsWith('.'))
            return "File extension must start with a dot (e.g., '.mkv')";

        if (request.MatchConfidence < 0.0 || request.MatchConfidence > 1.0)
            return "Match confidence must be between 0.0 and 1.0";

        return null;
    }
}
