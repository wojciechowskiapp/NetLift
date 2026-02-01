// TASK-064: Comprehensive HTML Report Generator - Usage Example

using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Validation;

// =============================================================================
// EXAMPLE 1: Generate a minimal migration report
// =============================================================================

var generator = new FullHtmlReportGenerator();

var reportData = new MigrationReportData
{
    SolutionName = "MyLegacyApp",
    TargetFramework = "net8.0",
    ProjectCount = 5,
    FilesTransformed = 127,
    GeneratedAt = DateTime.UtcNow,
    NetLiftVersion = "1.0.0"
};

string html = generator.Generate(reportData);
File.WriteAllText("migration-report.html", html);

// =============================================================================
// EXAMPLE 2: Complete report with build, tests, and confidence score
// =============================================================================

var fullReportData = new MigrationReportData
{
    SolutionName = "EnterpriseApp.sln",
    TargetFramework = "net9.0",
    ProjectCount = 12,
    FilesTransformed = 456,
    GeneratedAt = DateTime.UtcNow,

    // Build validation results
    BuildResult = new BuildResult
    {
        Success = true,
        ExitCode = 0,
        Duration = TimeSpan.FromMinutes(2.5),
        Errors = [],
        Warnings =
        [
            new BuildDiagnostic
            {
                Code = "CS0618",
                Message = "Type is obsolete",
                File = "LegacyService.cs",
                Line = 45,
                Column = 12,
                Severity = DiagnosticSeverity.Warning
            }
        ]
    },

    // Test execution results
    TestResult = new TestResult
    {
        Success = true,
        ExitCode = 0,
        Duration = TimeSpan.FromSeconds(45),
        TotalTests = 523,
        PassedTests = 520,
        FailedTests = 3,
        SkippedTests = 0,
        Failures =
        [
            new TestFailure
            {
                TestName = "UserService_CreateUser_ShouldReturnNewUser",
                ClassName = "UserServiceTests",
                ErrorMessage = "Expected user ID to be greater than 0",
                StackTrace = "at UserServiceTests.CreateUser() line 42",
                Duration = TimeSpan.FromMilliseconds(120)
            }
        ]
    },

    // Confidence scoring
    ConfidenceScore = new ConfidenceScore
    {
        OverallScore = 87,
        Level = ConfidenceLevel.High,
        Components = new Dictionary<string, ScoreComponent>
        {
            ["Build"] = new ScoreComponent
            {
                Name = "Build Validation",
                Score = 98,
                Weight = 30,
                WeightedScore = 29,
                Rationale = "Build succeeded with 2 warnings"
            },
            ["Tests"] = new ScoreComponent
            {
                Name = "Test Results",
                Score = 99,
                Weight = 25,
                WeightedScore = 25,
                Rationale = "520/523 tests passed"
            },
            ["Complexity"] = new ScoreComponent
            {
                Name = "Migration Complexity",
                Score = 70,
                Weight = 20,
                WeightedScore = 14,
                Rationale = "Medium complexity migration"
            },
            ["Issues"] = new ScoreComponent
            {
                Name = "Migration Issues",
                Score = 95,
                Weight = 15,
                WeightedScore = 14,
                Rationale = "5 minor issues detected"
            },
            ["Compatibility"] = new ScoreComponent
            {
                Name = "Package Compatibility",
                Score = 88,
                Weight = 10,
                WeightedScore = 9,
                Rationale = "42/48 packages compatible"
            }
        },
        Recommendations =
        [
            "Review and address the 2 build warnings to improve code quality",
            "Investigate the 3 test failures to ensure functionality is preserved",
            "Update incompatible packages to their .NET 8 equivalents"
        ]
    },

    // Migration issues
    Issues =
    [
        new MigrationIssue
        {
            Code = "MVC001",
            Message = "Controller uses obsolete System.Web.Mvc.Controller base class",
            FilePath = "Controllers/HomeController.cs",
            Severity = IssueSeverity.Warning
        },
        new MigrationIssue
        {
            Code = "EF002",
            Message = "DbContext configuration requires manual migration",
            FilePath = "Data/ApplicationDbContext.cs",
            Severity = IssueSeverity.Info
        },
        new MigrationIssue
        {
            Code = "WCF001",
            Message = "WCF service contract detected - consider migrating to gRPC",
            FilePath = "Services/IUserService.cs",
            Severity = IssueSeverity.Warning
        }
    ]
};

string fullHtml = generator.Generate(fullReportData);
File.WriteAllText("full-migration-report.html", fullHtml);

// =============================================================================
// EXAMPLE 3: Using with DI container
// =============================================================================

// In your command/service class:
public class MigrateCommand
{
    private readonly IFullHtmlReportGenerator _reportGenerator;
    private readonly IBuildValidator _buildValidator;
    private readonly ITestRunner _testRunner;

    public MigrateCommand(
        IFullHtmlReportGenerator reportGenerator,
        IBuildValidator buildValidator,
        ITestRunner testRunner)
    {
        _reportGenerator = reportGenerator;
        _buildValidator = buildValidator;
        _testRunner = testRunner;
    }

    public async Task<int> ExecuteAsync(string solutionPath, string outputPath)
    {
        // ... perform migration ...

        // Validate the migrated solution
        var buildResult = await _buildValidator.ValidateAsync(solutionPath);
        var testResult = await _testRunner.RunTestsAsync(solutionPath);

        // Create report data
        var reportData = new MigrationReportData
        {
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            TargetFramework = "net8.0",
            ProjectCount = 10,
            FilesTransformed = 234,
            BuildResult = buildResult,
            TestResult = testResult,
            GeneratedAt = DateTime.UtcNow
        };

        // Generate HTML report
        var html = _reportGenerator.Generate(reportData);
        var reportPath = Path.Combine(outputPath, "migration-report.html");
        await File.WriteAllTextAsync(reportPath, html);

        Console.WriteLine($"Report generated: {reportPath}");
        return 0;
    }
}

// =============================================================================
// KEY FEATURES
// =============================================================================

/*
 * 1. STANDALONE HTML - No external CSS/JS dependencies
 * 2. DARK PURPLE THEME - Primary color #9333ea, dark backgrounds
 * 3. RESPONSIVE DESIGN - Mobile-first, works on all devices
 * 4. CONFIDENCE VISUALIZATION - SVG circle chart for score
 * 5. DETAILED SECTIONS:
 *    - Header with metadata
 *    - Confidence score with visual circle
 *    - Score breakdown table
 *    - Build results with error/warning details
 *    - Test results with failure details
 *    - Migration issues grouped by severity
 *    - Actionable recommendations
 * 6. PRINT-FRIENDLY - Media queries for clean printing
 * 7. XSS PROTECTION - All user input is HTML-escaped
 * 8. EMPTY STATES - Graceful handling of missing data
 */
