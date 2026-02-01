using NetLift.Core.Models;

namespace NetLift.Analysis.Interfaces;

/// <summary>
/// Interface for building migration analysis reports.
/// </summary>
public interface IReportBuilder
{
    /// <summary>
    /// Builds a complete analysis report from solution and project information.
    /// </summary>
    /// <param name="solutionInfo">The parsed solution information.</param>
    /// <param name="projectInfos">The collection of parsed project information.</param>
    /// <param name="targetFramework">The target framework for migration.</param>
    /// <returns>A complete analysis report.</returns>
    AnalysisReport BuildReport(
        SolutionInfo solutionInfo,
        IEnumerable<ProjectInfo> projectInfos,
        string targetFramework);

    /// <summary>
    /// Analyzes a single project and returns the analysis result.
    /// </summary>
    /// <param name="projectInfo">The project information to analyze.</param>
    /// <returns>The project analysis result.</returns>
    ProjectAnalysis AnalyzeProject(ProjectInfo projectInfo);

    /// <summary>
    /// Calculates the overall migration complexity for a collection of projects.
    /// </summary>
    /// <param name="projectAnalyses">The collection of project analyses.</param>
    /// <returns>The overall migration complexity assessment.</returns>
    MigrationComplexity CalculateOverallComplexity(IEnumerable<ProjectAnalysis> projectAnalyses);

    /// <summary>
    /// Generates recommended migration phases based on project analyses.
    /// </summary>
    /// <param name="projectAnalyses">The collection of project analyses.</param>
    /// <param name="dependencyGraph">The dependency graph of projects.</param>
    /// <returns>The list of recommended migration phases.</returns>
    List<MigrationPhase> GenerateMigrationPhases(
        IEnumerable<ProjectAnalysis> projectAnalyses,
        DependencyGraph? dependencyGraph = null);
}
