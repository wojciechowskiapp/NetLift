namespace NetLift.Core.Models;

/// <summary>
/// Represents a content item in a project (e.g., config files, web files).
/// </summary>
public class ContentItem
{
    /// <summary>
    /// Gets or sets the include path (relative to the project).
    /// </summary>
    public string Include { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to copy to output directory.
    /// </summary>
    public string? CopyToOutputDirectory { get; set; }
}
