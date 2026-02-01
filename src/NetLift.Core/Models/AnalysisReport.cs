namespace NetLift.Core.Models;

/// <summary>
/// Represents the complete analysis report for a .NET migration.
/// </summary>
public class AnalysisReport
{
    /// <summary>
    /// Gets or sets the timestamp when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Gets or sets the version of the NetLift tool that generated the report.
    /// </summary>
    public string ToolVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the solution file.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the solution name.
    /// </summary>
    public string SolutionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of projects in the solution.
    /// </summary>
    public int TotalProjects { get; set; }

    /// <summary>
    /// Gets or sets the target framework moniker (e.g., "net8.0").
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of project analyses.
    /// </summary>
    public List<ProjectAnalysis> Projects { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall migration complexity.
    /// </summary>
    public MigrationComplexity? OverallComplexity { get; set; }

    /// <summary>
    /// Gets or sets the estimated percentage of automated migration.
    /// </summary>
    public int EstimatedAutoMigrationPercentage { get; set; }

    /// <summary>
    /// Gets or sets the list of compatibility issues found.
    /// </summary>
    public List<CompatibilityIssue> Issues { get; set; } = new();

    /// <summary>
    /// Gets or sets the recommended migration phases.
    /// </summary>
    public List<MigrationPhase> RecommendedPhases { get; set; } = new();
}
