using System.Text.Json;
using NetLift.Analysis.Interfaces;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace NetLift.Cli.Commands;

/// <summary>
/// Command to analyze a solution for migration readiness.
/// </summary>
public sealed class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings>
{
    private readonly ISolutionParser _solutionParser;
    private readonly IProjectParser _projectParser;
    private readonly IPackagesConfigParser _packagesConfigParser;
    private readonly IProjectTypeDetector _projectTypeDetector;
    private readonly IDependencyGraphBuilder _dependencyGraphBuilder;
    private readonly IReportBuilder _reportBuilder;
    private readonly IHtmlReportGenerator _htmlReportGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzeCommand"/> class.
    /// </summary>
    public AnalyzeCommand(
        ISolutionParser solutionParser,
        IProjectParser projectParser,
        IPackagesConfigParser packagesConfigParser,
        IProjectTypeDetector projectTypeDetector,
        IDependencyGraphBuilder dependencyGraphBuilder,
        IReportBuilder reportBuilder,
        IHtmlReportGenerator htmlReportGenerator)
    {
        _solutionParser = solutionParser ?? throw new ArgumentNullException(nameof(solutionParser));
        _projectParser = projectParser ?? throw new ArgumentNullException(nameof(projectParser));
        _packagesConfigParser = packagesConfigParser ?? throw new ArgumentNullException(nameof(packagesConfigParser));
        _projectTypeDetector = projectTypeDetector ?? throw new ArgumentNullException(nameof(projectTypeDetector));
        _dependencyGraphBuilder = dependencyGraphBuilder ?? throw new ArgumentNullException(nameof(dependencyGraphBuilder));
        _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
        _htmlReportGenerator = htmlReportGenerator ?? throw new ArgumentNullException(nameof(htmlReportGenerator));
    }

    /// <summary>
    /// Settings for the analyze command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Path to the solution file to analyze.
        /// </summary>
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to solution file")]
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>
        /// Output directory for the analysis report.
        /// </summary>
        [CommandOption("-o|--output")]
        [Description("Output directory for report")]
        public string? OutputPath { get; set; }

        /// <summary>
        /// Target framework for migration.
        /// </summary>
        [CommandOption("-t|--target")]
        [Description("Target framework")]
        [DefaultValue("net8.0")]
        public string TargetFramework { get; set; } = "net8.0";

        /// <summary>
        /// Enable verbose output.
        /// </summary>
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Output JSON only without console summary.
        /// </summary>
        [CommandOption("--json")]
        [Description("Output JSON only (no console summary)")]
        [DefaultValue(false)]
        public bool JsonOnly { get; set; }

        /// <summary>
        /// Generate HTML report in addition to JSON.
        /// </summary>
        [CommandOption("--html")]
        [Description("Generate HTML report")]
        [DefaultValue(false)]
        public bool GenerateHtml { get; set; }
    }

    /// <summary>
    /// Executes the analyze command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>Exit code (0 for success).</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate solution path
            if (!File.Exists(settings.SolutionPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Solution file not found");
                return 1;
            }

            if (!_solutionParser.IsValidSolutionFile(settings.SolutionPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Invalid solution file format");
                return 1;
            }

            SolutionInfo solution;
            var projectInfos = new List<ProjectInfo>();

            // Run analysis with progress indicator
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var parseTask = ctx.AddTask("[cyan]Parsing solution[/]");
                    var analyzeTask = ctx.AddTask("[cyan]Analyzing projects[/]");
                    var graphTask = ctx.AddTask("[cyan]Building dependency graph[/]");
                    var reportTask = ctx.AddTask("[cyan]Generating report[/]");

                    // Step 1: Parse solution
                    solution = await _solutionParser.ParseAsync(settings.SolutionPath);
                    parseTask.Value = 100;

                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]Found {solution.Projects.Count} projects[/]");
                    }

                    // Step 2: Analyze each project
                    var actualProjects = solution.Projects
                        .Where(p => p.DetectedType != ProjectType.SolutionFolder)
                        .ToList();

                    foreach (var projectRef in actualProjects)
                    {
                        if (!File.Exists(projectRef.AbsolutePath))
                        {
                            if (settings.Verbose)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Project file not found: {projectRef.AbsolutePath}");
                            }
                            continue;
                        }

                        if (!_projectParser.CanParse(projectRef.AbsolutePath))
                        {
                            if (settings.Verbose)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Cannot parse project: {projectRef.Name}");
                            }
                            continue;
                        }

                        var projectInfo = await _projectParser.AnalyzeAsync(projectRef.AbsolutePath);

                        // Parse packages.config if exists
                        var projectDir = Path.GetDirectoryName(projectRef.AbsolutePath);
                        if (!string.IsNullOrEmpty(projectDir))
                        {
                            var packagesConfigPath = Path.Combine(projectDir, "packages.config");
                            if (File.Exists(packagesConfigPath))
                            {
                                var packages = _packagesConfigParser.Parse(packagesConfigPath);
                                projectInfo.PackageReferences.AddRange(packages);
                            }
                        }

                        projectInfos.Add(projectInfo);
                        analyzeTask.Increment(100.0 / actualProjects.Count);
                    }

                    // Step 3: Build dependency graph
                    var graph = _dependencyGraphBuilder.Build(solution, projectInfos);
                    graphTask.Value = 100;

                    if (settings.Verbose && graph.CircularPaths.Any())
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Found {graph.CircularPaths.Count} circular dependencies");
                    }

                    // Step 4: Generate report
                    reportTask.Value = 100;
                });

            // Re-parse to get the solution (async lambda scope issue)
            solution = await _solutionParser.ParseAsync(settings.SolutionPath);

            // Re-analyze projects
            projectInfos.Clear();
            var actualProjects = solution.Projects
                .Where(p => p.DetectedType != ProjectType.SolutionFolder)
                .ToList();

            foreach (var projectRef in actualProjects)
            {
                if (!File.Exists(projectRef.AbsolutePath) || !_projectParser.CanParse(projectRef.AbsolutePath))
                {
                    continue;
                }

                var projectInfo = await _projectParser.AnalyzeAsync(projectRef.AbsolutePath);

                var projectDir = Path.GetDirectoryName(projectRef.AbsolutePath);
                if (!string.IsNullOrEmpty(projectDir))
                {
                    var packagesConfigPath = Path.Combine(projectDir, "packages.config");
                    if (File.Exists(packagesConfigPath))
                    {
                        var packages = _packagesConfigParser.Parse(packagesConfigPath);
                        projectInfo.PackageReferences.AddRange(packages);
                    }
                }

                projectInfos.Add(projectInfo);
            }

            // Build report
            var report = _reportBuilder.BuildReport(solution, projectInfos, settings.TargetFramework);

            // Save JSON report
            var outputDir = settings.OutputPath ?? Path.Combine(Path.GetDirectoryName(settings.SolutionPath) ?? ".", "netlift-report");
            Directory.CreateDirectory(outputDir);
            var jsonPath = Path.Combine(outputDir, "analysis.json");

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions));

            // Generate HTML report if requested
            if (settings.GenerateHtml)
            {
                var htmlPath = Path.Combine(outputDir, "analysis.html");
                var htmlContent = _htmlReportGenerator.Generate(report);
                await File.WriteAllTextAsync(htmlPath, htmlContent);
                AnsiConsole.MarkupLine($"[green]HTML report saved to:[/] {htmlPath}");
            }

            // Display summary
            if (!settings.JsonOnly)
            {
                DisplaySummary(report, settings.Verbose);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]JSON report saved to:[/] {jsonPath}");
            AnsiConsole.MarkupLine($"[blue]Next step:[/] netlift migrate {settings.SolutionPath}");

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {ex.FileName}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Analysis failed");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private void DisplaySummary(AnalysisReport report, bool verbose)
    {
        AnsiConsole.WriteLine();

        // Create summary table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple);

        table.AddColumn(new TableColumn("[bold]NetLift Analysis Report[/]").Centered());

        table.AddRow($"[cyan]Solution:[/] {report.SolutionName}");
        table.AddRow($"[cyan]Projects:[/] {report.TotalProjects}");
        table.AddRow($"[cyan]Target:[/] {report.TargetFramework}");
        table.AddRow($"[cyan]Complexity:[/] {report.OverallComplexity?.Level.ToString() ?? "Unknown"} (Score: {report.OverallComplexity?.Score ?? 0}/100)");

        AnsiConsole.Write(table);

        // Display project breakdown
        if (verbose && report.Projects.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Project Breakdown:[/]");

            var projectTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Grey);

            projectTable.AddColumn("Project");
            projectTable.AddColumn("Type");
            projectTable.AddColumn("Complexity");
            projectTable.AddColumn("Dependencies");

            foreach (var project in report.Projects)
            {
                var complexityColor = (project.Complexity?.Level ?? ComplexityLevel.Low) switch
                {
                    ComplexityLevel.Low => "green",
                    ComplexityLevel.Medium => "yellow",
                    ComplexityLevel.High => "orange1",
                    ComplexityLevel.VeryHigh => "red",
                    _ => "white"
                };

                projectTable.AddRow(
                    project.ProjectName,
                    project.PrimaryType.ToString(),
                    $"[{complexityColor}]{project.Complexity?.Level.ToString() ?? "Unknown"}[/]",
                    project.DependencyCount.ToString()
                );
            }

            AnsiConsole.Write(projectTable);
        }

        // Display issues
        if (report.Issues.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Issues Found:[/] {report.Issues.Count}");

            var errors = report.Issues.Count(i => i.Severity == IssueSeverity.Error);
            var warnings = report.Issues.Count(i => i.Severity == IssueSeverity.Warning);

            if (errors > 0)
            {
                AnsiConsole.MarkupLine($"  [red]Errors:[/] {errors}");
            }

            if (warnings > 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]Warnings:[/] {warnings}");
            }

            if (verbose)
            {
                AnsiConsole.WriteLine();
                foreach (var issue in report.Issues.Take(5))
                {
                    var severityColor = issue.Severity == IssueSeverity.Error ? "red" : "yellow";
                    AnsiConsole.MarkupLine($"  [{severityColor}]{issue.Severity}:[/] {issue.Description}");
                }

                if (report.Issues.Count > 5)
                {
                    AnsiConsole.MarkupLine($"  [dim]... and {report.Issues.Count - 5} more (see JSON report)[/]");
                }
            }
        }

        // Display migration phases
        if (report.RecommendedPhases.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Recommended Migration Phases:[/]");

            foreach (var phase in report.RecommendedPhases)
            {
                AnsiConsole.MarkupLine($"  [cyan]Phase {phase.Order}:[/] {phase.Name}");
                AnsiConsole.MarkupLine($"    {phase.Description}");
                AnsiConsole.MarkupLine($"    [dim]Projects:[/] {phase.AffectedProjects.Count}");
                if (verbose)
                {
                    foreach (var project in phase.AffectedProjects.Take(3))
                    {
                        AnsiConsole.MarkupLine($"      - {project}");
                    }
                    if (phase.AffectedProjects.Count > 3)
                    {
                        AnsiConsole.MarkupLine($"      [dim]... and {phase.AffectedProjects.Count - 3} more[/]");
                    }
                }
            }
        }

        // Display auto-migration percentage
        AnsiConsole.WriteLine();
        var percentage = report.EstimatedAutoMigrationPercentage;
        var barColor = percentage switch
        {
            >= 80 => Color.Green,
            >= 60 => Color.Yellow,
            >= 40 => Color.Orange1,
            _ => Color.Red
        };

        AnsiConsole.Write(new BarChart()
            .Width(60)
            .Label("[bold]Estimated Auto-Migration[/]")
            .CenterLabel()
            .AddItem("Auto-migratable", percentage, barColor)
            .AddItem("Manual required", 100 - percentage, Color.Grey));
    }
}
