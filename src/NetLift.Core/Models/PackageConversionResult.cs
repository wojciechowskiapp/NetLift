namespace NetLift.Core.Models;

/// <summary>
/// Represents the result of converting packages.config to PackageReference format.
/// </summary>
public class PackageConversionResult
{
    /// <summary>
    /// Gets or sets the list of packages to include in the new project file.
    /// </summary>
    public List<PackageReference> Packages { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of packages that were removed (now part of framework).
    /// </summary>
    public List<PackageReference> RemovedPackages { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of packages that require manual review.
    /// </summary>
    public List<PackageReference> ManualReviewRequired { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of package replacements that occurred.
    /// </summary>
    public List<PackageReplacement> Replacements { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of warnings generated during conversion.
    /// </summary>
    public List<ConversionWarning> Warnings { get; set; } = new();
}

/// <summary>
/// Represents a package replacement during conversion.
/// </summary>
public class PackageReplacement
{
    /// <summary>
    /// Gets or sets the original package from packages.config.
    /// </summary>
    public PackageReference OldPackage { get; set; } = null!;

    /// <summary>
    /// Gets or sets the new replacement package.
    /// </summary>
    public PackageReference NewPackage { get; set; } = null!;

    /// <summary>
    /// Gets or sets the reason for the replacement.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Represents a warning generated during package conversion.
/// </summary>
public class ConversionWarning
{
    /// <summary>
    /// Gets or sets the severity of the warning.
    /// </summary>
    public WarningSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package ID related to this warning.
    /// </summary>
    public string? PackageId { get; set; }
}

/// <summary>
/// Represents the severity of a conversion warning.
/// </summary>
public enum WarningSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that may require attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that requires manual intervention.
    /// </summary>
    Error
}
