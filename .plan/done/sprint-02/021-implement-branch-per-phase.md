# [TASK-021] Implement Branch-Per-Phase Git Strategy

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-013
- **Blocks:** TASK-022

---

## Description

Implement a git branching strategy that creates a separate branch for each migration phase. This provides clear separation of concerns, enables easy rollback of specific phases, and creates a clean audit trail of the migration process. Each branch represents a distinct transformation step (csproj/, config/, mvc/, ef/, wcf/).

---

## Acceptance Criteria

- [ ] LibGit2Sharp creates branches automatically during migration
- [ ] Branch naming convention enforced: `netlift/<phase>/<timestamp>`
- [ ] Creates branches for each phase: csproj, config, mvc, ef, wcf
- [ ] Validates repository is clean before creating branches
- [ ] Handles existing branches gracefully (skip or error based on config)
- [ ] Supports custom branch prefix via configuration
- [ ] Branches created from current HEAD
- [ ] Logs branch creation with commit reference
- [ ] Unit tests for branch creation logic
- [ ] Integration tests with real git repositories

---

## Technical Notes

### Branch Strategy Service:

```csharp
public interface IBranchStrategy
{
    Task<BranchInfo> CreatePhaseBranchAsync(
        Repository repo,
        MigrationPhase phase,
        BranchOptions options);

    Task<bool> ValidateRepositoryStateAsync(Repository repo);
    IEnumerable<string> GetPhaseBranchNames(string prefix);
}

public class PhaseBranchStrategy : IBranchStrategy
{
    private readonly ILogger<PhaseBranchStrategy> _logger;

    public async Task<BranchInfo> CreatePhaseBranchAsync(
        Repository repo,
        MigrationPhase phase,
        BranchOptions options)
    {
        // Validate repo state
        if (!await ValidateRepositoryStateAsync(repo))
        {
            throw new InvalidOperationException(
                "Repository has uncommitted changes. Commit or stash before migration.");
        }

        var branchName = GenerateBranchName(phase, options.Prefix);

        // Check if branch exists
        var existingBranch = repo.Branches[branchName];
        if (existingBranch != null)
        {
            if (options.OverwriteExisting)
            {
                repo.Branches.Remove(existingBranch);
                _logger.LogWarning("Removed existing branch: {BranchName}", branchName);
            }
            else
            {
                throw new BranchExistsException(branchName);
            }
        }

        // Create branch from HEAD
        var branch = repo.CreateBranch(branchName);

        // Checkout the new branch
        Commands.Checkout(repo, branch);

        _logger.LogInformation(
            "Created branch {BranchName} from {CommitSha}",
            branchName,
            repo.Head.Tip.Sha[..8]);

        return new BranchInfo
        {
            Name = branchName,
            Phase = phase,
            CommitSha = repo.Head.Tip.Sha,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<bool> ValidateRepositoryStateAsync(Repository repo)
    {
        var status = repo.RetrieveStatus();
        return !status.IsDirty;
    }

    private string GenerateBranchName(MigrationPhase phase, string prefix)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var phaseName = phase.ToString().ToLowerInvariant();
        return $"{prefix}/{phaseName}/{timestamp}";
    }
}
```

### Migration Phases:

```csharp
public enum MigrationPhase
{
    [Description("Convert .csproj to SDK-style")]
    Csproj,

    [Description("Migrate configuration files")]
    Config,

    [Description("Convert ASP.NET MVC to ASP.NET Core")]
    Mvc,

    [Description("Migrate Entity Framework to EF Core")]
    EntityFramework,

    [Description("Convert WCF to gRPC/REST")]
    Wcf
}

public class BranchOptions
{
    public string Prefix { get; set; } = "netlift";
    public bool OverwriteExisting { get; set; } = false;
    public bool CreateFromHead { get; set; } = true;
    public string? BaseBranch { get; set; }
}

public class BranchInfo
{
    public string Name { get; set; } = string.Empty;
    public MigrationPhase Phase { get; set; }
    public string CommitSha { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
```

### Phase Branch Coordinator:

```csharp
public class PhaseBranchCoordinator
{
    private readonly IBranchStrategy _branchStrategy;
    private readonly ILogger<PhaseBranchCoordinator> _logger;

    public async Task<IReadOnlyList<BranchInfo>> CreateMigrationBranchesAsync(
        string repoPath,
        IEnumerable<MigrationPhase> phases,
        BranchOptions options)
    {
        using var repo = new Repository(repoPath);
        var branches = new List<BranchInfo>();

        foreach (var phase in phases)
        {
            var branchInfo = await _branchStrategy.CreatePhaseBranchAsync(
                repo, phase, options);
            branches.Add(branchInfo);

            _logger.LogInformation(
                "Phase {Phase}: Branch {Branch} ready",
                phase, branchInfo.Name);
        }

        return branches;
    }

    public async Task<BranchInfo> SwitchToPhaseAsync(
        string repoPath,
        MigrationPhase phase,
        string prefix = "netlift")
    {
        using var repo = new Repository(repoPath);

        var branchPattern = $"{prefix}/{phase.ToString().ToLowerInvariant()}/";
        var phaseBranch = repo.Branches
            .Where(b => b.FriendlyName.StartsWith(branchPattern))
            .OrderByDescending(b => b.Tip.Author.When)
            .FirstOrDefault();

        if (phaseBranch == null)
        {
            throw new BranchNotFoundException(phase);
        }

        Commands.Checkout(repo, phaseBranch);

        return new BranchInfo
        {
            Name = phaseBranch.FriendlyName,
            Phase = phase,
            CommitSha = phaseBranch.Tip.Sha
        };
    }
}
```

### Files to create/modify:

- `src/NetLift.Migration/Git/IBranchStrategy.cs` - Interface
- `src/NetLift.Migration/Git/PhaseBranchStrategy.cs` - Implementation
- `src/NetLift.Migration/Git/PhaseBranchCoordinator.cs` - Coordinator
- `src/NetLift.Migration/Git/Models/BranchInfo.cs` - Branch info model
- `src/NetLift.Migration/Git/Models/BranchOptions.cs` - Options model
- `src/NetLift.Migration/Git/Exceptions/BranchExistsException.cs` - Exception
- `tests/NetLift.Tests/Git/PhaseBranchStrategyTests.cs` - Unit tests

### Key Decisions:

- Use timestamp in branch name to avoid conflicts
- Validate clean repository state before branching
- Support both automatic and manual phase switching
- Keep LibGit2Sharp operations abstracted behind interfaces for testability

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
