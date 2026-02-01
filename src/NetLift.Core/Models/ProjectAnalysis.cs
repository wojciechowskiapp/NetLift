namespace NetLift.Core.Models;

/// <summary>
/// Represents the analysis of a single project for migration.
/// </summary>
public class ProjectAnalysis
{
    /// <summary>
    /// Gets or sets the absolute path to the project file.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary type of the project.
    /// </summary>
    public ProjectType PrimaryType { get; set; }

    /// <summary>
    /// Gets or sets the current target framework.
    /// </summary>
    public TargetFramework? CurrentFramework { get; set; }

    /// <summary>
    /// Gets or sets whether the project is an ASP.NET MVC project.
    /// </summary>
    public bool IsMvc { get; set; }

    /// <summary>
    /// Gets or sets whether the project is an ASP.NET Web API project.
    /// </summary>
    public bool IsWebApi { get; set; }

    /// <summary>
    /// Gets or sets whether the project is a WCF service project.
    /// </summary>
    public bool IsWcfService { get; set; }

    /// <summary>
    /// Gets or sets whether the project uses Entity Framework 6.
    /// </summary>
    public bool UsesEf6 { get; set; }

    /// <summary>
    /// Gets or sets the number of source files in the project.
    /// </summary>
    public int SourceFileCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated lines of code in the project.
    /// </summary>
    public int EstimatedLinesOfCode { get; set; }

    /// <summary>
    /// Gets or sets the number of dependencies.
    /// </summary>
    public int DependencyCount { get; set; }

    /// <summary>
    /// Gets or sets the list of dependency analyses.
    /// </summary>
    public List<DependencyAnalysis> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the migration complexity assessment.
    /// </summary>
    public MigrationComplexity? Complexity { get; set; }
}
