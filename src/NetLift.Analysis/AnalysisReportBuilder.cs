using System.Reflection;
using NetLift.Analysis.Interfaces;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis;

/// <summary>
/// Builds comprehensive migration analysis reports from parsed solution and project data.
/// </summary>
public class AnalysisReportBuilder : IReportBuilder
{
    private readonly IProjectTypeDetector _projectTypeDetector;
    private readonly ComplexityCalculator _complexityCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisReportBuilder"/> class.
    /// </summary>
    /// <param name="projectTypeDetector">The project type detector.</param>
    public AnalysisReportBuilder(IProjectTypeDetector projectTypeDetector)
    {
        _projectTypeDetector = projectTypeDetector ?? throw new ArgumentNullException(nameof(projectTypeDetector));
        _complexityCalculator = new ComplexityCalculator();
    }

    /// <summary>
    /// Builds a complete analysis report from solution and project information.
    /// </summary>
    /// <param name="solutionInfo">The parsed solution information.</param>
    /// <param name="projectInfos">The collection of parsed project information.</param>
    /// <param name="targetFramework">The target framework for migration.</param>
    /// <returns>A complete analysis report.</returns>
    public AnalysisReport BuildReport(
        SolutionInfo solutionInfo,
        IEnumerable<ProjectInfo> projectInfos,
        string targetFramework)
    {
        var projectInfoList = projectInfos.ToList();
        var projectAnalyses = new List<ProjectAnalysis>();

        // Analyze each project
        foreach (var projectInfo in projectInfoList)
        {
            var analysis = AnalyzeProject(projectInfo);
            projectAnalyses.Add(analysis);
        }

        // Calculate overall complexity
        var overallComplexity = CalculateOverallComplexity(projectAnalyses);

        // Collect all issues
        var issues = CollectCompatibilityIssues(projectAnalyses);

        // Generate migration phases
        var phases = GenerateMigrationPhases(projectAnalyses);

        // Calculate estimated auto-migration percentage
        var autoMigrationPercentage = CalculateAutoMigrationPercentage(projectAnalyses);

        // Get tool version
        var toolVersion = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "1.0.0";

        return new AnalysisReport
        {
            GeneratedAt = DateTime.UtcNow,
            ToolVersion = toolVersion,
            SolutionPath = solutionInfo.FilePath,
            SolutionName = solutionInfo.Name,
            TotalProjects = projectInfoList.Count,
            TargetFramework = targetFramework,
            Projects = projectAnalyses,
            OverallComplexity = overallComplexity,
            EstimatedAutoMigrationPercentage = autoMigrationPercentage,
            Issues = issues,
            RecommendedPhases = phases
        };
    }

    /// <summary>
    /// Analyzes a single project and returns the analysis result.
    /// </summary>
    /// <param name="projectInfo">The project information to analyze.</param>
    /// <returns>The project analysis result.</returns>
    public ProjectAnalysis AnalyzeProject(ProjectInfo projectInfo)
    {
        var detection = _projectTypeDetector.Detect(projectInfo);

        var analysis = new ProjectAnalysis
        {
            ProjectPath = projectInfo.FilePath,
            ProjectName = projectInfo.Name,
            PrimaryType = detection.PrimaryType,
            CurrentFramework = projectInfo.TargetFramework,
            IsMvc = detection.IsMvc.Detected,
            IsWebApi = detection.IsWebApi.Detected,
            IsWcfService = detection.IsWcfService.Detected,
            UsesEf6 = detection.UsesEntityFramework6.Detected,
            SourceFileCount = projectInfo.CompileItems.Count,
            EstimatedLinesOfCode = EstimateLineCount(projectInfo),
            DependencyCount = projectInfo.PackageReferences.Count,
            Dependencies = AnalyzeDependencies(projectInfo.PackageReferences)
        };

        // Calculate complexity
        analysis.Complexity = _complexityCalculator.Calculate(analysis);

        return analysis;
    }

    /// <summary>
    /// Calculates the overall migration complexity for a collection of projects.
    /// </summary>
    /// <param name="projectAnalyses">The collection of project analyses.</param>
    /// <returns>The overall migration complexity assessment.</returns>
    public MigrationComplexity CalculateOverallComplexity(IEnumerable<ProjectAnalysis> projectAnalyses)
    {
        return _complexityCalculator.CalculateOverall(projectAnalyses);
    }

    /// <summary>
    /// Generates recommended migration phases based on project analyses.
    /// </summary>
    /// <param name="projectAnalyses">The collection of project analyses.</param>
    /// <param name="dependencyGraph">The dependency graph of projects.</param>
    /// <returns>The list of recommended migration phases.</returns>
    public List<MigrationPhase> GenerateMigrationPhases(
        IEnumerable<ProjectAnalysis> projectAnalyses,
        DependencyGraph? dependencyGraph = null)
    {
        var projects = projectAnalyses.ToList();
        var phases = new List<MigrationPhase>();

        if (projects.Count == 0)
        {
            return phases;
        }

        // Phase 1: Low complexity class libraries and test projects
        var phase1Projects = projects
            .Where(p => (p.PrimaryType == ProjectType.CSharpClassLibrary || p.PrimaryType == ProjectType.CSharpTest)
                       && (p.Complexity?.Level ?? ComplexityLevel.Low) <= ComplexityLevel.Low)
            .Select(p => p.ProjectName)
            .ToList();

        if (phase1Projects.Any())
        {
            phases.Add(new MigrationPhase
            {
                Order = 1,
                Name = "Foundation Libraries",
                Description = "Migrate low-complexity class libraries and test projects first to establish a solid foundation.",
                AffectedProjects = phase1Projects,
                EstimatedAutoPercentage = 85,
                ManualSteps = new List<string>
                {
                    "Review and update package references",
                    "Verify test execution in new framework",
                    "Update any incompatible APIs"
                }
            });
        }

        // Phase 2: Medium complexity libraries and console applications
        var phase2Projects = projects
            .Where(p => (p.PrimaryType == ProjectType.CSharpClassLibrary ||
                        p.PrimaryType == ProjectType.CSharpConsole)
                       && (p.Complexity?.Level ?? ComplexityLevel.Low) == ComplexityLevel.Medium)
            .Select(p => p.ProjectName)
            .ToList();

        if (phase2Projects.Any())
        {
            phases.Add(new MigrationPhase
            {
                Order = 2,
                Name = "Core Applications",
                Description = "Migrate medium-complexity libraries and console applications.",
                AffectedProjects = phase2Projects,
                EstimatedAutoPercentage = 60,
                ManualSteps = new List<string>
                {
                    "Update configuration system to use modern patterns",
                    "Replace deprecated packages",
                    "Update dependency injection patterns if applicable"
                }
            });
        }

        // Phase 3: Web applications (MVC, Web API)
        var phase3Projects = projects
            .Where(p => p.IsMvc || p.IsWebApi || p.PrimaryType == ProjectType.AspNetWebForms)
            .Select(p => p.ProjectName)
            .ToList();

        if (phase3Projects.Any())
        {
            phases.Add(new MigrationPhase
            {
                Order = 3,
                Name = "Web Applications",
                Description = "Migrate web applications to ASP.NET Core.",
                AffectedProjects = phase3Projects,
                EstimatedAutoPercentage = 40,
                ManualSteps = new List<string>
                {
                    "Convert to ASP.NET Core project structure",
                    "Update routing and middleware pipeline",
                    "Migrate authentication and authorization",
                    "Update views and static files",
                    "Convert Web.config to appsettings.json"
                }
            });
        }

        // Phase 4: WCF Services (highest complexity)
        var phase4Projects = projects
            .Where(p => p.IsWcfService)
            .Select(p => p.ProjectName)
            .ToList();

        if (phase4Projects.Any())
        {
            phases.Add(new MigrationPhase
            {
                Order = 4,
                Name = "WCF Services",
                Description = "Migrate or replace WCF services with modern alternatives.",
                AffectedProjects = phase4Projects,
                EstimatedAutoPercentage = 20,
                ManualSteps = new List<string>
                {
                    "Evaluate CoreWCF vs gRPC vs REST API alternatives",
                    "Migrate service contracts and implementations",
                    "Update service configuration",
                    "Migrate bindings and behaviors",
                    "Test service interoperability with clients"
                }
            });
        }

        // Phase 5: Desktop applications (WPF, WinForms)
        var phase5Projects = projects
            .Where(p => p.PrimaryType == ProjectType.CSharpWpf || p.PrimaryType == ProjectType.CSharpWinForms)
            .Select(p => p.ProjectName)
            .ToList();

        if (phase5Projects.Any())
        {
            phases.Add(new MigrationPhase
            {
                Order = 5,
                Name = "Desktop Applications",
                Description = "Migrate desktop applications to .NET.",
                AffectedProjects = phase5Projects,
                EstimatedAutoPercentage = 70,
                ManualSteps = new List<string>
                {
                    "Update project file to SDK-style",
                    "Verify UI rendering and behavior",
                    "Update any platform-specific code",
                    "Test on target operating systems"
                }
            });
        }

        return phases.OrderBy(p => p.Order).ToList();
    }

    /// <summary>
    /// Analyzes package dependencies for compatibility.
    /// </summary>
    /// <param name="packages">The package references to analyze.</param>
    /// <returns>The list of dependency analyses.</returns>
    private List<DependencyAnalysis> AnalyzeDependencies(List<PackageReference> packages)
    {
        var analyses = new List<DependencyAnalysis>();

        foreach (var package in packages)
        {
            var analysis = new DependencyAnalysis
            {
                PackageId = package.Name,
                CurrentVersion = package.Version,
                Compatibility = DeterminePackageCompatibility(package.Name),
                RecommendedVersion = GetRecommendedVersion(package.Name),
                ReplacementPackage = GetReplacementPackage(package.Name),
                Notes = GetPackageNotes(package.Name)
            };

            analyses.Add(analysis);
        }

        return analyses;
    }

    /// <summary>
    /// Determines the compatibility of a package with modern .NET.
    /// </summary>
    private PackageCompatibility DeterminePackageCompatibility(string packageId)
    {
        // Known incompatible packages
        var incompatiblePackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Web",
            "System.Web.Http",
            "System.Web.Mvc",
            "System.ServiceModel",
            "Microsoft.AspNet.WebApi",
            "Microsoft.AspNet.Mvc"
        };

        // Packages with modern replacements
        var hasReplacementPackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EntityFramework"] = "Microsoft.EntityFrameworkCore",
            ["Newtonsoft.Json"] = "System.Text.Json",
            ["Microsoft.Owin"] = "Microsoft.AspNetCore"
        };

        // Deprecated packages
        var deprecatedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.AspNet.WebPages",
            "Microsoft.AspNet.Razor"
        };

        if (incompatiblePackages.Contains(packageId))
        {
            return PackageCompatibility.Incompatible;
        }

        if (hasReplacementPackages.ContainsKey(packageId))
        {
            return PackageCompatibility.HasReplacement;
        }

        if (deprecatedPackages.Contains(packageId))
        {
            return PackageCompatibility.Deprecated;
        }

        // Default to compatible for unknown packages
        return PackageCompatibility.Compatible;
    }

    /// <summary>
    /// Gets the recommended version for a package.
    /// </summary>
    private string? GetRecommendedVersion(string packageId)
    {
        // This is a simplified implementation
        // In production, this would query NuGet API or a compatibility database
        return null;
    }

    /// <summary>
    /// Gets the replacement package for an incompatible package.
    /// </summary>
    private string? GetReplacementPackage(string packageId)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EntityFramework"] = "Microsoft.EntityFrameworkCore",
            ["Newtonsoft.Json"] = "System.Text.Json",
            ["System.Web.Http"] = "Microsoft.AspNetCore.Mvc",
            ["System.Web.Mvc"] = "Microsoft.AspNetCore.Mvc",
            ["Microsoft.Owin"] = "Microsoft.AspNetCore"
        };

        return replacements.TryGetValue(packageId, out var replacement) ? replacement : null;
    }

    /// <summary>
    /// Gets notes about a package's migration.
    /// </summary>
    private string? GetPackageNotes(string packageId)
    {
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EntityFramework"] = "EF6 can be migrated to EF Core, but may require schema and query changes.",
            ["Newtonsoft.Json"] = "Consider migrating to System.Text.Json for better performance.",
            ["System.Web.Http"] = "Migrate to ASP.NET Core MVC with API controllers.",
            ["System.Web.Mvc"] = "Migrate to ASP.NET Core MVC.",
            ["System.ServiceModel"] = "Consider CoreWCF for WCF compatibility or migrate to gRPC/REST."
        };

        return notes.TryGetValue(packageId, out var note) ? note : null;
    }

    /// <summary>
    /// Estimates the total line count for a project.
    /// </summary>
    private int EstimateLineCount(ProjectInfo projectInfo)
    {
        // Simplified estimation: average 100 lines per C# file
        // In production, this could actually read and count lines
        return projectInfo.CompileItems.Count * 100;
    }

    /// <summary>
    /// Collects all compatibility issues from project analyses.
    /// </summary>
    private List<CompatibilityIssue> CollectCompatibilityIssues(List<ProjectAnalysis> projectAnalyses)
    {
        var issues = new List<CompatibilityIssue>();

        foreach (var project in projectAnalyses)
        {
            // Add issues for incompatible packages
            foreach (var dependency in project.Dependencies)
            {
                if (dependency.Compatibility == PackageCompatibility.Incompatible)
                {
                    issues.Add(new CompatibilityIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = "NuGet",
                        Description = $"Package '{dependency.PackageId}' is not compatible with modern .NET.",
                        AffectedProject = project.ProjectName,
                        Recommendation = dependency.ReplacementPackage != null
                            ? $"Replace with '{dependency.ReplacementPackage}'."
                            : "Remove or find alternative solution.",
                        DocumentationUrl = null
                    });
                }
                else if (dependency.Compatibility == PackageCompatibility.HasReplacement)
                {
                    issues.Add(new CompatibilityIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "NuGet",
                        Description = $"Package '{dependency.PackageId}' has a modern replacement available.",
                        AffectedProject = project.ProjectName,
                        Recommendation = dependency.ReplacementPackage != null
                            ? $"Consider migrating to '{dependency.ReplacementPackage}'."
                            : "Review migration options.",
                        DocumentationUrl = null
                    });
                }
                else if (dependency.Compatibility == PackageCompatibility.Deprecated)
                {
                    issues.Add(new CompatibilityIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "NuGet",
                        Description = $"Package '{dependency.PackageId}' is deprecated.",
                        AffectedProject = project.ProjectName,
                        Recommendation = "Find alternative package or update to supported version.",
                        DocumentationUrl = null
                    });
                }
            }

            // Add issues for specific project types
            if (project.IsWcfService)
            {
                issues.Add(new CompatibilityIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Technology",
                    Description = "WCF is not natively supported in .NET Core/.NET.",
                    AffectedProject = project.ProjectName,
                    Recommendation = "Consider CoreWCF for compatibility or migrate to gRPC/REST API.",
                    DocumentationUrl = "https://learn.microsoft.com/en-us/dotnet/core/porting/wcf"
                });
            }

            if (project.PrimaryType == ProjectType.AspNetWebForms)
            {
                issues.Add(new CompatibilityIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = "Technology",
                    Description = "ASP.NET Web Forms is not supported in .NET Core/.NET.",
                    AffectedProject = project.ProjectName,
                    Recommendation = "Migrate to ASP.NET Core MVC or Blazor.",
                    DocumentationUrl = "https://learn.microsoft.com/en-us/aspnet/core/migration/proper-to-2x/"
                });
            }

            if (project.UsesEf6)
            {
                issues.Add(new CompatibilityIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Technology",
                    Description = "Entity Framework 6 requires migration to EF Core.",
                    AffectedProject = project.ProjectName,
                    Recommendation = "Plan for EF Core migration; review breaking changes and feature differences.",
                    DocumentationUrl = "https://learn.microsoft.com/en-us/ef/efcore-and-ef6/"
                });
            }
        }

        return issues;
    }

    /// <summary>
    /// Calculates the estimated percentage of automated migration.
    /// </summary>
    private int CalculateAutoMigrationPercentage(List<ProjectAnalysis> projectAnalyses)
    {
        if (projectAnalyses.Count == 0)
        {
            return 0;
        }

        var totalLoc = projectAnalyses.Sum(p => p.EstimatedLinesOfCode);
        if (totalLoc == 0)
        {
            return 0;
        }

        var weightedAutoPercentage = 0.0;

        foreach (var project in projectAnalyses)
        {
            var autoPercentage = project.Complexity?.Level switch
            {
                ComplexityLevel.Low => 85,
                ComplexityLevel.Medium => 60,
                ComplexityLevel.High => 40,
                ComplexityLevel.VeryHigh => 20,
                _ => 50
            };

            var weight = (double)project.EstimatedLinesOfCode / totalLoc;
            weightedAutoPercentage += autoPercentage * weight;
        }

        return (int)Math.Round(weightedAutoPercentage);
    }
}
