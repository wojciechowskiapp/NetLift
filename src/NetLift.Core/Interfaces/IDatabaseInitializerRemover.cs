namespace NetLift.Core.Interfaces;

/// <summary>
/// Removes EF6 Database.SetInitializer calls and adds migration guidance for EF Core.
/// Handles all EF6 initializer patterns: CreateDatabaseIfNotExists, MigrateDatabaseToLatestVersion,
/// DropCreateDatabaseIfModelChanges, DropCreateDatabaseAlways, and null initializers.
/// </summary>
public interface IDatabaseInitializerRemover
{
    /// <summary>
    /// Rewrites source code text to remove Database.SetInitializer calls and add migration guidance.
    /// </summary>
    /// <param name="sourceCode">The C# source code to rewrite.</param>
    /// <returns>The rewritten source code with commented-out initializers and guidance.</returns>
    string Rewrite(string sourceCode);

    /// <summary>
    /// Gets the collection of new using directives that were added during rewriting.
    /// Empty for this rewriter as no new usings are needed.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the rewrite operation (0-100).
    /// Always 95 for Database.SetInitializer removal (straightforward transformation).
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets information about initializers that were removed for reporting.
    /// </summary>
    IReadOnlyCollection<RemovedInitializerInfo> RemovedInitializers { get; }
}

/// <summary>
/// Represents information about a database initializer that was removed.
/// </summary>
/// <param name="InitializerType">The type of initializer (e.g., "CreateDatabaseIfNotExists", "MigrateDatabaseToLatestVersion").</param>
/// <param name="ContextType">The DbContext type name.</param>
/// <param name="OriginalCode">The original Database.SetInitializer statement.</param>
public sealed record RemovedInitializerInfo(
    string InitializerType,
    string ContextType,
    string OriginalCode);
