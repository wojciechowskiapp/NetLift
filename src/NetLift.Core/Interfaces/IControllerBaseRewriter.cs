namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites controller base classes from System.Web.Mvc/Http to ASP.NET Core equivalents using Roslyn.
/// Handles Controller, ApiController, and custom base controllers.
/// </summary>
public interface IControllerBaseRewriter
{
    /// <summary>
    /// Rewrites source code text to update controller base classes and add necessary attributes.
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
    /// 100 = direct 1:1 mapping with known patterns, 90 = custom base controller, 60 = multiple constructors.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the collection of dependencies that were added during rewriting.
    /// </summary>
    IReadOnlyCollection<DependencyInfo> AddedDependencies { get; }
}

/// <summary>
/// Represents a dependency that was injected via constructor.
/// </summary>
/// <param name="TypeName">The full type name of the dependency (e.g., "ILogger&lt;HomeController&gt;").</param>
/// <param name="ParameterName">The parameter name in the constructor (e.g., "logger").</param>
/// <param name="FieldName">The private field name (e.g., "_logger").</param>
public sealed record DependencyInfo(
    string TypeName,
    string ParameterName,
    string FieldName);
