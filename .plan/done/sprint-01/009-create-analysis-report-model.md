# [TASK-009] Create Analysis Report Model

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-008
- **Blocks:** TASK-010, TASK-014

---

## Description

Design and implement the data models for the analysis report that will be shown to users and used for migration planning.

---

## Acceptance Criteria

- [ ] AnalysisReport model captures all relevant information
- [ ] Includes migration complexity scoring
- [ ] Includes compatibility issues list
- [ ] Includes recommended migration phases
- [ ] Includes package compatibility information
- [ ] Can be serialized to JSON
- [ ] Unit tests for model validation

---

## Technical Notes

### AnalysisReport model:

```csharp
public class AnalysisReport
{
    public DateTime GeneratedAt { get; set; }
    public string ToolVersion { get; set; }

    // Solution info
    public string SolutionPath { get; set; }
    public string SolutionName { get; set; }
    public int TotalProjects { get; set; }

    // Target
    public string TargetFramework { get; set; }  // e.g., "net8.0"

    // Project analysis
    public List<ProjectAnalysis> Projects { get; set; }

    // Overall metrics
    public MigrationComplexity OverallComplexity { get; set; }
    public int EstimatedAutoMigrationPercentage { get; set; }

    // Issues found
    public List<CompatibilityIssue> Issues { get; set; }

    // Recommended migration plan
    public List<MigrationPhase> RecommendedPhases { get; set; }
}

public class ProjectAnalysis
{
    public string ProjectPath { get; set; }
    public string ProjectName { get; set; }
    public ProjectType PrimaryType { get; set; }
    public TargetFramework CurrentFramework { get; set; }

    // Detections
    public bool IsMvc { get; set; }
    public bool IsWebApi { get; set; }
    public bool IsWcfService { get; set; }
    public bool UsesEf6 { get; set; }

    // Metrics
    public int SourceFileCount { get; set; }
    public int EstimatedLinesOfCode { get; set; }

    // Dependencies
    public int DependencyCount { get; set; }
    public List<DependencyAnalysis> Dependencies { get; set; }

    // Complexity
    public MigrationComplexity Complexity { get; set; }
}

public class MigrationComplexity
{
    public ComplexityLevel Level { get; set; }  // Low, Medium, High, VeryHigh
    public int Score { get; set; }  // 0-100
    public List<string> Factors { get; set; }  // What contributes to complexity
}

public enum ComplexityLevel
{
    Low,      // 0-25: Mostly auto-migration
    Medium,   // 26-50: Some manual work
    High,     // 51-75: Significant manual work
    VeryHigh  // 76-100: Major rewrite needed
}

public class CompatibilityIssue
{
    public IssueSeverity Severity { get; set; }
    public string Category { get; set; }  // "NuGet", "API", "Pattern", etc.
    public string Description { get; set; }
    public string AffectedProject { get; set; }
    public string? AffectedFile { get; set; }
    public int? LineNumber { get; set; }
    public string? Recommendation { get; set; }
    public string? DocumentationUrl { get; set; }
}

public enum IssueSeverity
{
    Info,      // FYI, no action needed
    Warning,   // May need attention
    Error,     // Must be addressed
    Blocker    // Cannot migrate without fixing
}

public class MigrationPhase
{
    public int Order { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> AffectedProjects { get; set; }
    public int EstimatedAutoPercentage { get; set; }
    public List<string> ManualSteps { get; set; }
}

public class DependencyAnalysis
{
    public string PackageId { get; set; }
    public string CurrentVersion { get; set; }
    public PackageCompatibility Compatibility { get; set; }
    public string? RecommendedVersion { get; set; }
    public string? ReplacementPackage { get; set; }
    public string? Notes { get; set; }
}
```

### Complexity scoring factors:

```csharp
public class ComplexityCalculator
{
    public MigrationComplexity Calculate(ProjectAnalysis project)
    {
        var score = 0;
        var factors = new List<string>();

        // Base complexity by type
        if (project.IsMvc) { score += 20; factors.Add("ASP.NET MVC"); }
        if (project.IsWcfService) { score += 35; factors.Add("WCF Service"); }
        if (project.UsesEf6) { score += 15; factors.Add("Entity Framework 6"); }

        // Incompatible packages
        var incompatible = project.Dependencies
            .Count(d => d.Compatibility == PackageCompatibility.Incompatible);
        if (incompatible > 0)
        {
            score += incompatible * 5;
            factors.Add($"{incompatible} incompatible packages");
        }

        // Size factor
        if (project.EstimatedLinesOfCode > 50000)
        {
            score += 10;
            factors.Add("Large codebase (>50k LOC)");
        }

        return new MigrationComplexity
        {
            Score = Math.Min(score, 100),
            Level = ScoreToLevel(score),
            Factors = factors
        };
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
