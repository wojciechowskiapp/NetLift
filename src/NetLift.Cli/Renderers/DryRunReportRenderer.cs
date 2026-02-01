using NetLift.Core.Models;
using Spectre.Console;

namespace NetLift.Cli.Renderers;

/// <summary>
/// Renders dry-run reports with color-coded diff output using Spectre.Console.
/// </summary>
public sealed class DryRunReportRenderer
{
    private const int MaxPreviewLines = 50;
    private const int ContextLines = 3;

    /// <summary>
    /// Renders a dry-run report to the console.
    /// </summary>
    /// <param name="report">The dry-run report to render.</param>
    public void Render(DryRunReport report)
    {
        RenderHeader();
        RenderSummary(report.Summary);
        AnsiConsole.WriteLine();

        if (report.FileDiffs.Any())
        {
            RenderFileDiffs(report.FileDiffs);
            AnsiConsole.WriteLine();
        }

        if (report.Warnings.Any())
        {
            RenderWarnings(report.Warnings);
            AnsiConsole.WriteLine();
        }

        if (report.Errors.Any())
        {
            RenderErrors(report.Errors);
            AnsiConsole.WriteLine();
        }

        RenderConclusion(report);
    }

    private static void RenderHeader()
    {
        var rule = new Rule("[bold purple]Dry Run Preview[/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("purple")
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    private static void RenderSummary(DryRunSummary summary)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn().NoWrap());

        grid.AddRow(
            new Markup("[bold cyan]Files to create:[/]"),
            new Markup($"[green]{summary.FilesToCreate}[/]"));

        grid.AddRow(
            new Markup("[bold cyan]Files to modify:[/]"),
            new Markup($"[yellow]{summary.FilesToModify}[/]"));

        grid.AddRow(
            new Markup("[bold cyan]Files to delete:[/]"),
            new Markup($"[red]{summary.FilesToDelete}[/]"));

        if (summary.FilesToBackup > 0)
        {
            grid.AddRow(
                new Markup("[bold cyan]Backups to create:[/]"),
                new Markup($"[blue]{summary.FilesToBackup}[/]"));
        }

        grid.AddRow(
            new Markup("[bold cyan]Total files affected:[/]"),
            new Markup($"[white]{summary.TotalFilesAffected}[/]"));

        if (summary.WarningCount > 0)
        {
            grid.AddRow(
                new Markup("[bold cyan]Warnings:[/]"),
                new Markup($"[yellow]{summary.WarningCount}[/]"));
        }

        if (summary.ErrorCount > 0)
        {
            grid.AddRow(
                new Markup("[bold cyan]Errors:[/]"),
                new Markup($"[red]{summary.ErrorCount}[/]"));
        }

        var panel = new Panel(grid)
        {
            Header = new PanelHeader("[bold]Summary[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("purple")
        };

        AnsiConsole.Write(panel);
    }

    private static void RenderFileDiffs(List<FileDiff> fileDiffs)
    {
        var rule = new Rule("[bold]File Changes[/]")
        {
            Style = Style.Parse("dim")
        };
        AnsiConsole.Write(rule);

        foreach (var diff in fileDiffs)
        {
            RenderFileDiff(diff);
        }
    }

    private static void RenderFileDiff(FileDiff diff)
    {
        AnsiConsole.WriteLine();

        // Render file header with color coding based on change type
        var (header, color) = diff.ChangeType switch
        {
            ChangeType.Create => ($"[green]+++ {diff.FilePath}[/]", "green"),
            ChangeType.Delete => ($"[red]--- {diff.FilePath}[/]", "red"),
            ChangeType.Modify => ($"[yellow]~~~ {diff.FilePath}[/]", "yellow"),
            ChangeType.Backup => ($"[blue]>>> {diff.FilePath}.bak[/]", "blue"),
            _ => (diff.FilePath, "white")
        };

        AnsiConsole.MarkupLine(header);

        // Show preview if no hunks
        if (diff.Hunks.Count == 0 && !string.IsNullOrEmpty(diff.Preview))
        {
            AnsiConsole.MarkupLine($"[dim]    {diff.Preview}[/]");
            return;
        }

        // Render hunks
        var lineCount = 0;
        foreach (var hunk in diff.Hunks)
        {
            if (lineCount > MaxPreviewLines)
            {
                AnsiConsole.MarkupLine("[dim]    ... (output truncated)[/]");
                break;
            }

            RenderHunk(hunk, ref lineCount);
        }
    }

    private static void RenderHunk(DiffHunk hunk, ref int lineCount)
    {
        // Render hunk header
        AnsiConsole.MarkupLine(
            $"[dim cyan]@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@[/]");
        lineCount++;

        // Render lines
        foreach (var line in hunk.Lines)
        {
            if (lineCount > MaxPreviewLines)
            {
                break;
            }

            RenderDiffLine(line);
            lineCount++;
        }
    }

    private static void RenderDiffLine(DiffLine line)
    {
        var content = Markup.Escape(line.Content);

        var markup = line.Type switch
        {
            DiffLineType.Addition => $"[green]+{content}[/]",
            DiffLineType.Deletion => $"[red]-{content}[/]",
            DiffLineType.Context => $" {content}",
            _ => content
        };

        AnsiConsole.MarkupLine(markup);
    }

    private static void RenderWarnings(List<string> warnings)
    {
        var rule = new Rule("[bold yellow]Warnings[/]")
        {
            Style = Style.Parse("yellow")
        };
        AnsiConsole.Write(rule);

        foreach (var warning in warnings)
        {
            AnsiConsole.MarkupLine($"  [yellow]![/] {Markup.Escape(warning)}");
        }
    }

    private static void RenderErrors(List<string> errors)
    {
        var rule = new Rule("[bold red]Errors[/]")
        {
            Style = Style.Parse("red")
        };
        AnsiConsole.Write(rule);

        foreach (var error in errors)
        {
            AnsiConsole.MarkupLine($"  [red]X[/] {Markup.Escape(error)}");
        }
    }

    private static void RenderConclusion(DryRunReport report)
    {
        if (report.WouldSucceed)
        {
            var panel = new Panel(
                new Markup("[green]Migration would complete successfully.[/]\n\n" +
                          "Run without [bold]--dry-run[/] to apply these changes."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("green")
            };
            AnsiConsole.Write(panel);
        }
        else
        {
            var panel = new Panel(
                new Markup("[red]Migration would fail.[/]\n\n" +
                          "Please review and fix the errors above before proceeding."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("red")
            };
            AnsiConsole.Write(panel);
        }
    }

    /// <summary>
    /// Writes a dry-run report to a file.
    /// </summary>
    /// <param name="report">The dry-run report.</param>
    /// <param name="outputPath">The output file path.</param>
    public async Task WriteToFileAsync(DryRunReport report, string outputPath)
    {
        var lines = new List<string>
        {
            "# NetLift Dry Run Report",
            "",
            "## Summary",
            "",
            $"- Files to create: {report.Summary.FilesToCreate}",
            $"- Files to modify: {report.Summary.FilesToModify}",
            $"- Files to delete: {report.Summary.FilesToDelete}",
            $"- Backups to create: {report.Summary.FilesToBackup}",
            $"- Total files affected: {report.Summary.TotalFilesAffected}",
            $"- Warnings: {report.Summary.WarningCount}",
            $"- Errors: {report.Summary.ErrorCount}",
            "",
            "## File Changes",
            ""
        };

        foreach (var diff in report.FileDiffs)
        {
            var operation = diff.ChangeType switch
            {
                ChangeType.Create => "CREATE",
                ChangeType.Modify => "MODIFY",
                ChangeType.Delete => "DELETE",
                ChangeType.Backup => "BACKUP",
                _ => "UNKNOWN"
            };

            lines.Add($"### {operation}: {diff.FilePath}");
            lines.Add("");

            if (diff.Hunks.Count > 0)
            {
                foreach (var hunk in diff.Hunks)
                {
                    lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

                    foreach (var line in hunk.Lines)
                    {
                        var prefix = line.Type switch
                        {
                            DiffLineType.Addition => "+",
                            DiffLineType.Deletion => "-",
                            _ => " "
                        };
                        lines.Add($"{prefix}{line.Content}");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(diff.Preview))
            {
                lines.Add(diff.Preview);
            }

            lines.Add("");
        }

        if (report.Warnings.Any())
        {
            lines.Add("## Warnings");
            lines.Add("");
            foreach (var warning in report.Warnings)
            {
                lines.Add($"- {warning}");
            }
            lines.Add("");
        }

        if (report.Errors.Any())
        {
            lines.Add("## Errors");
            lines.Add("");
            foreach (var error in report.Errors)
            {
                lines.Add($"- {error}");
            }
            lines.Add("");
        }

        lines.Add("## Conclusion");
        lines.Add("");
        lines.Add(report.WouldSucceed
            ? "Migration would complete successfully."
            : "Migration would fail. Please review and fix the errors above.");

        await File.WriteAllLinesAsync(outputPath, lines);
    }
}
