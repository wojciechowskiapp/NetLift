namespace NetLift.Core.Models;

/// <summary>
/// Represents a mapping rule for converting a legacy NuGet package to a modern equivalent.
/// </summary>
public record PackageMappingRule
{
    /// <summary>
    /// Gets the old package identifier.
    /// </summary>
    public required string OldPackageId { get; init; }

    /// <summary>
    /// Gets the action to take for this package.
    /// </summary>
    public required MappingAction Action { get; init; }

    /// <summary>
    /// Gets the new package identifier (for Replace and Upgrade actions).
    /// </summary>
    public string? NewPackageId { get; init; }

    /// <summary>
    /// Gets the minimum version of the old package this rule applies to.
    /// </summary>
    public string? MinVersion { get; init; }

    /// <summary>
    /// Gets the version mapping for different target frameworks.
    /// Key: target framework (e.g., "net8.0", "net48"), Value: recommended version.
    /// </summary>
    public IReadOnlyDictionary<string, string>? VersionMapping { get; init; }

    /// <summary>
    /// Gets the reason for this mapping.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets a value indicating whether this mapping requires code changes.
    /// </summary>
    public bool RequiresCodeChanges { get; init; }

    /// <summary>
    /// Gets the URL to a migration guide for this package.
    /// </summary>
    public string? MigrationGuide { get; init; }

    /// <summary>
    /// Gets additional notes about the migration.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Gets the suggestion for manual migration scenarios.
    /// </summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Gets a list of related packages to consider.
    /// </summary>
    public IReadOnlyList<string>? RelatedPackages { get; init; }

    /// <summary>
    /// Gets framework-specific compatibility rules.
    /// </summary>
    public IReadOnlyDictionary<string, FrameworkMappingAction>? FrameworkCompatibility { get; init; }

    /// <summary>
    /// Gets a list of additional packages suggested when keeping this package.
    /// </summary>
    public IReadOnlyList<string>? SuggestedAdditional { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a security-related update.
    /// </summary>
    public bool SecurityUpdate { get; init; }

    /// <summary>
    /// Gets a value indicating whether breaking changes are expected.
    /// </summary>
    public bool BreakingChanges { get; init; }

    /// <summary>
    /// Gets a list of target frameworks this rule applies to.
    /// </summary>
    public IReadOnlyList<string>? AppliesTo { get; init; }
}

/// <summary>
/// Represents a framework-specific mapping action.
/// </summary>
public record FrameworkMappingAction
{
    /// <summary>
    /// Gets the action to take for this framework.
    /// </summary>
    public required MappingAction Action { get; init; }

    /// <summary>
    /// Gets the recommended version for this framework.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the new package ID override for this framework.
    /// </summary>
    public string? NewPackageId { get; init; }
}
