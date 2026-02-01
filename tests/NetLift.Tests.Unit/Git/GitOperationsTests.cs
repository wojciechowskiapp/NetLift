using FluentAssertions;
using LibGit2Sharp;
using NetLift.Core.Exceptions;
using NetLift.Git;

namespace NetLift.Tests.Unit.Git;

public class GitOperationsTests : IDisposable
{
    private readonly string _tempRepoPath;
    private readonly GitOperations _gitOperations;

    public GitOperationsTests()
    {
        _gitOperations = new GitOperations();
        _tempRepoPath = Path.Combine(Path.GetTempPath(), $"test-repo-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRepoPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRepoPath))
        {
            // Force delete read-only files that Git creates
            var directory = new DirectoryInfo(_tempRepoPath);
            SetAttributesNormal(directory);
            Directory.Delete(_tempRepoPath, true);
        }
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var subDir in dir.GetDirectories())
        {
            SetAttributesNormal(subDir);
        }

        foreach (var file in dir.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
        }
    }

    [Fact]
    public void IsGitRepository_WithNonGitDirectory_ReturnsFalse()
    {
        // Act
        var result = _gitOperations.IsGitRepository(_tempRepoPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WithGitDirectory_ReturnsTrue()
    {
        // Arrange
        Repository.Init(_tempRepoPath);

        // Act
        var result = _gitOperations.IsGitRepository(_tempRepoPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetCurrentBranch_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = () => _gitOperations.GetCurrentBranch(_tempRepoPath);

        // Assert
        act.Should().Throw<NotAGitRepositoryException>()
            .WithMessage($"'{_tempRepoPath}' is not a Git repository")
            .And.Path.Should().Be(_tempRepoPath);
    }

    [Fact]
    public void GetCurrentBranch_WithGitRepository_ReturnsCurrentBranch()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        // Act
        var branchName = _gitOperations.GetCurrentBranch(_tempRepoPath);

        // Assert
        branchName.Should().Be("master");
    }

    [Fact]
    public async Task CreateBranchAsync_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = async () => await _gitOperations.CreateBranchAsync(_tempRepoPath, "feature-branch");

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>()
            .WithMessage($"'{_tempRepoPath}' is not a Git repository");
    }

    [Fact]
    public async Task CreateBranchAsync_WithValidRepository_CreatesBranch()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        // Act
        var branchName = await _gitOperations.CreateBranchAsync(_tempRepoPath, "feature-branch");

        // Assert
        branchName.Should().Be("feature-branch");

        using var repo = new Repository(_tempRepoPath);
        repo.Branches["feature-branch"].Should().NotBeNull();
    }

    [Fact]
    public async Task CheckoutAsync_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = async () => await _gitOperations.CheckoutAsync(_tempRepoPath, "master");

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task CheckoutAsync_WithNonExistentBranch_ThrowsInvalidOperationException()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        // Act
        var act = async () => await _gitOperations.CheckoutAsync(_tempRepoPath, "non-existent");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Branch 'non-existent' not found");
    }

    [Fact]
    public async Task CheckoutAsync_WithExistingBranch_ChecksOutBranch()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);
        await _gitOperations.CreateBranchAsync(_tempRepoPath, "feature-branch");

        // Act
        await _gitOperations.CheckoutAsync(_tempRepoPath, "feature-branch");

        // Assert
        var currentBranch = _gitOperations.GetCurrentBranch(_tempRepoPath);
        currentBranch.Should().Be("feature-branch");
    }

    [Fact]
    public async Task StageAsync_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = async () => await _gitOperations.StageAsync(_tempRepoPath, new[] { "test.txt" });

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task StageAsync_WithValidFiles_StagesFiles()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        var testFile = Path.Combine(_tempRepoPath, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        await _gitOperations.StageAsync(_tempRepoPath, new[] { testFile });

        // Assert
        using var repo = new Repository(_tempRepoPath);
        var status = repo.RetrieveStatus(new StatusOptions());
        var stagedFiles = status.Where(s => s.State.HasFlag(FileStatus.NewInIndex) ||
                                             s.State.HasFlag(FileStatus.ModifiedInIndex)).ToList();
        stagedFiles.Should().HaveCount(1);
        stagedFiles.First().FilePath.Should().Be("test.txt");
    }

    [Fact]
    public async Task CommitAsync_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = async () => await _gitOperations.CommitAsync(
            _tempRepoPath,
            "test commit",
            "Test Author",
            "test@example.com");

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task CommitAsync_WithStagedChanges_CreatesCommit()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        var testFile = Path.Combine(_tempRepoPath, "test.txt");
        File.WriteAllText(testFile, "test content");
        await _gitOperations.StageAsync(_tempRepoPath, new[] { testFile });

        // Act
        var commitSha = await _gitOperations.CommitAsync(
            _tempRepoPath,
            "Add test file",
            "Test Author",
            "test@example.com");

        // Assert
        commitSha.Should().NotBeNullOrEmpty();
        commitSha.Length.Should().Be(40); // SHA1 hash length

        using var repo = new Repository(_tempRepoPath);
        var commit = repo.Lookup<Commit>(commitSha);
        commit.Should().NotBeNull();
        commit!.Message.Should().Be("Add test file\n");
        commit.Author.Name.Should().Be("Test Author");
        commit.Author.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = async () => await _gitOperations.HasUncommittedChangesAsync(_tempRepoPath);

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithCleanRepository_ReturnsFalse()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        // Act
        var hasChanges = await _gitOperations.HasUncommittedChangesAsync(_tempRepoPath);

        // Assert
        hasChanges.Should().BeFalse();
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithUncommittedChanges_ReturnsTrue()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        var testFile = Path.Combine(_tempRepoPath, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var hasChanges = await _gitOperations.HasUncommittedChangesAsync(_tempRepoPath);

        // Assert
        hasChanges.Should().BeTrue();
    }

    [Fact]
    public void GetModifiedFiles_WithNonGitDirectory_ThrowsNotAGitRepositoryException()
    {
        // Act
        var act = () => _gitOperations.GetModifiedFiles(_tempRepoPath);

        // Assert
        act.Should().Throw<NotAGitRepositoryException>();
    }

    [Fact]
    public void GetModifiedFiles_WithCleanRepository_ReturnsEmpty()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        // Act
        var modifiedFiles = _gitOperations.GetModifiedFiles(_tempRepoPath);

        // Assert
        modifiedFiles.Should().BeEmpty();
    }

    [Fact]
    public void GetModifiedFiles_WithModifiedFiles_ReturnsModifiedFiles()
    {
        // Arrange
        Repository.Init(_tempRepoPath);
        CreateInitialCommit(_tempRepoPath);

        var testFile1 = Path.Combine(_tempRepoPath, "test1.txt");
        var testFile2 = Path.Combine(_tempRepoPath, "test2.txt");
        File.WriteAllText(testFile1, "test content 1");
        File.WriteAllText(testFile2, "test content 2");

        // Act
        var modifiedFiles = _gitOperations.GetModifiedFiles(_tempRepoPath).ToList();

        // Assert
        modifiedFiles.Should().HaveCount(2);
        modifiedFiles.Should().Contain("test1.txt");
        modifiedFiles.Should().Contain("test2.txt");
    }

    private void CreateInitialCommit(string repoPath)
    {
        using var repo = new Repository(repoPath);

        // Create initial file
        var readmeFile = Path.Combine(repoPath, "README.md");
        File.WriteAllText(readmeFile, "# Test Repository");

        // Stage and commit
        LibGit2Sharp.Commands.Stage(repo, "*");

        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        repo.Commit("Initial commit", signature, signature);
    }
}
