namespace NetLift.Core.Models;

/// <summary>
/// Represents a single line in a diff hunk.
/// </summary>
public sealed class DiffLine
{
    /// <summary>
    /// Gets or sets the type of the line.
    /// </summary>
    public DiffLineType Type { get; set; }

    /// <summary>
    /// Gets or sets the content of the line.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the line number in the old file (null for additions).
    /// </summary>
    public int? OldLineNumber { get; set; }

    /// <summary>
    /// Gets or sets the line number in the new file (null for deletions).
    /// </summary>
    public int? NewLineNumber { get; set; }
}
