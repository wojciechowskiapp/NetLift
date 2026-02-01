namespace NetLift.Core.Models;

/// <summary>
/// Context for calculating migration validation confidence score.
/// </summary>
public sealed record MigrationValidationContext
{
    /// <summary>
    /// Gets the build validation result.
    /// </summary>
    public BuildResult? BuildResult { get; init; }

    /// <summary>
    /// Gets the test execution result.
    /// </summary>
    public TestResult? TestResult { get; init; }

    /// <summary>
    /// Gets the number of transformations that were applied during migration.
    /// </summary>
    public int TransformationsApplied { get; init; }

    /// <summary>
    /// Gets the number of warnings generated during migration.
    /// </summary>
    public int WarningsGenerated { get; init; }

    /// <summary>
    /// Gets the number of TODO comments generated during migration.
    /// </summary>
    public int TodosGenerated { get; init; }
}

/// <summary>
/// Represents the overall confidence score for a migration.
/// </summary>
public sealed record ConfidenceScore
{
    /// <summary>
    /// Gets the overall confidence score (0-100).
    /// </summary>
    public int OverallScore { get; init; }

    /// <summary>
    /// Gets the confidence level based on the overall score.
    /// </summary>
    public ConfidenceLevel Level { get; init; }

    /// <summary>
    /// Gets the individual score components that contribute to the overall score.
    /// </summary>
    public IReadOnlyDictionary<string, ScoreComponent> Components { get; init; } = new Dictionary<string, ScoreComponent>();

    /// <summary>
    /// Gets the list of recommendations for improving the migration.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];
}

/// <summary>
/// Represents the confidence level of a migration.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// High confidence (80-100). Migration is likely production-ready.
    /// </summary>
    High,

    /// <summary>
    /// Medium confidence (50-79). Migration requires review and testing.
    /// </summary>
    Medium,

    /// <summary>
    /// Low confidence (0-49). Migration requires significant work.
    /// </summary>
    Low
}

/// <summary>
/// Represents an individual component of the confidence score.
/// </summary>
public sealed record ScoreComponent
{
    /// <summary>
    /// Gets the name of the score component.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw score for this component (0-100).
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Gets the weight of this component in the overall score.
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// Gets the weighted score contribution (Score * Weight / 100).
    /// </summary>
    public int WeightedScore { get; init; }

    /// <summary>
    /// Gets the rationale explaining how this score was calculated.
    /// </summary>
    public required string Rationale { get; init; }
}
