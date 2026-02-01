using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Defines the contract for building dependency graphs from solution and project information.
/// </summary>
public interface IDependencyGraphBuilder
{
    /// <summary>
    /// Builds a dependency graph from solution information and its projects.
    /// </summary>
    /// <param name="solution">The solution information containing project references.</param>
    /// <param name="projects">The list of parsed project information.</param>
    /// <returns>A dependency graph representing project relationships.</returns>
    DependencyGraph Build(SolutionInfo solution, List<ProjectInfo> projects);

    /// <summary>
    /// Detects circular dependencies in a dependency graph.
    /// </summary>
    /// <param name="graph">The dependency graph to analyze.</param>
    /// <returns>A list of circular dependency paths, or empty if no cycles exist.</returns>
    List<string> DetectCircularDependencies(DependencyGraph graph);
}
