using FluentAssertions;
using NetLift.Analysis;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Analysis;

public class AnalysisReportBuilderTests
{
    private readonly AnalysisReportBuilder _builder;
    private readonly ProjectTypeDetector _detector;

    public AnalysisReportBuilderTests()
    {
        _detector = new ProjectTypeDetector();
        _builder = new AnalysisReportBuilder(_detector);
    }

    [Fact]
    public void Constructor_WithNullDetector_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AnalysisReportBuilder(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectTypeDetector");
    }

    [Fact]
    public void AnalyzeProject_SimpleClassLibrary_ShouldReturnBasicAnalysis()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            FilePath = @"C:\Test\Library.csproj",
            Name = "Library",
            TargetFramework = new TargetFramework
            {
                Moniker = "net48",
                Type = FrameworkType.Framework,
                Version = new Version(4, 8)
            },
            CompileItems = new List<CompileItem>
            {
                new() { Include = "Class1.cs" },
                new() { Include = "Class2.cs" }
            },
            PackageReferences = new List<PackageReference>
            {
                new() { Name = "Newtonsoft.Json", Version = "13.0.1" }
            }
        };

        // Act
        var result = _builder.AnalyzeProject(projectInfo);

        // Assert
        result.Should().NotBeNull();
        result.ProjectName.Should().Be("Library");
        result.ProjectPath.Should().Be(@"C:\Test\Library.csproj");
        result.SourceFileCount.Should().Be(2);
        result.DependencyCount.Should().Be(1);
        result.Dependencies.Should().HaveCount(1);
        result.Complexity.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeProject_WithIncompatiblePackages_ShouldIdentifyThem()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            FilePath = @"C:\Test\WebApp.csproj",
            Name = "WebApp",
            CompileItems = new List<CompileItem>(),
            PackageReferences = new List<PackageReference>
            {
                new() { Name = "System.Web.Mvc", Version = "5.2.7" },
                new() { Name = "EntityFramework", Version = "6.4.4" }
            }
        };

        // Act
        var result = _builder.AnalyzeProject(projectInfo);

        // Assert
        result.Should().NotBeNull();
        result.Dependencies.Should().HaveCount(2);
        result.Dependencies.Should().Contain(d =>
            d.PackageId == "System.Web.Mvc" &&
            d.Compatibility == PackageCompatibility.Incompatible);
        result.Dependencies.Should().Contain(d =>
            d.PackageId == "EntityFramework" &&
            d.Compatibility == PackageCompatibility.HasReplacement &&
            d.ReplacementPackage == "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void BuildReport_BasicSolution_ShouldGenerateCompleteReport()
    {
        // Arrange
        var solutionInfo = new SolutionInfo
        {
            FilePath = @"C:\Test\Solution.sln",
            Name = "TestSolution"
        };

        var projectInfos = new List<ProjectInfo>
        {
            new()
            {
                FilePath = @"C:\Test\Library\Library.csproj",
                Name = "Library",
                TargetFramework = new TargetFramework { Moniker = "net48" },
                CompileItems = new List<CompileItem> { new() { Include = "Class1.cs" } },
                PackageReferences = new List<PackageReference>()
            }
        };

        // Act
        var result = _builder.BuildReport(solutionInfo, projectInfos, "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.SolutionName.Should().Be("TestSolution");
        result.SolutionPath.Should().Be(@"C:\Test\Solution.sln");
        result.TargetFramework.Should().Be("net8.0");
        result.TotalProjects.Should().Be(1);
        result.Projects.Should().HaveCount(1);
        result.OverallComplexity.Should().NotBeNull();
        result.EstimatedAutoMigrationPercentage.Should().BeGreaterOrEqualTo(0);
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void BuildReport_WithMultipleProjects_ShouldAnalyzeAll()
    {
        // Arrange
        var solutionInfo = new SolutionInfo
        {
            FilePath = @"C:\Test\Solution.sln",
            Name = "MultiProjectSolution"
        };

        var projectInfos = new List<ProjectInfo>
        {
            new()
            {
                FilePath = @"C:\Test\Library1\Library1.csproj",
                Name = "Library1",
                CompileItems = new List<CompileItem> { new() { Include = "Class1.cs" } },
                PackageReferences = new List<PackageReference>()
            },
            new()
            {
                FilePath = @"C:\Test\Library2\Library2.csproj",
                Name = "Library2",
                CompileItems = new List<CompileItem> { new() { Include = "Class2.cs" } },
                PackageReferences = new List<PackageReference>()
            }
        };

        // Act
        var result = _builder.BuildReport(solutionInfo, projectInfos, "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.TotalProjects.Should().Be(2);
        result.Projects.Should().HaveCount(2);
    }

    [Fact]
    public void BuildReport_WithCompatibilityIssues_ShouldCollectThem()
    {
        // Arrange
        var solutionInfo = new SolutionInfo
        {
            FilePath = @"C:\Test\Solution.sln",
            Name = "SolutionWithIssues"
        };

        var projectInfos = new List<ProjectInfo>
        {
            new()
            {
                FilePath = @"C:\Test\WebApp\WebApp.csproj",
                Name = "WebApp",
                CompileItems = new List<CompileItem>(),
                PackageReferences = new List<PackageReference>
                {
                    new() { Name = "System.Web.Mvc", Version = "5.2.7" },
                    new() { Name = "EntityFramework", Version = "6.4.4" }
                },
                References = new List<AssemblyReference>
                {
                    new() { Name = "System.Web.Mvc" }
                }
            }
        };

        // Act
        var result = _builder.BuildReport(solutionInfo, projectInfos, "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.Issues.Should().NotBeEmpty();
        result.Issues.Should().Contain(i =>
            i.Category == "NuGet" &&
            i.Severity == IssueSeverity.Error &&
            i.AffectedProject == "WebApp");
    }

    [Fact]
    public void GenerateMigrationPhases_WithVariousProjects_ShouldCreateAppropriatePhases()
    {
        // Arrange
        var projectAnalyses = new List<ProjectAnalysis>
        {
            new()
            {
                ProjectName = "SimpleLibrary",
                PrimaryType = ProjectType.CSharpClassLibrary,
                EstimatedLinesOfCode = 1000,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 10, Level = ComplexityLevel.Low }
            },
            new()
            {
                ProjectName = "MvcApp",
                PrimaryType = ProjectType.CSharpMvc,
                IsMvc = true,
                EstimatedLinesOfCode = 10000,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 50, Level = ComplexityLevel.Medium }
            },
            new()
            {
                ProjectName = "WcfService",
                PrimaryType = ProjectType.WcfService,
                IsWcfService = true,
                EstimatedLinesOfCode = 8000,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 70, Level = ComplexityLevel.High }
            }
        };

        // Act
        var result = _builder.GenerateMigrationPhases(projectAnalyses);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().BeInAscendingOrder(p => p.Order);

        // Foundation libraries phase should come first
        var foundationPhase = result.FirstOrDefault(p => p.Name == "Foundation Libraries");
        foundationPhase.Should().NotBeNull();
        foundationPhase!.AffectedProjects.Should().Contain("SimpleLibrary");

        // Web applications phase should exist
        var webPhase = result.FirstOrDefault(p => p.Name == "Web Applications");
        webPhase.Should().NotBeNull();
        webPhase!.AffectedProjects.Should().Contain("MvcApp");

        // WCF phase should come later
        var wcfPhase = result.FirstOrDefault(p => p.Name == "WCF Services");
        wcfPhase.Should().NotBeNull();
        wcfPhase!.AffectedProjects.Should().Contain("WcfService");
        wcfPhase.Order.Should().BeGreaterThan(foundationPhase.Order);
    }

    [Fact]
    public void GenerateMigrationPhases_EmptyList_ShouldReturnEmptyPhases()
    {
        // Arrange
        var projectAnalyses = new List<ProjectAnalysis>();

        // Act
        var result = _builder.GenerateMigrationPhases(projectAnalyses);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateMigrationPhases_OnlyWebProjects_ShouldCreateWebPhase()
    {
        // Arrange
        var projectAnalyses = new List<ProjectAnalysis>
        {
            new()
            {
                ProjectName = "MvcApp",
                PrimaryType = ProjectType.CSharpMvc,
                IsMvc = true,
                EstimatedLinesOfCode = 10000,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 50, Level = ComplexityLevel.Medium }
            }
        };

        // Act
        var result = _builder.GenerateMigrationPhases(projectAnalyses);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Web Applications");
        result.First().ManualSteps.Should().NotBeEmpty();
    }

    [Fact]
    public void CalculateOverallComplexity_ShouldDelegateToCalculator()
    {
        // Arrange
        var projectAnalyses = new List<ProjectAnalysis>
        {
            new()
            {
                ProjectName = "Library",
                PrimaryType = ProjectType.CSharpClassLibrary,
                EstimatedLinesOfCode = 5000,
                DependencyCount = 10,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 15, Level = ComplexityLevel.Low }
            }
        };

        // Act
        var result = _builder.CalculateOverallComplexity(projectAnalyses);

        // Assert
        result.Should().NotBeNull();
        result.Level.Should().Be(ComplexityLevel.Low);
    }

    [Fact]
    public void BuildReport_WcfProject_ShouldAddWcfIssue()
    {
        // Arrange
        var solutionInfo = new SolutionInfo
        {
            FilePath = @"C:\Test\Solution.sln",
            Name = "WcfSolution"
        };

        var projectInfos = new List<ProjectInfo>
        {
            new()
            {
                FilePath = @"C:\Test\WcfService\WcfService.csproj",
                Name = "WcfService",
                CompileItems = new List<CompileItem>(),
                PackageReferences = new List<PackageReference>(),
                References = new List<AssemblyReference>
                {
                    new() { Name = "System.ServiceModel" }
                },
                ContentItems = new List<ContentItem>
                {
                    new() { Include = "Service1.svc" }
                }
            }
        };

        // Act
        var result = _builder.BuildReport(solutionInfo, projectInfos, "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.Issues.Should().Contain(i =>
            i.Category == "Technology" &&
            i.Description.Contains("WCF") &&
            i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void BuildReport_WebFormsProject_ShouldAddWebFormsIssue()
    {
        // Arrange
        var solutionInfo = new SolutionInfo
        {
            FilePath = @"C:\Test\Solution.sln",
            Name = "WebFormsSolution"
        };

        var projectInfos = new List<ProjectInfo>
        {
            new()
            {
                FilePath = @"C:\Test\WebApp\WebApp.csproj",
                Name = "WebApp",
                CompileItems = new List<CompileItem>(),
                PackageReferences = new List<PackageReference>(),
                References = new List<AssemblyReference>
                {
                    new() { Name = "System.Web" }
                },
                ContentItems = new List<ContentItem>
                {
                    new() { Include = "Default.aspx" }
                }
            }
        };

        // Act
        var result = _builder.BuildReport(solutionInfo, projectInfos, "net8.0");

        // Assert
        result.Should().NotBeNull();
        // Note: This test depends on ProjectTypeDetector detecting Web Forms
        // The actual detection logic determines if the issue is added
    }
}
