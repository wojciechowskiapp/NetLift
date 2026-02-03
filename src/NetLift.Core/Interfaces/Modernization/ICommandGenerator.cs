using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates CQRS Command classes from controller actions.
/// </summary>
public interface ICommandGenerator
{
    /// <summary>
    /// Generates a Command class for an action.
    /// </summary>
    /// <param name="commandInfo">Information about the command to generate</param>
    /// <returns>Generated C# source code for the command class</returns>
    string Generate(CommandInfo commandInfo);

    /// <summary>
    /// Generates a CommandHandler class.
    /// </summary>
    /// <param name="commandInfo">Information about the command handler to generate</param>
    /// <returns>Generated C# source code for the command handler class</returns>
    string GenerateHandler(CommandInfo commandInfo);
}
