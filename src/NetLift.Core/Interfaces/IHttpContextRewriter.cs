namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites HttpContext.Current usages from System.Web to ASP.NET Core patterns using Roslyn.
/// In controller classes, uses base properties (User, Request, Response).
/// In non-controller classes, adds IHttpContextAccessor injection.
/// </summary>
public interface IHttpContextRewriter
{
    /// <summary>
    /// Rewrites source code text to update HttpContext.Current usages.
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
    /// 100 = controller properties, 95 = IHttpContextAccessor injection, 60 = complex Session usage.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets whether any class requires IHttpContextAccessor injection.
    /// </summary>
    bool RequiresHttpContextAccessor { get; }

    /// <summary>
    /// Gets the collection of class names that need IHttpContextAccessor injection.
    /// </summary>
    IReadOnlyCollection<string> ClassesNeedingAccessor { get; }
}
