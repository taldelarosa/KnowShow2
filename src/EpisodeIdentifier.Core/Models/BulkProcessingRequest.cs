using System.ComponentModel.DataAnnotations;

namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents a request for bulk processing of files or directories for episode identification.
/// </summary>
public class BulkProcessingRequest
{
    /// <summary>
    /// Gets or sets the paths to files or directories to process.
    /// Can be a mix of individual files and directories.
    /// </summary>
    [Required]
    public List<string> Paths { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for bulk processing behavior.
    /// </summary>
    [Required]
    public BulkProcessingOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique identifier for this processing request.
    /// Auto-generated if not provided.
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the timestamp when this request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets optional metadata for this request.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the cancellation token for this request.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}
