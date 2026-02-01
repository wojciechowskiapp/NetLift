using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms convention-based routing to attribute routing in ASP.NET Core.
/// Adds [Route] attributes to controllers and HTTP method attributes to actions.
/// </summary>
public interface IAttributeRoutingTransformer
{
    /// <summary>
    /// Rewrites source code to add attribute routing.
    /// </summary>
    /// <param name="sourceCode">The C# source code to rewrite.</param>
    /// <param name="routes">Optional route definitions from RouteConfig.cs for enhanced transformation.</param>
    /// <returns>The rewritten source code with attribute routing.</returns>
    string Rewrite(string sourceCode, IReadOnlyList<RouteDefinition>? routes = null);

    /// <summary>
    /// Gets the collection of new using directives that were added during rewriting.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the rewrite operation (0-100).
    /// 95-100 = Method name inference, 85-94 = Constraint conversion, &lt;85 = Complex patterns.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}
