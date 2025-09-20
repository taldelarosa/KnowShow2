using System;

namespace EpisodeIdentifier.Core.Models
{
    /// <summary>
    /// Result of processing a video file, including identification results and file operation outcomes.
    /// </summary>
    public class VideoFileProcessingResult
    {
        public string FilePath { get; set; } = "";
        public DateTime ProcessingStarted { get; set; }
        public DateTime ProcessingCompleted { get; set; }
        public TimeSpan ProcessingDuration => ProcessingCompleted - ProcessingStarted;

        public IdentificationResult? IdentificationResult { get; set; }

        public bool HasError => IdentificationResult?.HasError ?? Error != null;
        public IdentificationError? Error { get; set; }

        // File rename information
        public bool FileRenamed { get; set; } = false;
        public string? OriginalFilename { get; set; }
        public string? SuggestedFilename { get; set; }
        public string? NewFilePath { get; set; }
    }
}
