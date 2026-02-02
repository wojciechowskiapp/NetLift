namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents the result of analyzing a project for modernization opportunities.
/// </summary>
public sealed record ModernizationAnalysis
{
    /// <summary>
    /// Gets the list of controllers analyzed.
    /// </summary>
    public IReadOnlyList<ControllerInfo> Controllers { get; init; } = [];

    /// <summary>
    /// Gets the list of recommendations for modernization.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets the estimated confidence score for the overall modernization (0-100).
    /// </summary>
    public int EstimatedConfidence { get; init; }

    /// <summary>
    /// Gets the count of different patterns detected.
    /// Key: Pattern type (e.g., "Commands", "Queries", "Validators")
    /// Value: Count of instances
    /// </summary>
    public IReadOnlyDictionary<string, int> PatternCounts { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the potential commands that could be generated.
    /// </summary>
    public IReadOnlyList<CommandInfo> PotentialCommands { get; init; } = [];

    /// <summary>
    /// Gets the potential queries that could be generated.
    /// </summary>
    public IReadOnlyList<QueryInfo> PotentialQueries { get; init; } = [];

    /// <summary>
    /// Gets the potential validators that could be generated.
    /// </summary>
    public IReadOnlyList<ValidatorInfo> PotentialValidators { get; init; } = [];

    /// <summary>
    /// Gets the analysis duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the diagnostics generated during analysis.
    /// </summary>
    public IReadOnlyList<ModernizationDiagnostic> Diagnostics { get; init; } = [];
}
