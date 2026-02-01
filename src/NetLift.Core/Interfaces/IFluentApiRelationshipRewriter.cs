namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites Entity Framework 6 Fluent API relationship methods to EF Core equivalents using Roslyn.
/// Transforms HasRequired/HasOptional to HasOne with appropriate IsRequired() calls.
/// </summary>
public interface IFluentApiRelationshipRewriter
{
    /// <summary>
    /// Rewrites source code text to update EF6 relationship configuration to EF Core.
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
    /// 95 = Simple HasRequired/HasOptional, 85 = Complex chains, 75 = WithOptionalPrincipal/Dependent.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}
