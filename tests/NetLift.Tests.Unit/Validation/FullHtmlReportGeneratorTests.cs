namespace NetLift.Tests.Unit.Validation;

using FluentAssertions;
using NetLift.Core.Models;
using NetLift.Validation;
using Xunit;

public class FullHtmlReportGeneratorTests
{
    private readonly FullHtmlReportGenerator _generator;

    public FullHtmlReportGeneratorTests()
    {
        _generator = new FullHtmlReportGenerator();
    }

    [Fact]
    public void Generate_ValidReportData_ReturnsValidHtmlStructure()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().NotBeNullOrEmpty();
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("</html>");
        html.Should().Contain("<head>");
        html.Should().Contain("<body>");
        html.Should().Contain("</body>");
    }

    [Fact]
    public void Generate_ValidReportData_IncludesResponsiveMetaTags()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("<meta charset=\"UTF-8\">");
        html.Should().Contain("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    }

    [Fact]
    public void Generate_ValidReportData_IncludesSolutionName()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("TestSolution");
    }

    [Fact]
    public void Generate_ValidReportData_IncludesAllRequiredSections()
    {
        // Arrange
        var reportData = CreateReportDataWithScoreComponents();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("NetLift Migration Report");
        html.Should().Contain("Confidence Score");
        html.Should().Contain("Score Breakdown");
        html.Should().Contain("Build Results");
        html.Should().Contain("Test Results");
        html.Should().Contain("Migration Issues");
        html.Should().Contain("Recommendations");
    }

    [Fact]
    public void Generate_WithConfidenceScore_DisplaysScoreCircle()
    {
        // Arrange
        var reportData = CreateReportDataWithConfidence(85, ConfidenceLevel.High);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("confidence-circle");
        html.Should().Contain("85");
        html.Should().Contain("High Confidence");
        html.Should().Contain("<svg");
        html.Should().Contain("<circle");
    }

    [Fact]
    public void Generate_WithHighConfidence_UsesSuccessColor()
    {
        // Arrange
        var reportData = CreateReportDataWithConfidence(90, ConfidenceLevel.High);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("#22c55e"); // Success color
    }

    [Fact]
    public void Generate_WithMediumConfidence_UsesWarningColor()
    {
        // Arrange
        var reportData = CreateReportDataWithConfidence(65, ConfidenceLevel.Medium);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("#f59e0b"); // Warning color
    }

    [Fact]
    public void Generate_WithLowConfidence_UsesErrorColor()
    {
        // Arrange
        var reportData = CreateReportDataWithConfidence(30, ConfidenceLevel.Low);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("#ef4444"); // Error color
    }

    [Fact]
    public void Generate_WithScoreComponents_DisplaysBreakdownTable()
    {
        // Arrange
        var reportData = CreateReportDataWithScoreComponents();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Score Breakdown");
        html.Should().Contain("score-table");
        html.Should().Contain("Build Validation");
        html.Should().Contain("Test Results");
        html.Should().Contain("Weighted Score");
    }

    [Fact]
    public void Generate_WithBuildSuccess_DisplaysSuccessStatus()
    {
        // Arrange
        var reportData = CreateReportDataWithBuildResult(success: true, errors: 0, warnings: 2);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Build Results");
        html.Should().Contain("SUCCESS");
        html.Should().Contain("badge-success");
    }

    [Fact]
    public void Generate_WithBuildFailure_DisplaysErrorStatus()
    {
        // Arrange
        var reportData = CreateReportDataWithBuildResult(success: false, errors: 5, warnings: 3);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Build Results");
        html.Should().Contain("FAILED");
        html.Should().Contain("badge-error");
    }

    [Fact]
    public void Generate_WithBuildErrors_DisplaysErrorList()
    {
        // Arrange
        var errors = new List<BuildDiagnostic>
        {
            new() { Code = "CS0103", Message = "The name does not exist", File = "Test.cs", Line = 10, Column = 5, Severity = DiagnosticSeverity.Error }
        };
        var reportData = CreateReportDataWithBuildDiagnostics(errors, []);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Build Errors");
        html.Should().Contain("CS0103");
        html.Should().Contain("The name does not exist");
        html.Should().Contain("Test.cs");
    }

    [Fact]
    public void Generate_WithTestResults_DisplaysTestMetrics()
    {
        // Arrange
        var reportData = CreateReportDataWithTestResult(total: 100, passed: 95, failed: 5, skipped: 0);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Test Results");
        html.Should().Contain("95/100");
        html.Should().Match("*95?0%*"); // Pass rate (using wildcard for culture-specific decimal separator)
    }

    [Fact]
    public void Generate_WithTestFailures_DisplaysFailureDetails()
    {
        // Arrange
        var failures = new List<TestFailure>
        {
            new()
            {
                TestName = "TestMethod1",
                ClassName = "TestClass",
                ErrorMessage = "Expected true but was false",
                StackTrace = "at TestClass.TestMethod1()",
                Duration = TimeSpan.FromSeconds(0.5)
            }
        };
        var reportData = CreateReportDataWithTestFailures(failures);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Test Failures");
        html.Should().Contain("TestMethod1");
        html.Should().Contain("Expected true but was false");
    }

    [Fact]
    public void Generate_WithMigrationIssues_DisplaysIssuesBySeAnverity()
    {
        // Arrange
        var issues = new List<MigrationIssue>
        {
            new() { Code = "MVC001", Message = "Controller needs update", Severity = IssueSeverity.Error, FilePath = "HomeController.cs" },
            new() { Code = "EF001", Message = "DbContext migration required", Severity = IssueSeverity.Warning }
        };
        var reportData = CreateReportDataWithIssues(issues);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Migration Issues");
        html.Should().Contain("MVC001");
        html.Should().Contain("Controller needs update");
        html.Should().Contain("HomeController.cs");
        html.Should().Contain("EF001");
        html.Should().Contain("DbContext migration required");
    }

    [Fact]
    public void Generate_WithRecommendations_DisplaysRecommendationsList()
    {
        // Arrange
        var recommendations = new List<string>
        {
            "Fix compilation errors before deploying",
            "Review and address build warnings"
        };
        var reportData = CreateReportDataWithRecommendations(recommendations);

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Recommendations");
        html.Should().Contain("Fix compilation errors before deploying");
        html.Should().Contain("Review and address build warnings");
    }

    [Fact]
    public void Generate_WithNoBuildResult_DisplaysEmptyState()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Build validation not performed");
    }

    [Fact]
    public void Generate_WithNoTestResult_DisplaysEmptyState()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("Test execution not performed");
    }

    [Fact]
    public void Generate_WithNoIssues_DisplaysEmptyState()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("No migration issues detected");
    }

    [Fact]
    public void Generate_IncludesPurplePrimaryColor()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("--primary: #9333ea");
    }

    [Fact]
    public void Generate_IncludesDarkThemeColors()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("--bg-dark: #0f0f0f");
        html.Should().Contain("--bg-card: #1a1a1a");
        html.Should().Contain("--success: #22c55e");
        html.Should().Contain("--warning: #f59e0b");
        html.Should().Contain("--error: #ef4444");
    }

    [Fact]
    public void Generate_IncludesPrintStyles()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("@media print");
    }

    [Fact]
    public void Generate_EscapesHtmlInUserContent()
    {
        // Arrange
        var reportData = new MigrationReportData
        {
            SolutionName = "<script>alert('xss')</script>",
            TargetFramework = "net8.0",
            GeneratedAt = DateTime.UtcNow,
            Issues =
            [
                new MigrationIssue
                {
                    Code = "TEST001",
                    Message = "<b>Malicious content</b>",
                    Severity = IssueSeverity.Info
                }
            ]
        };

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().NotContain("<script>alert('xss')</script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<b>Malicious content</b>");
        html.Should().Contain("&lt;b&gt;Malicious content&lt;/b&gt;");
    }

    [Fact]
    public void Generate_IncludesMetadata()
    {
        // Arrange
        var reportData = CreateMinimalReportData();

        // Act
        var html = _generator.Generate(reportData);

        // Assert
        html.Should().Contain("net8.0");
        html.Should().Contain("1.0.0"); // NetLift version
        html.Should().Contain("3 Projects");
        html.Should().Contain("15 Files Transformed");
    }

    // Helper methods

    private MigrationReportData CreateMinimalReportData()
    {
        return new MigrationReportData
        {
            SolutionName = "TestSolution",
            TargetFramework = "net8.0",
            ProjectCount = 3,
            FilesTransformed = 15,
            GeneratedAt = new DateTime(2026, 1, 15, 10, 30, 0),
            NetLiftVersion = "1.0.0"
        };
    }

    private MigrationReportData CreateFullReportData()
    {
        return new MigrationReportData
        {
            SolutionName = "TestSolution",
            TargetFramework = "net8.0",
            ProjectCount = 3,
            FilesTransformed = 15,
            GeneratedAt = DateTime.UtcNow,
            BuildResult = new BuildResult { Success = true, ExitCode = 0, Duration = TimeSpan.FromSeconds(30) },
            TestResult = new TestResult { Success = true, TotalTests = 50, PassedTests = 50, Duration = TimeSpan.FromSeconds(10) },
            ConfidenceScore = new ConfidenceScore { OverallScore = 85, Level = ConfidenceLevel.High },
            Issues = []
        };
    }

    private MigrationReportData CreateReportDataWithConfidence(int score, ConfidenceLevel level)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            ConfidenceScore = new ConfidenceScore
            {
                OverallScore = score,
                Level = level
            }
        };
    }

    private MigrationReportData CreateReportDataWithScoreComponents()
    {
        var data = CreateMinimalReportData();
        return data with
        {
            BuildResult = new BuildResult { Success = true, ExitCode = 0, Duration = TimeSpan.FromSeconds(30) },
            TestResult = new TestResult { Success = true, TotalTests = 50, PassedTests = 50, Duration = TimeSpan.FromSeconds(10) },
            ConfidenceScore = new ConfidenceScore
            {
                OverallScore = 85,
                Level = ConfidenceLevel.High,
                Components = new Dictionary<string, ScoreComponent>
                {
                    ["Build"] = new ScoreComponent
                    {
                        Name = "Build Validation",
                        Score = 100,
                        Weight = 30,
                        WeightedScore = 30,
                        Rationale = "Build succeeded with no errors"
                    },
                    ["Tests"] = new ScoreComponent
                    {
                        Name = "Test Results",
                        Score = 95,
                        Weight = 25,
                        WeightedScore = 24,
                        Rationale = "47/50 tests passed"
                    }
                }
            }
        };
    }

    private MigrationReportData CreateReportDataWithBuildResult(bool success, int errors, int warnings)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            BuildResult = new BuildResult
            {
                Success = success,
                ExitCode = success ? 0 : 1,
                Duration = TimeSpan.FromSeconds(30),
                Errors = Enumerable.Range(0, errors)
                    .Select(i => new BuildDiagnostic
                    {
                        Code = $"CS{i:D4}",
                        Message = $"Error message {i}",
                        Severity = DiagnosticSeverity.Error
                    })
                    .ToList(),
                Warnings = Enumerable.Range(0, warnings)
                    .Select(i => new BuildDiagnostic
                    {
                        Code = $"CS{i + 1000:D4}",
                        Message = $"Warning message {i}",
                        Severity = DiagnosticSeverity.Warning
                    })
                    .ToList()
            }
        };
    }

    private MigrationReportData CreateReportDataWithBuildDiagnostics(
        List<BuildDiagnostic> errors,
        List<BuildDiagnostic> warnings)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            BuildResult = new BuildResult
            {
                Success = errors.Count == 0,
                ExitCode = errors.Count == 0 ? 0 : 1,
                Duration = TimeSpan.FromSeconds(30),
                Errors = errors,
                Warnings = warnings
            }
        };
    }

    private MigrationReportData CreateReportDataWithTestResult(int total, int passed, int failed, int skipped)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            TestResult = new TestResult
            {
                Success = failed == 0,
                ExitCode = failed == 0 ? 0 : 1,
                Duration = TimeSpan.FromSeconds(15),
                TotalTests = total,
                PassedTests = passed,
                FailedTests = failed,
                SkippedTests = skipped
            }
        };
    }

    private MigrationReportData CreateReportDataWithTestFailures(List<TestFailure> failures)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            TestResult = new TestResult
            {
                Success = false,
                ExitCode = 1,
                Duration = TimeSpan.FromSeconds(15),
                TotalTests = 100,
                PassedTests = 100 - failures.Count,
                FailedTests = failures.Count,
                Failures = failures
            }
        };
    }

    private MigrationReportData CreateReportDataWithIssues(List<MigrationIssue> issues)
    {
        var data = CreateMinimalReportData();
        return data with { Issues = issues };
    }

    private MigrationReportData CreateReportDataWithRecommendations(List<string> recommendations)
    {
        var data = CreateMinimalReportData();
        return data with
        {
            ConfidenceScore = new ConfidenceScore
            {
                OverallScore = 75,
                Level = ConfidenceLevel.Medium,
                Recommendations = recommendations
            }
        };
    }
}
