namespace NetLift.Core.Models;

/// <summary>
/// Represents the differences for a single file.
/// </summary>
public sealed class FileDiff
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of change.
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the original content (null for new files).
    /// </summary>
    public string? OriginalContent { get; set; }

    /// <summary>
    /// Gets or sets the new content (null for deleted files).
    /// </summary>
    public string? NewContent { get; set; }

    /// <summary>
    /// Gets or sets the diff hunks for this file.
    /// </summary>
    public List<DiffHunk> Hunks { get; set; } = new();

    /// <summary>
    /// Gets or sets a preview of the changes.
    /// </summary>
    public string Preview { get; set; } = string.Empty;
}
