namespace NetLift.Transforms.Mvc.Configuration;

/// <summary>
/// Provides mappings for action filter types from ASP.NET MVC to ASP.NET Core.
/// </summary>
public static class FilterMigrationMappings
{
    /// <summary>
    /// Gets the filter base class mapping dictionary from ASP.NET MVC to ASP.NET Core.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BaseClassMappings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ActionFilterAttribute"] = "IActionFilter",
        ["IActionFilter"] = "IActionFilter",
        ["IAuthorizationFilter"] = "IAuthorizationFilter",
        ["IExceptionFilter"] = "IExceptionFilter",
        ["IResultFilter"] = "IResultFilter",
        ["FilterAttribute"] = "IActionFilter"
    };

    /// <summary>
    /// Gets the filter method name mappings from ASP.NET MVC to ASP.NET Core.
    /// </summary>
    public static IReadOnlyDictionary<string, string> MethodMappings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Action filter methods
        ["OnActionExecuting"] = "OnActionExecuting",
        ["OnActionExecuted"] = "OnActionExecuted",

        // Authorization filter methods
        ["OnAuthorization"] = "OnAuthorization",

        // Exception filter methods
        ["OnException"] = "OnException",

        // Result filter methods
        ["OnResultExecuting"] = "OnResultExecuting",
        ["OnResultExecuted"] = "OnResultExecuted"
    };

    /// <summary>
    /// Gets the set of filter attributes that should be transformed to TypeFilter.
    /// </summary>
    public static IReadOnlySet<string> TypeFilterCandidates { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "HandleError",
        "HandleErrorAttribute"
    };

    /// <summary>
    /// Tries to get the mapped interface for a given legacy filter base class.
    /// </summary>
    /// <param name="legacyBaseClass">The legacy filter base class to map.</param>
    /// <param name="mappedInterface">The mapped ASP.NET Core interface if found.</param>
    /// <returns>True if a mapping exists; otherwise, false.</returns>
    public static bool TryGetBaseClassMapping(string legacyBaseClass, out string? mappedInterface)
    {
        if (string.IsNullOrWhiteSpace(legacyBaseClass))
        {
            mappedInterface = null;
            return false;
        }

        return BaseClassMappings.TryGetValue(legacyBaseClass, out mappedInterface);
    }

    /// <summary>
    /// Checks if a base class should be mapped to ASP.NET Core.
    /// </summary>
    /// <param name="baseClassName">The base class name to check.</param>
    /// <returns>True if the base class has a mapping; otherwise, false.</returns>
    public static bool RequiresBaseClassMapping(string baseClassName)
    {
        if (string.IsNullOrWhiteSpace(baseClassName))
        {
            return false;
        }

        return BaseClassMappings.ContainsKey(baseClassName);
    }

    /// <summary>
    /// Checks if an attribute should be transformed to TypeFilter.
    /// </summary>
    /// <param name="attributeName">The attribute name to check.</param>
    /// <returns>True if the attribute should be transformed; otherwise, false.</returns>
    public static bool IsTypeFilterCandidate(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        // Remove "Attribute" suffix if present
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            ? attributeName.Substring(0, attributeName.Length - 9)
            : attributeName;

        return TypeFilterCandidates.Contains(normalizedName) || TypeFilterCandidates.Contains(attributeName);
    }
}
