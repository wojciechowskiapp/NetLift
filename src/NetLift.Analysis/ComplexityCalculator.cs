using NetLift.Core.Models;

namespace NetLift.Analysis;

/// <summary>
/// Calculates migration complexity scores for projects.
/// </summary>
public class ComplexityCalculator
{
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
            score += 20;
            factors.Add("ASP.NET MVC");
        }

        if (project.IsWebApi)
        {
            score += 15;
            factors.Add("ASP.NET Web API");
        }

        if (project.IsWcfService)
        {
            score += 35;
            factors.Add("WCF Service");
        }

        if (project.UsesEf6)
        {
            score += 15;
            factors.Add("Entity Framework 6");
        }

        // Incompatible packages
        var incompatible = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.Incompatible);
        if (incompatible > 0)
        {
            score += incompatible * 5;
            factors.Add($"{incompatible} incompatible package{(incompatible > 1 ? "s" : "")}");
        }

        // Packages requiring replacement
        var needsReplacement = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.HasReplacement);
        if (needsReplacement > 0)
        {
            score += needsReplacement * 3;
            factors.Add($"{needsReplacement} package{(needsReplacement > 1 ? "s" : "")} requiring replacement");
        }

        // Deprecated packages
        var deprecated = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.Deprecated);
        if (deprecated > 0)
        {
            score += deprecated * 4;
            factors.Add($"{deprecated} deprecated package{(deprecated > 1 ? "s" : "")}");
        }

        // Size factor
        if (project.EstimatedLinesOfCode > 50000)
        {
            score += 10;
            factors.Add("Large codebase (>50k LOC)");
        }
        else if (project.EstimatedLinesOfCode > 20000)
        {
            score += 5;
            factors.Add("Medium codebase (>20k LOC)");
        }

        // High dependency count
        if (project.DependencyCount > 50)
        {
            score += 8;
            factors.Add("High dependency count (>50)");
        }
        else if (project.DependencyCount > 25)
        {
            score += 4;
            factors.Add("Moderate dependency count (>25)");
        }

        // Legacy project types
        switch (project.PrimaryType)
        {
            case ProjectType.AspNetWebForms:
                score += 25;
                factors.Add("ASP.NET Web Forms");
                break;
            case ProjectType.CSharpWinForms:
                score += 10;
                factors.Add("Windows Forms");
                break;
            case ProjectType.CSharpWpf:
                score += 8;
                factors.Add("WPF");
                break;
        }

        return new MigrationComplexity
        {
            Score = Math.Min(score, 100),
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
            Score = Math.Min(overallScore, 100),
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
            <= 25 => ComplexityLevel.Low,
            <= 50 => ComplexityLevel.Medium,
            <= 75 => ComplexityLevel.High,
            _ => ComplexityLevel.VeryHigh
        };
    }
}
