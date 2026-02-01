namespace NetLift.Core.Models.Mvc;

/// <summary>
/// Represents a bundle definition parsed from BundleConfig.cs.
/// </summary>
public sealed record BundleDefinition
{
    /// <summary>
    /// Gets the virtual path of the bundle (e.g., "~/bundles/jquery").
    /// </summary>
    public string VirtualPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the bundle type (Script or Style).
    /// </summary>
    public BundleType Type { get; init; }

    /// <summary>
    /// Gets the list of included file patterns (e.g., "~/Scripts/jquery-*.js").
    /// </summary>
    public List<string> IncludedFiles { get; init; } = new();

    /// <summary>
    /// Gets the list of included directory patterns.
    /// </summary>
    public List<string> IncludedDirectories { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether this bundle should be minified.
    /// </summary>
    public bool IsMinified { get; init; }

    /// <summary>
    /// Gets the optional CDN path for the bundle.
    /// </summary>
    public string? CdnPath { get; init; }

    /// <summary>
    /// Gets the optional CDN fallback expression.
    /// </summary>
    public string? CdnFallbackExpression { get; init; }
}
