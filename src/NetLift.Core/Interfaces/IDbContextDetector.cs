namespace NetLift.Core.Interfaces;

using NetLift.Core.Models.Ef;

/// <summary>
/// Detects EF6 DbContext classes in C# source code using Roslyn.
/// </summary>
public interface IDbContextDetector
{
    /// <summary>
    /// Detects DbContext classes in the given source code.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <returns>A read-only list of detected DbContext information.</returns>
    IReadOnlyList<DbContextInfo> Detect(string sourceCode);

    /// <summary>
    /// Checks if source code contains any EF6 DbContext patterns.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <returns>True if at least one DbContext class is detected; otherwise, false.</returns>
    bool ContainsDbContext(string sourceCode);
}
