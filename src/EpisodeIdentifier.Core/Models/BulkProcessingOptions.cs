using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Configuration options for bulk processing operations.
/// </summary>
public class BulkProcessingOptions
{
    /// <summary>
    /// Gets or sets whether to process directories recursively.
    /// Default is true.
    /// </summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum depth for recursive directory processing.
    /// 0 means unlimited depth. Default is 0.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxDepth { get; set; } = 0;

    /// <summary>
    /// Gets or sets the file extensions to include in processing.
    /// If empty, all supported extensions are processed.
    /// </summary>
    public List<string> IncludeExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets the file extensions to exclude from processing.
    /// Takes precedence over IncludeExtensions.
    /// </summary>
    public List<string> ExcludeExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets the batch size for processing files.
    /// Larger batches use more memory but may be faster.
    /// Default is 100.
    /// </summary>
    [Range(1, 10000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of concurrent processing tasks.
    /// Default is the number of processor cores.
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the maximum number of errors to tolerate before stopping.
    /// 0 means unlimited errors. Default is 0.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxErrors { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to continue processing after individual file errors.
    /// Default is true.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create backups before processing files.
    /// Default is false.
    /// </summary>
    public bool CreateBackups { get; set; } = false;

    /// <summary>
    /// Gets or sets the interval for progress reporting in milliseconds.
    /// Default is 1000ms (1 second).
    /// </summary>
    [Range(100, 60000)]
    public int ProgressReportingInterval { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to force garbage collection between batches.
    /// Useful for large processing operations. Default is true.
    /// </summary>
    public bool ForceGarbageCollection { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of errors before aborting processing.
    /// If null, processing continues regardless of error count.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxErrorsBeforeAbort { get; set; } = null;

    /// <summary>
    /// Gets or sets the timeout for processing individual files.
    /// If null, no timeout is applied.
    /// </summary>
    public TimeSpan? FileProcessingTimeout { get; set; } = null;

    /// <summary>
    /// Gets or sets the specific file extensions to process.
    /// If empty, default video file extensions are used.
    /// </summary>
    public List<string> FileExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets additional options for the processing operation.
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}