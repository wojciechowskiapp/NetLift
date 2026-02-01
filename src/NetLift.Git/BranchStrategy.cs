using NetLift.Core.Exceptions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Git;

/// <summary>
/// Implementation of Git branch strategy for migration phases.
/// </summary>
public class BranchStrategy : IBranchStrategy
{
    private const string BranchPrefix = "netlift";
    private readonly IGitOperations _gitOperations;

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchStrategy"/> class.
    /// </summary>
    /// <param name="gitOperations">The Git operations service.</param>
    public BranchStrategy(IGitOperations gitOperations)
    {
        _gitOperations = gitOperations ?? throw new ArgumentNullException(nameof(gitOperations));
    }

    /// <inheritdoc />
    public string GetBranchName(MigrationPhaseType phase)
    {
        var phaseName = phase switch
        {
            MigrationPhaseType.ProjectFiles => "project-files",
            MigrationPhaseType.Configuration => "configuration",
            MigrationPhaseType.Controllers => "controllers",
            MigrationPhaseType.EntityFramework => "entity-framework",
            MigrationPhaseType.Wcf => "wcf",
            MigrationPhaseType.Validation => "validation",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown migration phase type.")
        };

        return $"{BranchPrefix}/{phaseName}";
    }

    /// <inheritdoc />
    public async Task<string> CreatePhaseBranchAsync(string repoPath, MigrationPhaseType phase)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            throw new ArgumentException("Repository path cannot be null or empty.", nameof(repoPath));
        }

        if (!_gitOperations.IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        // Validate repository is clean
        if (!await ValidateRepositoryCleanAsync(repoPath))
        {
            var modifiedFiles = _gitOperations.GetModifiedFiles(repoPath);
            throw new DirtyRepositoryException(repoPath, modifiedFiles);
        }

        var branchName = GetBranchName(phase);

        // Check if branch already exists
        if (_gitOperations.BranchExists(repoPath, branchName))
        {
            throw new BranchAlreadyExistsException(branchName);
        }

        // Create the branch
        return await _gitOperations.CreateBranchAsync(repoPath, branchName);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateRepositoryCleanAsync(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            throw new ArgumentException("Repository path cannot be null or empty.", nameof(repoPath));
        }

        if (!_gitOperations.IsGitRepository(repoPath))
        {
            throw new NotAGitRepositoryException(repoPath);
        }

        var hasChanges = await _gitOperations.HasUncommittedChangesAsync(repoPath);
        return !hasChanges;
    }
}
