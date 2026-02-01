namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides Git repository operations for migration workflows.
/// </summary>
public interface IGitOperations
{
    /// <summary>
    /// Determines whether the specified path is a valid Git repository.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>true if the path is a Git repository; otherwise, false.</returns>
    bool IsGitRepository(string path);

    /// <summary>
    /// Gets the name of the current branch.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <returns>The friendly name of the current branch.</returns>
    string GetCurrentBranch(string repoPath);

    /// <summary>
    /// Creates a new branch from the current HEAD.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <param name="branchName">The name of the branch to create.</param>
    /// <returns>The friendly name of the created branch.</returns>
    Task<string> CreateBranchAsync(string repoPath, string branchName);

    /// <summary>
    /// Checks out the specified branch.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <param name="branchName">The name of the branch to checkout.</param>
    Task CheckoutAsync(string repoPath, string branchName);

    /// <summary>
    /// Stages the specified files for commit.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <param name="files">The files to stage.</param>
    Task StageAsync(string repoPath, IEnumerable<string> files);

    /// <summary>
    /// Creates a commit with the staged changes.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="authorName">The author's name.</param>
    /// <param name="authorEmail">The author's email.</param>
    /// <returns>The SHA hash of the created commit.</returns>
    Task<string> CommitAsync(string repoPath, string message, string authorName, string authorEmail);

    /// <summary>
    /// Determines whether the repository has uncommitted changes.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <returns>true if the repository has uncommitted changes; otherwise, false.</returns>
    Task<bool> HasUncommittedChangesAsync(string repoPath);

    /// <summary>
    /// Gets the list of modified files in the repository.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <returns>An enumerable of file paths that have been modified.</returns>
    IEnumerable<string> GetModifiedFiles(string repoPath);

    /// <summary>
    /// Determines whether a branch with the specified name exists.
    /// </summary>
    /// <param name="repoPath">The path to the repository.</param>
    /// <param name="branchName">The name of the branch to check.</param>
    /// <returns>true if the branch exists; otherwise, false.</returns>
    bool BranchExists(string repoPath, string branchName);
}
