using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Interface for parsing .NET project files.
/// </summary>
public interface IProjectParser
{
    /// <summary>
    /// Analyzes a project file and extracts comprehensive information.
    /// </summary>
    /// <param name="projectPath">The absolute path to the .csproj file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ProjectInfo object containing all extracted information.</returns>
    Task<ProjectInfo> AnalyzeAsync(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this parser can handle the specified project file.
    /// </summary>
    /// <param name="projectPath">The absolute path to the .csproj file.</param>
    /// <returns>True if this parser can handle the project file; otherwise, false.</returns>
    bool CanParse(string projectPath);
}
