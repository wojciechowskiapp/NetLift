namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms action filters from ASP.NET MVC to ASP.NET Core using Roslyn.
/// Handles filter base classes, attribute transformations, and policy generation.
/// </summary>
public interface IActionFilterTransformer
{
    /// <summary>
    /// Rewrites source code text to transform action filter implementations and attributes.
    /// </summary>
    /// <param name="sourceCode">The C# source code to rewrite.</param>
    /// <returns>The rewritten source code.</returns>
    string Rewrite(string sourceCode);

    /// <summary>
    /// Gets the collection of new using directives that were added during rewriting.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the rewrite operation (0-100).
    /// 100 = simple transformations, 90 = role to policy conversion, 70 = complex filters.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the collection of authorization policies generated during transformation.
    /// </summary>
    IReadOnlyCollection<PolicyDefinition> GeneratedPolicies { get; }
}

/// <summary>
/// Represents an authorization policy definition generated from role-based authorization.
/// </summary>
/// <param name="Name">The policy name.</param>
/// <param name="Roles">The roles required for this policy.</param>
public sealed record PolicyDefinition(string Name, string[] Roles);
