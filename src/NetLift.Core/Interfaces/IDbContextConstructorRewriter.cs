namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites EF6 DbContext constructor patterns to EF Core constructor patterns using Roslyn.
/// Handles parameterless constructors, connection string constructors, and preserves custom logic.
/// </summary>
public interface IDbContextConstructorRewriter
{
    /// <summary>
    /// Rewrites source code text to transform DbContext constructors to EF Core pattern.
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
    /// 95 = simple constructor replacement, 80 = multiple constructors, 75 = custom logic preserved.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets information about removed connection string patterns for migration to appsettings.json.
    /// </summary>
    IReadOnlyCollection<DbContextConnectionStringInfo> RemovedConnectionStrings { get; }
}

/// <summary>
/// Represents information about a connection string that was removed from a DbContext constructor.
/// </summary>
/// <param name="ContextName">The name of the DbContext class.</param>
/// <param name="ConnectionStringName">The connection string name from "name=..." pattern, or null if direct connection string.</param>
public sealed record DbContextConnectionStringInfo(
    string ContextName,
    string? ConnectionStringName);
