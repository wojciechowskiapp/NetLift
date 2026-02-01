namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites System.Web.Mvc namespaces to ASP.NET Core equivalents using Roslyn.
/// Handles using directives, qualified names, and fully qualified type references.
/// </summary>
public interface IMvcNamespaceRewriter
{
    /// <summary>
    /// Rewrites source code text to replace System.Web.Mvc namespaces with ASP.NET Core equivalents.
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
    /// 100 = direct 1:1 mapping, 90 = merged namespaces, 70 = uncertain/removed.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Represents a diagnostic message from the rewriter.
/// </summary>
/// <param name="Message">The diagnostic message.</param>
/// <param name="Severity">The severity level of the diagnostic.</param>
public sealed record RewriterDiagnostic(
    string Message,
    RewriterDiagnosticSeverity Severity);

/// <summary>
/// Severity levels for rewriter diagnostics.
/// </summary>
public enum RewriterDiagnosticSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning message.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error message.
    /// </summary>
    Error = 2
}
