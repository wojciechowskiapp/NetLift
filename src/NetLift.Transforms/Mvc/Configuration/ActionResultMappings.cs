namespace NetLift.Transforms.Mvc.Configuration;

/// <summary>
/// Provides type mappings from ASP.NET MVC ActionResult types to ASP.NET Core equivalents.
/// </summary>
public static class ActionResultMappings
{
    /// <summary>
    /// Gets the ActionResult type mapping dictionary from ASP.NET MVC to ASP.NET Core.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Mappings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Base types
        ["ActionResult"] = "IActionResult",
        ["IHttpActionResult"] = "IActionResult",

        // Specific result types that remain the same
        ["ViewResult"] = "ViewResult",
        ["PartialViewResult"] = "PartialViewResult",
        ["JsonResult"] = "JsonResult",
        ["RedirectResult"] = "RedirectResult",
        ["ContentResult"] = "ContentResult",
        ["FileResult"] = "FileResult",
        ["EmptyResult"] = "EmptyResult",

        // Types that change
        ["RedirectToRouteResult"] = "RedirectToActionResult",
        ["HttpStatusCodeResult"] = "StatusCodeResult",
        ["HttpNotFoundResult"] = "NotFoundResult",
        ["HttpUnauthorizedResult"] = "UnauthorizedResult"
    };

    /// <summary>
    /// Tries to get the mapped type for a given legacy ActionResult type.
    /// </summary>
    /// <param name="legacyType">The legacy ActionResult type to map.</param>
    /// <param name="mappedType">The mapped ASP.NET Core type if found.</param>
    /// <returns>True if a mapping exists; otherwise, false.</returns>
    public static bool TryGetMapping(string legacyType, out string? mappedType)
    {
        if (string.IsNullOrWhiteSpace(legacyType))
        {
            mappedType = null;
            return false;
        }

        return Mappings.TryGetValue(legacyType, out mappedType);
    }

    /// <summary>
    /// Checks if a type should be mapped to ASP.NET Core.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type has a mapping; otherwise, false.</returns>
    public static bool RequiresMapping(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return Mappings.ContainsKey(typeName);
    }

    /// <summary>
    /// Gets the mapped type for a given legacy ActionResult type.
    /// </summary>
    /// <param name="legacyType">The legacy ActionResult type to map.</param>
    /// <returns>The mapped type if found; otherwise, null.</returns>
    public static string? GetMapping(string legacyType)
    {
        if (string.IsNullOrWhiteSpace(legacyType))
        {
            return null;
        }

        return Mappings.TryGetValue(legacyType, out var mapped) ? mapped : null;
    }
}
