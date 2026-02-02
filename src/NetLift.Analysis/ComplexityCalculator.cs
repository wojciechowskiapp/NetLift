using NetLift.Core.Models;

namespace NetLift.Analysis;

/// <summary>
/// Calculates migration complexity scores for projects.
/// </summary>
public class ComplexityCalculator
{
    // Technology complexity scores
    private const int MvcComplexityScore = 20;
    private const int WebApiComplexityScore = 15;
    private const int WcfComplexityScore = 35;
    private const int Ef6ComplexityScore = 15;
    private const int WebFormsComplexityScore = 25;
    private const int WinFormsComplexityScore = 10;
    private const int WpfComplexityScore = 8;

    // Package compatibility scores (per package)
    private const int IncompatiblePackageScore = 5;
    private const int ReplacementPackageScore = 3;
    private const int DeprecatedPackageScore = 4;

    // Size thresholds
    private const int LargeCodebaseThreshold = 50000;
    private const int MediumCodebaseThreshold = 20000;
    private const int LargeCodebaseScore = 10;
    private const int MediumCodebaseScore = 5;

    // Dependency count thresholds
    private const int HighDependencyThreshold = 50;
    private const int ModerateDependencyThreshold = 25;
    private const int HighDependencyScore = 8;
    private const int ModerateDependencyScore = 4;

    // Complexity level boundaries
    private const int LowComplexityThreshold = 25;
    private const int MediumComplexityThreshold = 50;
    private const int HighComplexityThreshold = 75;
    private const int MaxComplexityScore = 100;

    /// <summary>
    /// Calculates the migration complexity for a project.
    /// </summary>
    /// <param name="project">The project analysis to assess.</param>
    /// <returns>The migration complexity assessment.</returns>
    public MigrationComplexity Calculate(ProjectAnalysis project)
    {
        var score = 0;
        var factors = new List<string>();

        // Base complexity by type
        if (project.IsMvc)
        {
            score += MvcComplexityScore;
            factors.Add("ASP.NET MVC");
        }

        if (project.IsWebApi)
        {
            score += WebApiComplexityScore;
            factors.Add("ASP.NET Web API");
        }

        if (project.IsWcfService)
        {
            score += WcfComplexityScore;
            factors.Add("WCF Service");
        }

        if (project.UsesEf6)
        {
            score += Ef6ComplexityScore;
            factors.Add("Entity Framework 6");
        }

        // Incompatible packages
        var incompatible = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.Incompatible);
        if (incompatible > 0)
        {
            score += incompatible * IncompatiblePackageScore;
            factors.Add($"{incompatible} incompatible package{(incompatible > 1 ? "s" : "")}");
        }

        // Packages requiring replacement
        var needsReplacement = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.HasReplacement);
        if (needsReplacement > 0)
        {
            score += needsReplacement * ReplacementPackageScore;
            factors.Add($"{needsReplacement} package{(needsReplacement > 1 ? "s" : "")} requiring replacement");
        }

        // Deprecated packages
        var deprecated = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.Deprecated);
        if (deprecated > 0)
        {
            score += deprecated * DeprecatedPackageScore;
            factors.Add($"{deprecated} deprecated package{(deprecated > 1 ? "s" : "")}");
        }

        // Size factor
        if (project.EstimatedLinesOfCode > LargeCodebaseThreshold)
        {
            score += LargeCodebaseScore;
            factors.Add($"Large codebase (>{LargeCodebaseThreshold / 1000}k LOC)");
        }
        else if (project.EstimatedLinesOfCode > MediumCodebaseThreshold)
        {
            score += MediumCodebaseScore;
            factors.Add($"Medium codebase (>{MediumCodebaseThreshold / 1000}k LOC)");
        }

        // High dependency count
        if (project.DependencyCount > HighDependencyThreshold)
        {
            score += HighDependencyScore;
            factors.Add($"High dependency count (>{HighDependencyThreshold})");
        }
        else if (project.DependencyCount > ModerateDependencyThreshold)
        {
            score += ModerateDependencyScore;
            factors.Add($"Moderate dependency count (>{ModerateDependencyThreshold})");
        }

        // Legacy project types
        switch (project.PrimaryType)
        {
            case ProjectType.AspNetWebForms:
                score += WebFormsComplexityScore;
                factors.Add("ASP.NET Web Forms");
                break;
            case ProjectType.CSharpWinForms:
                score += WinFormsComplexityScore;
                factors.Add("Windows Forms");
                break;
            case ProjectType.CSharpWpf:
                score += WpfComplexityScore;
                factors.Add("WPF");
                break;
        }

        return new MigrationComplexity
        {
            Score = Math.Min(score, MaxComplexityScore),
            Level = ScoreToLevel(score),
            Factors = factors
        };
    }

    /// <summary>
    /// Calculates the overall complexity for multiple projects.
    /// </summary>
    /// <param name="projects">The collection of project analyses.</param>
    /// <returns>The overall migration complexity assessment.</returns>
    public MigrationComplexity CalculateOverall(IEnumerable<ProjectAnalysis> projects)
    {
        var projectList = projects.ToList();

        if (projectList.Count == 0)
        {
            return new MigrationComplexity
            {
                Score = 0,
                Level = ComplexityLevel.Low,
                Factors = new List<string> { "No projects to analyze" }
            };
        }

        // Calculate weighted average based on project size
        var totalLoc = projectList.Sum(p => p.EstimatedLinesOfCode);
        var weightedScore = 0.0;

        foreach (var project in projectList)
        {
            var projectComplexity = project.Complexity ?? Calculate(project);
            var weight = totalLoc > 0 ? (double)project.EstimatedLinesOfCode / totalLoc : 1.0 / projectList.Count;
            weightedScore += projectComplexity.Score * weight;
        }

        var overallScore = (int)Math.Round(weightedScore);
        var factors = new List<string>();

        // Count projects by complexity level
        var highComplexity = projectList.Count(p =>
            (p.Complexity?.Level ?? ComplexityLevel.Low) >= ComplexityLevel.High);
        var mediumComplexity = projectList.Count(p =>
            (p.Complexity?.Level ?? ComplexityLevel.Low) == ComplexityLevel.Medium);

        if (highComplexity > 0)
        {
            factors.Add($"{highComplexity} project{(highComplexity > 1 ? "s" : "")} with high complexity");
        }

        if (mediumComplexity > 0)
        {
            factors.Add($"{mediumComplexity} project{(mediumComplexity > 1 ? "s" : "")} with medium complexity");
        }

        // Count specific technologies across all projects
        var mvcCount = projectList.Count(p => p.IsMvc);
        var wcfCount = projectList.Count(p => p.IsWcfService);
        var ef6Count = projectList.Count(p => p.UsesEf6);

        if (mvcCount > 0)
        {
            factors.Add($"{mvcCount} ASP.NET MVC project{(mvcCount > 1 ? "s" : "")}");
        }

        if (wcfCount > 0)
        {
            factors.Add($"{wcfCount} WCF service{(wcfCount > 1 ? "s" : "")}");
        }

        if (ef6Count > 0)
        {
            factors.Add($"{ef6Count} project{(ef6Count > 1 ? "s" : "")} using EF6");
        }

        return new MigrationComplexity
        {
            Score = Math.Min(overallScore, MaxComplexityScore),
            Level = ScoreToLevel(overallScore),
            Factors = factors
        };
    }

    /// <summary>
    /// Converts a numeric score to a complexity level.
    /// </summary>
    /// <param name="score">The complexity score (0-100).</param>
    /// <returns>The corresponding complexity level.</returns>
    private static ComplexityLevel ScoreToLevel(int score)
    {
        return score switch
        {
            <= LowComplexityThreshold => ComplexityLevel.Low,
            <= MediumComplexityThreshold => ComplexityLevel.Medium,
            <= HighComplexityThreshold => ComplexityLevel.High,
            _ => ComplexityLevel.VeryHigh
        };
    }
}
