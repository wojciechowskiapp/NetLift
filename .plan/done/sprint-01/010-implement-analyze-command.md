# [TASK-010] Implement Analyze Command

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 1 |
| **Agent** | Claude (Sonnet 4.5) |
| **Started** | 2026-01-31 |
| **Completed** | 2026-01-31 |

## Dependencies

- **Depends on:** TASK-003, TASK-004, TASK-007, TASK-008, TASK-009
- **Blocks:** TASK-014

---

## Description

Wire up all the analysis components into the `netlift analyze` command to produce a complete analysis report.

---

## Acceptance Criteria

- [x] `netlift analyze <solution>` works end-to-end
- [x] Displays progress during analysis
- [x] Shows summary in console (table format)
- [x] Saves detailed report to JSON
- [x] Handles errors gracefully
- [x] Shows actionable next steps
- [x] Integration test with test fixture

---

## Technical Notes

### Command implementation:

```csharp
public class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings>
{
    private readonly ISolutionAnalyzer _solutionAnalyzer;
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IProjectTypeDetector _typeDetector;
    private readonly IDependencyGraphBuilder _graphBuilder;
    private readonly IReportGenerator _reportGenerator;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to solution file (.sln)")]
        public string SolutionPath { get; set; } = "";

        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory for report")]
        [DefaultValue("./netlift-report")]
        public string OutputPath { get; set; } = "./netlift-report";

        [CommandOption("-t|--target <FRAMEWORK>")]
        [Description("Target framework")]
        [DefaultValue("net8.0")]
        public string TargetFramework { get; set; } = "net8.0";

        [CommandOption("--json")]
        [Description("Output JSON only (no console summary)")]
        public bool JsonOnly { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings)
    {
        // Validate input
        if (!File.Exists(settings.SolutionPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Solution not found");
            return 1;
        }

        var report = new AnalysisReport();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task1 = ctx.AddTask("Parsing solution");
                var task2 = ctx.AddTask("Analyzing projects");
                var task3 = ctx.AddTask("Building dependency graph");
                var task4 = ctx.AddTask("Generating report");

                // Step 1: Parse solution
                var solution = await _solutionAnalyzer.AnalyzeAsync(
                    settings.SolutionPath);
                task1.Value = 100;

                // Step 2: Analyze each project
                foreach (var projectRef in solution.Projects)
                {
                    var project = await _projectAnalyzer.AnalyzeAsync(
                        projectRef.AbsolutePath);
                    var typeResult = _typeDetector.Detect(project);
                    report.Projects.Add(CreateProjectAnalysis(project, typeResult));
                    task2.Increment(100.0 / solution.Projects.Count);
                }

                // Step 3: Build dependency graph
                var graph = _graphBuilder.Build(solution);
                task3.Value = 100;

                // Step 4: Generate report
                report = _reportGenerator.Generate(solution, report.Projects, graph);
                task4.Value = 100;
            });

        // Save JSON report
        var jsonPath = Path.Combine(settings.OutputPath, "analysis.json");
        Directory.CreateDirectory(settings.OutputPath);
        await File.WriteAllTextAsync(jsonPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

        // Display summary
        if (!settings.JsonOnly)
        {
            DisplaySummary(report);
        }

        AnsiConsole.MarkupLine($"\n[green]Report saved to:[/] {jsonPath}");
        AnsiConsole.MarkupLine($"\n[blue]Next step:[/] netlift migrate {settings.SolutionPath}");

        return 0;
    }
}
```

### Console output (summary):

```
╔══════════════════════════════════════════════════════════════╗
║                    NetLift Analysis Report                   ║
╠══════════════════════════════════════════════════════════════╣
║ Solution: MyApp.sln                                          ║
║ Projects: 5                                                  ║
║ Target: .NET 8.0                                             ║
╠══════════════════════════════════════════════════════════════╣
║ DETECTED COMPONENTS:                                         ║
║ ├── ASP.NET MVC Controllers: 12                              ║
║ ├── WCF Services: 2                                          ║
║ ├── Entity Framework DbContexts: 1                           ║
║ └── NuGet Packages: 34 (5 need replacement)                  ║
╠══════════════════════════════════════════════════════════════╣
║ MIGRATION COMPLEXITY: Medium (Score: 45/100)                 ║
║                                                              ║
║ ██████████████░░░░░░  65% Auto-migratable                    ║
║ ████████░░░░░░░░░░░░  25% Needs review                       ║
║ ██░░░░░░░░░░░░░░░░░░  10% Manual required                    ║
╠══════════════════════════════════════════════════════════════╣
║ ISSUES: 2 Warnings, 1 Error                                  ║
║ See: ./netlift-report/analysis.json for details              ║
╚══════════════════════════════════════════════════════════════╝
```

### Error handling:

```csharp
try
{
    // ... analysis
}
catch (FileNotFoundException ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {ex.FileName}");
    return 1;
}
catch (XmlException ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid XML in project file: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
