namespace NetLift.Core.Models;

/// <summary>
/// Represents a project-to-project reference.
/// </summary>
public class ProjectReference
{
    /// <summary>
    /// Gets or sets the relative or absolute path to the referenced project.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the referenced project.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the GUID of the referenced project (old-style only).
    /// </summary>
    public string? Guid { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the project reference.
    /// Common metadata includes: ReferenceOutputAssembly, PrivateAssets, IncludeAssets, ExcludeAssets, Aliases, EmbedInteropTypes.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
