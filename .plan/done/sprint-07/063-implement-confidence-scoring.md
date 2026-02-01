# [TASK-063] Implement Confidence Scoring

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 7 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-061, TASK-062
- **Blocks:** TASK-064

---

## Description

Implement a confidence scoring system that calculates an overall migration confidence score (0-100) based on build results, test results, detected issues, and migration complexity.

---

## Acceptance Criteria

- [ ] `IConfidenceScorer` interface created
- [ ] Calculates weighted confidence score from multiple factors
- [ ] Provides breakdown of score components
- [ ] Returns confidence level (High/Medium/Low)
- [ ] Includes actionable recommendations
- [ ] Unit tests for scoring logic
- [ ] Score thresholds are configurable

---

## Technical Notes

### Interface:

```csharp
namespace NetLift.Validation;

public interface IConfidenceScorer
{
    ConfidenceScore CalculateScore(MigrationValidationContext context);
}

public record MigrationValidationContext
{
    public BuildResult? BuildResult { get; init; }
    public TestResult? TestResult { get; init; }
    public AnalysisReport AnalysisReport { get; init; } = null!;
    public MigrationReport MigrationReport { get; init; } = null!;
}

public record ConfidenceScore
{
    public int OverallScore { get; init; }
    public ConfidenceLevel Level { get; init; }
    public IReadOnlyDictionary<string, ScoreComponent> Components { get; init; } =
        new Dictionary<string, ScoreComponent>();
    public IReadOnlyList<string> Recommendations { get; init; } = [];
}

public enum ConfidenceLevel
{
    High,    // 80-100
    Medium,  // 50-79
    Low      // 0-49
}

public record ScoreComponent
{
    public string Name { get; init; } = "";
    public int Score { get; init; }
    public int Weight { get; init; }
    public int WeightedScore { get; init; }
    public string Rationale { get; init; } = "";
}
```

### Implementation:

```csharp
public class ConfidenceScorer : IConfidenceScorer
{
    private const int BuildSuccessWeight = 30;
    private const int TestSuccessWeight = 25;
    private const int MigrationComplexityWeight = 20;
    private const int IssueCountWeight = 15;
    private const int ApiCompatibilityWeight = 10;

    public ConfidenceScore CalculateScore(MigrationValidationContext context)
    {
        var components = new Dictionary<string, ScoreComponent>
        {
            ["Build"] = CalculateBuildScore(context.BuildResult),
            ["Tests"] = CalculateTestScore(context.TestResult),
            ["Complexity"] = CalculateComplexityScore(context.AnalysisReport),
            ["Issues"] = CalculateIssueScore(context.MigrationReport),
            ["Compatibility"] = CalculateCompatibilityScore(context.AnalysisReport)
        };

        var overallScore = components.Values.Sum(c => c.WeightedScore);
        var level = overallScore switch
        {
            >= 80 => ConfidenceLevel.High,
            >= 50 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };

        var recommendations = GenerateRecommendations(components, context);

        return new ConfidenceScore
        {
            OverallScore = overallScore,
            Level = level,
            Components = components,
            Recommendations = recommendations
        };
    }

    private static ScoreComponent CalculateBuildScore(BuildResult? buildResult)
    {
        if (buildResult == null)
        {
            return new ScoreComponent
            {
                Name = "Build Validation",
                Score = 0,
                Weight = BuildSuccessWeight,
                WeightedScore = 0,
                Rationale = "Build not executed"
            };
        }

        // Perfect score if no errors
        // Deduct 10 points per error, 2 points per warning
        var errorPenalty = Math.Min(buildResult.Errors.Count * 10, 100);
        var warningPenalty = Math.Min(buildResult.Warnings.Count * 2, 20);
        var score = Math.Max(0, 100 - errorPenalty - warningPenalty);

        return new ScoreComponent
        {
            Name = "Build Validation",
            Score = score,
            Weight = BuildSuccessWeight,
            WeightedScore = score * BuildSuccessWeight / 100,
            Rationale = buildResult.Success
                ? $"Build succeeded with {buildResult.Warnings.Count} warnings"
                : $"Build failed with {buildResult.Errors.Count} errors"
        };
    }

    private static ScoreComponent CalculateTestScore(TestResult? testResult)
    {
        if (testResult == null || testResult.TotalTests == 0)
        {
            return new ScoreComponent
            {
                Name = "Test Results",
                Score = 50, // Neutral score if no tests
                Weight = TestSuccessWeight,
                WeightedScore = 50 * TestSuccessWeight / 100,
                Rationale = "No tests found"
            };
        }

        // Score based on pass rate
        var passRate = testResult.TotalTests > 0
            ? (double)testResult.PassedTests / testResult.TotalTests
            : 0.0;
        var score = (int)(passRate * 100);

        return new ScoreComponent
        {
            Name = "Test Results",
            Score = score,
            Weight = TestSuccessWeight,
            WeightedScore = score * TestSuccessWeight / 100,
            Rationale = $"{testResult.PassedTests}/{testResult.TotalTests} tests passed"
        };
    }

    private static ScoreComponent CalculateComplexityScore(AnalysisReport report)
    {
        // Lower complexity = higher score
        var complexityScore = report.MigrationComplexity switch
        {
            MigrationComplexity.Low => 100,
            MigrationComplexity.Medium => 70,
            MigrationComplexity.High => 40,
            MigrationComplexity.VeryHigh => 20,
            _ => 50
        };

        return new ScoreComponent
        {
            Name = "Migration Complexity",
            Score = complexityScore,
            Weight = MigrationComplexityWeight,
            WeightedScore = complexityScore * MigrationComplexityWeight / 100,
            Rationale = $"Complexity level: {report.MigrationComplexity}"
        };
    }

    private static ScoreComponent CalculateIssueScore(MigrationReport report)
    {
        var totalIssues = report.Issues.Count;
        var criticalIssues = report.Issues.Count(i => i.Severity == IssueSeverity.Error);

        // Deduct 20 points per critical issue, 5 points per warning
        var penalty = (criticalIssues * 20) + ((totalIssues - criticalIssues) * 5);
        var score = Math.Max(0, 100 - penalty);

        return new ScoreComponent
        {
            Name = "Migration Issues",
            Score = score,
            Weight = IssueCountWeight,
            WeightedScore = score * IssueCountWeight / 100,
            Rationale = $"{criticalIssues} critical issues, {totalIssues} total"
        };
    }

    private static ScoreComponent CalculateCompatibilityScore(AnalysisReport report)
    {
        var totalPackages = report.PackageDependencies.Count;
        if (totalPackages == 0)
        {
            return new ScoreComponent
            {
                Name = "API Compatibility",
                Score = 100,
                Weight = ApiCompatibilityWeight,
                WeightedScore = ApiCompatibilityWeight,
                Rationale = "No external packages"
            };
        }

        var compatiblePackages = report.PackageDependencies
            .Count(p => p.IsCompatible ?? true);
        var compatibilityRate = (double)compatiblePackages / totalPackages;
        var score = (int)(compatibilityRate * 100);

        return new ScoreComponent
        {
            Name = "API Compatibility",
            Score = score,
            Weight = ApiCompatibilityWeight,
            WeightedScore = score * ApiCompatibilityWeight / 100,
            Rationale = $"{compatiblePackages}/{totalPackages} packages compatible"
        };
    }

    private static List<string> GenerateRecommendations(
        Dictionary<string, ScoreComponent> components,
        MigrationValidationContext context)
    {
        var recommendations = new List<string>();

        // Build recommendations
        if (components["Build"].Score < 100)
        {
            if (context.BuildResult?.Errors.Any() == true)
            {
                recommendations.Add(
                    "Fix compilation errors before deploying to production");
            }
            if (context.BuildResult?.Warnings.Count > 10)
            {
                recommendations.Add(
                    "Review and address build warnings to improve code quality");
            }
        }

        // Test recommendations
        if (components["Tests"].Score < 80)
        {
            recommendations.Add(
                "Investigate test failures to ensure functionality is preserved");
        }

        // Complexity recommendations
        if (components["Complexity"].Score < 50)
        {
            recommendations.Add(
                "Consider migrating in phases due to high complexity");
            recommendations.Add(
                "Allocate additional time for manual code review");
        }

        // Issue recommendations
        if (components["Issues"].Score < 70)
        {
            recommendations.Add(
                "Address critical migration issues before deployment");
        }

        // Compatibility recommendations
        if (components["Compatibility"].Score < 80)
        {
            recommendations.Add(
                "Review incompatible packages and find .NET 8 alternatives");
        }

        // Overall confidence recommendations
        var overallScore = components.Values.Sum(c => c.WeightedScore);
        if (overallScore < 50)
        {
            recommendations.Add(
                "Low confidence score - recommend manual review before production use");
        }
        else if (overallScore >= 80)
        {
            recommendations.Add(
                "High confidence - proceed with thorough testing and staged rollout");
        }

        return recommendations;
    }
}
```

### Usage in reporting:

```csharp
var validationContext = new MigrationValidationContext
{
    BuildResult = buildResult,
    TestResult = testResult,
    AnalysisReport = analysisReport,
    MigrationReport = migrationReport
};

var confidenceScore = _confidenceScorer.CalculateScore(validationContext);

AnsiConsole.MarkupLine($"\n[bold]Migration Confidence Score: {confidenceScore.OverallScore}/100[/]");
AnsiConsole.MarkupLine($"Confidence Level: [{GetLevelColor(confidenceScore.Level)}]{confidenceScore.Level}[/]");

var table = new Table();
table.AddColumn("Component");
table.AddColumn("Score");
table.AddColumn("Weight");
table.AddColumn("Weighted");
table.AddColumn("Rationale");

foreach (var (name, component) in confidenceScore.Components)
{
    table.AddRow(
        name,
        $"{component.Score}/100",
        $"{component.Weight}%",
        component.WeightedScore.ToString(),
        component.Rationale);
}

AnsiConsole.Write(table);

if (confidenceScore.Recommendations.Any())
{
    AnsiConsole.MarkupLine("\n[bold]Recommendations:[/]");
    foreach (var rec in confidenceScore.Recommendations)
    {
        AnsiConsole.MarkupLine($"  • {rec}");
    }
}
```

### Unit tests:

```csharp
public class ConfidenceScorerTests
{
    [Fact]
    public void CalculateScore_PerfectMigration_ReturnsHighConfidence()
    {
        var context = new MigrationValidationContext
        {
            BuildResult = new BuildResult { Success = true, Errors = [], Warnings = [] },
            TestResult = new TestResult { TotalTests = 100, PassedTests = 100 },
            AnalysisReport = new AnalysisReport { MigrationComplexity = MigrationComplexity.Low },
            MigrationReport = new MigrationReport { Issues = [] }
        };

        var scorer = new ConfidenceScorer();
        var score = scorer.CalculateScore(context);

        Assert.Equal(ConfidenceLevel.High, score.Level);
        Assert.True(score.OverallScore >= 80);
    }

    [Fact]
    public void CalculateScore_BuildFailures_ReducesScore()
    {
        var contextWithErrors = CreateContext(buildErrors: 5);
        var contextWithoutErrors = CreateContext(buildErrors: 0);

        var scorer = new ConfidenceScorer();

        Assert.True(
            scorer.CalculateScore(contextWithErrors).OverallScore <
            scorer.CalculateScore(contextWithoutErrors).OverallScore);
    }

    [Fact]
    public void CalculateScore_TestFailures_GeneratesRecommendation()
    {
        var context = CreateContext(testPassRate: 0.5);
        var scorer = new ConfidenceScorer();
        var score = scorer.CalculateScore(context);

        Assert.Contains(score.Recommendations,
            r => r.Contains("test failures", StringComparison.OrdinalIgnoreCase));
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
