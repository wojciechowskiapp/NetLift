namespace NetLift.Core.Models;

/// <summary>
/// Represents the type of dependency in the dependency graph.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Project-to-project reference.
    /// </summary>
    Project,

    /// <summary>
    /// NuGet package reference.
    /// </summary>
    Package,

    /// <summary>
    /// Assembly (DLL) reference.
    /// </summary>
    Assembly
}
