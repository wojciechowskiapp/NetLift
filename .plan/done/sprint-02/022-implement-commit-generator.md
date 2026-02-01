# [TASK-022] Implement Auto-Commit Generator

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | Claude |
| **Started** | 2026-01-31 |
| **Completed** | 2026-01-31 |

## Dependencies

- **Depends on:** TASK-021
- **Blocks:** -

---

## Description

Implement automatic git commit generation after each transformation step. Each commit includes meaningful messages that describe the transformation, list affected files, and include metadata about confidence scores. This creates a detailed audit trail of all migration changes.

---

## Acceptance Criteria

- [ ] Auto-commits after each transformation completes
- [ ] Commit messages include transformation type in subject line
- [ ] Commit body contains list of affected files
- [ ] Commit body includes confidence score for the transformation
- [ ] Follows conventional commit format (feat/fix/refactor)
- [ ] Supports custom commit message templates
- [ ] Includes git trailers for machine-readable metadata
- [ ] Handles empty commits gracefully (skip with warning)
- [ ] Configurable author identity for commits
- [ ] Unit tests for message generation
- [ ] Integration tests with real git operations

---

## Technical Notes

### Commit Generator Service:

```csharp
public interface ICommitGenerator
{
    Task<CommitInfo> CommitTransformationAsync(
        Repository repo,
        TransformationResult transformation,
        CommitOptions options);

    string GenerateCommitMessage(
        TransformationResult transformation,
        CommitMessageOptions options);
}

public class TransformationCommitGenerator : ICommitGenerator
{
    private readonly ILogger<TransformationCommitGenerator> _logger;

    public async Task<CommitInfo> CommitTransformationAsync(
        Repository repo,
        TransformationResult transformation,
        CommitOptions options)
    {
        // Stage all changed files
        foreach (var file in transformation.ModifiedFiles)
        {
            Commands.Stage(repo, file);
        }

        // Check if there are staged changes
        var status = repo.RetrieveStatus();
        if (!status.Staged.Any())
        {
            _logger.LogWarning(
                "No changes to commit for transformation {Type}",
                transformation.Type);
            return CommitInfo.Empty;
        }

        // Generate commit message
        var message = GenerateCommitMessage(transformation, options.MessageOptions);

        // Create signature
        var signature = new Signature(
            options.AuthorName ?? "NetLift",
            options.AuthorEmail ?? "netlift@migration.local",
            DateTimeOffset.UtcNow);

        // Create commit
        var commit = repo.Commit(message, signature, signature);

        _logger.LogInformation(
            "Created commit {Sha} for {Type}: {FileCount} files",
            commit.Sha[..8],
            transformation.Type,
            transformation.ModifiedFiles.Count);

        return new CommitInfo
        {
            Sha = commit.Sha,
            Message = message,
            FileCount = transformation.ModifiedFiles.Count,
            Timestamp = commit.Author.When
        };
    }

    public string GenerateCommitMessage(
        TransformationResult transformation,
        CommitMessageOptions options)
    {
        var sb = new StringBuilder();

        // Subject line (conventional commit format)
        var prefix = GetConventionalPrefix(transformation.Type);
        sb.AppendLine($"{prefix}({transformation.Scope}): {transformation.Summary}");
        sb.AppendLine();

        // Body - file list
        sb.AppendLine("Modified files:");
        foreach (var file in transformation.ModifiedFiles.Take(20))
        {
            sb.AppendLine($"  - {file}");
        }

        if (transformation.ModifiedFiles.Count > 20)
        {
            sb.AppendLine($"  ... and {transformation.ModifiedFiles.Count - 20} more");
        }
        sb.AppendLine();

        // Transformation details
        if (transformation.Details.Any())
        {
            sb.AppendLine("Changes:");
            foreach (var detail in transformation.Details)
            {
                sb.AppendLine($"  - {detail}");
            }
            sb.AppendLine();
        }

        // Git trailers for machine-readable metadata
        sb.AppendLine($"Transformation-Type: {transformation.Type}");
        sb.AppendLine($"Confidence-Score: {transformation.ConfidenceScore:P0}");
        sb.AppendLine($"Files-Changed: {transformation.ModifiedFiles.Count}");
        sb.AppendLine($"NetLift-Version: {options.NetLiftVersion}");

        if (transformation.Warnings.Any())
        {
            sb.AppendLine($"Warnings-Count: {transformation.Warnings.Count}");
        }

        return sb.ToString();
    }

    private string GetConventionalPrefix(TransformationType type) => type switch
    {
        TransformationType.CsprojConversion => "refactor",
        TransformationType.PackageUpgrade => "chore",
        TransformationType.ConfigMigration => "refactor",
        TransformationType.MvcConversion => "feat",
        TransformationType.EfMigration => "feat",
        TransformationType.WcfConversion => "feat",
        TransformationType.CodeFix => "fix",
        _ => "chore"
    };
}
```

### Transformation Result Model:

```csharp
public class TransformationResult
{
    public TransformationType Type { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> ModifiedFiles { get; set; } = new();
    public List<string> Details { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public TimeSpan Duration { get; set; }
}

public enum TransformationType
{
    CsprojConversion,
    PackageUpgrade,
    ConfigMigration,
    MvcConversion,
    EfMigration,
    WcfConversion,
    CodeFix,
    AssemblyInfoExtraction,
    ProjectReferenceUpdate
}

public class CommitOptions
{
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public CommitMessageOptions MessageOptions { get; set; } = new();
}

public class CommitMessageOptions
{
    public string NetLiftVersion { get; set; } = "1.0.0";
    public bool IncludeFileList { get; set; } = true;
    public bool IncludeTrailers { get; set; } = true;
    public int MaxFilesInMessage { get; set; } = 20;
    public string? CustomTemplate { get; set; }
}

public class CommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public static CommitInfo Empty => new()
    {
        Sha = string.Empty,
        Message = "No changes",
        FileCount = 0
    };

    public bool IsEmpty => string.IsNullOrEmpty(Sha);
}
```

### Commit Message Examples:

```
refactor(MyProject.csproj): Convert to SDK-style project format

Modified files:
  - src/MyProject/MyProject.csproj
  - src/MyProject/Properties/AssemblyInfo.cs (deleted)

Changes:
  - Converted from old-style to SDK-style project format
  - Migrated 15 PackageReferences from packages.config
  - Extracted assembly info to csproj properties
  - Removed 47 auto-included file references

Transformation-Type: CsprojConversion
Confidence-Score: 95%
Files-Changed: 2
NetLift-Version: 1.0.0
```

### Files to create/modify:

- `src/NetLift.Migration/Git/ICommitGenerator.cs` - Interface
- `src/NetLift.Migration/Git/TransformationCommitGenerator.cs` - Implementation
- `src/NetLift.Migration/Git/Models/CommitInfo.cs` - Commit info model
- `src/NetLift.Migration/Git/Models/CommitOptions.cs` - Options model
- `src/NetLift.Migration/Models/TransformationResult.cs` - Transformation result
- `tests/NetLift.Tests/Git/TransformationCommitGeneratorTests.cs` - Unit tests

### Key Decisions:

- Use conventional commit format for compatibility with tools
- Include git trailers for machine-readable metadata parsing
- Limit file list in message to prevent overly long commits
- Support confidence score to indicate transformation reliability
- Skip empty commits with warning instead of failing

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
| 2026-01-31 | Claude | Implemented ICommitGenerator interface in NetLift.Core |
| 2026-01-31 | Claude | Implemented CommitGenerator in NetLift.Git with conventional commit format |
| 2026-01-31 | Claude | Created comprehensive unit tests (20 tests, all passing) |
| 2026-01-31 | Claude | Completed - All acceptance criteria met |
