namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites controller method bodies from ASP.NET MVC patterns to ASP.NET Core.
/// Handles DbContext references, TryUpdateModel, FormCollection, and authentication patterns.
/// </summary>
public interface IControllerMethodBodyRewriter
{
    /// <summary>
    /// Rewrites controller source code to transform method body patterns.
    /// </summary>
    /// <param name="sourceCode">The C# source code to rewrite.</param>
    /// <returns>The rewritten source code.</returns>
    string Rewrite(string sourceCode);

    /// <summary>
    /// Rewrites controller source code with known DbContext type information for accurate detection.
    /// This overload provides higher accuracy by using pre-analyzed DbContext types from the project.
    /// </summary>
    /// <param name="sourceCode">The C# source code to rewrite.</param>
    /// <param name="knownDbContextTypes">
    /// Set of DbContext class names detected from the project (e.g., "MusicStoreEntities", "ApplicationDbContext").
    /// When provided, field detection uses exact type matching instead of pattern matching.
    /// </param>
    /// <returns>The rewritten source code.</returns>
    string Rewrite(string sourceCode, ISet<string>? knownDbContextTypes);

    /// <summary>
    /// Gets the usings required by the transformation.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the transformation (0-100).
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets the diagnostics generated during transformation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }
}
