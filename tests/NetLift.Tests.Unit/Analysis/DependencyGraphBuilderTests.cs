using FluentAssertions;
using NetLift.Analysis;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Analysis;

public class DependencyGraphBuilderTests
{
    private readonly DependencyGraphBuilder _builder;

    public DependencyGraphBuilderTests()
    {
        _builder = new DependencyGraphBuilder();
    }

    [Fact]
    public void Build_WithSingleProject_CreatesGraphWithOneNode()
    {
        // Arrange
        var project = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj");
        var solution = CreateSolution("Solution", new[] { project });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { project });

        // Assert
        graph.Should().NotBeNull();
        graph.Nodes.Should().HaveCount(1);
        graph.Nodes[0].Project.Should().Be(project);
        graph.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithTwoProjectsAndDependency_CreatesEdge()
    {
        // Arrange
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj");
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB });

        // Assert
        graph.Nodes.Should().HaveCount(2);
        graph.Edges.Should().HaveCount(1);

        var edge = graph.Edges[0];
        edge.From.Should().Be(projectA);
        edge.To.Should().Be(projectB);
        edge.Type.Should().Be(DependencyType.Project);
    }

    [Fact]
    public void Build_WithDependencies_UpdatesNodeDegrees()
    {
        // Arrange
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj");
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC });

        // Assert
        var nodeA = graph.Nodes.First(n => n.Project.Name == "ProjectA");
        var nodeB = graph.Nodes.First(n => n.Project.Name == "ProjectB");
        var nodeC = graph.Nodes.First(n => n.Project.Name == "ProjectC");

        // ProjectA depends on ProjectB (OutDegree = 1, InDegree = 0)
        nodeA.OutDegree.Should().Be(1);
        nodeA.InDegree.Should().Be(0);

        // ProjectB depends on ProjectC, and is depended on by ProjectA (OutDegree = 1, InDegree = 1)
        nodeB.OutDegree.Should().Be(1);
        nodeB.InDegree.Should().Be(1);

        // ProjectC is depended on by ProjectB (OutDegree = 0, InDegree = 1)
        nodeC.OutDegree.Should().Be(0);
        nodeC.InDegree.Should().Be(1);
    }

    [Fact]
    public void Build_WithCircularDependency_DetectsCycle()
    {
        // Arrange - A -> B -> C -> A
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectA\\ProjectA.csproj", Name = "ProjectA" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC });

        // Assert
        graph.HasCircularDependencies.Should().BeTrue();
        graph.CircularPaths.Should().NotBeEmpty();
        graph.CircularPaths[0].Should().Contain("ProjectA");
        graph.CircularPaths[0].Should().Contain("ProjectB");
        graph.CircularPaths[0].Should().Contain("ProjectC");
    }

    [Fact]
    public void Build_WithMultipleCircularDependencies_DetectsAllCycles()
    {
        // Arrange - A <-> B and C <-> D
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectA\\ProjectA.csproj", Name = "ProjectA" } });
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectD\\ProjectD.csproj", Name = "ProjectD" } });
        var projectD = CreateProject("ProjectD", "F:\\ProjectD\\ProjectD.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC, projectD });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC, projectD });

        // Assert
        graph.HasCircularDependencies.Should().BeTrue();
        graph.CircularPaths.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetLeafProjects_ReturnsProjectsWithNoDependencies()
    {
        // Arrange
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj");
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC });

        // Act
        var leafProjects = graph.GetLeafProjects();

        // Assert
        leafProjects.Should().HaveCount(1);
        leafProjects[0].Should().Be(projectC);
    }

    [Fact]
    public void GetRootProjects_ReturnsProjectsWithNoIncomingDependencies()
    {
        // Arrange
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj");
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC });

        // Act
        var rootProjects = graph.GetRootProjects();

        // Assert
        rootProjects.Should().HaveCount(1);
        rootProjects[0].Should().Be(projectA);
    }

    [Fact]
    public void GetMigrationOrder_WithLinearDependencies_ReturnsCorrectOrder()
    {
        // Arrange - A -> B -> C (C should be first, A should be last)
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj");
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" } });
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC });

        // Act
        var migrationOrder = graph.GetMigrationOrder();

        // Assert
        migrationOrder.Should().HaveCount(3);
        migrationOrder[0].Should().Be(projectC); // Leaf project first
        migrationOrder[1].Should().Be(projectB);
        migrationOrder[2].Should().Be(projectA); // Root project last
    }

    [Fact]
    public void GetMigrationOrder_WithDiamondDependencies_ReturnsValidTopologicalOrder()
    {
        // Arrange - Diamond: A -> B, A -> C, B -> D, C -> D
        var projectD = CreateProject("ProjectD", "F:\\ProjectD\\ProjectD.csproj");
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectD\\ProjectD.csproj", Name = "ProjectD" } });
        var projectC = CreateProject("ProjectC", "F:\\ProjectC\\ProjectC.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectD\\ProjectD.csproj", Name = "ProjectD" } });
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[]
            {
                new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" },
                new ProjectReference { Path = "..\\ProjectC\\ProjectC.csproj", Name = "ProjectC" }
            });

        var solution = CreateSolution("Solution", new[] { projectA, projectB, projectC, projectD });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB, projectC, projectD });

        // Act
        var migrationOrder = graph.GetMigrationOrder();

        // Assert
        migrationOrder.Should().HaveCount(4);
        migrationOrder[0].Should().Be(projectD); // D must be first (leaf)
        migrationOrder[3].Should().Be(projectA); // A must be last (root)

        // B and C can be in any order, but both must come after D and before A
        var indexB = migrationOrder.IndexOf(projectB);
        var indexC = migrationOrder.IndexOf(projectC);
        var indexD = migrationOrder.IndexOf(projectD);
        var indexA = migrationOrder.IndexOf(projectA);

        indexD.Should().BeLessThan(indexB);
        indexD.Should().BeLessThan(indexC);
        indexB.Should().BeLessThan(indexA);
        indexC.Should().BeLessThan(indexA);
    }

    [Fact]
    public void GetMigrationOrder_WithCircularDependencies_ThrowsInvalidOperationException()
    {
        // Arrange - A -> B -> A
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectA\\ProjectA.csproj", Name = "ProjectA" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB });

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => graph.GetMigrationOrder());
        exception.Message.Should().Contain("circular dependencies");
    }

    [Fact]
    public void Build_WithPackageDependencies_StoresPackageReferences()
    {
        // Arrange
        var packages = new[]
        {
            new PackageReference { Id = "Newtonsoft.Json", Version = "13.0.1" },
            new PackageReference { Id = "AutoMapper", Version = "12.0.0" }
        };

        var project = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj", packageReferences: packages);
        var solution = CreateSolution("Solution", new[] { project });

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo> { project });

        // Assert
        var node = graph.Nodes[0];
        node.PackageDependencies.Should().HaveCount(2);
        node.PackageDependencies.Should().Contain(p => p.Id == "Newtonsoft.Json");
        node.PackageDependencies.Should().Contain(p => p.Id == "AutoMapper");
    }

    [Fact]
    public void Build_WithComplexGraph_HandlesMultipleDependencies()
    {
        // Arrange - Complex scenario:
        // WebApp -> BusinessLogic -> DataAccess -> Common
        //                         -> Common
        //       -> Common
        var common = CreateProject("Common", "F:\\Common\\Common.csproj");
        var dataAccess = CreateProject("DataAccess", "F:\\DataAccess\\DataAccess.csproj",
            new[] { new ProjectReference { Path = "..\\Common\\Common.csproj", Name = "Common" } });
        var businessLogic = CreateProject("BusinessLogic", "F:\\BusinessLogic\\BusinessLogic.csproj",
            new[]
            {
                new ProjectReference { Path = "..\\DataAccess\\DataAccess.csproj", Name = "DataAccess" },
                new ProjectReference { Path = "..\\Common\\Common.csproj", Name = "Common" }
            });
        var webApp = CreateProject("WebApp", "F:\\WebApp\\WebApp.csproj",
            new[]
            {
                new ProjectReference { Path = "..\\BusinessLogic\\BusinessLogic.csproj", Name = "BusinessLogic" },
                new ProjectReference { Path = "..\\Common\\Common.csproj", Name = "Common" }
            });

        var solution = CreateSolution("Solution", new[] { webApp, businessLogic, dataAccess, common });
        var graph = _builder.Build(solution, new List<ProjectInfo> { webApp, businessLogic, dataAccess, common });

        // Act
        var migrationOrder = graph.GetMigrationOrder();

        // Assert
        migrationOrder.Should().HaveCount(4);
        migrationOrder[0].Should().Be(common); // Common must be first
        migrationOrder[3].Should().Be(webApp); // WebApp must be last

        // DataAccess must come before BusinessLogic
        var indexDataAccess = migrationOrder.IndexOf(dataAccess);
        var indexBusinessLogic = migrationOrder.IndexOf(businessLogic);
        indexDataAccess.Should().BeLessThan(indexBusinessLogic);
    }

    [Fact]
    public void Build_WithNoProjects_ReturnsEmptyGraph()
    {
        // Arrange
        var solution = CreateSolution("Solution", Array.Empty<ProjectInfo>());

        // Act
        var graph = _builder.Build(solution, new List<ProjectInfo>());

        // Assert
        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
        graph.HasCircularDependencies.Should().BeFalse();
    }

    [Fact]
    public void DetectCircularDependencies_WithNoCycles_ReturnsEmptyList()
    {
        // Arrange
        var projectB = CreateProject("ProjectB", "F:\\ProjectB\\ProjectB.csproj");
        var projectA = CreateProject("ProjectA", "F:\\ProjectA\\ProjectA.csproj",
            new[] { new ProjectReference { Path = "..\\ProjectB\\ProjectB.csproj", Name = "ProjectB" } });

        var solution = CreateSolution("Solution", new[] { projectA, projectB });
        var graph = _builder.Build(solution, new List<ProjectInfo> { projectA, projectB });

        // Act
        var cycles = _builder.DetectCircularDependencies(graph);

        // Assert
        cycles.Should().BeEmpty();
    }

    // Helper methods

    private ProjectInfo CreateProject(
        string name,
        string filePath,
        ProjectReference[]? projectReferences = null,
        PackageReference[]? packageReferences = null)
    {
        return new ProjectInfo
        {
            Name = name,
            FilePath = filePath,
            ProjectReferences = projectReferences?.ToList() ?? new List<ProjectReference>(),
            PackageReferences = packageReferences?.ToList() ?? new List<PackageReference>()
        };
    }

    private SolutionInfo CreateSolution(string name, ProjectInfo[] projects)
    {
        return new SolutionInfo
        {
            Name = name,
            FilePath = $"F:\\{name}\\{name}.sln"
        };
    }
}
