namespace NetLift.Transforms.Mvc.Configuration;

/// <summary>
/// Provides mappings for HttpContext.Current property migrations from System.Web to ASP.NET Core.
/// </summary>
public static class HttpContextMappings
{
    /// <summary>
    /// Gets the property mappings from HttpContext.Current.X to ASP.NET Core equivalents.
    /// For controller classes, uses base class properties (User, Request, Response).
    /// For non-controller classes, uses IHttpContextAccessor.
    /// </summary>
    public static IReadOnlyDictionary<string, HttpContextMapping> Mappings { get; } = new Dictionary<string, HttpContextMapping>(StringComparer.Ordinal)
    {
        // Controller base properties (available directly in Controller/ControllerBase)
        ["User"] = new HttpContextMapping(
            ControllerProperty: "User",
            AccessorProperty: "_httpContextAccessor.HttpContext?.User",
            RequiresNullConditional: true,
            ConfidenceScore: 100),

        ["Request"] = new HttpContextMapping(
            ControllerProperty: "Request",
            AccessorProperty: "_httpContextAccessor.HttpContext?.Request",
            RequiresNullConditional: true,
            ConfidenceScore: 100),

        ["Response"] = new HttpContextMapping(
            ControllerProperty: "Response",
            AccessorProperty: "_httpContextAccessor.HttpContext?.Response",
            RequiresNullConditional: true,
            ConfidenceScore: 100),

        // Requires IHttpContextAccessor injection even in controllers
        ["Session"] = new HttpContextMapping(
            ControllerProperty: "_httpContextAccessor.HttpContext?.Session",
            AccessorProperty: "_httpContextAccessor.HttpContext?.Session",
            RequiresNullConditional: true,
            ConfidenceScore: 60), // Lower confidence due to Session type changes

        ["Items"] = new HttpContextMapping(
            ControllerProperty: "_httpContextAccessor.HttpContext?.Items",
            AccessorProperty: "_httpContextAccessor.HttpContext?.Items",
            RequiresNullConditional: true,
            ConfidenceScore: 95),

        // Direct HttpContext access
        [""] = new HttpContextMapping(
            ControllerProperty: "HttpContext",
            AccessorProperty: "_httpContextAccessor.HttpContext",
            RequiresNullConditional: false, // HttpContext property in controllers is not nullable
            ConfidenceScore: 100)
    };

    /// <summary>
    /// Gets the set of properties available directly on Controller/ControllerBase.
    /// These don't require IHttpContextAccessor injection when used in controllers.
    /// </summary>
    public static IReadOnlySet<string> ControllerBaseProperties { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "User",
        "Request",
        "Response",
        "HttpContext",
        "RouteData",
        "Url"
    };

    /// <summary>
    /// Gets the mapping for a given HttpContext.Current property.
    /// </summary>
    /// <param name="propertyName">The property name (e.g., "User", "Request", "Session").</param>
    /// <returns>The mapping if found; otherwise, null.</returns>
    public static HttpContextMapping? GetMapping(string propertyName)
    {
        if (propertyName == null)
        {
            return null;
        }

        return Mappings.TryGetValue(propertyName, out var mapping) ? mapping : null;
    }

    /// <summary>
    /// Checks if a property is available directly on Controller base classes.
    /// </summary>
    /// <param name="propertyName">The property name to check.</param>
    /// <returns>True if the property is available on Controller; otherwise, false.</returns>
    public static bool IsControllerBaseProperty(string propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName) &&
               ControllerBaseProperties.Contains(propertyName);
    }
}

/// <summary>
/// Represents a mapping configuration for an HttpContext.Current property.
/// </summary>
/// <param name="ControllerProperty">The replacement when used in a controller class.</param>
/// <param name="AccessorProperty">The replacement when IHttpContextAccessor is needed.</param>
/// <param name="RequiresNullConditional">Whether to add null-conditional operators.</param>
/// <param name="ConfidenceScore">Confidence score for this mapping (0-100).</param>
public sealed record HttpContextMapping(
    string ControllerProperty,
    string AccessorProperty,
    bool RequiresNullConditional,
    int ConfidenceScore);
