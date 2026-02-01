using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses RouteConfig.cs files to extract route definitions from MapRoute() calls.
/// </summary>
public interface IRouteConfigParser
{
    /// <summary>
    /// Parses C# source code to extract route definitions.
    /// </summary>
    /// <param name="sourceCode">The RouteConfig.cs source code.</param>
    /// <returns>A list of parsed route definitions.</returns>
    IReadOnlyList<RouteDefinition> Parse(string sourceCode);
}
