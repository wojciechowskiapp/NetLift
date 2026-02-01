namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites ActionResult types and related method calls from ASP.NET MVC to ASP.NET Core equivalents using Roslyn.
/// Handles return types, Json() calls, HttpNotFound(), HttpStatusCodeResult, and RedirectToRoute().
/// </summary>
public interface IActionResultRewriter
{
    /// <summary>
    /// Rewrites source code text to update ActionResult types and related method calls.
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
    /// 100 = simple type/method replacements, 90 = RedirectToRoute conversion, 70 = complex anonymous objects.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}
