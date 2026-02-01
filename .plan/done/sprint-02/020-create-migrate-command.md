# [TASK-020] Implement `netlift migrate` Command

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-010, TASK-015
- **Blocks:** TASK-023, TASK-024

---

## Description

Implement the main `netlift migrate` command that orchestrates the entire csproj conversion process. This command serves as the primary entry point for users migrating their .NET Framework solutions to modern SDK-style projects. It coordinates all converters, manages the migration workflow, and provides real-time progress feedback using Spectre.Console.

---

## Acceptance Criteria

- [ ] Command accepts solution path as required argument
- [ ] Validates solution file exists and is valid .sln format
- [ ] Discovers all projects within the solution
- [ ] Calls appropriate converters for each project type
- [ ] Shows real-time progress using Spectre.Console progress bars
- [ ] Displays summary table of converted projects with status
- [ ] Handles errors gracefully with meaningful error messages
- [ ] Supports `--output` flag to specify output directory
- [ ] Returns appropriate exit codes (0=success, 1=partial, 2=failure)
- [ ] Unit tests for command argument parsing
- [ ] Integration tests with sample solutions

---

## Technical Notes

### Command Structure:

```csharp
[Command("migrate", Description = "Migrate .NET Framework solution to modern SDK-style projects")]
public class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to the .sln file to migrate")]
        public string SolutionPath { get; set; } = string.Empty;

        [CommandOption("-o|--output <PATH>")]
        [Description("Output directory for migrated projects (default: in-place)")]
        public string? OutputPath { get; set; }

        [CommandOption("--target-framework <TFM>")]
        [Description("Target framework (default: net8.0)")]
        [DefaultValue("net8.0")]
        public string TargetFramework { get; set; } = "net8.0";

        [CommandOption("-v|--verbose")]
        [Description("Enable verbose output")]
        public bool Verbose { get; set; }
    }

    private readonly IMigrationOrchestrator _orchestrator;
    private readonly ILogger<MigrateCommand> _logger;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Validate solution exists
        if (!File.Exists(settings.SolutionPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Solution file not found");
            return 2;
        }

        // Run migration with progress
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var result = await _orchestrator.MigrateAsync(
                    settings.SolutionPath,
                    new MigrationOptions
                    {
                        OutputPath = settings.OutputPath,
                        TargetFramework = settings.TargetFramework,
                        Verbose = settings.Verbose
                    },
                    ctx);

                DisplaySummary(result);
                return result.Success ? 0 : (result.PartialSuccess ? 1 : 2);
            });

        return 0;
    }

    private void DisplaySummary(MigrationResult result)
    {
        var table = new Table();
        table.AddColumn("Project");
        table.AddColumn("Status");
        table.AddColumn("Warnings");

        foreach (var project in result.Projects)
        {
            var status = project.Success
                ? "[green]Migrated[/]"
                : "[red]Failed[/]";

            table.AddRow(
                project.Name,
                status,
                project.Warnings.Count.ToString());
        }

        AnsiConsole.Write(table);
    }
}
```

### Migration Orchestrator:

```csharp
public interface IMigrationOrchestrator
{
    Task<MigrationResult> MigrateAsync(
        string solutionPath,
        MigrationOptions options,
        ProgressContext? progressContext = null);
}

public class MigrationOrchestrator : IMigrationOrchestrator
{
    private readonly ISolutionParser _solutionParser;
    private readonly ICsprojConverter _csprojConverter;
    private readonly IPackageConverter _packageConverter;
    private readonly ILogger<MigrationOrchestrator> _logger;

    public async Task<MigrationResult> MigrateAsync(
        string solutionPath,
        MigrationOptions options,
        ProgressContext? progressContext = null)
    {
        var solution = await _solutionParser.ParseAsync(solutionPath);
        var results = new List<ProjectMigrationResult>();

        var task = progressContext?.AddTask(
            $"[green]Migrating {solution.Projects.Count} projects[/]",
            maxValue: solution.Projects.Count);

        foreach (var project in solution.Projects)
        {
            task?.Description = $"Converting {project.Name}...";

            var result = await MigrateProjectAsync(project, options);
            results.Add(result);

            task?.Increment(1);
        }

        return new MigrationResult
        {
            Projects = results,
            Success = results.All(r => r.Success),
            PartialSuccess = results.Any(r => r.Success)
        };
    }
}
```

### Files to create/modify:

- `src/NetLift.Cli/Commands/MigrateCommand.cs` - Main command implementation
- `src/NetLift.Migration/Orchestration/MigrationOrchestrator.cs` - Orchestration logic
- `src/NetLift.Migration/Orchestration/IMigrationOrchestrator.cs` - Interface
- `src/NetLift.Migration/Models/MigrationOptions.cs` - Options model
- `src/NetLift.Migration/Models/MigrationResult.cs` - Result model
- `tests/NetLift.Tests/Commands/MigrateCommandTests.cs` - Unit tests

### Key Decisions:

- Use Spectre.Console for rich terminal output and progress visualization
- Support both in-place and output directory modes
- Return meaningful exit codes for CI/CD integration
- Enable verbose mode for debugging migration issues

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
