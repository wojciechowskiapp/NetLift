namespace NetLift.Core.Models;

/// <summary>
/// Represents a compile item (source code file) in a project.
/// </summary>
public class CompileItem
{
    /// <summary>
    /// Gets or sets the include path (relative to the project).
    /// </summary>
    public string Include { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file this item is dependent upon.
    /// </summary>
    public string? DependentUpon { get; set; }

    /// <summary>
    /// Gets or sets the sub-type (e.g., "Code", "Designer").
    /// </summary>
    public string? SubType { get; set; }
}
