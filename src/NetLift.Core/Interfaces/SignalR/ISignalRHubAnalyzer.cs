using NetLift.Core.Models.SignalR;

namespace NetLift.Core.Interfaces.SignalR;

/// <summary>
/// Analyzes SignalR Hub classes to identify transformation requirements.
/// </summary>
public interface ISignalRHubAnalyzer
{
    /// <summary>
    /// Analyzes a source file to detect SignalR Hub classes.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>List of SignalR Hub information found in the file.</returns>
    IReadOnlyList<SignalRHubInfo> AnalyzeFile(string sourceCode, string filePath);

    /// <summary>
    /// Analyzes a project directory for all SignalR Hubs.
    /// </summary>
    /// <param name="projectPath">The path to the project directory.</param>
    /// <returns>List of all SignalR Hubs found in the project.</returns>
    Task<IReadOnlyList<SignalRHubInfo>> AnalyzeProjectAsync(string projectPath);

    /// <summary>
    /// Checks if the source code contains SignalR Hub references.
    /// </summary>
    /// <param name="sourceCode">The C# source code to check.</param>
    /// <returns>True if SignalR Hub patterns are detected.</returns>
    bool ContainsSignalRHub(string sourceCode);
}
