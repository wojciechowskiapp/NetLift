namespace NetLift.Core.Models;

/// <summary>
/// Represents the result of a package mapping operation.
/// </summary>
public record PackageMappingResult
{
    /// <summary>
    /// Gets the original package identifier.
    /// </summary>
    public required string OriginalPackageId { get; init; }

    /// <summary>
    /// Gets the original package version.
    /// </summary>
    public required string OriginalVersion { get; init; }

    /// <summary>
    /// Gets the action taken.
    /// </summary>
    public required MappingAction Action { get; init; }

    /// <summary>
    /// Gets the new package identifier (if replaced).
    /// </summary>
    public string? NewPackageId { get; init; }

    /// <summary>
    /// Gets the recommended version for the new package.
    /// </summary>
    public string? RecommendedVersion { get; init; }

    /// <summary>
    /// Gets the reason for the mapping.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets a value indicating whether code changes are required.
    /// </summary>
    public bool RequiresCodeChanges { get; init; }

    /// <summary>
    /// Gets the migration guide URL.
    /// </summary>
    public string? MigrationGuide { get; init; }

    /// <summary>
    /// Gets additional notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Gets suggested additional packages.
    /// </summary>
    public IReadOnlyList<string>? SuggestedAdditional { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a security update.
    /// </summary>
    public bool SecurityUpdate { get; init; }

    /// <summary>
    /// Gets warning messages for the user.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }

    /// <summary>
    /// Creates a result indicating no mapping was found.
    /// </summary>
    public static PackageMappingResult NoMapping(string packageId, string version)
    {
        return new PackageMappingResult
        {
            OriginalPackageId = packageId,
            OriginalVersion = version,
            Action = MappingAction.Keep,
            Reason = "No mapping rule found - keeping original package"
        };
    }
}
