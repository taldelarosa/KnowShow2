namespace EpisodeIdentifier.Core.Models;

public enum FileRenameError
{
    FileNotFound,
    TargetExists,
    PermissionDenied,
    InvalidPath,
    DiskFull,
    PathTooLong
}
