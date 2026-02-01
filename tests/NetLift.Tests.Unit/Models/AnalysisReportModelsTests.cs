using FluentAssertions;
using NetLift.Core.Models;
using System.Text.Json;

namespace NetLift.Tests.Unit.Models;

public class AnalysisReportModelsTests
{
    [Fact]
    public void AnalysisReport_CanBeSerializedToJson()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "1.0.0",
            SolutionPath = @"C:\Test\Solution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 5,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Projects = new List<ProjectAnalysis>
            {
                new()
                {
                    ProjectName = "Library1",
                    ProjectPath = @"C:\Test\Library1\Library1.csproj",
                    PrimaryType = ProjectType.CSharpClassLibrary,
                    SourceFileCount = 10,
                    EstimatedLinesOfCode = 1000
                }
            },
            OverallComplexity = new MigrationComplexity
            {
                Level = ComplexityLevel.Medium,
                Score = 45,
                Factors = new List<string> { "Some complexity factor" }
            },
            Issues = new List<CompatibilityIssue>
            {
                new()
                {
                    Severity = IssueSeverity.Warning,
                    Category = "NuGet",
                    Description = "Test issue",
                    AffectedProject = "Library1"
                }
            },
            RecommendedPhases = new List<MigrationPhase>
            {
                new()
                {
                    Order = 1,
                    Name = "Phase 1",
                    Description = "First phase",
                    AffectedProjects = new List<string> { "Library1" },
                    EstimatedAutoPercentage = 80,
                    ManualSteps = new List<string> { "Step 1", "Step 2" }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<AnalysisReport>(json);

        // Assert
        json.Should().NotBeNullOrEmpty();
        deserialized.Should().NotBeNull();
        deserialized!.SolutionName.Should().Be("TestSolution");
        deserialized.TotalProjects.Should().Be(5);
        deserialized.Projects.Should().HaveCount(1);
        deserialized.Issues.Should().HaveCount(1);
        deserialized.RecommendedPhases.Should().HaveCount(1);
    }

    [Fact]
    public void MigrationComplexity_ShouldHaveCorrectLevels()
    {
        // Arrange & Act
        var lowComplexity = new MigrationComplexity { Score = 20, Level = ComplexityLevel.Low };
        var mediumComplexity = new MigrationComplexity { Score = 40, Level = ComplexityLevel.Medium };
        var highComplexity = new MigrationComplexity { Score = 65, Level = ComplexityLevel.High };
        var veryHighComplexity = new MigrationComplexity { Score = 90, Level = ComplexityLevel.VeryHigh };

        // Assert
        lowComplexity.Level.Should().Be(ComplexityLevel.Low);
        mediumComplexity.Level.Should().Be(ComplexityLevel.Medium);
        highComplexity.Level.Should().Be(ComplexityLevel.High);
        veryHighComplexity.Level.Should().Be(ComplexityLevel.VeryHigh);
    }

    [Fact]
    public void CompatibilityIssue_AllSeverityLevels_ShouldBeValid()
    {
        // Arrange & Act
        var infoIssue = new CompatibilityIssue { Severity = IssueSeverity.Info };
        var warningIssue = new CompatibilityIssue { Severity = IssueSeverity.Warning };
        var errorIssue = new CompatibilityIssue { Severity = IssueSeverity.Error };
        var blockerIssue = new CompatibilityIssue { Severity = IssueSeverity.Blocker };

        // Assert
        infoIssue.Severity.Should().Be(IssueSeverity.Info);
        warningIssue.Severity.Should().Be(IssueSeverity.Warning);
        errorIssue.Severity.Should().Be(IssueSeverity.Error);
        blockerIssue.Severity.Should().Be(IssueSeverity.Blocker);
    }

    [Fact]
    public void ProjectAnalysis_WithAllFlags_ShouldStoreCorrectly()
    {
        // Arrange & Act
        var analysis = new ProjectAnalysis
        {
            ProjectName = "TestProject",
            IsMvc = true,
            IsWebApi = true,
            IsWcfService = false,
            UsesEf6 = true,
            SourceFileCount = 50,
            EstimatedLinesOfCode = 5000,
            DependencyCount = 25
        };

        // Assert
        analysis.IsMvc.Should().BeTrue();
        analysis.IsWebApi.Should().BeTrue();
        analysis.IsWcfService.Should().BeFalse();
        analysis.UsesEf6.Should().BeTrue();
        analysis.SourceFileCount.Should().Be(50);
        analysis.EstimatedLinesOfCode.Should().Be(5000);
        analysis.DependencyCount.Should().Be(25);
    }

    [Fact]
    public void DependencyAnalysis_WithReplacementPackage_ShouldStoreInformation()
    {
        // Arrange & Act
        var dependency = new DependencyAnalysis
        {
            PackageId = "EntityFramework",
            CurrentVersion = "6.4.4",
            Compatibility = PackageCompatibility.HasReplacement,
            RecommendedVersion = null,
            ReplacementPackage = "Microsoft.EntityFrameworkCore",
            Notes = "Migrate to EF Core for better performance"
        };

        // Assert
        dependency.PackageId.Should().Be("EntityFramework");
        dependency.Compatibility.Should().Be(PackageCompatibility.HasReplacement);
        dependency.ReplacementPackage.Should().Be("Microsoft.EntityFrameworkCore");
        dependency.Notes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MigrationPhase_WithManualSteps_ShouldStoreAllData()
    {
        // Arrange & Act
        var phase = new MigrationPhase
        {
            Order = 1,
            Name = "Foundation Libraries",
            Description = "Migrate base libraries first",
            AffectedProjects = new List<string> { "Library1", "Library2" },
            EstimatedAutoPercentage = 85,
            ManualSteps = new List<string>
            {
                "Review package references",
                "Update incompatible APIs",
                "Run tests"
            }
        };

        // Assert
        phase.Order.Should().Be(1);
        phase.Name.Should().Be("Foundation Libraries");
        phase.AffectedProjects.Should().HaveCount(2);
        phase.ManualSteps.Should().HaveCount(3);
        phase.EstimatedAutoPercentage.Should().Be(85);
    }

    [Fact]
    public void CompatibilityIssue_WithOptionalFields_ShouldHandleNulls()
    {
        // Arrange & Act
        var issue = new CompatibilityIssue
        {
            Severity = IssueSeverity.Warning,
            Category = "API",
            Description = "API change detected",
            AffectedProject = "Project1",
            AffectedFile = null,
            LineNumber = null,
            Recommendation = "Update to new API",
            DocumentationUrl = null
        };

        // Assert
        issue.AffectedFile.Should().BeNull();
        issue.LineNumber.Should().BeNull();
        issue.DocumentationUrl.Should().BeNull();
        issue.Recommendation.Should().NotBeNull();
    }

    [Fact]
    public void AnalysisReport_DefaultValues_ShouldInitializeCollections()
    {
        // Arrange & Act
        var report = new AnalysisReport();

        // Assert
        report.Projects.Should().NotBeNull().And.BeEmpty();
        report.Issues.Should().NotBeNull().And.BeEmpty();
        report.RecommendedPhases.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProjectAnalysis_DefaultValues_ShouldInitializeCollections()
    {
        // Arrange & Act
        var analysis = new ProjectAnalysis();

        // Assert
        analysis.Dependencies.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MigrationComplexity_DefaultValues_ShouldInitializeFactors()
    {
        // Arrange & Act
        var complexity = new MigrationComplexity();

        // Assert
        complexity.Factors.Should().NotBeNull().And.BeEmpty();
    }
}
