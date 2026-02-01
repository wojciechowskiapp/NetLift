namespace NetLift.Tests.Unit.Validation;

using NetLift.Core.Models;
using NetLift.Validation;
using Xunit;
using FluentAssertions;

/// <summary>
/// Unit tests for <see cref="HtmlReportGenerator"/>.
/// </summary>
public class HtmlReportGeneratorTests
{
    private readonly HtmlReportGenerator _generator;

    public HtmlReportGeneratorTests()
    {
        _generator = new HtmlReportGenerator();
    }

    [Fact]
    public void Generate_WithValidReport_ShouldReturnValidHtml()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().NotBeNullOrEmpty();
        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("</html>");
    }

    [Fact]
    public void Generate_ShouldIncludeDoctype()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().StartWith("<!DOCTYPE html>");
    }

    [Fact]
    public void Generate_ShouldIncludeSolutionName()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("TestSolution");
    }

    [Fact]
    public void Generate_ShouldIncludeEmbeddedCss()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("<style>");
        html.Should().Contain("--primary:");
        html.Should().Contain("--bg:");
        html.Should().Contain(".card");
        html.Should().Contain(".badge");
        html.Should().NotContain("<link rel=\"stylesheet\"");
    }

    [Fact]
    public void Generate_ShouldIncludeMetaViewportForMobileFirst()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    }

    [Fact]
    public void Generate_ShouldIncludeGenerationTimestamp()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Generated:");
        html.Should().Contain(report.GeneratedAt.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void Generate_ShouldIncludeToolVersion()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Tool Version:");
        html.Should().Contain("0.1.0");
    }

    [Fact]
    public void Generate_ShouldIncludeOverviewSection()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Solution Overview");
        html.Should().Contain("Total Projects");
        html.Should().Contain("Overall Complexity");
        html.Should().Contain("Estimated Auto-Migration");
        html.Should().Contain("Target Framework");
    }

    [Fact]
    public void Generate_ShouldIncludeProjectCards()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Projects");
        html.Should().Contain("TestProject1");
        html.Should().Contain("TestProject2");
        html.Should().Contain("project-card");
    }

    [Fact]
    public void Generate_ShouldIncludeComplexityBadges()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("badge-low");
        html.Should().Contain("badge-medium");
    }

    [Fact]
    public void Generate_ShouldColorCodeComplexityCorrectly()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 4,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Projects = new List<ProjectAnalysis>
            {
                new ProjectAnalysis
                {
                    ProjectName = "LowComplexity",
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.Low, Score = 20 }
                },
                new ProjectAnalysis
                {
                    ProjectName = "MediumComplexity",
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.Medium, Score = 40 }
                },
                new ProjectAnalysis
                {
                    ProjectName = "HighComplexity",
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.High, Score = 60 }
                },
                new ProjectAnalysis
                {
                    ProjectName = "VeryHighComplexity",
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.VeryHigh, Score = 90 }
                }
            }
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("badge-low");
        html.Should().Contain("badge-medium");
        html.Should().Contain("badge-high");
        html.Should().Contain("badge-very-high");
    }

    [Fact]
    public void Generate_ShouldIncludePackagesSection()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Package Dependencies");
        html.Should().Contain("Newtonsoft.Json");
        html.Should().Contain("EntityFramework");
        html.Should().Contain("Compatibility");
    }

    [Fact]
    public void Generate_ShouldIncludePackageCompatibilityStatus()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("badge-compatible");
        html.Should().Contain("badge-has-replacement");
    }

    [Fact]
    public void Generate_ShouldIncludeIssuesSection()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Compatibility Issues");
        html.Should().Contain("System.Web dependency detected");
        html.Should().Contain("WCF service detected");
    }

    [Fact]
    public void Generate_ShouldGroupIssuesBySeverity()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Error");
        html.Should().Contain("Warning");
        html.Should().Contain("badge-error");
        html.Should().Contain("badge-warning");
    }

    [Fact]
    public void Generate_ShouldIncludeMigrationPlanSection()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Recommended Migration Plan");
        html.Should().Contain("Phase 1: Class Libraries");
        html.Should().Contain("Phase 2: Web Applications");
    }

    [Fact]
    public void Generate_ShouldIncludePhaseDetails()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Affected Projects:");
        html.Should().Contain("Manual Steps:");
        html.Should().Contain("Update package references");
        html.Should().Contain("Migrate to ASP.NET Core");
    }

    [Fact]
    public void Generate_ShouldEscapeHtmlInContent()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "Test<Solution>&\"Name\"",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Projects = new List<ProjectAnalysis>
            {
                new ProjectAnalysis
                {
                    ProjectName = "Test<Project>&\"Name\"",
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.Low, Score = 20 }
                }
            }
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Test&lt;Solution&gt;&amp;&quot;Name&quot;");
        html.Should().Contain("Test&lt;Project&gt;&amp;&quot;Name&quot;");
        html.Should().NotContain("Test<Solution>&\"Name\"");
    }

    [Fact]
    public void Generate_ShouldHandleEmptyProjects()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "EmptySolution",
            TotalProjects = 0,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 0,
            Projects = new List<ProjectAnalysis>()
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("No projects found");
    }

    [Fact]
    public void Generate_ShouldHandleEmptyIssues()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 100,
            Issues = new List<CompatibilityIssue>()
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("No compatibility issues found");
    }

    [Fact]
    public void Generate_ShouldHandleEmptyMigrationPhases()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            RecommendedPhases = new List<MigrationPhase>()
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("No migration phases defined");
    }

    [Fact]
    public void Generate_ShouldIncludeProgressBars()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("progress-bar");
        html.Should().Contain("progress-fill");
    }

    [Fact]
    public void Generate_ShouldIncludePurpleColorScheme()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("--primary: #8b5cf6");
        html.Should().Contain("--primary-dark: #6d28d9");
    }

    [Fact]
    public void Generate_ShouldIncludeDarkTheme()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("--bg: #0f0f1e");
        html.Should().Contain("--bg-secondary: #1a1a2e");
        html.Should().Contain("--card: #16213e");
    }

    [Fact]
    public void Generate_ShouldIncludeProjectTypeDetection()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Projects = new List<ProjectAnalysis>
            {
                new ProjectAnalysis
                {
                    ProjectName = "WebProject",
                    IsMvc = true,
                    IsWebApi = true,
                    UsesEf6 = true,
                    Complexity = new MigrationComplexity { Level = ComplexityLevel.Medium, Score = 50 }
                }
            }
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("MVC");
        html.Should().Contain("Web API");
        html.Should().Contain("EF6");
    }

    [Fact]
    public void Generate_ShouldIncludeProjectStatistics()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("Framework:");
        html.Should().Contain("Dependencies:");
        html.Should().Contain("Files:");
        html.Should().Contain("Est. LOC:");
    }

    [Fact]
    public void Generate_ShouldHandleAllPackageCompatibilityStatuses()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Projects = new List<ProjectAnalysis>
            {
                new ProjectAnalysis
                {
                    ProjectName = "TestProject",
                    Dependencies = new List<DependencyAnalysis>
                    {
                        new DependencyAnalysis
                        {
                            PackageId = "CompatiblePackage",
                            CurrentVersion = "1.0.0",
                            Compatibility = PackageCompatibility.Compatible
                        },
                        new DependencyAnalysis
                        {
                            PackageId = "IncompatiblePackage",
                            CurrentVersion = "1.0.0",
                            Compatibility = PackageCompatibility.Incompatible
                        },
                        new DependencyAnalysis
                        {
                            PackageId = "DeprecatedPackage",
                            CurrentVersion = "1.0.0",
                            Compatibility = PackageCompatibility.Deprecated
                        },
                        new DependencyAnalysis
                        {
                            PackageId = "UnknownPackage",
                            CurrentVersion = "1.0.0",
                            Compatibility = PackageCompatibility.Unknown
                        }
                    }
                }
            }
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("badge-compatible");
        html.Should().Contain("badge-incompatible");
        html.Should().Contain("badge-deprecated");
        html.Should().Contain("badge-unknown");
    }

    [Fact]
    public void Generate_ShouldHandleAllIssueSeverities()
    {
        // Arrange
        var report = new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 1,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            Issues = new List<CompatibilityIssue>
            {
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = "Info",
                    Description = "Info issue",
                    AffectedProject = "Test"
                },
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Warning",
                    Description = "Warning issue",
                    AffectedProject = "Test"
                },
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Error",
                    Description = "Error issue",
                    AffectedProject = "Test"
                },
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Blocker,
                    Category = "Blocker",
                    Description = "Blocker issue",
                    AffectedProject = "Test"
                }
            }
        };

        // Act
        var html = _generator.Generate(report);

        // Assert
        html.Should().Contain("badge-info");
        html.Should().Contain("badge-warning");
        html.Should().Contain("badge-error");
        html.Should().Contain("badge-blocker");
    }

    private AnalysisReport CreateSampleReport()
    {
        return new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = "0.1.0",
            SolutionPath = "C:\\Test\\TestSolution.sln",
            SolutionName = "TestSolution",
            TotalProjects = 2,
            TargetFramework = "net8.0",
            EstimatedAutoMigrationPercentage = 75,
            OverallComplexity = new MigrationComplexity
            {
                Level = ComplexityLevel.Medium,
                Score = 50,
                Factors = new List<string> { "Legacy frameworks", "Package dependencies" }
            },
            Projects = new List<ProjectAnalysis>
            {
                new ProjectAnalysis
                {
                    ProjectName = "TestProject1",
                    ProjectPath = "C:\\Test\\TestProject1\\TestProject1.csproj",
                    PrimaryType = ProjectType.CSharpClassLibrary,
                    IsMvc = false,
                    IsWebApi = false,
                    UsesEf6 = false,
                    SourceFileCount = 10,
                    EstimatedLinesOfCode = 500,
                    DependencyCount = 3,
                    Complexity = new MigrationComplexity
                    {
                        Level = ComplexityLevel.Low,
                        Score = 25
                    },
                    Dependencies = new List<DependencyAnalysis>
                    {
                        new DependencyAnalysis
                        {
                            PackageId = "Newtonsoft.Json",
                            CurrentVersion = "12.0.3",
                            Compatibility = PackageCompatibility.Compatible
                        }
                    }
                },
                new ProjectAnalysis
                {
                    ProjectName = "TestProject2",
                    ProjectPath = "C:\\Test\\TestProject2\\TestProject2.csproj",
                    PrimaryType = ProjectType.CSharpWeb,
                    IsMvc = true,
                    IsWebApi = true,
                    UsesEf6 = true,
                    SourceFileCount = 50,
                    EstimatedLinesOfCode = 2500,
                    DependencyCount = 15,
                    Complexity = new MigrationComplexity
                    {
                        Level = ComplexityLevel.Medium,
                        Score = 50
                    },
                    Dependencies = new List<DependencyAnalysis>
                    {
                        new DependencyAnalysis
                        {
                            PackageId = "EntityFramework",
                            CurrentVersion = "6.4.4",
                            Compatibility = PackageCompatibility.HasReplacement,
                            ReplacementPackage = "Microsoft.EntityFrameworkCore"
                        }
                    }
                }
            },
            Issues = new List<CompatibilityIssue>
            {
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "API",
                    Description = "System.Web dependency detected",
                    AffectedProject = "TestProject2",
                    Recommendation = "Migrate to ASP.NET Core"
                },
                new CompatibilityIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "WCF",
                    Description = "WCF service detected",
                    AffectedProject = "TestProject2",
                    Recommendation = "Consider migrating to gRPC or REST API"
                }
            },
            RecommendedPhases = new List<MigrationPhase>
            {
                new MigrationPhase
                {
                    Order = 1,
                    Name = "Phase 1: Class Libraries",
                    Description = "Migrate class libraries first as they have fewer dependencies",
                    AffectedProjects = new List<string> { "TestProject1" },
                    EstimatedAutoPercentage = 90,
                    ManualSteps = new List<string>
                    {
                        "Update package references",
                        "Verify unit tests"
                    }
                },
                new MigrationPhase
                {
                    Order = 2,
                    Name = "Phase 2: Web Applications",
                    Description = "Migrate web applications after updating dependencies",
                    AffectedProjects = new List<string> { "TestProject2" },
                    EstimatedAutoPercentage = 60,
                    ManualSteps = new List<string>
                    {
                        "Migrate to ASP.NET Core",
                        "Update authentication",
                        "Test all endpoints"
                    }
                }
            }
        };
    }
}
