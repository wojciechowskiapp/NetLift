namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Options for controlling the modernization process.
/// </summary>
public sealed class ModernizationOptions
{
    /// <summary>
    /// Gets a value indicating whether to only analyze without making changes.
    /// </summary>
    public bool AnalyzeOnly { get; init; }

    /// <summary>
    /// Gets a value indicating whether to perform a dry run without writing files.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether to run in interactive mode with user prompts.
    /// </summary>
    public bool Interactive { get; init; }

    /// <summary>
    /// Gets the minimum confidence threshold (0-100) for automatic transformations.
    /// Default is 80.
    /// </summary>
    public int ConfidenceThreshold { get; init; } = 80;

    /// <summary>
    /// Gets the set of modernization patterns to apply.
    /// </summary>
    public HashSet<ModernizationPattern> Patterns { get; init; } = new();

    /// <summary>
    /// Gets the optional output path for generated files.
    /// If null, files are generated in-place.
    /// </summary>
    public string? OutputPath { get; init; }
}

/// <summary>
/// Represents the available modernization patterns.
/// </summary>
public enum ModernizationPattern
{
    /// <summary>
    /// Command Query Responsibility Segregation pattern.
    /// </summary>
    Cqrs,

    /// <summary>
    /// Clean Architecture with layered separation.
    /// </summary>
    CleanArchitecture,

    /// <summary>
    /// FluentValidation for input validation.
    /// </summary>
    FluentValidation,

    /// <summary>
    /// Repository pattern for data access abstraction.
    /// </summary>
    Repository
}
