using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for bulk processing of files for episode identification.
/// Provides high-level orchestration of file discovery, processing, and reporting.
/// </summary>
public interface IBulkProcessor
{
    /// <summary>
    /// Processes multiple files or directories for episode identification.
    /// </summary>
    /// <param name="request">The bulk processing request containing paths and options.</param>
    /// <returns>A task representing the bulk processing result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid data.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when specified directories don't exist.</exception>
    /// <exception cref="FileNotFoundException">Thrown when specified files don't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to files/directories is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request);

    /// <summary>
    /// Processes multiple files or directories with progress reporting.
    /// </summary>
    /// <param name="request">The bulk processing request containing paths and options.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <returns>A task representing the bulk processing result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid data.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when specified directories don't exist.</exception>
    /// <exception cref="FileNotFoundException">Thrown when specified files don't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to files/directories is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request, IProgress<BulkProcessingProgress>? progressCallback = null);

    /// <summary>
    /// Validates a bulk processing request without executing it.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>A task representing the validation result. True if valid, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    Task<bool> ValidateRequestAsync(BulkProcessingRequest request);

    /// <summary>
    /// Gets detailed validation errors for a bulk processing request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>A task representing a list of validation errors. Empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    Task<List<BulkProcessingError>> GetValidationErrorsAsync(BulkProcessingRequest request);

    /// <summary>
    /// Estimates the processing time and resource requirements for a request.
    /// </summary>
    /// <param name="request">The request to estimate.</param>
    /// <returns>A task representing the processing estimate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid data.</exception>
    Task<ProcessingEstimate> EstimateProcessingAsync(BulkProcessingRequest request);

    /// <summary>
    /// Cancels an ongoing bulk processing operation.
    /// </summary>
    /// <param name="requestId">The ID of the request to cancel.</param>
    /// <returns>A task representing the cancellation operation. True if cancelled, false if not found or already completed.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    Task<bool> CancelProcessingAsync(string requestId);

    /// <summary>
    /// Gets the current status and progress of an ongoing bulk processing operation.
    /// </summary>
    /// <param name="requestId">The ID of the request to check.</param>
    /// <returns>A task representing the current progress, or null if the request is not found.</returns>
    /// <exception cref="ArgumentException">Thrown when requestId is null or empty.</exception>
    Task<BulkProcessingProgress?> GetProgressAsync(string requestId);
}

/// <summary>
/// Represents an estimate of processing requirements for a bulk processing request.
/// </summary>
public class ProcessingEstimate
{
    /// <summary>
    /// Gets or sets the estimated number of files to process.
    /// </summary>
    public int EstimatedFileCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated processing time.
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage in bytes.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the estimated disk space needed for backups in bytes.
    /// </summary>
    public long EstimatedBackupSpace { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the estimate (0-100).
    /// </summary>
    public int ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets additional estimation details.
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}
