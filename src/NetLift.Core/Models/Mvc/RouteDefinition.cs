namespace NetLift.Core.Models.Mvc;

/// <summary>
/// Represents a route definition parsed from RouteConfig.cs MapRoute() calls.
/// </summary>
public sealed record RouteDefinition
{
    /// <summary>
    /// Gets the route name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the URL template/pattern.
    /// </summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>
    /// Gets the default route values.
    /// Use <see cref="OptionalParameter"/> as the value to indicate UrlParameter.Optional.
    /// </summary>
    public Dictionary<string, object?> Defaults { get; init; } = new();

    /// <summary>
    /// Gets the route constraints.
    /// </summary>
    public Dictionary<string, string> Constraints { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether this is the default route (name == "Default").
    /// </summary>
    public bool IsDefaultRoute { get; init; }

    /// <summary>
    /// Sentinel value representing UrlParameter.Optional in the Defaults dictionary.
    /// </summary>
    public static readonly object OptionalParameter = new();
}
