namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the different types of errors that can occur during bulk processing.
/// </summary>
public enum BulkProcessingErrorType
{
    /// <summary>
    /// Unknown or unspecified error.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Invalid input parameters or configuration.
    /// </summary>
    InvalidInput = 1,

    /// <summary>
    /// File or directory not found.
    /// </summary>
    FileNotFound = 2,

    /// <summary>
    /// Access denied to file or directory.
    /// </summary>
    AccessDenied = 3,

    /// <summary>
    /// General file system error (disk full, I/O error, etc.).
    /// </summary>
    FileSystemError = 4,

    /// <summary>
    /// Unsupported file format.
    /// </summary>
    UnsupportedFormat = 5,

    /// <summary>
    /// Error during episode identification.
    /// </summary>
    IdentificationError = 6,

    /// <summary>
    /// Error during file renaming.
    /// </summary>
    RenameError = 7,

    /// <summary>
    /// Error during backup creation.
    /// </summary>
    BackupError = 8,

    /// <summary>
    /// Operation was cancelled by user.
    /// </summary>
    Cancelled = 9,

    /// <summary>
    /// Timeout occurred during processing.
    /// </summary>
    Timeout = 10,

    /// <summary>
    /// Insufficient memory or resources.
    /// </summary>
    InsufficientResources = 11,

    /// <summary>
    /// Network-related error.
    /// </summary>
    NetworkError = 12,

    /// <summary>
    /// Database-related error.
    /// </summary>
    DatabaseError = 13,

    /// <summary>
    /// Configuration error.
    /// </summary>
    ConfigurationError = 14,

    /// <summary>
    /// Maximum error limit reached.
    /// </summary>
    ErrorLimitExceeded = 15,

    /// <summary>
    /// Operation was cancelled.
    /// </summary>
    OperationCancelled = 16,

    /// <summary>
    /// File access error (permissions, locks, etc.).
    /// </summary>
    FileAccessError = 17,

    /// <summary>
    /// Unsupported file type.
    /// </summary>
    UnsupportedFileType = 18,

    /// <summary>
    /// Invalid file format or corrupted file.
    /// </summary>
    InvalidFileFormat = 19,

    /// <summary>
    /// Processing timeout occurred.
    /// </summary>
    ProcessingTimeout = 20,

    /// <summary>
    /// System-level error (out of memory, etc.).
    /// </summary>
    SystemError = 21,

    /// <summary>
    /// General processing error.
    /// </summary>
    ProcessingError = 22
}