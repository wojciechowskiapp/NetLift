using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using Spectre.Console;

namespace NetLift.Cli.Services;

/// <summary>
/// Interactive service implementation using Spectre.Console for user prompts.
/// </summary>
public sealed class InteractiveService : IInteractiveService
{
    private bool _applyAll;

    /// <summary>
    /// Prompts the user for confirmation with a yes/no question.
    /// </summary>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if the user confirms, false otherwise.</returns>
    public Task<bool> ConfirmAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        var result = AnsiConsole.Confirm(message);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Prompts the user to choose an action for a project migration.
    /// </summary>
    /// <param name="message">The message to display to the user.</param>
    /// <param name="changedFiles">List of files that will be modified.</param>
    /// <returns>The user's choice.</returns>
    public Task<InteractiveChoice> PromptChoiceAsync(string message, IEnumerable<string> changedFiles)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        if (changedFiles == null)
        {
            throw new ArgumentNullException(nameof(changedFiles));
        }

        // If user previously selected "Apply All", automatically return Apply
        if (_applyAll)
        {
            return Task.FromResult(InteractiveChoice.Apply);
        }

        // Display the message and changed files
        AnsiConsole.WriteLine();
        var panel = new Panel(new Markup($"[bold yellow]{message}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(panel);

        // Display changed files
        var fileList = changedFiles.ToList();
        if (fileList.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Files to be changed:[/]");
            foreach (var file in fileList)
            {
                AnsiConsole.MarkupLine($"  [dim]•[/] {Markup.Escape(file)}");
            }
        }

        AnsiConsole.WriteLine();

        // Prompt for choice
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "Apply - Migrate this project",
                    "Skip - Skip this project",
                    "Preview - Show detailed changes",
                    "Apply All - Migrate this and all remaining projects",
                    "Abort - Cancel the entire migration"
                })
                .HighlightStyle(new Style(Color.Purple)));

        var result = choice switch
        {
            "Apply - Migrate this project" => InteractiveChoice.Apply,
            "Skip - Skip this project" => InteractiveChoice.Skip,
            "Preview - Show detailed changes" => InteractiveChoice.Preview,
            "Apply All - Migrate this and all remaining projects" => SetApplyAll(),
            "Abort - Cancel the entire migration" => InteractiveChoice.Abort,
            _ => InteractiveChoice.Skip
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Sets the "Apply All" flag and returns Apply choice.
    /// </summary>
    private InteractiveChoice SetApplyAll()
    {
        _applyAll = true;
        AnsiConsole.MarkupLine("[green]All remaining projects will be migrated without further prompts.[/]");
        return InteractiveChoice.Apply;
    }

    /// <summary>
    /// Resets the "Apply All" flag.
    /// </summary>
    public void Reset()
    {
        _applyAll = false;
    }
}
