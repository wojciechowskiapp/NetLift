using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Service for interactive user prompts during migration.
/// </summary>
public interface IInteractiveService
{
    /// <summary>
    /// Prompts the user for confirmation with a yes/no question.
    /// </summary>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if the user confirms, false otherwise.</returns>
    Task<bool> ConfirmAsync(string message);

    /// <summary>
    /// Prompts the user to choose an action for a project migration.
    /// </summary>
    /// <param name="message">The message to display to the user.</param>
    /// <param name="changedFiles">List of files that will be modified.</param>
    /// <returns>The user's choice.</returns>
    Task<InteractiveChoice> PromptChoiceAsync(string message, IEnumerable<string> changedFiles);
}
