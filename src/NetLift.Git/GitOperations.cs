using LibGit2Sharp;
using NetLift.Core.Exceptions;
using NetLift.Core.Interfaces;

namespace NetLift.Git;

/// <summary>
/// Implementation of Git operations using LibGit2Sharp.
/// </summary>
public class GitOperations : IGitOperations
{
    /// <inheritdoc />
    public bool IsGitRepository(string path)
    {
        return Repository.IsValid(path);
    }

    /// <inheritdoc />
    public string GetCurrentBranch(string repoPath)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);
            return repo.Head.FriendlyName;
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public Task<string> CreateBranchAsync(string repoPath, string branchName)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            // Create branch from current HEAD
            var branch = repo.CreateBranch(branchName);

            return Task.FromResult(branch.FriendlyName);
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public Task CheckoutAsync(string repoPath, string branchName)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            var branch = repo.Branches[branchName];
            if (branch == null)
            {
                throw new InvalidOperationException($"Branch '{branchName}' not found");
            }

            Commands.Checkout(repo, branch);

            return Task.CompletedTask;
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public Task StageAsync(string repoPath, IEnumerable<string> files)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            foreach (var file in files)
            {
                // Convert to relative path
                var relativePath = Path.GetRelativePath(repoPath, file);
                Commands.Stage(repo, relativePath);
            }

            return Task.CompletedTask;
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public Task<string> CommitAsync(
        string repoPath,
        string message,
        string authorName,
        string authorEmail)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);
            var committer = author;

            var commit = repo.Commit(message, author, committer);

            return Task.FromResult(commit.Sha);
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasUncommittedChangesAsync(string repoPath)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            var status = repo.RetrieveStatus();
            return Task.FromResult(status.IsDirty);
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetModifiedFiles(string repoPath)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);

            var status = repo.RetrieveStatus();

            return status
                .Where(s => s.State != FileStatus.Ignored && s.State != FileStatus.Unaltered)
                .Select(s => s.FilePath)
                .ToList();
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }

    /// <inheritdoc />
    public bool BranchExists(string repoPath, string branchName)
    {
        if (!IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        try
        {
            using var repo = new Repository(repoPath);
            return repo.Branches[branchName] != null;
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new NotAGitRepositoryException(repoPath, ex);
        }
    }
}
