namespace NetLift.Core.Models;

/// <summary>
/// Represents a dependency graph of projects and their relationships.
/// </summary>
public class DependencyGraph
{
    /// <summary>
    /// Gets or sets the collection of nodes in the graph.
    /// </summary>
    public List<DependencyNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of edges (dependencies) in the graph.
    /// </summary>
    public List<DependencyEdge> Edges { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the graph contains circular dependencies.
    /// </summary>
    public bool HasCircularDependencies => CircularPaths.Count > 0;

    /// <summary>
    /// Gets or sets the list of circular dependency paths found in the graph.
    /// Each path is a string representation like "A -> B -> C -> A".
    /// </summary>
    public List<string> CircularPaths { get; set; } = new();

    /// <summary>
    /// Returns projects in migration order (dependencies first).
    /// Projects with no dependencies are returned first, projects that depend on others come last.
    /// </summary>
    /// <returns>A topologically sorted list of projects.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph has circular dependencies.</exception>
    public List<ProjectInfo> GetMigrationOrder()
    {
        if (HasCircularDependencies)
        {
            throw new InvalidOperationException(
                $"Cannot determine migration order: circular dependencies detected.{Environment.NewLine}" +
                string.Join(Environment.NewLine, CircularPaths));
        }

        return TopologicalSort();
    }

    /// <summary>
    /// Gets projects with no dependencies (leaf nodes in the dependency tree).
    /// These projects can be migrated first as nothing depends on them being in a specific state.
    /// </summary>
    /// <returns>List of projects with no project dependencies.</returns>
    public List<ProjectInfo> GetLeafProjects()
    {
        return Nodes
            .Where(n => n.OutDegree == 0)
            .Select(n => n.Project)
            .ToList();
    }

    /// <summary>
    /// Gets projects that nothing depends on (root nodes/entry points).
    /// These projects typically represent applications or test projects.
    /// </summary>
    /// <returns>List of projects that have no incoming dependencies.</returns>
    public List<ProjectInfo> GetRootProjects()
    {
        return Nodes
            .Where(n => n.InDegree == 0)
            .Select(n => n.Project)
            .ToList();
    }

    /// <summary>
    /// Performs topological sort using Kahn's algorithm.
    /// For migration order, we start with leaf nodes (no dependencies) and work up to root nodes.
    /// </summary>
    /// <returns>Topologically sorted list of projects.</returns>
    private List<ProjectInfo> TopologicalSort()
    {
        var result = new List<ProjectInfo>();
        var outDegree = new Dictionary<ProjectInfo, int>();
        var queue = new Queue<ProjectInfo>();

        // Initialize out-degrees - we want to process nodes with no dependencies first
        foreach (var node in Nodes)
        {
            outDegree[node.Project] = node.OutDegree;
            if (node.OutDegree == 0)
            {
                // Leaf nodes - no dependencies, can be migrated first
                queue.Enqueue(node.Project);
            }
        }

        // Process nodes with no outgoing edges (dependencies)
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            // Find all edges TO current project (projects that depend on current)
            var incomingEdges = Edges.Where(e => e.To == current).ToList();

            foreach (var edge in incomingEdges)
            {
                outDegree[edge.From]--;
                if (outDegree[edge.From] == 0)
                {
                    // All dependencies of this project have been processed
                    queue.Enqueue(edge.From);
                }
            }
        }

        return result;
    }
}
