namespace NetLift.Core.Models;

/// <summary>
/// Represents a NuGet package reference from packages.config.
/// </summary>
public class PackageReference
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package name (alias for Id for backward compatibility).
    /// </summary>
    public string Name
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target framework moniker (e.g., "net48").
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a development-only dependency.
    /// </summary>
    public bool IsDevelopmentDependency { get; set; }

    /// <summary>
    /// Gets or sets the compatibility status with .NET Core/.NET for migration planning.
    /// </summary>
    public PackageCompatibility Compatibility { get; set; } = PackageCompatibility.Unknown;

    /// <summary>
    /// Gets or sets the replacement package ID for .NET Core/.NET, if applicable.
    /// </summary>
    public string? ReplacementPackageId { get; set; }

    /// <summary>
    /// Gets or sets the recommended version of the replacement package.
    /// </summary>
    public string? ReplacementVersion { get; set; }
}
