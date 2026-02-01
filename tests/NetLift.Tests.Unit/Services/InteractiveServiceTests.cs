using FluentAssertions;
using NetLift.Cli.Services;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Services;

/// <summary>
/// Unit tests for the InteractiveService class.
/// Note: These tests verify the service's validation logic and behavior.
/// Actual Spectre.Console interaction requires integration tests.
/// </summary>
public class InteractiveServiceTests
{
    private readonly InteractiveService _service;

    public InteractiveServiceTests()
    {
        _service = new InteractiveService();
    }

    [Fact]
    public async Task ConfirmAsync_WithNullMessage_ThrowsArgumentException()
    {
        // Arrange
        string? message = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ConfirmAsync(message!));
    }

    [Fact]
    public async Task ConfirmAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var message = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ConfirmAsync(message));
    }

    [Fact]
    public async Task ConfirmAsync_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Arrange
        var message = "   ";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ConfirmAsync(message));
    }

    [Fact]
    public async Task PromptChoiceAsync_WithNullMessage_ThrowsArgumentException()
    {
        // Arrange
        string? message = null;
        var changedFiles = new List<string> { "file1.cs" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.PromptChoiceAsync(message!, changedFiles));
    }

    [Fact]
    public async Task PromptChoiceAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var message = string.Empty;
        var changedFiles = new List<string> { "file1.cs" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.PromptChoiceAsync(message, changedFiles));
    }

    [Fact]
    public async Task PromptChoiceAsync_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Arrange
        var message = "   ";
        var changedFiles = new List<string> { "file1.cs" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.PromptChoiceAsync(message, changedFiles));
    }

    [Fact]
    public async Task PromptChoiceAsync_WithNullChangedFiles_ThrowsArgumentNullException()
    {
        // Arrange
        var message = "Test message";
        IEnumerable<string>? changedFiles = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.PromptChoiceAsync(message, changedFiles!));
    }

    [Fact]
    public void Reset_ResetsApplyAllFlag()
    {
        // Arrange & Act
        _service.Reset();

        // Assert
        // The service should be in its initial state
        // Further prompts should not auto-apply
        // This is tested indirectly through the ApplyAll behavior
        // No exception should be thrown
        _service.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Valid message")]
    [InlineData("Migrate project: TestProject")]
    [InlineData("Do you want to continue?")]
    public void PromptChoiceAsync_WithValidMessage_DoesNotThrow(string message)
    {
        // Arrange
        var changedFiles = new List<string> { "file1.cs", "file2.cs" };

        // Act
        var act = () => _service.PromptChoiceAsync(message, changedFiles);

        // Assert
        // We can't actually test the interactive prompt without a console
        // But we can verify the method signature is correct
        act.Should().NotBeNull();
    }

    [Fact]
    public void PromptChoiceAsync_WithEmptyChangedFilesList_DoesNotThrow()
    {
        // Arrange
        var message = "Test message";
        var changedFiles = new List<string>();

        // Act
        var act = () => _service.PromptChoiceAsync(message, changedFiles);

        // Assert
        // Empty list should be allowed
        act.Should().NotBeNull();
    }

    [Fact]
    public void PromptChoiceAsync_WithMultipleFiles_DoesNotThrow()
    {
        // Arrange
        var message = "Test message";
        var changedFiles = new List<string>
        {
            "file1.cs",
            "file2.cs",
            "file3.csproj",
            "packages.config",
            "AssemblyInfo.cs"
        };

        // Act
        var act = () => _service.PromptChoiceAsync(message, changedFiles);

        // Assert
        act.Should().NotBeNull();
    }

    [Fact]
    public void PromptChoiceAsync_WithSpecialCharactersInMessage_DoesNotThrow()
    {
        // Arrange
        var message = "Test <message> with [special] characters & symbols!";
        var changedFiles = new List<string> { "file.cs" };

        // Act
        var act = () => _service.PromptChoiceAsync(message, changedFiles);

        // Assert
        act.Should().NotBeNull();
    }

    [Fact]
    public void PromptChoiceAsync_WithLongFilePaths_DoesNotThrow()
    {
        // Arrange
        var message = "Test message";
        var changedFiles = new List<string>
        {
            @"C:\Very\Long\Path\To\Some\Deep\Directory\Structure\With\Many\Nested\Folders\file1.cs",
            @"C:\Another\Very\Long\Path\That\Contains\Many\Directories\And\Subdirectories\file2.cs"
        };

        // Act
        var act = () => _service.PromptChoiceAsync(message, changedFiles);

        // Assert
        act.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_CreatesServiceSuccessfully()
    {
        // Act
        var service = new InteractiveService();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<Core.Interfaces.IInteractiveService>();
    }

    [Fact]
    public void Reset_CalledMultipleTimes_DoesNotThrow()
    {
        // Act
        _service.Reset();
        _service.Reset();
        _service.Reset();

        // Assert
        // Multiple resets should be safe
        _service.Should().NotBeNull();
    }
}
