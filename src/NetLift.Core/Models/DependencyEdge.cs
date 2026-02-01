namespace NetLift.Core.Models;

/// <summary>
/// Represents an edge (dependency relationship) in the dependency graph.
/// </summary>
public class DependencyEdge
{
    /// <summary>
    /// Gets or sets the source project (the project that has the dependency).
    /// </summary>
    public ProjectInfo From { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target project (the project being depended upon).
    /// </summary>
    public ProjectInfo To { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of dependency.
    /// </summary>
    public DependencyType Type { get; set; } = DependencyType.Project;
}
