using System.ComponentModel;
using NetLift.Cli.Renderers;
using NetLift.Core.Interfaces;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;
using Spectre.Console;
using Spectre.Console.Cli;
using DiagnosticSeverity = NetLift.Core.Models.Modernization.DiagnosticSeverity;

namespace NetLift.Cli.Commands;

/// <summary>
/// Command to modernize a solution to Clean Architecture with CQRS.
/// </summary>
public sealed class ModernizeCommand : AsyncCommand<ModernizeCommand.Settings>
{
    private readonly IProjectParser _projectParser;
    private readonly ISolutionParser _solutionParser;
    private readonly IModernizationOrchestrator _orchestrator;
    private readonly IInteractiveService? _interactiveService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModernizeCommand"/> class.
    /// </summary>
    public ModernizeCommand(
        IProjectParser projectParser,
        ISolutionParser solutionParser,
        IModernizationOrchestrator orchestrator,
        IInteractiveService? interactiveService = null)
    {
        _projectParser = projectParser ?? throw new ArgumentNullException(nameof(projectParser));
        _solutionParser = solutionParser ?? throw new ArgumentNullException(nameof(solutionParser));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _interactiveService = interactiveService;
    }

    /// <summary>
    /// Settings for the modernize command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Path to the solution or project file to modernize.
        /// </summary>
        [CommandArgument(0, "<PATH>")]
        [Description("Path to solution or project file")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Only analyze, don't apply changes.
        /// </summary>
        [CommandOption("-a|--analyze-only")]
        [Description("Only analyze, don't apply changes")]
        [DefaultValue(false)]
        public bool AnalyzeOnly { get; set; }

        /// <summary>
        /// Preview changes without applying them.
        /// </summary>
        [CommandOption("-d|--dry-run")]
        [Description("Preview changes without applying them")]
        [DefaultValue(false)]
        public bool DryRun { get; set; }

        /// <summary>
        /// Patterns to apply during modernization.
        /// </summary>
        [CommandOption("-p|--pattern")]
        [Description("Patterns to apply: cqrs, clean-architecture, fluentvalidation, repository")]
        public string[]? Patterns { get; set; }

        /// <summary>
        /// Interactive mode with confirmations.
        /// </summary>
        [CommandOption("-i|--interactive")]
        [Description("Interactive mode with confirmations")]
        [DefaultValue(false)]
        public bool Interactive { get; set; }

        /// <summary>
        /// Minimum confidence threshold (0-100).
        /// </summary>
        [CommandOption("-c|--confidence-threshold")]
        [Description("Minimum confidence threshold (0-100)")]
        [DefaultValue(80)]
        public int ConfidenceThreshold { get; set; }

        /// <summary>
        /// Output directory for generated files.
        /// </summary>
        [CommandOption("-o|--output")]
        [Description("Output directory for generated files")]
        public string? Output { get; set; }

        /// <summary>
        /// Enable verbose output.
        /// </summary>
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }

    /// <summary>
    /// Executes the modernize command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>Exit code (0 for success).</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate input path
            if (!File.Exists(settings.Path))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] File not found");
                return 2;
            }

            // Display header
            var rule = new Rule("[bold purple]NetLift Modernization[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Parse patterns
            var patterns = ParsePatterns(settings.Patterns);
            if (patterns.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No patterns specified, using default: CQRS");
                patterns.Add(ModernizationPattern.Cqrs);
            }

            // Display settings
            AnsiConsole.MarkupLine($"[cyan]Path:[/] {settings.Path}");
            AnsiConsole.MarkupLine($"[cyan]Patterns:[/] {string.Join(", ", patterns.Select(p => p.ToString()))}");
            AnsiConsole.MarkupLine($"[cyan]Confidence Threshold:[/] {settings.ConfidenceThreshold}%");

            if (settings.AnalyzeOnly)
            {
                AnsiConsole.MarkupLine("[magenta]Mode:[/] Analysis only");
            }
            else if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[magenta]Mode:[/] Dry run (preview only)");
            }

            if (settings.Interactive)
            {
                AnsiConsole.MarkupLine("[magenta]Interactive:[/] Enabled");
            }

            if (!string.IsNullOrEmpty(settings.Output))
            {
                AnsiConsole.MarkupLine($"[cyan]Output:[/] {settings.Output}");
            }

            AnsiConsole.WriteLine();

            // Determine if it's a solution or project
            var isSolution = settings.Path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

            List<ProjectInfo> projectsToModernize;

            if (isSolution)
            {
                // Parse solution
                if (!_solutionParser.IsValidSolutionFile(settings.Path))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Invalid solution file format");
                    return 2;
                }

                var solution = await _solutionParser.ParseAsync(settings.Path);
                var projectRefs = solution.Projects
                    .Where(p => p.DetectedType == ProjectType.CSharpMvc ||
                               p.DetectedType == ProjectType.AspNetWebApi ||
                               p.DetectedType == ProjectType.AspNetCore)
                    .Where(p => File.Exists(p.AbsolutePath))
                    .ToList();

                if (projectRefs.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Warning:[/] No ASP.NET MVC or Web API projects found in solution");
                    return 1;
                }

                // Parse each project
                projectsToModernize = new List<ProjectInfo>();
                foreach (var projectRef in projectRefs)
                {
                    if (_projectParser.CanParse(projectRef.AbsolutePath))
                    {
                        var projectInfo = await _projectParser.AnalyzeAsync(projectRef.AbsolutePath);
                        projectsToModernize.Add(projectInfo);
                    }
                }
            }
            else
            {
                // Single project
                if (!_projectParser.CanParse(settings.Path))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Cannot parse project file");
                    return 2;
                }

                var projectInfo = await _projectParser.AnalyzeAsync(settings.Path);
                projectsToModernize = new List<ProjectInfo> { projectInfo };
            }

            if (projectsToModernize.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No projects to modernize");
                return 1;
            }

            // Create modernization options
            var modernizationOptions = new ModernizationOptions
            {
                AnalyzeOnly = settings.AnalyzeOnly,
                DryRun = settings.DryRun,
                Interactive = settings.Interactive,
                ConfidenceThreshold = settings.ConfidenceThreshold,
                Patterns = patterns,
                OutputPath = settings.Output
            };

            // Process each project
            var allResults = new List<ProjectModernizationResult>();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var progressTask = ctx.AddTask(
                        $"[cyan]Processing {projectsToModernize.Count} project(s)[/]",
                        maxValue: projectsToModernize.Count);

                    foreach (var projectInfo in projectsToModernize)
                    {
                        progressTask.Description = $"[cyan]Modernizing {projectInfo.AssemblyName}[/]";

                        var result = await ModernizeProjectAsync(projectInfo, modernizationOptions, settings);
                        allResults.Add(result);

                        progressTask.Increment(1);
                    }

                    progressTask.StopTask();
                });

            // Display results
            if (settings.AnalyzeOnly)
            {
                DisplayAnalysisResults(allResults, settings);
            }
            else
            {
                DisplayModernizationResults(allResults, settings);
            }

            // Determine exit code
            var hasErrors = allResults.Any(r => !r.Success);
            var hasWarnings = allResults.Any(r => r.Warnings.Count > 0);

            if (hasErrors)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[red]Modernization completed with errors[/]");
                return 1;
            }

            if (hasWarnings)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Modernization completed with warnings[/]");
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]Modernization completed successfully[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Error:[/] Modernization failed");
            AnsiConsole.WriteException(ex);
            return 2;
        }
    }

    private async Task<ProjectModernizationResult> ModernizeProjectAsync(
        ProjectInfo projectInfo,
        ModernizationOptions options,
        Settings settings)
    {
        var result = new ProjectModernizationResult
        {
            ProjectName = projectInfo.AssemblyName ?? projectInfo.Name,
            ProjectPath = projectInfo.FilePath
        };

        try
        {
            // Step 1: Analyze
            var analysis = await _orchestrator.AnalyzeAsync(
                projectInfo,
                options,
                CancellationToken.None);

            result.Analysis = analysis;

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Analyzed {projectInfo.AssemblyName ?? projectInfo.Name}: " +
                    $"{analysis.Controllers.Count} controllers, " +
                    $"{analysis.PotentialCommands.Count} commands, " +
                    $"{analysis.PotentialQueries.Count} queries[/]");
            }

            // If analyze-only, stop here
            if (options.AnalyzeOnly)
            {
                result.Success = true;
                return result;
            }

            // Step 2: Interactive confirmation (if enabled)
            if (settings.Interactive && _interactiveService != null)
            {
                var summary = $"Modernize {projectInfo.AssemblyName ?? projectInfo.Name}?\n" +
                    $"  Controllers: {analysis.Controllers.Count}\n" +
                    $"  Commands to generate: {analysis.PotentialCommands.Count}\n" +
                    $"  Queries to generate: {analysis.PotentialQueries.Count}\n" +
                    $"  Validators to generate: {analysis.PotentialValidators.Count}\n" +
                    $"  Confidence: {analysis.EstimatedConfidence}%";

                var choice = await _interactiveService.PromptChoiceAsync(
                    summary,
                    new List<string>());

                if (choice == InteractiveChoice.Abort)
                {
                    result.Success = false;
                    result.ErrorMessage = "Aborted by user";
                    return result;
                }

                if (choice == InteractiveChoice.Skip)
                {
                    result.Success = true;
                    result.Warnings.Add("Skipped by user in interactive mode");
                    return result;
                }
            }

            // Step 3: Apply modernization
            var modernizationResult = await _orchestrator.ModernizeAsync(
                projectInfo,
                options,
                CancellationToken.None);

            result.ModernizationResult = modernizationResult;
            result.Success = modernizationResult.Success;
            result.GeneratedFilesCount = modernizationResult.GeneratedFiles.Count;
            result.ModifiedFilesCount = modernizationResult.ModifiedFiles.Count;

            // Collect diagnostics
            foreach (var diagnostic in modernizationResult.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    result.Warnings.Add(diagnostic.Message);
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    result.Errors.Add(diagnostic.Message);
                }
            }

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Modernized {projectInfo.AssemblyName ?? projectInfo.Name}: " +
                    $"{modernizationResult.GeneratedFiles.Count} files generated, " +
                    $"{modernizationResult.ModifiedFiles.Count} files modified[/]");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[red]Failed to modernize {projectInfo.AssemblyName ?? projectInfo.Name}: {ex.Message}[/]");
            }
        }

        return result;
    }

    private static HashSet<ModernizationPattern> ParsePatterns(string[]? patternStrings)
    {
        var patterns = new HashSet<ModernizationPattern>();

        if (patternStrings == null || patternStrings.Length == 0)
        {
            return patterns;
        }

        foreach (var patternString in patternStrings)
        {
            var normalized = patternString.ToLowerInvariant().Replace("-", "");

            var pattern = normalized switch
            {
                "cqrs" => ModernizationPattern.Cqrs,
                "cleanarchitecture" => ModernizationPattern.CleanArchitecture,
                "fluentvalidation" => ModernizationPattern.FluentValidation,
                "repository" => ModernizationPattern.Repository,
                _ => (ModernizationPattern?)null
            };

            if (pattern.HasValue)
            {
                patterns.Add(pattern.Value);
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Unknown pattern '{patternString}', skipping");
            }
        }

        return patterns;
    }

    private static void DisplayAnalysisResults(List<ProjectModernizationResult> results, Settings settings)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold purple]Analysis Results[/]") { Justification = Justify.Left });
        AnsiConsole.WriteLine();

        foreach (var result in results)
        {
            if (result.Analysis == null) continue;

            var analysis = result.Analysis;

            AnsiConsole.MarkupLine($"[bold cyan]{result.ProjectName}[/]");
            AnsiConsole.WriteLine();

            // Summary table
            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Purple);

            summaryTable.AddColumn("[bold]Metric[/]");
            summaryTable.AddColumn("[bold]Count[/]");

            summaryTable.AddRow("Controllers", analysis.Controllers.Count.ToString());
            summaryTable.AddRow("Potential Commands", analysis.PotentialCommands.Count.ToString());
            summaryTable.AddRow("Potential Queries", analysis.PotentialQueries.Count.ToString());
            summaryTable.AddRow("Potential Validators", analysis.PotentialValidators.Count.ToString());
            summaryTable.AddRow("Estimated Confidence", $"{analysis.EstimatedConfidence}%");

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();

            // Controllers detail
            if (analysis.Controllers.Count > 0 && settings.Verbose)
            {
                var controllerTable = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Purple);

                controllerTable.AddColumn("[bold]Controller[/]");
                controllerTable.AddColumn("[bold]Actions[/]");

                foreach (var controller in analysis.Controllers.Take(10))
                {
                    controllerTable.AddRow(controller.ClassName, controller.Actions.Count.ToString());
                }

                if (analysis.Controllers.Count > 10)
                {
                    controllerTable.AddRow("[dim]...[/]", $"[dim]({analysis.Controllers.Count - 10} more)[/]");
                }

                AnsiConsole.Write(controllerTable);
                AnsiConsole.WriteLine();
            }

            // Recommendations
            if (analysis.Recommendations.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold yellow]Recommendations:[/]");
                foreach (var recommendation in analysis.Recommendations.Take(5))
                {
                    AnsiConsole.MarkupLine($"  [yellow]•[/] {recommendation}");
                }
                if (analysis.Recommendations.Count > 5)
                {
                    AnsiConsole.MarkupLine($"  [dim]... and {analysis.Recommendations.Count - 5} more[/]");
                }
                AnsiConsole.WriteLine();
            }

            // Diagnostics
            var warnings = analysis.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
            var errors = analysis.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (warnings.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warnings:[/] {warnings.Count}");
            }

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]Errors:[/] {errors.Count}");
            }

            AnsiConsole.WriteLine();
        }
    }

    private static void DisplayModernizationResults(List<ProjectModernizationResult> results, Settings settings)
    {
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple);

        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Generated[/]");
        table.AddColumn("[bold]Modified[/]");
        table.AddColumn("[bold]Confidence[/]");
        table.AddColumn("[bold]Warnings[/]");

        foreach (var result in results)
        {
            var status = result.Success
                ? "[green]Success[/]"
                : "[red]Failed[/]";

            var generated = result.GeneratedFilesCount > 0
                ? $"[cyan]{result.GeneratedFilesCount}[/]"
                : "[dim]0[/]";

            var modified = result.ModifiedFilesCount > 0
                ? $"[cyan]{result.ModifiedFilesCount}[/]"
                : "[dim]0[/]";

            var confidence = result.ModernizationResult?.Confidence ?? result.Analysis?.EstimatedConfidence ?? 0;
            var confidenceColor = confidence >= 80 ? "green" : confidence >= 60 ? "yellow" : "red";
            var confidenceDisplay = confidence > 0
                ? $"[{confidenceColor}]{confidence}%[/]"
                : "[dim]N/A[/]";

            var warningCount = result.Warnings.Count > 0
                ? $"[yellow]{result.Warnings.Count}[/]"
                : "[dim]0[/]";

            table.AddRow(
                result.ProjectName,
                status,
                generated,
                modified,
                confidenceDisplay,
                warningCount);
        }

        AnsiConsole.Write(table);

        // Display warnings if verbose
        if (settings.Verbose && results.Any(r => r.Warnings.Count > 0))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Warnings:[/]");

            foreach (var result in results.Where(r => r.Warnings.Count > 0))
            {
                AnsiConsole.MarkupLine($"[bold]{result.ProjectName}:[/]");
                foreach (var warning in result.Warnings)
                {
                    AnsiConsole.MarkupLine($"  [yellow]•[/] {warning}");
                }
            }
        }

        // Display errors
        var failedProjects = results.Where(r => !r.Success).ToList();
        if (failedProjects.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold red]Errors:[/]");

            foreach (var result in failedProjects)
            {
                AnsiConsole.MarkupLine($"[bold]{result.ProjectName}:[/]");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {result.ErrorMessage}");
                }
                foreach (var error in result.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {error}");
                }
            }
        }

        // Display generated files summary
        if (settings.Verbose)
        {
            var allGenerated = results
                .Where(r => r.ModernizationResult != null)
                .SelectMany(r => r.ModernizationResult!.GeneratedFiles)
                .ToList();

            if (allGenerated.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold magenta]Generated Files:[/]");

                var fileTypeGroups = allGenerated
                    .GroupBy(f => f.FileType)
                    .OrderByDescending(g => g.Count());

                foreach (var group in fileTypeGroups)
                {
                    AnsiConsole.MarkupLine($"  [magenta]•[/] {group.Key}: {group.Count()}");
                }
            }
        }
    }
}

/// <summary>
/// Represents the result of modernizing a single project.
/// </summary>
internal sealed class ProjectModernizationResult
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public ModernizationAnalysis? Analysis { get; set; }
    public ModernizationResult? ModernizationResult { get; set; }
    public int GeneratedFilesCount { get; set; }
    public int ModifiedFilesCount { get; set; }
}
