using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Extracts VobSub (.idx/.sub) files from MKV containers.
/// </summary>
public interface IVobSubExtractor
{
    /// <summary>
    /// Extracts DVD subtitle track from MKV file to VobSub format.
    /// </summary>
    /// <param name="videoPath">Absolute path to MKV video file. Must exist and be a .mkv file.</param>
    /// <param name="trackIndex">Subtitle track index from SubtitleTrackInfo. Must be >= 0.</param>
    /// <param name="outputDirectory">Directory for extracted .idx and .sub files. Must be writable.</param>
    /// <param name="cancellationToken">Cancellation token for timeout handling.</param>
    /// <returns>Result containing paths to extracted files or error details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when videoPath or outputDirectory is null.</exception>
    /// <exception cref="ArgumentException">Thrown when videoPath doesn't exist or trackIndex is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when extraction exceeds timeout.</exception>
    Task<VobSubExtractionResult> ExtractAsync(string videoPath, int trackIndex, string outputDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if mkvextract tool is available on system PATH.
    /// </summary>
    /// <returns>True if mkvextract is available, false otherwise.</returns>
    Task<bool> IsMkvExtractAvailableAsync();
}
