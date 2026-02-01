using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides automatic commit generation for migration transformations.
/// </summary>
public interface ICommitGenerator
{
    /// <summary>
    /// Generates a conventional commit message for the specified migration phase.
    /// </summary>
    /// <param name="phase">The migration phase type.</param>
    /// <param name="changedFiles">The files that were changed during the transformation.</param>
    /// <returns>A formatted commit message following conventional commit standards.</returns>
    Task<string> GenerateCommitMessageAsync(
        MigrationPhaseType phase,
        IEnumerable<string> changedFiles);

    /// <summary>
    /// Commits all changes for the specified migration phase.
    /// </summary>
    /// <param name="repoPath">The path to the Git repository.</param>
    /// <param name="phase">The migration phase type.</param>
    /// <param name="changedFiles">The files that were changed during the transformation.</param>
    /// <returns>The SHA hash of the created commit, or empty string if no changes were committed.</returns>
    Task<string> CommitChangesAsync(
        string repoPath,
        MigrationPhaseType phase,
        IEnumerable<string> changedFiles);
}
