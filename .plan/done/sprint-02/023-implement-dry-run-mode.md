# [TASK-023] Implement Dry-Run Mode

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | fullstack-agent |
| **Started** | 2026-02-01 |
| **Completed** | 2026-02-01 |

## Dependencies

- **Depends on:** TASK-020
- **Blocks:** -

---

## Description

Add a `--dry-run` flag to the migrate command that shows exactly what changes would be made without actually modifying any files. This enables users to preview the migration, understand the scope of changes, and validate the transformation logic before committing to actual file modifications.

---

## Acceptance Criteria

- [x] `--dry-run` flag available on migrate command
- [x] Shows diff preview for each file that would change
- [x] Displays summary of all planned transformations
- [x] No file system changes occur during dry-run
- [x] No git operations occur during dry-run
- [x] Shows warnings and potential issues
- [x] Supports output to file for review (`--dry-run-output`)
- [x] Color-coded diff output (green=additions, red=deletions)
- [x] Exit code indicates if migration would succeed
- [x] Unit tests for dry-run logic
- [x] Integration tests verifying no file changes

---

## Technical Notes

### Dry-Run Implementation:

```csharp
public class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        // ... existing settings ...

        [CommandOption("--dry-run")]
        [Description("Preview changes without modifying files")]
        public bool DryRun { get; set; }

        [CommandOption("--dry-run-output <PATH>")]
        [Description("Write dry-run report to file")]
        public string? DryRunOutput { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings)
    {
        if (settings.DryRun)
        {
            return await ExecuteDryRunAsync(settings);
        }

        return await ExecuteMigrationAsync(settings);
    }

    private async Task<int> ExecuteDryRunAsync(Settings settings)
    {
        var report = await _orchestrator.PreviewMigrationAsync(
            settings.SolutionPath,
            new MigrationOptions { TargetFramework = settings.TargetFramework });

        // Display preview
        DisplayDryRunReport(report);

        // Write to file if requested
        if (!string.IsNullOrEmpty(settings.DryRunOutput))
        {
            await WriteDryRunReportAsync(report, settings.DryRunOutput);
        }

        return report.WouldSucceed ? 0 : 1;
    }
}
```

### Dry-Run Report Model:

```csharp
public class DryRunReport
{
    public bool WouldSucceed { get; set; }
    public List<FileDiff> FileDiffs { get; set; } = new();
    public List<PlannedTransformation> Transformations { get; set; } = new();
    public List<DryRunWarning> Warnings { get; set; } = new();
    public DryRunSummary Summary { get; set; } = new();
}

public class FileDiff
{
    public string FilePath { get; set; } = string.Empty;
    public DiffOperation Operation { get; set; }
    public string? OriginalContent { get; set; }
    public string? NewContent { get; set; }
    public List<DiffHunk> Hunks { get; set; } = new();
}

public enum DiffOperation
{
    Create,
    Modify,
    Delete,
    Rename
}

public class DiffHunk
{
    public int OldStart { get; set; }
    public int OldCount { get; set; }
    public int NewStart { get; set; }
    public int NewCount { get; set; }
    public List<DiffLine> Lines { get; set; } = new();
}

public class DiffLine
{
    public DiffLineType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
}

public enum DiffLineType
{
    Context,
    Addition,
    Deletion
}

public class DryRunSummary
{
    public int FilesToCreate { get; set; }
    public int FilesToModify { get; set; }
    public int FilesToDelete { get; set; }
    public int TotalTransformations { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
}
```

### Diff Display with Spectre.Console:

```csharp
public class DryRunReportRenderer
{
    public void Render(DryRunReport report)
    {
        // Summary panel
        var summaryPanel = new Panel(
            new Markup($"""
                [bold]Dry Run Summary[/]

                Files to create: [green]{report.Summary.FilesToCreate}[/]
                Files to modify: [yellow]{report.Summary.FilesToModify}[/]
                Files to delete: [red]{report.Summary.FilesToDelete}[/]

                Transformations: {report.Summary.TotalTransformations}
                Warnings: [yellow]{report.Summary.WarningCount}[/]
                """))
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(summaryPanel);

        // File diffs
        foreach (var diff in report.FileDiffs)
        {
            RenderFileDiff(diff);
        }

        // Warnings
        if (report.Warnings.Any())
        {
            AnsiConsole.MarkupLine("\n[yellow bold]Warnings:[/]");
            foreach (var warning in report.Warnings)
            {
                AnsiConsole.MarkupLine($"  [yellow]![/] {warning.Message}");
            }
        }

        // Final verdict
        if (report.WouldSucceed)
        {
            AnsiConsole.MarkupLine(
                "\n[green]Migration would complete successfully.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "\n[red]Migration would fail. Review errors above.[/]");
        }
    }

    private void RenderFileDiff(FileDiff diff)
    {
        var header = diff.Operation switch
        {
            DiffOperation.Create => $"[green]+++ {diff.FilePath}[/]",
            DiffOperation.Delete => $"[red]--- {diff.FilePath}[/]",
            DiffOperation.Modify => $"[yellow]~~~ {diff.FilePath}[/]",
            DiffOperation.Rename => $"[blue]>>> {diff.FilePath}[/]",
            _ => diff.FilePath
        };

        AnsiConsole.MarkupLine($"\n{header}");

        foreach (var hunk in diff.Hunks)
        {
            AnsiConsole.MarkupLine(
                $"[dim]@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@[/]");

            foreach (var line in hunk.Lines)
            {
                var markup = line.Type switch
                {
                    DiffLineType.Addition => $"[green]+{Markup.Escape(line.Content)}[/]",
                    DiffLineType.Deletion => $"[red]-{Markup.Escape(line.Content)}[/]",
                    DiffLineType.Context => $" {Markup.Escape(line.Content)}",
                    _ => line.Content
                };

                AnsiConsole.MarkupLine(markup);
            }
        }
    }
}
```

### Orchestrator Preview Method:

```csharp
public class MigrationOrchestrator : IMigrationOrchestrator
{
    public async Task<DryRunReport> PreviewMigrationAsync(
        string solutionPath,
        MigrationOptions options)
    {
        var report = new DryRunReport();
        var solution = await _solutionParser.ParseAsync(solutionPath);

        foreach (var project in solution.Projects)
        {
            // Generate what conversion would produce
            var conversionPreview = await _csprojConverter.PreviewConversionAsync(
                project,
                options);

            // Create diff
            var diff = GenerateDiff(
                project.FilePath,
                await File.ReadAllTextAsync(project.FilePath),
                conversionPreview.NewContent);

            report.FileDiffs.Add(diff);
            report.Transformations.Add(new PlannedTransformation
            {
                Type = TransformationType.CsprojConversion,
                Target = project.FilePath,
                Description = conversionPreview.Summary
            });

            report.Warnings.AddRange(conversionPreview.Warnings
                .Select(w => new DryRunWarning { Message = w }));
        }

        report.Summary = CalculateSummary(report);
        report.WouldSucceed = !report.Warnings.Any(w => w.IsCritical);

        return report;
    }
}
```

### Files to create/modify:

- `src/NetLift.Cli/Commands/MigrateCommand.cs` - Add dry-run flag handling
- `src/NetLift.Migration/DryRun/DryRunReport.cs` - Report model
- `src/NetLift.Migration/DryRun/DryRunReportRenderer.cs` - Console renderer
- `src/NetLift.Migration/DryRun/FileDiff.cs` - Diff models
- `src/NetLift.Migration/DryRun/DiffGenerator.cs` - Diff generation logic
- `tests/NetLift.Tests/DryRun/DryRunReportTests.cs` - Unit tests
- `tests/NetLift.Tests/DryRun/DiffGeneratorTests.cs` - Diff tests

### Key Decisions:

- Use unified diff format for familiarity
- Color-code output for quick visual scanning
- Support file output for sharing/review
- Include confidence scores in preview
- Make dry-run the recommended first step in documentation

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
| 2026-02-01 | fullstack-agent | Completed - Implemented dry-run mode with full diff preview, color-coded output, and comprehensive testing |
