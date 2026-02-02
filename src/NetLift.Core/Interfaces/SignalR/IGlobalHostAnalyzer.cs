using NetLift.Core.Models.SignalR;

namespace NetLift.Core.Interfaces.SignalR;

/// <summary>
/// Analyzes GlobalHost usage that needs to be transformed to IHubContext injection.
/// </summary>
public interface IGlobalHostAnalyzer
{
    /// <summary>
    /// Analyzes a source file for GlobalHost usage.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>Information about GlobalHost usage, or null if none found.</returns>
    GlobalHostUsageInfo? AnalyzeFile(string sourceCode, string filePath);

    /// <summary>
    /// Analyzes a project for all GlobalHost usages.
    /// </summary>
    /// <param name="projectPath">The path to the project directory.</param>
    /// <returns>List of all GlobalHost usages found.</returns>
    Task<IReadOnlyList<GlobalHostUsageInfo>> AnalyzeProjectAsync(string projectPath);

    /// <summary>
    /// Checks if the source code contains GlobalHost references.
    /// </summary>
    /// <param name="sourceCode">The C# source code to check.</param>
    /// <returns>True if GlobalHost patterns are detected.</returns>
    bool ContainsGlobalHost(string sourceCode);
}
