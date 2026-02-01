namespace NetLift.Core.Interfaces;

/// <summary>
/// Rewrites EF6 many-to-many relationship configurations to EF Core equivalents using Roslyn.
/// Transforms HasMany().WithMany() patterns, including Map() configurations, to UsingEntity patterns.
/// </summary>
public interface IManyToManyRewriter
{
    /// <summary>
    /// Rewrites source code text to transform EF6 many-to-many configurations to EF Core.
    /// </summary>
    /// <param name="sourceCode">The C# source code containing DbContext configuration.</param>
    /// <returns>The rewritten source code with transformed many-to-many configurations.</returns>
    string Rewrite(string sourceCode);

    /// <summary>
    /// Gets the collection of new using directives that were added during rewriting.
    /// </summary>
    IReadOnlyCollection<string> RequiredUsings { get; }

    /// <summary>
    /// Gets the confidence score for the rewrite operation (0-100).
    /// 95 = simple many-to-many without Map(), 80 = standard Map() with table/keys, 65 = complex Map() scenarios.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages generated during the rewrite operation.
    /// </summary>
    IReadOnlyCollection<RewriterDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets detected many-to-many configurations that were transformed or need review.
    /// </summary>
    IReadOnlyCollection<ManyToManyInfo> DetectedRelationships { get; }
}

/// <summary>
/// Represents a detected many-to-many relationship configuration.
/// </summary>
/// <param name="LeftEntity">The left entity type name (e.g., "Student").</param>
/// <param name="RightEntity">The right entity type name (e.g., "Course").</param>
/// <param name="JoinTableName">The join table name from Map().ToTable(), if specified.</param>
/// <param name="LeftKeyName">The left foreign key name from Map().MapLeftKey(), if specified.</param>
/// <param name="RightKeyName">The right foreign key name from Map().MapRightKey(), if specified.</param>
public sealed record ManyToManyInfo(
    string LeftEntity,
    string RightEntity,
    string? JoinTableName,
    string? LeftKeyName,
    string? RightKeyName);
