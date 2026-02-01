using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Xml.Linq;

namespace NetLift.Cli.Commands;

/// <summary>
/// Command to validate a migrated solution.
/// </summary>
public sealed class ValidateCommand : AsyncCommand<ValidateCommand.Settings>
{
    private readonly IBuildValidator _buildValidator;
    private readonly ITestRunner _testRunner;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly IFullHtmlReportGenerator _htmlReportGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateCommand"/> class.
    /// </summary>
    /// <param name="buildValidator">The build validator service.</param>
    /// <param name="testRunner">The test runner service.</param>
    /// <param name="confidenceScorer">The confidence scorer service.</param>
    /// <param name="htmlReportGenerator">The HTML report generator service.</param>
    public ValidateCommand(
        IBuildValidator buildValidator,
        ITestRunner testRunner,
        IConfidenceScorer confidenceScorer,
        IFullHtmlReportGenerator htmlReportGenerator)
    {
        _buildValidator = buildValidator ?? throw new ArgumentNullException(nameof(buildValidator));
        _testRunner = testRunner ?? throw new ArgumentNullException(nameof(testRunner));
        _confidenceScorer = confidenceScorer ?? throw new ArgumentNullException(nameof(confidenceScorer));
        _htmlReportGenerator = htmlReportGenerator ?? throw new ArgumentNullException(nameof(htmlReportGenerator));
    }

    /// <summary>
    /// Settings for the validate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Path to the solution file to validate.
        /// </summary>
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to solution file")]
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>
        /// Enable strict validation mode.
        /// </summary>
        [CommandOption("--strict")]
        [Description("Enable strict validation mode")]
        [DefaultValue(false)]
        public bool Strict { get; set; }

        /// <summary>
        /// Enable verbose output.
        /// </summary>
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Output format for validation results.
        /// </summary>
        [CommandOption("-f|--format")]
        [Description("Output format (text, json, xml)")]
        [DefaultValue("text")]
        public string Format { get; set; } = "text";

        /// <summary>
        /// Output directory for HTML report.
        /// </summary>
        [CommandOption("-o|--output")]
        [Description("Output directory for HTML report")]
        public string? OutputDirectory { get; set; }

        /// <summary>
        /// Skip running tests.
        /// </summary>
        [CommandOption("--skip-tests")]
        [Description("Skip running tests")]
        [DefaultValue(false)]
        public bool SkipTests { get; set; }
    }

    /// <summary>
    /// Executes the validate command asynchronously.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>Exit code (0 for success).</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate solution file exists
            if (!File.Exists(settings.SolutionPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Solution file not found: {settings.SolutionPath}");
                return 1;
            }

            var solutionPath = Path.GetFullPath(settings.SolutionPath);
            var solutionName = Path.GetFileNameWithoutExtension(solutionPath);

            // Display header
            DisplayHeader(solutionName, settings);

            BuildResult? buildResult = null;
            TestResult? testResult = null;

            // Run build and tests with progress
            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    // Build validation
                    var buildTask = ctx.AddTask("[purple]Building solution[/]", maxValue: 100);
                    buildTask.StartTask();

                    try
                    {
                        buildResult = await _buildValidator.ValidateAsync(solutionPath);
                        buildTask.Value = 100;
                        buildTask.StopTask();
                    }
                    catch (Exception ex)
                    {
                        buildTask.StopTask();
                        AnsiConsole.MarkupLine($"[red]Build validation failed:[/] {ex.Message}");
                    }

                    // Run tests if enabled
                    if (!settings.SkipTests)
                    {
                        var testTask = ctx.AddTask("[purple]Running tests[/]", maxValue: 100);
                        testTask.StartTask();

                        try
                        {
                            testResult = await _testRunner.RunTestsAsync(solutionPath);
                            testTask.Value = 100;
                            testTask.StopTask();
                        }
                        catch (Exception ex)
                        {
                            testTask.StopTask();
                            if (settings.Verbose)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Test execution failed: {ex.Message}");
                            }
                        }
                    }
                });

            AnsiConsole.WriteLine();

            // Calculate confidence score
            var validationContext = new MigrationValidationContext
            {
                BuildResult = buildResult,
                TestResult = testResult,
                TransformationsApplied = 0,
                WarningsGenerated = 0,
                TodosGenerated = 0
            };

            var confidenceScore = _confidenceScorer.CalculateScore(validationContext);

            // Display results based on format
            if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                DisplayJsonResults(buildResult, testResult, confidenceScore);
            }
            else if (settings.Format.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                DisplayXmlResults(buildResult, testResult, confidenceScore);
            }
            else
            {
                DisplayTextResults(buildResult, testResult, confidenceScore, settings.Verbose);
            }

            // Generate HTML report
            var outputDir = settings.OutputDirectory ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outputDir);

            var reportData = new MigrationReportData
            {
                SolutionName = solutionName,
                TargetFramework = "net8.0",
                ProjectCount = 0,
                FilesTransformed = 0,
                BuildResult = buildResult,
                TestResult = testResult,
                ConfidenceScore = confidenceScore,
                GeneratedAt = DateTime.UtcNow,
                NetLiftVersion = "0.1.0"
            };

            var htmlReport = _htmlReportGenerator.Generate(reportData);
            var reportPath = Path.Combine(outputDir, "validation-report.html");
            await File.WriteAllTextAsync(reportPath, htmlReport);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]HTML report generated:[/] {reportPath}");

            // Determine exit code
            var exitCode = DetermineExitCode(buildResult, testResult, confidenceScore, settings.Strict);

            return exitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void DisplayHeader(string solutionName, Settings settings)
    {
        var rule = new Rule($"[purple]NetLift Validation - {solutionName}[/]")
        {
            Style = Style.Parse("purple")
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[dim]Solution:[/]", solutionName);
        grid.AddRow("[dim]Format:[/]", settings.Format);

        if (settings.Strict)
        {
            grid.AddRow("[dim]Mode:[/]", "[yellow]Strict[/]");
        }

        if (settings.SkipTests)
        {
            grid.AddRow("[dim]Tests:[/]", "[yellow]Skipped[/]");
        }

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    private static void DisplayTextResults(
        BuildResult? buildResult,
        TestResult? testResult,
        ConfidenceScore confidenceScore,
        bool verbose)
    {
        // Display confidence score
        DisplayConfidenceScore(confidenceScore);
        AnsiConsole.WriteLine();

        // Display build results
        if (buildResult != null)
        {
            DisplayBuildResults(buildResult, verbose);
            AnsiConsole.WriteLine();
        }

        // Display test results
        if (testResult != null)
        {
            DisplayTestResults(testResult, verbose);
            AnsiConsole.WriteLine();
        }

        // Display recommendations
        if (confidenceScore.Recommendations.Any())
        {
            DisplayRecommendations(confidenceScore.Recommendations);
        }
    }

    private static void DisplayConfidenceScore(ConfidenceScore score)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple)
            .AddColumn(new TableColumn("[purple]Confidence Score[/]").Centered());

        var scoreColor = score.Level switch
        {
            ConfidenceLevel.High => "green",
            ConfidenceLevel.Medium => "yellow",
            ConfidenceLevel.Low => "red",
            _ => "white"
        };

        table.AddRow($"[bold {scoreColor}]{score.OverallScore}/100[/]");
        table.AddRow($"[{scoreColor}]{score.Level} Confidence[/]");

        AnsiConsole.Write(table);
    }

    private static void DisplayBuildResults(BuildResult buildResult, bool verbose)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple)
            .AddColumn("[purple]Build Results[/]")
            .AddColumn("[purple]Value[/]");

        var statusColor = buildResult.Success ? "green" : "red";
        var statusText = buildResult.Success ? "SUCCESS" : "FAILED";

        table.AddRow("Status", $"[bold {statusColor}]{statusText}[/]");
        table.AddRow("Duration", $"{buildResult.Duration.TotalSeconds:F1}s");
        table.AddRow("Errors", buildResult.Errors.Count > 0 ? $"[red]{buildResult.Errors.Count}[/]" : "[green]0[/]");
        table.AddRow("Warnings", buildResult.Warnings.Count > 0 ? $"[yellow]{buildResult.Warnings.Count}[/]" : "[green]0[/]");

        AnsiConsole.Write(table);

        // Display errors
        if (buildResult.Errors.Any() && verbose)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red bold]Build Errors:[/]");

            foreach (var error in buildResult.Errors.Take(10))
            {
                AnsiConsole.MarkupLine($"  [red]{error.Code}:[/] {Markup.Escape(error.Message)}");
                if (!string.IsNullOrEmpty(error.File))
                {
                    AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(error.File)}({error.Line},{error.Column})[/]");
                }
            }

            if (buildResult.Errors.Count > 10)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {buildResult.Errors.Count - 10} more errors[/]");
            }
        }

        // Display warnings
        if (buildResult.Warnings.Any() && verbose)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow bold]Build Warnings:[/]");

            foreach (var warning in buildResult.Warnings.Take(5))
            {
                AnsiConsole.MarkupLine($"  [yellow]{warning.Code}:[/] {Markup.Escape(warning.Message)}");
            }

            if (buildResult.Warnings.Count > 5)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {buildResult.Warnings.Count - 5} more warnings[/]");
            }
        }
    }

    private static void DisplayTestResults(TestResult testResult, bool verbose)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple)
            .AddColumn("[purple]Test Results[/]")
            .AddColumn("[purple]Value[/]");

        var statusColor = testResult.Success ? "green" : "red";
        var statusText = testResult.Success ? "PASSED" : "FAILED";
        var passRate = testResult.TotalTests > 0 ? (testResult.PassedTests * 100.0 / testResult.TotalTests) : 0;

        table.AddRow("Status", $"[bold {statusColor}]{statusText}[/]");
        table.AddRow("Duration", $"{testResult.Duration.TotalSeconds:F1}s");
        table.AddRow("Total Tests", testResult.TotalTests.ToString());
        table.AddRow("Passed", $"[green]{testResult.PassedTests}[/]");
        table.AddRow("Failed", testResult.FailedTests > 0 ? $"[red]{testResult.FailedTests}[/]" : "[green]0[/]");
        table.AddRow("Skipped", testResult.SkippedTests > 0 ? $"[yellow]{testResult.SkippedTests}[/]" : "0");
        table.AddRow("Pass Rate", $"{passRate:F1}%");

        AnsiConsole.Write(table);

        // Display failures
        if (testResult.Failures.Any() && verbose)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red bold]Test Failures:[/]");

            foreach (var failure in testResult.Failures.Take(5))
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(failure.TestName)}[/]");
                AnsiConsole.MarkupLine($"    {Markup.Escape(failure.ErrorMessage)}");
            }

            if (testResult.Failures.Count > 5)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {testResult.Failures.Count - 5} more failures[/]");
            }
        }
    }

    private static void DisplayRecommendations(IReadOnlyList<string> recommendations)
    {
        var panel = new Panel(string.Join("\n", recommendations.Select((r, i) => $"{i + 1}. {r}")))
        {
            Header = new PanelHeader("[purple]Recommendations[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("purple")
        };

        AnsiConsole.Write(panel);
    }

    private static void DisplayJsonResults(BuildResult? buildResult, TestResult? testResult, ConfidenceScore confidenceScore)
    {
        var results = new
        {
            confidenceScore = new
            {
                overallScore = confidenceScore.OverallScore,
                level = confidenceScore.Level.ToString(),
                components = confidenceScore.Components,
                recommendations = confidenceScore.Recommendations
            },
            buildResult = buildResult != null ? new
            {
                success = buildResult.Success,
                duration = buildResult.Duration.TotalSeconds,
                errors = buildResult.Errors.Count,
                warnings = buildResult.Warnings.Count
            } : null,
            testResult = testResult != null ? new
            {
                success = testResult.Success,
                duration = testResult.Duration.TotalSeconds,
                totalTests = testResult.TotalTests,
                passedTests = testResult.PassedTests,
                failedTests = testResult.FailedTests,
                skippedTests = testResult.SkippedTests
            } : null
        };

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        AnsiConsole.WriteLine(json);
    }

    private static void DisplayXmlResults(BuildResult? buildResult, TestResult? testResult, ConfidenceScore confidenceScore)
    {
        var root = new XElement("ValidationResults",
            new XElement("ConfidenceScore",
                new XElement("OverallScore", confidenceScore.OverallScore),
                new XElement("Level", confidenceScore.Level.ToString()),
                new XElement("Recommendations",
                    confidenceScore.Recommendations.Select(r => new XElement("Recommendation", r)))),
            buildResult != null ? new XElement("BuildResult",
                new XElement("Success", buildResult.Success),
                new XElement("Duration", buildResult.Duration.TotalSeconds),
                new XElement("Errors", buildResult.Errors.Count),
                new XElement("Warnings", buildResult.Warnings.Count)) : null,
            testResult != null ? new XElement("TestResult",
                new XElement("Success", testResult.Success),
                new XElement("Duration", testResult.Duration.TotalSeconds),
                new XElement("TotalTests", testResult.TotalTests),
                new XElement("PassedTests", testResult.PassedTests),
                new XElement("FailedTests", testResult.FailedTests),
                new XElement("SkippedTests", testResult.SkippedTests)) : null);

        AnsiConsole.WriteLine(root.ToString());
    }

    private static int DetermineExitCode(
        BuildResult? buildResult,
        TestResult? testResult,
        ConfidenceScore confidenceScore,
        bool strict)
    {
        // Build must succeed
        if (buildResult != null && !buildResult.Success)
        {
            return 1;
        }

        // Tests must pass
        if (testResult != null && !testResult.Success)
        {
            return 1;
        }

        // In strict mode, require high confidence
        if (strict && confidenceScore.Level != ConfidenceLevel.High)
        {
            return 1;
        }

        return 0;
    }
}
