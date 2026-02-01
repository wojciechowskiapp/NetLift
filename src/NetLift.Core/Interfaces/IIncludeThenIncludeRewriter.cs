namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites Entity Framework 6 Include chaining with Select to EF Core ThenInclude pattern using Roslyn.
/// Transforms nested Include(x => x.Items.Select(i => i.Product)) to Include(x => x.Items).ThenInclude(i => i.Product).
/// </summary>
public interface IIncludeThenIncludeRewriter
{
    /// <summary>
    /// Rewrites source code text to update EF6 Include patterns to EF Core ThenInclude chains.
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
    /// 100 = Simple includes (no change), 90 = Lambda-based with Select, 75 = String-based includes.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}
