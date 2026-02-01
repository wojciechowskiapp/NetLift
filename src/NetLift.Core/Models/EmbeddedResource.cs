namespace NetLift.Core.Models;

/// <summary>
/// Represents an embedded resource in a project (e.g., .resx files).
/// </summary>
public class EmbeddedResource
{
    /// <summary>
    /// Gets or sets the include path (relative to the project).
    /// </summary>
    public string Include { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file this resource is dependent upon.
    /// </summary>
    public string? DependentUpon { get; set; }

    /// <summary>
    /// Gets or sets the generator tool.
    /// </summary>
    public string? Generator { get; set; }

    /// <summary>
    /// Gets or sets the last generated output file.
    /// </summary>
    public string? LastGenOutput { get; set; }
}
