namespace NetLift.Core.Models.SignalR;

/// <summary>
/// Information about GlobalHost usage that needs to be transformed to IHubContext.
/// </summary>
public record GlobalHostUsageInfo
{
    /// <summary>
    /// The file path containing the GlobalHost usage.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The class name where GlobalHost is used.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Individual GlobalHost usages found.
    /// </summary>
    public IReadOnlyList<GlobalHostUsage> Usages { get; init; } = [];

    /// <summary>
    /// Hub types referenced via GlobalHost.
    /// </summary>
    public IReadOnlyList<string> ReferencedHubTypes { get; init; } = [];

    /// <summary>
    /// Confidence score for transformation (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// A single GlobalHost usage instance.
/// </summary>
public record GlobalHostUsage
{
    /// <summary>
    /// The usage pattern (e.g., "GlobalHost.ConnectionManager.GetHubContext&lt;T&gt;").
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// The Hub type being accessed.
    /// </summary>
    public required string HubType { get; init; }

    /// <summary>
    /// The line number of the usage.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// Suggested transformation (IHubContext injection).
    /// </summary>
    public string? SuggestedTransformation { get; init; }
}
