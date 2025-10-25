using EpisodeIdentifier.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Core.Interfaces
{
    /// <summary>
    /// Interface for video file processing service that coordinates the complete video file processing workflow.
    /// </summary>
    public interface IVideoFileProcessingService
    {
        /// <summary>
        /// Processes a video file completely, including subtitle extraction, episode identification, and optional file renaming.
        /// </summary>
        /// <param name="filePath">The path to the video file to process.</param>
        /// <param name="shouldRename">Whether the file should be automatically renamed if identification succeeds with high confidence.</param>
        /// <param name="language">The preferred language for subtitle processing.</param>
        /// <param name="seriesFilter">Optional series name to filter identification results.</param>
        /// <param name="seasonFilter">Optional season number to filter identification results (requires seriesFilter).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The complete processing result including identification results and any rename operations.</returns>
        Task<VideoFileProcessingResult> ProcessVideoFileAsync(
            string filePath,
            bool shouldRename = false,
            string? language = null,
            string? seriesFilter = null,
            int? seasonFilter = null,
            CancellationToken cancellationToken = default);
    }
}
