namespace NetLift.Core.Models;

/// <summary>
/// Represents a node in the dependency graph with its dependencies.
/// </summary>
public class DependencyNode
{
    /// <summary>
    /// Gets or sets the project information for this node.
    /// </summary>
    public ProjectInfo Project { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of project dependencies (outgoing edges).
    /// </summary>
    public List<ProjectInfo> ProjectDependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of package dependencies.
    /// </summary>
    public List<PackageReference> PackageDependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the in-degree (number of projects that depend on this one).
    /// </summary>
    public int InDegree { get; set; }

    /// <summary>
    /// Gets or sets the out-degree (number of projects this one depends on).
    /// </summary>
    public int OutDegree { get; set; }
}
