namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites EF6 lazy loading configuration to EF Core patterns using Roslyn.
/// Handles Configuration.LazyLoadingEnabled and Configuration.ProxyCreationEnabled settings.
/// EF6 has lazy loading ON by default, EF Core has it OFF by default.
/// </summary>
public interface ILazyLoadingConfigRewriter
{
    /// <summary>
    /// Rewrites source code text to transform EF6 lazy loading configuration to EF Core.
    /// Removes Configuration.LazyLoadingEnabled and Configuration.ProxyCreationEnabled assignments,
    /// and adds appropriate TODO comments with guidance.
    /// </summary>
    /// <param name="sourceCode">The C# source code containing DbContext configuration.</param>
    /// <returns>The rewritten source code with transformed lazy loading configuration.</returns>
    string Rewrite(string sourceCode);

    /// <summary>
    /// Gets the collection of new using directives that were added during rewriting.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the rewrite operation (0-100).
    /// 95 = explicit disable found, 85 = explicit enable found (needs package + virtual props),
    /// 75 = no explicit setting (assume EF6 default).
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Whether lazy loading was enabled in the original code.
    /// True if explicitly enabled or if no explicit setting found (EF6 default).
    /// </summary>
    bool LazyLoadingWasEnabled { get; }

    /// <summary>
    /// Whether proxy creation was enabled in the original code.
    /// True if explicitly enabled or if no explicit setting found (EF6 default).
    /// </summary>
    bool ProxyCreationWasEnabled { get; }
}
