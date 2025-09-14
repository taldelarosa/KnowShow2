namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents an error that occurred during bulk processing.
/// </summary>
public class BulkProcessingError
{
    /// <summary>
    /// Gets or sets the type of error that occurred.
    /// </summary>
    public BulkProcessingErrorType ErrorType { get; set; } = BulkProcessingErrorType.Unknown;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed error description or stack trace.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the file path associated with this error (if applicable).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the phase during which the error occurred.
    /// </summary>
    public BulkProcessingPhase Phase { get; set; } = BulkProcessingPhase.Unknown;

    /// <summary>
    /// Gets or sets whether this error is considered recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this error should stop the entire processing operation.
    /// </summary>
    public bool IsFatal { get; set; } = false;

    /// <summary>
    /// Gets or sets the inner exception details if available.
    /// </summary>
    public string? InnerException { get; set; }

    /// <summary>
    /// Gets or sets additional context information about the error.
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Gets or sets a suggested action for resolving this error.
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Initializes a new instance of the BulkProcessingError class.
    /// </summary>
    public BulkProcessingError()
    {
    }

    /// <summary>
    /// Initializes a new instance of the BulkProcessingError class with the specified parameters.
    /// </summary>
    /// <param name="errorType">The type of error.</param>
    /// <param name="message">The error message.</param>
    /// <param name="filePath">The file path associated with the error.</param>
    /// <param name="phase">The phase during which the error occurred.</param>
    public BulkProcessingError(BulkProcessingErrorType errorType, string message, string? filePath = null, BulkProcessingPhase phase = BulkProcessingPhase.Unknown)
    {
        ErrorType = errorType;
        Message = message;
        FilePath = filePath;
        Phase = phase;
    }

    /// <summary>
    /// Creates a BulkProcessingError from an exception.
    /// </summary>
    /// <param name="exception">The exception to create the error from.</param>
    /// <param name="filePath">The file path associated with the error.</param>
    /// <param name="phase">The phase during which the error occurred.</param>
    /// <returns>A new BulkProcessingError instance.</returns>
    public static BulkProcessingError FromException(Exception exception, string? filePath = null, BulkProcessingPhase phase = BulkProcessingPhase.Unknown)
    {
        var errorType = exception switch
        {
            UnauthorizedAccessException => BulkProcessingErrorType.AccessDenied,
            DirectoryNotFoundException => BulkProcessingErrorType.FileNotFound,
            FileNotFoundException => BulkProcessingErrorType.FileNotFound,
            IOException => BulkProcessingErrorType.FileSystemError,
            ArgumentException => BulkProcessingErrorType.InvalidInput,
            OperationCanceledException => BulkProcessingErrorType.Cancelled,
            _ => BulkProcessingErrorType.Unknown
        };

        return new BulkProcessingError
        {
            ErrorType = errorType,
            Message = exception.Message,
            Details = exception.ToString(),
            FilePath = filePath,
            Phase = phase,
            InnerException = exception.InnerException?.ToString(),
            IsRecoverable = errorType != BulkProcessingErrorType.Cancelled,
            IsFatal = errorType == BulkProcessingErrorType.Cancelled
        };
    }
}