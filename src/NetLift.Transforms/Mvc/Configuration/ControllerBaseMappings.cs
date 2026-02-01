namespace NetLift.Transforms.Mvc.Configuration;

/// <summary>
/// Provides base class mappings for MVC and API controllers from .NET Framework to ASP.NET Core.
/// </summary>
public static class ControllerBaseMappings
{
    /// <summary>
    /// Gets the base class mapping dictionary from legacy controller base classes to ASP.NET Core.
    /// </summary>
    public static IReadOnlyDictionary<string, ControllerBaseMapping> Mappings { get; } = new Dictionary<string, ControllerBaseMapping>(StringComparer.Ordinal)
    {
        ["Controller"] = new ControllerBaseMapping(
            NewBaseName: "Controller",
            RequiresApiControllerAttribute: false,
            RequiresRouteAttribute: false,
            ConfidenceScore: 100),

        ["ApiController"] = new ControllerBaseMapping(
            NewBaseName: "ControllerBase",
            RequiresApiControllerAttribute: true,
            RequiresRouteAttribute: true,
            ConfidenceScore: 100)
    };

    /// <summary>
    /// Checks if a base class name needs to be mapped.
    /// </summary>
    /// <param name="baseName">The base class name to check.</param>
    /// <returns>True if the base class has a mapping; otherwise, false.</returns>
    public static bool RequiresMapping(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return false;
        }

        return Mappings.ContainsKey(baseName);
    }

    /// <summary>
    /// Gets the mapping for a given base class name.
    /// </summary>
    /// <param name="baseName">The base class name to map.</param>
    /// <returns>The mapping if found; otherwise, null.</returns>
    public static ControllerBaseMapping? GetMapping(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        return Mappings.TryGetValue(baseName, out var mapping) ? mapping : null;
    }

    /// <summary>
    /// Determines if a base class name appears to be a custom controller base class.
    /// </summary>
    /// <param name="baseName">The base class name to check.</param>
    /// <returns>True if it appears to be a custom base controller; otherwise, false.</returns>
    public static bool IsCustomBaseController(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return false;
        }

        // Common patterns for custom base controllers
        return baseName.EndsWith("Controller", StringComparison.Ordinal) &&
               !Mappings.ContainsKey(baseName);
    }

    /// <summary>
    /// Gets the confidence score for a custom base controller mapping.
    /// </summary>
    /// <returns>Confidence score for custom base controller (90%).</returns>
    public static int GetCustomBaseControllerConfidence()
    {
        return 90; // High confidence, but not 100% since we're keeping the name as-is
    }
}

/// <summary>
/// Represents a mapping configuration for a controller base class.
/// </summary>
/// <param name="NewBaseName">The new base class name for ASP.NET Core.</param>
/// <param name="RequiresApiControllerAttribute">Whether [ApiController] attribute is needed.</param>
/// <param name="RequiresRouteAttribute">Whether [Route] attribute is needed.</param>
/// <param name="ConfidenceScore">Confidence score for this mapping (0-100).</param>
public sealed record ControllerBaseMapping(
    string NewBaseName,
    bool RequiresApiControllerAttribute,
    bool RequiresRouteAttribute,
    int ConfidenceScore);
