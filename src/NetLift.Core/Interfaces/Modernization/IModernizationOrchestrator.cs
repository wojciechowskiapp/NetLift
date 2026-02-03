using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Orchestrates the complete modernization process for transforming
/// legacy MVC architecture to Clean Architecture with CQRS.
/// </summary>
public interface IModernizationOrchestrator
{
    /// <summary>
    /// Analyzes a project for modernization opportunities.
    /// </summary>
    Task<ModernizationAnalysis> AnalyzeAsync(
        ProjectInfo projectInfo,
        ModernizationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes modernization transformations on a project.
    /// </summary>
    Task<ModernizationResult> ModernizeAsync(
        ProjectInfo projectInfo,
        ModernizationOptions options,
        CancellationToken cancellationToken = default);
}
