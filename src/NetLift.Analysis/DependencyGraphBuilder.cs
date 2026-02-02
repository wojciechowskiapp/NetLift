using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis;

/// <summary>
/// Builds dependency graphs from solution and project information.
/// </summary>
public class DependencyGraphBuilder : IDependencyGraphBuilder
{
    /// <summary>
    /// Builds a dependency graph from solution information and its projects.
    /// </summary>
    /// <param name="solution">The solution information containing project references.</param>
    /// <param name="projects">The list of parsed project information.</param>
    /// <returns>A dependency graph representing project relationships.</returns>
    public DependencyGraph Build(SolutionInfo solution, List<ProjectInfo> projects)
    {
        var graph = new DependencyGraph();

        // Create a lookup dictionary for fast project resolution
        var projectLookup = projects.ToDictionary(
            p => NormalizePath(p.FilePath),
            p => p,
            StringComparer.OrdinalIgnoreCase);

        // Add all projects as nodes
        foreach (var project in projects)
        {
            var node = new DependencyNode
            {
                Project = project,
                PackageDependencies = project.PackageReferences.ToList()
            };
            graph.Nodes.Add(node);
        }

        // Build edges based on project references
        foreach (var project in projects)
        {
            foreach (var projectRef in project.ProjectReferences)
            {
                // Resolve the referenced project
                var referencePath = ResolveProjectPath(project.FilePath, projectRef.Path);
                var normalizedPath = NormalizePath(referencePath);

                if (projectLookup.TryGetValue(normalizedPath, out var referencedProject))
                {
                    // Add edge: project -> referencedProject
                    var edge = new DependencyEdge
                    {
                        From = project,
                        To = referencedProject,
                        Type = DependencyType.Project
                    };
                    graph.Edges.Add(edge);

                    // Update node degrees
                    var fromNode = graph.Nodes.First(n => n.Project == project);
                    var toNode = graph.Nodes.First(n => n.Project == referencedProject);

                    fromNode.ProjectDependencies.Add(referencedProject);
                    fromNode.OutDegree++;
                    toNode.InDegree++;
                }
            }
        }

        // Detect circular dependencies
        graph.CircularPaths = DetectCircularDependencies(graph);

        return graph;
    }

    /// <summary>
    /// Detects circular dependencies in a dependency graph using depth-first search.
    /// </summary>
    /// <param name="graph">The dependency graph to analyze.</param>
    /// <returns>A list of circular dependency paths.</returns>
    public List<string> DetectCircularDependencies(DependencyGraph graph)
    {
        var cycles = new List<string>();
        var visited = new HashSet<ProjectInfo>();
        var recursionStack = new HashSet<ProjectInfo>();
        var currentPath = new Stack<ProjectInfo>();

        foreach (var node in graph.Nodes)
        {
            if (!visited.Contains(node.Project))
            {
                DetectCyclesRecursive(node.Project, graph, visited, recursionStack, currentPath, cycles);
            }
        }

        return cycles;
    }

    /// <summary>
    /// Recursive helper for cycle detection using DFS.
    /// </summary>
    private void DetectCyclesRecursive(
        ProjectInfo current,
        DependencyGraph graph,
        HashSet<ProjectInfo> visited,
        HashSet<ProjectInfo> recursionStack,
        Stack<ProjectInfo> currentPath,
        List<string> cycles)
    {
        visited.Add(current);
        recursionStack.Add(current);
        currentPath.Push(current);

        // Get all dependencies of the current project
        var dependencies = graph.Edges
            .Where(e => e.From == current)
            .Select(e => e.To)
            .ToList();

        foreach (var dependency in dependencies)
        {
            if (!visited.Contains(dependency))
            {
                DetectCyclesRecursive(dependency, graph, visited, recursionStack, currentPath, cycles);
            }
            else if (recursionStack.Contains(dependency))
            {
                // Found a cycle - build the cycle path
                var cyclePath = BuildCyclePath(currentPath, dependency);
                cycles.Add(cyclePath);
            }
        }

        currentPath.Pop();
        recursionStack.Remove(current);
    }

    /// <summary>
    /// Builds a string representation of a circular dependency path.
    /// </summary>
    private string BuildCyclePath(Stack<ProjectInfo> path, ProjectInfo cycleStart)
    {
        var pathList = path.ToList();
        pathList.Reverse(); // Stack is in reverse order

        var cycleIndex = pathList.FindIndex(p => p == cycleStart);
        if (cycleIndex == -1)
        {
            cycleIndex = 0;
        }

        var cyclePart = pathList.Skip(cycleIndex).ToList();
        cyclePart.Add(cycleStart); // Close the cycle

        return string.Join(" -> ", cyclePart.Select(p => p.Name));
    }

    /// <summary>
    /// Resolves a project reference path relative to the referencing project.
    /// </summary>
    private string ResolveProjectPath(string baseProjectPath, string referencePath)
    {
        var baseDir = Path.GetDirectoryName(baseProjectPath) ?? string.Empty;
        // Normalize path separators for cross-platform compatibility
        var normalizedReferencePath = referencePath.Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(baseDir, normalizedReferencePath);
        return Path.GetFullPath(fullPath);
    }

    /// <summary>
    /// Normalizes a file path for consistent comparison.
    /// </summary>
    private string NormalizePath(string path)
    {
        // Normalize path separators for cross-platform compatibility before GetFullPath
        var normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(normalizedPath);
    }
}
