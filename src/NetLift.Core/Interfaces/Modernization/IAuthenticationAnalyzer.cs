using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Analyzes .NET Framework projects to detect authentication and authorization patterns.
/// Identifies [Authorize] attributes, role usage, Membership API calls, and custom identity implementations.
/// </summary>
public interface IAuthenticationAnalyzer
{
    /// <summary>
    /// Analyzes a single C# source file for authentication patterns.
    /// </summary>
    /// <param name="filePath">Path to the .cs file to analyze.</param>
    /// <param name="sourceCode">Source code content of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication information if patterns are found, null otherwise.</returns>
    Task<AuthenticationInfo?> AnalyzeFileAsync(
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes all source files in a project for authentication patterns.
    /// </summary>
    /// <param name="projectInfo">Project information including file paths.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated authentication information for the entire project.</returns>
    Task<AuthenticationInfo> AnalyzeProjectAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default);
}
