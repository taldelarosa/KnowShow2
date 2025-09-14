using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for console-based progress reporting during bulk processing operations.
/// </summary>
public interface IConsoleProgressReporter
{
    /// <summary>
    /// Reports progress updates for the specified request.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="progress">The current progress information.</param>
    void ReportProgress(string requestId, BulkProcessingProgress progress);

    /// <summary>
    /// Reports the completion of a bulk processing request.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="result">The final processing result.</param>
    void ReportCompletion(string requestId, BulkProcessingResult result);

    /// <summary>
    /// Reports an error during bulk processing.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="error">The error that occurred.</param>
    void ReportError(string requestId, string error);

    /// <summary>
    /// Reports a warning during bulk processing.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="warning">The warning message.</param>
    void ReportWarning(string requestId, string warning);
}