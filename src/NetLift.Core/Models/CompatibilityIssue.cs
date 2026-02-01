namespace NetLift.Core.Models;

/// <summary>
/// Represents a compatibility issue found during migration analysis.
/// </summary>
public class CompatibilityIssue
{
    /// <summary>
    /// Gets or sets the severity of the issue.
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the category of the issue (e.g., "NuGet", "API", "Pattern").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the issue.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the affected project name.
    /// </summary>
    public string AffectedProject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the affected file path (if applicable).
    /// </summary>
    public string? AffectedFile { get; set; }

    /// <summary>
    /// Gets or sets the line number where the issue occurs (if applicable).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the recommendation for resolving the issue.
    /// </summary>
    public string? Recommendation { get; set; }

    /// <summary>
    /// Gets or sets the URL to documentation about the issue.
    /// </summary>
    public string? DocumentationUrl { get; set; }
}
