# [TASK-024] Implement Interactive Mode

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-020
- **Blocks:** -

---

## Description

Add an `--interactive` flag to the migrate command that prompts users for confirmation before each transformation step. This provides granular control over the migration process, allowing users to skip specific transformations, review changes before applying, and make informed decisions during the migration.

---

## Acceptance Criteria

- [ ] `--interactive` flag available on migrate command
- [ ] Spectre.Console prompts shown before each transformation
- [ ] Options: Apply, Skip, Preview diff, Apply all remaining, Abort
- [ ] Shows transformation details before prompting
- [ ] Remembers "Apply all" choice for remainder of session
- [ ] Displays running summary of applied/skipped transformations
- [ ] Supports keyboard shortcuts for quick navigation
- [ ] Graceful abort with cleanup of partial changes
- [ ] Unit tests for prompt logic
- [ ] Integration tests for interactive workflow

---

## Technical Notes

### Interactive Mode Implementation:

```csharp
public class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        // ... existing settings ...

        [CommandOption("-i|--interactive")]
        [Description("Confirm each transformation step interactively")]
        public bool Interactive { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings)
    {
        var options = new MigrationOptions
        {
            Interactive = settings.Interactive,
            // ... other options
        };

        if (settings.Interactive)
        {
            AnsiConsole.MarkupLine(
                "[dim]Interactive mode enabled. You will be prompted for each transformation.[/]");
            AnsiConsole.MarkupLine(
                "[dim]Shortcuts: (a)pply, (s)kip, (p)review, (A)pply all, (q)uit[/]\n");
        }

        return await _orchestrator.MigrateAsync(
            settings.SolutionPath,
            options);
    }
}
```

### Interactive Prompt Service:

```csharp
public interface IInteractivePrompt
{
    Task<TransformationDecision> PromptForTransformationAsync(
        PlannedTransformation transformation);
}

public enum TransformationDecision
{
    Apply,
    Skip,
    PreviewDiff,
    ApplyAllRemaining,
    Abort
}

public class SpectreInteractivePrompt : IInteractivePrompt
{
    private bool _applyAllRemaining = false;

    public async Task<TransformationDecision> PromptForTransformationAsync(
        PlannedTransformation transformation)
    {
        if (_applyAllRemaining)
        {
            return TransformationDecision.Apply;
        }

        // Display transformation info
        DisplayTransformationInfo(transformation);

        // Prompt for decision
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "Apply this transformation",
                    "Skip this transformation",
                    "Preview diff",
                    "Apply all remaining transformations",
                    "Abort migration"
                }));

        return choice switch
        {
            "Apply this transformation" => TransformationDecision.Apply,
            "Skip this transformation" => TransformationDecision.Skip,
            "Preview diff" => TransformationDecision.PreviewDiff,
            "Apply all remaining transformations" => SetApplyAll(),
            "Abort migration" => TransformationDecision.Abort,
            _ => TransformationDecision.Skip
        };
    }

    private void DisplayTransformationInfo(PlannedTransformation transformation)
    {
        var panel = new Panel(
            new Rows(
                new Markup($"[bold]{transformation.Type}[/]"),
                new Markup($"[dim]Target:[/] {transformation.Target}"),
                new Markup($"[dim]Description:[/] {transformation.Description}"),
                new Markup($"[dim]Confidence:[/] {transformation.ConfidenceScore:P0}"),
                transformation.Warnings.Any()
                    ? new Markup($"[yellow]Warnings: {transformation.Warnings.Count}[/]")
                    : new Markup("[green]No warnings[/]")
            ))
            .Header("[blue]Pending Transformation[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    private TransformationDecision SetApplyAll()
    {
        _applyAllRemaining = true;
        return TransformationDecision.Apply;
    }
}
```

### Interactive Orchestrator:

```csharp
public class InteractiveMigrationOrchestrator
{
    private readonly IMigrationOrchestrator _innerOrchestrator;
    private readonly IInteractivePrompt _prompt;
    private readonly IDiffGenerator _diffGenerator;
    private readonly ILogger<InteractiveMigrationOrchestrator> _logger;

    public async Task<MigrationResult> MigrateInteractivelyAsync(
        string solutionPath,
        MigrationOptions options)
    {
        var plan = await _innerOrchestrator.CreateMigrationPlanAsync(solutionPath, options);
        var results = new List<TransformationResult>();
        var skipped = new List<PlannedTransformation>();

        DisplayMigrationPlan(plan);

        foreach (var transformation in plan.Transformations)
        {
            var decision = await _prompt.PromptForTransformationAsync(transformation);

            switch (decision)
            {
                case TransformationDecision.Apply:
                    var result = await ApplyTransformationAsync(transformation);
                    results.Add(result);
                    DisplayTransformationResult(result);
                    break;

                case TransformationDecision.Skip:
                    skipped.Add(transformation);
                    AnsiConsole.MarkupLine("[yellow]Skipped[/]");
                    break;

                case TransformationDecision.PreviewDiff:
                    await ShowDiffPreviewAsync(transformation);
                    // Re-prompt after showing diff
                    var retryDecision = await _prompt.PromptForTransformationAsync(transformation);
                    // Handle retry decision...
                    break;

                case TransformationDecision.Abort:
                    AnsiConsole.MarkupLine("[red]Migration aborted by user[/]");
                    return CreateAbortedResult(results, skipped, plan.Transformations);

                case TransformationDecision.ApplyAllRemaining:
                    // Apply this and all remaining without prompting
                    results.Add(await ApplyTransformationAsync(transformation));
                    results.AddRange(await ApplyRemainingAsync(
                        plan.Transformations.SkipWhile(t => t != transformation).Skip(1)));
                    return CreateSuccessResult(results, skipped);
            }
        }

        DisplayFinalSummary(results, skipped);
        return CreateSuccessResult(results, skipped);
    }

    private void DisplayMigrationPlan(MigrationPlan plan)
    {
        var table = new Table()
            .AddColumn("#")
            .AddColumn("Type")
            .AddColumn("Target")
            .AddColumn("Confidence");

        var index = 1;
        foreach (var t in plan.Transformations)
        {
            table.AddRow(
                index++.ToString(),
                t.Type.ToString(),
                t.Target,
                $"{t.ConfidenceScore:P0}");
        }

        AnsiConsole.Write(new Panel(table)
            .Header("[blue]Migration Plan[/]")
            .Border(BoxBorder.Rounded));
    }

    private async Task ShowDiffPreviewAsync(PlannedTransformation transformation)
    {
        var diff = await _diffGenerator.GenerateDiffAsync(transformation);

        AnsiConsole.Write(new Panel(
            new Markup(FormatDiff(diff)))
            .Header($"[blue]Diff Preview: {transformation.Target}[/]")
            .Border(BoxBorder.Rounded));
    }

    private void DisplayFinalSummary(
        List<TransformationResult> applied,
        List<PlannedTransformation> skipped)
    {
        AnsiConsole.WriteLine();

        var summaryTable = new Table()
            .AddColumn("Status")
            .AddColumn("Count");

        summaryTable.AddRow("[green]Applied[/]", applied.Count.ToString());
        summaryTable.AddRow("[yellow]Skipped[/]", skipped.Count.ToString());
        summaryTable.AddRow("[red]Failed[/]", applied.Count(r => !r.Success).ToString());

        AnsiConsole.Write(new Panel(summaryTable)
            .Header("[blue]Migration Summary[/]")
            .Border(BoxBorder.Double));
    }
}
```

### Keyboard Shortcut Handler:

```csharp
public class KeyboardShortcutPrompt : IInteractivePrompt
{
    public async Task<TransformationDecision> PromptForTransformationAsync(
        PlannedTransformation transformation)
    {
        DisplayTransformationInfo(transformation);

        AnsiConsole.Markup(
            "[dim](a)pply | (s)kip | (p)review | (A)pply all | (q)uit:[/] ");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.KeyChar)
            {
                case 'a':
                    AnsiConsole.WriteLine("apply");
                    return TransformationDecision.Apply;

                case 's':
                    AnsiConsole.WriteLine("skip");
                    return TransformationDecision.Skip;

                case 'p':
                    AnsiConsole.WriteLine("preview");
                    return TransformationDecision.PreviewDiff;

                case 'A':
                    AnsiConsole.WriteLine("apply all");
                    return TransformationDecision.ApplyAllRemaining;

                case 'q':
                    AnsiConsole.WriteLine("quit");
                    return TransformationDecision.Abort;

                default:
                    // Invalid key, continue waiting
                    break;
            }
        }
    }
}
```

### Files to create/modify:

- `src/NetLift.Cli/Commands/MigrateCommand.cs` - Add interactive flag
- `src/NetLift.Migration/Interactive/IInteractivePrompt.cs` - Interface
- `src/NetLift.Migration/Interactive/SpectreInteractivePrompt.cs` - Spectre implementation
- `src/NetLift.Migration/Interactive/KeyboardShortcutPrompt.cs` - Keyboard shortcuts
- `src/NetLift.Migration/Interactive/InteractiveMigrationOrchestrator.cs` - Orchestrator
- `src/NetLift.Migration/Interactive/TransformationDecision.cs` - Decision enum
- `tests/NetLift.Tests/Interactive/InteractivePromptTests.cs` - Unit tests

### Key Decisions:

- Support both menu-based and keyboard shortcut modes
- Remember "Apply all" choice for session efficiency
- Show diff preview on demand to avoid information overload
- Provide abort option with graceful cleanup
- Display running summary for progress awareness

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
