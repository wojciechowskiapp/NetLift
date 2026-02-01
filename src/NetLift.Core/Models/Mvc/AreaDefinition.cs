namespace NetLift.Core.Models.Mvc;

/// <summary>
/// Represents an MVC Area definition parsed from AreaRegistration classes.
/// </summary>
public sealed record AreaDefinition
{
    /// <summary>
    /// Gets the area name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the route prefix for the area (typically matches the area name in lowercase).
    /// </summary>
    public string RoutePrefix { get; init; } = string.Empty;

    /// <summary>
    /// Gets the routes defined for this area.
    /// </summary>
    public List<RouteDefinition> Routes { get; init; } = new();

    /// <summary>
    /// Gets the source file path where this area registration was found.
    /// </summary>
    public string SourceFilePath { get; init; } = string.Empty;
}
