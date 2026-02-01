namespace NetLift.Core.Models;

/// <summary>
/// Represents a hunk (section of changes) in a file diff.
/// </summary>
public sealed class DiffHunk
{
    /// <summary>
    /// Gets or sets the starting line number in the old file.
    /// </summary>
    public int OldStart { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the old file.
    /// </summary>
    public int OldCount { get; set; }

    /// <summary>
    /// Gets or sets the starting line number in the new file.
    /// </summary>
    public int NewStart { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the new file.
    /// </summary>
    public int NewCount { get; set; }

    /// <summary>
    /// Gets or sets the lines in this hunk.
    /// </summary>
    public List<DiffLine> Lines { get; set; } = new();
}
