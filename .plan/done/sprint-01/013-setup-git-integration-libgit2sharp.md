# [TASK-013] Setup Git Integration (LibGit2Sharp)

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001
- **Blocks:** TASK-022, TASK-023

---

## Description

Setup LibGit2Sharp for Git operations and implement basic repository operations needed for migration workflow.

---

## Acceptance Criteria

- [ ] LibGit2Sharp package added to NetLift.Git
- [ ] Can detect if directory is a Git repository
- [ ] Can create new branch
- [ ] Can stage files
- [ ] Can create commit
- [ ] Can get current branch name
- [ ] Handles non-Git directories gracefully
- [ ] Unit tests for Git operations

---

## Technical Notes

### Package:

```xml
<PackageReference Include="LibGit2Sharp" Version="0.30.0" />
```

### IGitOperations interface:

```csharp
public interface IGitOperations
{
    bool IsGitRepository(string path);
    string GetCurrentBranch(string repoPath);
    Task<string> CreateBranchAsync(string repoPath, string branchName);
    Task CheckoutAsync(string repoPath, string branchName);
    Task StageAsync(string repoPath, IEnumerable<string> files);
    Task<string> CommitAsync(string repoPath, string message, string authorName, string authorEmail);
    Task<bool> HasUncommittedChangesAsync(string repoPath);
    IEnumerable<string> GetModifiedFiles(string repoPath);
}
```

### Implementation:

```csharp
using LibGit2Sharp;

public class GitOperations : IGitOperations
{
    public bool IsGitRepository(string path)
    {
        return Repository.IsValid(path);
    }

    public string GetCurrentBranch(string repoPath)
    {
        using var repo = new Repository(repoPath);
        return repo.Head.FriendlyName;
    }

    public Task<string> CreateBranchAsync(string repoPath, string branchName)
    {
        using var repo = new Repository(repoPath);

        // Create branch from current HEAD
        var branch = repo.CreateBranch(branchName);

        return Task.FromResult(branch.FriendlyName);
    }

    public Task CheckoutAsync(string repoPath, string branchName)
    {
        using var repo = new Repository(repoPath);

        var branch = repo.Branches[branchName];
        if (branch == null)
            throw new InvalidOperationException($"Branch '{branchName}' not found");

        Commands.Checkout(repo, branch);

        return Task.CompletedTask;
    }

    public Task StageAsync(string repoPath, IEnumerable<string> files)
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

    public Task<string> CommitAsync(
        string repoPath,
        string message,
        string authorName,
        string authorEmail)
    {
        using var repo = new Repository(repoPath);

        var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);
        var committer = author;

        var commit = repo.Commit(message, author, committer);

        return Task.FromResult(commit.Sha);
    }

    public Task<bool> HasUncommittedChangesAsync(string repoPath)
    {
        using var repo = new Repository(repoPath);

        var status = repo.RetrieveStatus();
        return Task.FromResult(status.IsDirty);
    }

    public IEnumerable<string> GetModifiedFiles(string repoPath)
    {
        using var repo = new Repository(repoPath);

        var status = repo.RetrieveStatus();

        return status
            .Where(s => s.State != FileStatus.Ignored && s.State != FileStatus.Unaltered)
            .Select(s => s.FilePath);
    }
}
```

### Error handling:

```csharp
public class GitOperations : IGitOperations
{
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
}

public class NotAGitRepositoryException : Exception
{
    public string Path { get; }

    public NotAGitRepositoryException(string path)
        : base($"'{path}' is not a Git repository")
    {
        Path = path;
    }
}
```

### Note on cross-platform:

LibGit2Sharp includes native binaries for Windows, Linux, and macOS. No additional setup needed.

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
