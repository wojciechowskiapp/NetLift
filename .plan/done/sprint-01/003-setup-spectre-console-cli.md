# [TASK-003] Setup Spectre.Console CLI

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001
- **Blocks:** TASK-010

---

## Description

Setup Spectre.Console.Cli for the command-line interface with basic command structure and help system.

---

## Acceptance Criteria

- [ ] Spectre.Console packages added to NetLift.Cli
- [ ] Basic command structure implemented (analyze, migrate, validate)
- [ ] `netlift --version` works
- [ ] `netlift --help` shows available commands
- [ ] `netlift analyze --help` shows analyze options
- [ ] Commands are placeholders (just print message for now)

---

## Technical Notes

### Packages to add:

```xml
<PackageReference Include="Spectre.Console" Version="0.48.0" />
<PackageReference Include="Spectre.Console.Cli" Version="0.48.0" />
```

### Command structure:

```csharp
// Program.cs
var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("netlift");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze a solution for migration");

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate a solution to .NET 8+");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate a migrated solution");
});

return app.Run(args);
```

### Command template:

```csharp
public class AnalyzeCommand : Command<AnalyzeCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to solution file")]
        public string SolutionPath { get; set; } = "";

        [CommandOption("-o|--output")]
        [Description("Output directory for report")]
        public string? OutputPath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine($"[green]Analyzing:[/] {settings.SolutionPath}");
        return 0;
    }
}
```

### Expected output:

```
$ netlift --help
USAGE:
    netlift [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help       Prints help information
    -v, --version    Prints version information

COMMANDS:
    analyze     Analyze a solution for migration
    migrate     Migrate a solution to .NET 8+
    validate    Validate a migrated solution
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
