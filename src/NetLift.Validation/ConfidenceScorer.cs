namespace NetLift.Validation;

using NetLift.Core.Interfaces;
using NetLift.Core.Models;

/// <summary>
/// Implementation of <see cref="IConfidenceScorer"/> for calculating migration confidence scores.
/// </summary>
public sealed class ConfidenceScorer : IConfidenceScorer
{
    private const int BuildWeight = 30;
    private const int TestWeight = 25;
    private const int TransformationWeight = 20;
    private const int WarningWeight = 15;
    private const int IssueWeight = 10;

    /// <inheritdoc/>
    public ConfidenceScore CalculateScore(MigrationValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var components = new Dictionary<string, ScoreComponent>();
        var recommendations = new List<string>();

        // Calculate build score
        var buildScore = CalculateBuildScore(context.BuildResult, out var buildRationale);
        components["Build"] = new ScoreComponent
        {
            Name = "Build",
            Score = buildScore,
            Weight = BuildWeight,
            WeightedScore = buildScore * BuildWeight / 100,
            Rationale = buildRationale
        };

        if (buildScore < 80)
        {
            recommendations.Add("Fix build errors and warnings to improve migration confidence");
        }

        // Calculate test score
        var testScore = CalculateTestScore(context.TestResult, out var testRationale);
        components["Tests"] = new ScoreComponent
        {
            Name = "Tests",
            Score = testScore,
            Weight = TestWeight,
            WeightedScore = testScore * TestWeight / 100,
            Rationale = testRationale
        };

        if (testScore < 80)
        {
            recommendations.Add("Fix failing tests or add test coverage to validate the migration");
        }

        // Calculate transformation score
        var transformationScore = CalculateTransformationScore(context.TodosGenerated, context.TransformationsApplied, out var transformationRationale);
        components["Transformations"] = new ScoreComponent
        {
            Name = "Transformations",
            Score = transformationScore,
            Weight = TransformationWeight,
            WeightedScore = transformationScore * TransformationWeight / 100,
            Rationale = transformationRationale
        };

        if (transformationScore < 80 && context.TodosGenerated > 0)
        {
            recommendations.Add($"Review and address {context.TodosGenerated} TODO comments generated during migration");
        }

        // Calculate warning score
        var warningScore = CalculateWarningScore(context.WarningsGenerated, out var warningRationale);
        components["Warnings"] = new ScoreComponent
        {
            Name = "Warnings",
            Score = warningScore,
            Weight = WarningWeight,
            WeightedScore = warningScore * WarningWeight / 100,
            Rationale = warningRationale
        };

        if (warningScore < 80 && context.WarningsGenerated > 0)
        {
            recommendations.Add($"Review {context.WarningsGenerated} migration warnings and address critical issues");
        }

        // Calculate issue score (based on TODOs as a proxy for migration issues)
        var issueScore = CalculateIssueScore(context.TodosGenerated, out var issueRationale);
        components["Issues"] = new ScoreComponent
        {
            Name = "Issues",
            Score = issueScore,
            Weight = IssueWeight,
            WeightedScore = issueScore * IssueWeight / 100,
            Rationale = issueRationale
        };

        // Calculate overall score
        var overallScore = components.Values.Sum(c => c.WeightedScore);
        var level = DetermineConfidenceLevel(overallScore);

        // Add level-specific recommendations
        if (level == ConfidenceLevel.Low)
        {
            recommendations.Add("Migration confidence is low - perform thorough manual review before deployment");
        }
        else if (level == ConfidenceLevel.Medium)
        {
            recommendations.Add("Migration confidence is medium - comprehensive testing recommended before deployment");
        }

        return new ConfidenceScore
        {
            OverallScore = overallScore,
            Level = level,
            Components = components,
            Recommendations = recommendations
        };
    }

    private static int CalculateBuildScore(BuildResult? buildResult, out string rationale)
    {
        if (buildResult == null)
        {
            rationale = "No build validation performed";
            return 0;
        }

        if (!buildResult.Success)
        {
            rationale = $"Build failed with {buildResult.Errors.Count} error(s)";
            return 0;
        }

        // Start with perfect score
        var score = 100;

        // Deduct 10 points per error (shouldn't happen if Success=true, but defensive)
        score -= buildResult.Errors.Count * 10;

        // Deduct 2 points per warning
        score -= buildResult.Warnings.Count * 2;

        // Ensure score doesn't go below 0
        score = Math.Max(0, score);

        rationale = buildResult.Warnings.Count > 0
            ? $"Build succeeded with {buildResult.Warnings.Count} warning(s)"
            : "Build succeeded with no warnings";

        return score;
    }

    private static int CalculateTestScore(TestResult? testResult, out string rationale)
    {
        if (testResult == null)
        {
            rationale = "No tests executed";
            return 50; // Neutral score if no tests
        }

        if (testResult.TotalTests == 0)
        {
            rationale = "No tests found in solution";
            return 50; // Neutral score if no tests exist
        }

        // Calculate pass rate percentage
        var passRate = testResult.PassedTests * 100 / testResult.TotalTests;

        rationale = testResult.FailedTests > 0
            ? $"{testResult.PassedTests}/{testResult.TotalTests} tests passed ({passRate}%)"
            : $"All {testResult.TotalTests} tests passed";

        return passRate;
    }

    private static int CalculateTransformationScore(int todosGenerated, int transformationsApplied, out string rationale)
    {
        if (transformationsApplied == 0)
        {
            rationale = "No transformations applied";
            return 100;
        }

        // Calculate the ratio of TODOs to transformations
        // Lower ratio = better (fewer manual interventions needed)
        var todoRatio = (double)todosGenerated / transformationsApplied;

        int score;
        if (todoRatio == 0)
        {
            score = 100;
            rationale = $"All {transformationsApplied} transformations completed successfully";
        }
        else if (todoRatio < 0.1) // Less than 10% TODOs
        {
            score = 90;
            rationale = $"{todosGenerated} TODOs generated from {transformationsApplied} transformations (excellent)";
        }
        else if (todoRatio < 0.25) // Less than 25% TODOs
        {
            score = 75;
            rationale = $"{todosGenerated} TODOs generated from {transformationsApplied} transformations (good)";
        }
        else if (todoRatio < 0.5) // Less than 50% TODOs
        {
            score = 60;
            rationale = $"{todosGenerated} TODOs generated from {transformationsApplied} transformations (fair)";
        }
        else
        {
            score = 40;
            rationale = $"{todosGenerated} TODOs generated from {transformationsApplied} transformations (needs review)";
        }

        return score;
    }

    private static int CalculateWarningScore(int warningsGenerated, out string rationale)
    {
        // Start with perfect score and deduct 5 points per warning
        var score = Math.Max(0, 100 - (warningsGenerated * 5));

        rationale = warningsGenerated == 0
            ? "No migration warnings generated"
            : $"{warningsGenerated} migration warning(s) generated";

        return score;
    }

    private static int CalculateIssueScore(int todosGenerated, out string rationale)
    {
        // Use TODOs as a proxy for migration issues
        // Deduct 5 points per TODO, min 0
        var score = Math.Max(0, 100 - (todosGenerated * 5));

        rationale = todosGenerated == 0
            ? "No manual intervention required"
            : $"{todosGenerated} item(s) require manual review";

        return score;
    }

    private static ConfidenceLevel DetermineConfidenceLevel(int overallScore)
    {
        return overallScore switch
        {
            >= 80 => ConfidenceLevel.High,
            >= 50 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };
    }
}
