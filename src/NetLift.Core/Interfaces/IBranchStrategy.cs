using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides Git branch strategy operations for migration phases.
/// </summary>
public interface IBranchStrategy
{
    /// <summary>
    /// Gets the branch name for a specific migration phase.
    /// </summary>
    /// <param name="phase">The migration phase type.</param>
    /// <returns>The branch name following the convention netlift/phase-name.</returns>
    string GetBranchName(MigrationPhaseType phase);

    /// <summary>
    /// Creates a new branch for the specified migration phase.
    /// </summary>
    /// <param name="repoPath">The path to the Git repository.</param>
    /// <param name="phase">The migration phase type.</param>
    /// <returns>The name of the created branch.</returns>
    /// <exception cref="Exceptions.NotAGitRepositoryException">Thrown when the path is not a valid Git repository.</exception>
    /// <exception cref="Exceptions.DirtyRepositoryException">Thrown when the repository has uncommitted changes.</exception>
    /// <exception cref="Exceptions.BranchAlreadyExistsException">Thrown when the branch already exists.</exception>
    Task<string> CreatePhaseBranchAsync(string repoPath, MigrationPhaseType phase);

    /// <summary>
    /// Validates that the repository is clean (no uncommitted changes).
    /// </summary>
    /// <param name="repoPath">The path to the Git repository.</param>
    /// <returns>true if the repository is clean; otherwise, false.</returns>
    /// <exception cref="Exceptions.NotAGitRepositoryException">Thrown when the path is not a valid Git repository.</exception>
    Task<bool> ValidateRepositoryCleanAsync(string repoPath);
}
