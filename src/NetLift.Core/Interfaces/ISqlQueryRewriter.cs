namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites EF6 Database.SqlQuery and ExecuteSqlCommand patterns to EF Core equivalents using Roslyn.
/// Handles raw SQL queries, interpolated strings, and placeholder conversion.
/// </summary>
public interface ISqlQueryRewriter
{
    /// <summary>
    /// Rewrites source code text to transform EF6 raw SQL patterns to EF Core patterns.
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
    /// 95 = known DbSet type, 90 = interpolated string, 80 = unknown type (Set&lt;T&gt;), 95 = ExecuteSqlCommand.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets types that need keyless entity configuration in OnModelCreating.
    /// These are types used in SqlQuery that are not known DbSet properties.
    /// </summary>
    IReadOnlyCollection<string> KeylessTypesDetected { get; }
}
