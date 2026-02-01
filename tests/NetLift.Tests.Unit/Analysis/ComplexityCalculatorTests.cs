using FluentAssertions;
using NetLift.Analysis;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Analysis;

public class ComplexityCalculatorTests
{
    private readonly ComplexityCalculator _calculator;

    public ComplexityCalculatorTests()
    {
        _calculator = new ComplexityCalculator();
    }

    [Fact]
    public void Calculate_SimpleClassLibrary_ShouldReturnLowComplexity()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "SimpleLibrary",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 1000,
            DependencyCount = 5,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Level.Should().Be(ComplexityLevel.Low);
        result.Score.Should().BeLessThanOrEqualTo(25);
        result.Factors.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_MvcProject_ShouldIncludeMvcFactor()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "MvcApp",
            PrimaryType = ProjectType.CSharpMvc,
            IsMvc = true,
            EstimatedLinesOfCode = 5000,
            DependencyCount = 15,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeGreaterOrEqualTo(20);
        result.Factors.Should().Contain("ASP.NET MVC");
    }

    [Fact]
    public void Calculate_WcfService_ShouldHaveHighComplexity()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "WcfService",
            PrimaryType = ProjectType.WcfService,
            IsWcfService = true,
            EstimatedLinesOfCode = 10000,
            DependencyCount = 20,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeGreaterOrEqualTo(35);
        result.Factors.Should().Contain("WCF Service");
        result.Level.Should().BeOneOf(ComplexityLevel.Medium, ComplexityLevel.High);
    }

    [Fact]
    public void Calculate_WithIncompatiblePackages_ShouldIncreaseScore()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "LibraryWithIssues",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 5000,
            DependencyCount = 10,
            Dependencies = new List<DependencyAnalysis>
            {
                new() { PackageId = "System.Web", Compatibility = PackageCompatibility.Incompatible },
                new() { PackageId = "System.Web.Mvc", Compatibility = PackageCompatibility.Incompatible },
                new() { PackageId = "Newtonsoft.Json", Compatibility = PackageCompatibility.Compatible }
            }
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeGreaterOrEqualTo(10); // 2 incompatible * 5 = 10
        result.Factors.Should().Contain(f => f.Contains("incompatible package"));
    }

    [Fact]
    public void Calculate_WithReplacementPackages_ShouldIncreaseScore()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "Library",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 3000,
            DependencyCount = 5,
            Dependencies = new List<DependencyAnalysis>
            {
                new() { PackageId = "EntityFramework", Compatibility = PackageCompatibility.HasReplacement },
                new() { PackageId = "Newtonsoft.Json", Compatibility = PackageCompatibility.HasReplacement }
            }
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeGreaterOrEqualTo(6); // 2 * 3 = 6
        result.Factors.Should().Contain(f => f.Contains("requiring replacement"));
    }

    [Fact]
    public void Calculate_LargeCodebase_ShouldIncludeCodebaseSize()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "LargeProject",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 60000,
            DependencyCount = 10,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Factors.Should().Contain(f => f.Contains("Large codebase"));
    }

    [Fact]
    public void Calculate_MediumCodebase_ShouldIncludeMediumCodebaseSize()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "MediumProject",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 30000,
            DependencyCount = 10,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Factors.Should().Contain(f => f.Contains("Medium codebase"));
    }

    [Fact]
    public void Calculate_HighDependencyCount_ShouldIncludeDependencyFactor()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "DependencyHeavyProject",
            PrimaryType = ProjectType.CSharpClassLibrary,
            EstimatedLinesOfCode = 5000,
            DependencyCount = 60,
            Dependencies = Enumerable.Range(0, 60)
                .Select(i => new DependencyAnalysis
                {
                    PackageId = $"Package{i}",
                    Compatibility = PackageCompatibility.Compatible
                })
                .ToList()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Factors.Should().Contain(f => f.Contains("High dependency count"));
    }

    [Fact]
    public void Calculate_WebFormsProject_ShouldHaveHighComplexity()
    {
        // Arrange
        var project = new ProjectAnalysis
        {
            ProjectName = "WebFormsApp",
            PrimaryType = ProjectType.AspNetWebForms,
            EstimatedLinesOfCode = 15000,
            DependencyCount = 20,
            Dependencies = new List<DependencyAnalysis>()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeGreaterOrEqualTo(25);
        result.Factors.Should().Contain("ASP.NET Web Forms");
    }

    [Fact]
    public void Calculate_ScoreShouldNotExceed100()
    {
        // Arrange - Create a project with many complexity factors
        var project = new ProjectAnalysis
        {
            ProjectName = "VeryComplexProject",
            PrimaryType = ProjectType.AspNetWebForms,
            IsMvc = true,
            IsWebApi = true,
            IsWcfService = true,
            UsesEf6 = true,
            EstimatedLinesOfCode = 100000,
            DependencyCount = 100,
            Dependencies = Enumerable.Range(0, 50)
                .Select(i => new DependencyAnalysis
                {
                    PackageId = $"Package{i}",
                    Compatibility = PackageCompatibility.Incompatible
                })
                .ToList()
        };

        // Act
        var result = _calculator.Calculate(project);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().BeLessThanOrEqualTo(100);
        result.Level.Should().Be(ComplexityLevel.VeryHigh);
    }

    [Fact]
    public void CalculateOverall_EmptyList_ShouldReturnLowComplexity()
    {
        // Arrange
        var projects = new List<ProjectAnalysis>();

        // Act
        var result = _calculator.CalculateOverall(projects);

        // Assert
        result.Should().NotBeNull();
        result.Level.Should().Be(ComplexityLevel.Low);
        result.Score.Should().Be(0);
        result.Factors.Should().Contain("No projects to analyze");
    }

    [Fact]
    public void CalculateOverall_MultipleProjects_ShouldCalculateWeightedAverage()
    {
        // Arrange
        var projects = new List<ProjectAnalysis>
        {
            new()
            {
                ProjectName = "SmallSimple",
                PrimaryType = ProjectType.CSharpClassLibrary,
                EstimatedLinesOfCode = 1000,
                DependencyCount = 5,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 10, Level = ComplexityLevel.Low }
            },
            new()
            {
                ProjectName = "LargeComplex",
                PrimaryType = ProjectType.CSharpMvc,
                IsMvc = true,
                EstimatedLinesOfCode = 9000,
                DependencyCount = 30,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 40, Level = ComplexityLevel.Medium }
            }
        };

        // Act
        var result = _calculator.CalculateOverall(projects);

        // Assert
        result.Should().NotBeNull();
        // Weighted: (1000/10000 * 10) + (9000/10000 * 40) = 1 + 36 = 37
        result.Score.Should().BeInRange(35, 40);
    }

    [Fact]
    public void CalculateOverall_ShouldIncludeProjectCountFactors()
    {
        // Arrange
        var projects = new List<ProjectAnalysis>
        {
            new()
            {
                ProjectName = "Project1",
                PrimaryType = ProjectType.CSharpMvc,
                IsMvc = true,
                EstimatedLinesOfCode = 5000,
                DependencyCount = 10,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 60, Level = ComplexityLevel.High }
            },
            new()
            {
                ProjectName = "Project2",
                PrimaryType = ProjectType.WcfService,
                IsWcfService = true,
                EstimatedLinesOfCode = 5000,
                DependencyCount = 10,
                Dependencies = new List<DependencyAnalysis>(),
                Complexity = new MigrationComplexity { Score = 70, Level = ComplexityLevel.High }
            }
        };

        // Act
        var result = _calculator.CalculateOverall(projects);

        // Assert
        result.Should().NotBeNull();
        result.Factors.Should().Contain(f => f.Contains("high complexity"));
        result.Factors.Should().Contain(f => f.Contains("ASP.NET MVC"));
        result.Factors.Should().Contain(f => f.Contains("WCF"));
    }
}
