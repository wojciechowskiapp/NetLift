namespace NetLift.Transforms.Ef.Configuration;

/// <summary>
/// Provides mappings for Entity Framework 6 to EF Core Fluent API method names.
/// </summary>
public static class FluentApiMappings
{
    /// <summary>
    /// Method names that indicate the start of a relationship configuration that requires transformation.
    /// </summary>
    private static readonly HashSet<string> RelationshipStarters = new(StringComparer.Ordinal)
    {
        "HasRequired",
        "HasOptional"
    };

    /// <summary>
    /// Method names in the fluent chain that need to be replaced.
    /// </summary>
    private static readonly Dictionary<string, string> MethodMappings = new(StringComparer.Ordinal)
    {
        { "HasRequired", "HasOne" },
        { "HasOptional", "HasOne" },
        { "WithRequired", "WithOne" },
        { "WithOptional", "WithOne" },
        { "WithOptionalPrincipal", "WithOne" },
        { "WithOptionalDependent", "WithOne" }
    };

    /// <summary>
    /// Checks if a method name starts a relationship configuration that requires transformation.
    /// </summary>
    public static bool IsRelationshipStarter(string methodName)
    {
        return RelationshipStarters.Contains(methodName);
    }

    /// <summary>
    /// Checks if a method name requires mapping.
    /// </summary>
    public static bool RequiresMapping(string methodName)
    {
        return MethodMappings.ContainsKey(methodName);
    }

    /// <summary>
    /// Gets the EF Core equivalent method name for an EF6 method.
    /// </summary>
    public static string? GetMapping(string methodName)
    {
        return MethodMappings.TryGetValue(methodName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// Checks if the method name indicates a required relationship.
    /// </summary>
    public static bool IsRequiredMethod(string methodName)
    {
        return methodName is "HasRequired" or "WithRequired";
    }

    /// <summary>
    /// Checks if the method name indicates an optional relationship.
    /// </summary>
    public static bool IsOptionalMethod(string methodName)
    {
        return methodName is "HasOptional" or "WithOptional" or "WithOptionalPrincipal" or "WithOptionalDependent";
    }
}
