# [TASK-007] Implement Dependency Graph

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | Claude Opus 4.5 |
| **Started** | 2026-01-31 |
| **Completed** | 2026-01-31 |

## Dependencies

- **Depends on:** TASK-004, TASK-005, TASK-006
- **Blocks:** TASK-010

---

## Description

Build a dependency graph between projects in a solution to determine correct migration order (leaf nodes first).

---

## Acceptance Criteria

- [x] Builds graph of project dependencies
- [x] Detects circular dependencies (error)
- [x] Returns topologically sorted order for migration
- [x] Identifies "leaf" projects (no dependencies)
- [x] Identifies "root" projects (nothing depends on them)
- [x] Handles cross-solution references gracefully
- [x] Unit tests with various graph shapes

---

## Technical Notes

### DependencyGraph model:

```csharp
public class DependencyGraph
{
    public List<ProjectNode> Nodes { get; set; }
    public List<DependencyEdge> Edges { get; set; }

    public bool HasCircularDependencies { get; }
    public List<string> CircularPaths { get; }

    // Returns projects in order: migrate these first
    public List<ProjectInfo> GetMigrationOrder();

    // Projects with no dependencies
    public List<ProjectInfo> GetLeafProjects();

    // Projects nothing depends on (entry points)
    public List<ProjectInfo> GetRootProjects();
}

public class ProjectNode
{
    public ProjectInfo Project { get; set; }
    public int InDegree { get; set; }   // How many depend on this
    public int OutDegree { get; set; }  // How many this depends on
}

public class DependencyEdge
{
    public ProjectInfo From { get; set; }  // Dependent
    public ProjectInfo To { get; set; }    // Dependency
}
```

### Example:

```
Solution:
  WebApp → BusinessLogic → DataAccess
                        → Common
       → Common

Migration order: Common, DataAccess, BusinessLogic, WebApp
```

### Implementation (Kahn's algorithm for topological sort):

```csharp
public class DependencyGraphBuilder
{
    public DependencyGraph Build(SolutionInfo solution)
    {
        var graph = new DependencyGraph();

        // Add all projects as nodes
        foreach (var project in solution.Projects)
        {
            graph.Nodes.Add(new ProjectNode { Project = project });
        }

        // Add edges based on ProjectReferences
        foreach (var project in solution.Projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = FindProject(solution, reference.Path);
                if (target != null)
                {
                    graph.Edges.Add(new DependencyEdge
                    {
                        From = project,
                        To = target
                    });
                }
            }
        }

        // Check for cycles
        graph.DetectCycles();

        return graph;
    }

    public List<ProjectInfo> TopologicalSort(DependencyGraph graph)
    {
        // Kahn's algorithm
        var result = new List<ProjectInfo>();
        var inDegree = new Dictionary<ProjectInfo, int>();
        var queue = new Queue<ProjectInfo>();

        // Initialize in-degrees
        // ... implementation

        return result;
    }
}
```

### Visualization (optional, for reports):

```
WebApp
├── BusinessLogic
│   ├── DataAccess
│   │   └── Common
│   └── Common
└── Common
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
| 2026-01-31 | Claude Opus 4.5 | Implemented dependency graph models, builder, and comprehensive tests |
| 2026-01-31 | Claude Opus 4.5 | Completed - All tests passing (99/99) |
