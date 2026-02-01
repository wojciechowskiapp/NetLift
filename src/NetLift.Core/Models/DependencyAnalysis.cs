namespace NetLift.Core.Models;

/// <summary>
/// Represents the analysis of a package dependency for migration compatibility.
/// </summary>
public class DependencyAnalysis
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current version of the package.
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compatibility status of the package.
    /// </summary>
    public PackageCompatibility Compatibility { get; set; }

    /// <summary>
    /// Gets or sets the recommended version for .NET migration (if applicable).
    /// </summary>
    public string? RecommendedVersion { get; set; }

    /// <summary>
    /// Gets or sets the replacement package (if the current package is incompatible).
    /// </summary>
    public string? ReplacementPackage { get; set; }

    /// <summary>
    /// Gets or sets additional notes about the dependency.
    /// </summary>
    public string? Notes { get; set; }
}
