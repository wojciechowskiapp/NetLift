namespace NetLift.Transforms.Mvc.Configuration;

/// <summary>
/// Provides namespace mappings from System.Web.Mvc to ASP.NET Core equivalents.
/// </summary>
public static class MvcNamespaceMappings
{
    /// <summary>
    /// Gets the namespace mapping dictionary from legacy System.Web.Mvc namespaces to ASP.NET Core.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Mappings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ASP.NET MVC → ASP.NET Core MVC
        ["System.Web.Mvc"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Ajax"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Async"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Html"] = "Microsoft.AspNetCore.Mvc.Rendering",
        ["System.Web.Routing"] = "Microsoft.AspNetCore.Routing",
        ["System.Web.Mvc.Filters"] = "Microsoft.AspNetCore.Mvc.Filters",
        ["System.Web.Mvc.ModelBinding"] = "Microsoft.AspNetCore.Mvc.ModelBinding",
        ["System.Web.WebPages"] = "Microsoft.AspNetCore.Mvc.Razor",
        ["System.Web.Helpers"] = "Microsoft.AspNetCore.Mvc.Rendering",
        ["System.Web.Optimization"] = "WebOptimizer",
        // Entity Framework 6 → Entity Framework Core
        ["System.Data.Entity"] = "Microsoft.EntityFrameworkCore",
        ["System.Data.Entity.Infrastructure"] = "Microsoft.EntityFrameworkCore.Infrastructure",
        ["System.Data.Entity.ModelConfiguration"] = "Microsoft.EntityFrameworkCore",
        ["System.Data.Entity.ModelConfiguration.Conventions"] = "Microsoft.EntityFrameworkCore",
        ["System.Data.Entity.Migrations"] = "Microsoft.EntityFrameworkCore.Migrations",
        ["System.Data.Entity.Validation"] = "Microsoft.EntityFrameworkCore",
        // System.Data for SqlClient
        ["System.Data.SqlClient"] = "Microsoft.Data.SqlClient",
        // PagedList → X.PagedList (ASP.NET Core compatible)
        ["PagedList"] = "X.PagedList",
        ["PagedList.Mvc"] = "X.PagedList.Mvc.Core"
    };

    /// <summary>
    /// Tries to get the mapped namespace for a given legacy namespace.
    /// </summary>
    /// <param name="legacyNamespace">The legacy namespace to map.</param>
    /// <param name="mappedNamespace">The mapped ASP.NET Core namespace if found.</param>
    /// <returns>True if a mapping exists; otherwise, false.</returns>
    public static bool TryGetMapping(string legacyNamespace, out string? mappedNamespace)
    {
        if (string.IsNullOrWhiteSpace(legacyNamespace))
        {
            mappedNamespace = null;
            return false;
        }

        return Mappings.TryGetValue(legacyNamespace, out mappedNamespace);
    }

    /// <summary>
    /// Checks if a namespace should be mapped to ASP.NET Core.
    /// </summary>
    /// <param name="namespaceName">The namespace to check.</param>
    /// <returns>True if the namespace has a mapping; otherwise, false.</returns>
    public static bool RequiresMapping(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return false;
        }

        // Check exact match first
        if (Mappings.ContainsKey(namespaceName))
        {
            return true;
        }

        // Check if it's a child namespace of any mapped namespace
        return Mappings.Keys.Any(key => namespaceName.StartsWith(key + ".", StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the best matching namespace for a given legacy namespace, including child namespaces.
    /// </summary>
    /// <param name="legacyNamespace">The legacy namespace to map.</param>
    /// <returns>The mapped namespace if found; otherwise, null.</returns>
    public static string? GetMapping(string legacyNamespace)
    {
        if (string.IsNullOrWhiteSpace(legacyNamespace))
        {
            return null;
        }

        // Try exact match first
        if (Mappings.TryGetValue(legacyNamespace, out var directMapping))
        {
            return directMapping;
        }

        // Find the longest matching prefix
        var longestMatch = Mappings.Keys
            .Where(key => legacyNamespace.StartsWith(key + ".", StringComparison.Ordinal))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();

        if (longestMatch != null)
        {
            var mappedBase = Mappings[longestMatch];
            var childPart = legacyNamespace.Substring(longestMatch.Length);
            return mappedBase + childPart;
        }

        return null;
    }

    /// <summary>
    /// Calculates confidence score for a namespace mapping.
    /// </summary>
    /// <param name="legacyNamespace">The legacy namespace that was mapped.</param>
    /// <param name="mappedNamespace">The resulting mapped namespace.</param>
    /// <returns>Confidence score: 100 for direct mapping, 95 for child namespace, 70 for uncertain.</returns>
    public static int CalculateConfidenceScore(string legacyNamespace, string? mappedNamespace)
    {
        if (mappedNamespace == null)
        {
            return 70; // Uncertain - no mapping found
        }

        // Direct 1:1 mapping
        if (Mappings.TryGetValue(legacyNamespace, out var directMapping) && directMapping == mappedNamespace)
        {
            return 100; // Perfect confidence for direct mapping
        }

        // Child namespace mapping
        if (legacyNamespace.Contains('.') && mappedNamespace.Contains('.'))
        {
            return 95; // High confidence for child namespace mapping
        }

        return 70; // Uncertain
    }

    /// <summary>
    /// Calculates confidence score considering multiple mappings to the same target.
    /// </summary>
    /// <param name="mappedNamespaces">Collection of all mapped namespaces in the current context.</param>
    /// <returns>Confidence score: 100 if all unique, 90 if merged namespaces detected.</returns>
    public static int CalculateOverallConfidence(IEnumerable<string> mappedNamespaces)
    {
        var distinctCount = mappedNamespaces.Distinct().Count();
        var totalCount = mappedNamespaces.Count();

        // If we have multiple legacy namespaces mapping to fewer targets, it's a merge scenario
        return distinctCount < totalCount ? 90 : 100;
    }
}
