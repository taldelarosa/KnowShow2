namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of processing a single file during bulk processing.
/// </summary>
public class FileProcessingResult
{
    /// <summary>
    /// Gets or sets the full path to the file that was processed.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original filename before processing.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new filename after processing (if renamed).
    /// </summary>
    public string? NewFileName { get; set; }

    /// <summary>
    /// Gets or sets the status of processing for this file.
    /// </summary>
    public FileProcessingStatus Status { get; set; } = FileProcessingStatus.NotProcessed;

    /// <summary>
    /// Gets or sets when processing of this file started.
    /// </summary>
    public DateTime ProcessingStarted { get; set; }

    /// <summary>
    /// Gets or sets when processing of this file completed.
    /// </summary>
    public DateTime? ProcessingCompleted { get; set; }

    /// <summary>
    /// Gets or sets the duration it took to process this file.
    /// </summary>
    public TimeSpan ProcessingDuration => ProcessingCompleted?.Subtract(ProcessingStarted) ?? TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the identification results found for this file.
    /// </summary>
    public List<object> IdentificationResults { get; set; } = new(); // Will be typed properly when we have the actual identification result types

    /// <summary>
    /// Gets or sets any error that occurred while processing this file.
    /// </summary>
    public BulkProcessingError? Error { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; } = 0;

    /// <summary>
    /// Gets or sets additional metadata about the file processing.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of retry attempts made for this file.
    /// A value of 0 indicates successful processing on the first attempt.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether this file was processed successfully.
    /// </summary>
    public bool IsSuccessful => Status == FileProcessingStatus.Success && Error == null;

    /// <summary>
    /// Gets a value indicating whether processing of this file was skipped.
    /// </summary>
    public bool WasSkipped => Status == FileProcessingStatus.Skipped;

    /// <summary>
    /// Gets a value indicating whether this file had an error during processing.
    /// </summary>
    public bool HasError => Error != null || Status == FileProcessingStatus.Failed;
}