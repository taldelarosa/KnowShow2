using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for extracting text-based subtitles from video files.
/// </summary>
public class TextSubtitleExtractor : ITextSubtitleExtractor
{
    private readonly IEnumerable<ISubtitleFormatHandler> _formatHandlers;

    public TextSubtitleExtractor(IEnumerable<ISubtitleFormatHandler> formatHandlers)
    {
        _formatHandlers = formatHandlers ?? throw new ArgumentNullException(nameof(formatHandlers));
    }

    public async Task<IReadOnlyList<TextSubtitleTrack>> DetectTextSubtitleTracksAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));

        var tracks = new List<TextSubtitleTrack>();

        // Look for external subtitle files
        var externalTracks = await DetectExternalSubtitleFilesAsync(videoFilePath, cancellationToken);
        tracks.AddRange(externalTracks);

        // TODO: Implement embedded subtitle detection using FFmpeg
        // For now, we only detect external subtitle files

        return tracks.AsReadOnly();
    }

    public async Task<TextSubtitleExtractionResult> ExtractTextSubtitleContentAsync(
        string videoFilePath,
        TextSubtitleTrack track,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));

        if (track == null)
            throw new ArgumentNullException(nameof(track));

        var result = new TextSubtitleExtractionResult
        {
            VideoFilePath = videoFilePath,
            ExtractedTracks = new List<TextSubtitleTrack>(),
            Status = ProcessingStatus.Processing
        };

        try
        {
            var subtitleResult = await ExtractAndParseSubtitleAsync(track, cancellationToken);
            if (subtitleResult != null)
            {
                track.Content = string.Join("\n", subtitleResult.Entries.Select(e => e.Text));
                track.SubtitleCount = subtitleResult.Entries.Count;
                track.Status = ProcessingStatus.Completed;

                result.ExtractedTracks = new List<TextSubtitleTrack> { track };
                result.Status = ProcessingStatus.Completed;
                result.SuccessfulExtractions = 1;
            }
            else
            {
                result.Status = ProcessingStatus.Failed;
                result.ErrorMessage = $"Failed to extract subtitle from track {track.Index}";
                result.FailedExtractions = 1;
            }
        }
        catch (Exception ex)
        {
            result.Status = ProcessingStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.FailedExtractions = 1;
        }

        return result;
    }

    public async Task<TextSubtitleExtractionResult> TryExtractAllTextSubtitlesAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));

        var result = new TextSubtitleExtractionResult
        {
            VideoFilePath = videoFilePath,
            ExtractedTracks = new List<TextSubtitleTrack>(),
            Status = ProcessingStatus.Processing
        };

        try
        {
            var tracks = await DetectTextSubtitleTracksAsync(videoFilePath, cancellationToken);
            var extractedTracks = new List<TextSubtitleTrack>();

            foreach (var track in tracks)
            {
                try
                {
                    var subtitleResult = await ExtractAndParseSubtitleAsync(track, cancellationToken);
                    if (subtitleResult != null)
                    {
                        track.Content = string.Join("\n", subtitleResult.Entries.Select(e => e.Text));
                        track.SubtitleCount = subtitleResult.Entries.Count;
                        track.Status = ProcessingStatus.Completed;
                        extractedTracks.Add(track);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with other tracks
                    track.Status = ProcessingStatus.Failed;
                    track.ErrorMessage = ex.Message;
                    Console.WriteLine($"Failed to extract subtitle from track {track.Index}: {ex.Message}");
                }
            }

            result.ExtractedTracks = extractedTracks;
            result.SuccessfulExtractions = extractedTracks.Count(t => t.Status == ProcessingStatus.Completed);
            result.FailedExtractions = extractedTracks.Count(t => t.Status == ProcessingStatus.Failed);
            result.Status = result.SuccessfulExtractions > 0 ? ProcessingStatus.Completed : ProcessingStatus.Failed;
        }
        catch (Exception ex)
        {
            result.Status = ProcessingStatus.Failed;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private Task<IEnumerable<TextSubtitleTrack>> DetectExternalSubtitleFilesAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default)
    {
        var tracks = new List<TextSubtitleTrack>();
        var videoDirectory = Path.GetDirectoryName(videoFilePath);
        var videoFileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoFilePath);

        if (string.IsNullOrEmpty(videoDirectory))
            return Task.FromResult<IEnumerable<TextSubtitleTrack>>(tracks);

        var subtitleExtensions = new[] { ".srt", ".ass", ".ssa", ".vtt" };

        // First try to find files that match the video filename exactly
        var searchPatterns = new[]
        {
            $"{videoFileNameWithoutExtension}.*",
            $"{videoFileNameWithoutExtension}*.*"
        };

        var index = 0;
        var foundFiles = new HashSet<string>();

        foreach (var pattern in searchPatterns)
        {
            var files = Directory.GetFiles(videoDirectory, pattern, SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (subtitleExtensions.Contains(extension) && !foundFiles.Contains(file))
                {
                    foundFiles.Add(file);
                    var format = GetSubtitleFormatFromExtension(extension);
                    var language = ExtractLanguageFromFilename(file);
                    var isDefault = string.IsNullOrEmpty(language) || language.Equals("en", StringComparison.OrdinalIgnoreCase);

                    tracks.Add(new TextSubtitleTrack
                    {
                        Index = index++,
                        Language = language ?? "und",
                        Format = format,
                        FilePath = file,
                        SourceType = SubtitleSourceType.External,
                        IsDefault = isDefault,
                        IsForced = false
                    });
                }
            }
        }

        // If no matching files found, look for any subtitle files in the directory
        if (tracks.Count == 0)
        {
            var allSubtitleFiles = subtitleExtensions
                .SelectMany(ext => Directory.GetFiles(videoDirectory, $"*{ext}", SearchOption.TopDirectoryOnly))
                .ToArray();

            foreach (var file in allSubtitleFiles)
            {
                if (!foundFiles.Contains(file))
                {
                    var format = GetSubtitleFormatFromExtension(Path.GetExtension(file));
                    var language = ExtractLanguageFromFilename(file);
                    var isDefault = string.IsNullOrEmpty(language) || language.Equals("en", StringComparison.OrdinalIgnoreCase);

                    tracks.Add(new TextSubtitleTrack
                    {
                        Index = index++,
                        Language = language ?? "und",
                        Format = format,
                        FilePath = file,
                        SourceType = SubtitleSourceType.External,
                        IsDefault = isDefault,
                        IsForced = false
                    });
                }
            }
        }

        return Task.FromResult<IEnumerable<TextSubtitleTrack>>(tracks);
    }

    private async Task<SubtitleParsingResult?> ExtractAndParseSubtitleAsync(
        TextSubtitleTrack track,
        CancellationToken cancellationToken = default)
    {
        if (track.SourceType != SubtitleSourceType.External || string.IsNullOrEmpty(track.FilePath))
            return null;

        var handler = _formatHandlers.FirstOrDefault(h => h.SupportedFormat == track.Format);
        if (handler == null)
            return null;

        using var fileStream = File.OpenRead(track.FilePath);
        return await handler.ParseSubtitleTextAsync(fileStream, null, cancellationToken);
    }

    private static SubtitleFormat GetSubtitleFormatFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".srt" => SubtitleFormat.SRT,
            ".ass" => SubtitleFormat.ASS,
            ".ssa" => SubtitleFormat.ASS,
            ".vtt" => SubtitleFormat.VTT,
            _ => SubtitleFormat.SRT // Default fallback
        };
    }

    private static string? ExtractLanguageFromFilename(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Look for language codes in common patterns
        // e.g., "movie.en.srt", "movie.english.srt", "movie_en.srt"
        var patterns = new[]
        {
            @"\.([a-z]{2,3})$",           // .en, .eng
            @"\.([a-z]{2,3})\..*$",       // .en.forced
            @"_([a-z]{2,3})$",            // _en
            @"_([a-z]{2,3})\..*$",        // _en.forced
            @"\.([a-z]{4,})$",            // .english
            @"\.([a-z]{4,})\..*$"         // .english.forced
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var language = match.Groups[1].Value.ToLowerInvariant();

                // Map common language names to ISO codes
                return language switch
                {
                    "english" => "en",
                    "spanish" => "es",
                    "french" => "fr",
                    "german" => "de",
                    "italian" => "it",
                    "portuguese" => "pt",
                    "japanese" => "ja",
                    "chinese" => "zh",
                    "korean" => "ko",
                    "russian" => "ru",
                    _ when language.Length <= 3 => language,
                    _ => null
                };
            }
        }

        return null;
    }
}
