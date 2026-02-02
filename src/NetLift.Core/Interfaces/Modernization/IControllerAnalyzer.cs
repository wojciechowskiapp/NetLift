using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Analyzes ASP.NET MVC controllers to extract action methods and patterns.
/// </summary>
public interface IControllerAnalyzer
{
    /// <summary>
    /// Analyzes a C# source file to extract controller information.
    /// </summary>
    /// <param name="filePath">Path to the .cs file</param>
    /// <param name="sourceCode">Source code content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ControllerInfo if file contains a controller, null otherwise</returns>
    Task<ControllerInfo?> AnalyzeAsync(
        string filePath, 
        string sourceCode, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyzes all controllers in a project.
    /// </summary>
    Task<IReadOnlyList<ControllerInfo>> AnalyzeProjectAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default);
}
