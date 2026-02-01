using NetLift.Core.Models;
using NetLift.Core.Services;
using Xunit;

namespace NetLift.Tests.Unit.Services;

/// <summary>
/// Unit tests for the DryRunService.
/// </summary>
public sealed class DryRunServiceTests
{
    [Fact]
    public void RecordChange_WithPreview_AddsFileDiff()
    {
        // Arrange
        var service = new DryRunService();
        var filePath = "test.csproj";
        var changeType = ChangeType.Modify;
        var preview = "Test preview";

        // Act
        service.RecordChange(filePath, changeType, preview);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.FileDiffs);
        Assert.Equal(filePath, report.FileDiffs[0].FilePath);
        Assert.Equal(changeType, report.FileDiffs[0].ChangeType);
        Assert.Equal(preview, report.FileDiffs[0].Preview);
    }

    [Fact]
    public void RecordChange_WithContent_GeneratesDiff()
    {
        // Arrange
        var service = new DryRunService();
        var filePath = "test.csproj";
        var originalContent = "Line 1\nLine 2\nLine 3";
        var newContent = "Line 1\nLine 2 Modified\nLine 3\nLine 4";

        // Act
        service.RecordChange(filePath, ChangeType.Modify, originalContent, newContent);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.FileDiffs);
        var diff = report.FileDiffs[0];
        Assert.Equal(filePath, diff.FilePath);
        Assert.Equal(ChangeType.Modify, diff.ChangeType);
        Assert.Equal(originalContent, diff.OriginalContent);
        Assert.Equal(newContent, diff.NewContent);
        Assert.NotEmpty(diff.Hunks);
    }

    [Fact]
    public void RecordChange_CreateNewFile_GeneratesCreateDiff()
    {
        // Arrange
        var service = new DryRunService();
        var filePath = "new.csproj";
        var newContent = "Line 1\nLine 2";

        // Act
        service.RecordChange(filePath, ChangeType.Create, null, newContent);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.FileDiffs);
        var diff = report.FileDiffs[0];
        Assert.Equal(filePath, diff.FilePath);
        Assert.Equal(ChangeType.Create, diff.ChangeType);
        Assert.Null(diff.OriginalContent);
        Assert.Equal(newContent, diff.NewContent);
        Assert.Contains("2 lines", diff.Preview);
    }

    [Fact]
    public void RecordChange_DeleteFile_GeneratesDeleteDiff()
    {
        // Arrange
        var service = new DryRunService();
        var filePath = "old.config";
        var originalContent = "Line 1\nLine 2\nLine 3";

        // Act
        service.RecordChange(filePath, ChangeType.Delete, originalContent, null);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.FileDiffs);
        var diff = report.FileDiffs[0];
        Assert.Equal(filePath, diff.FilePath);
        Assert.Equal(ChangeType.Delete, diff.ChangeType);
        Assert.Equal(originalContent, diff.OriginalContent);
        Assert.Null(diff.NewContent);
        Assert.Contains("3 lines", diff.Preview);
    }

    [Fact]
    public void RecordWarning_AddsWarning()
    {
        // Arrange
        var service = new DryRunService();
        var warning = "Test warning";

        // Act
        service.RecordWarning(warning);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.Warnings);
        Assert.Equal(warning, report.Warnings[0]);
        Assert.Equal(1, report.Summary.WarningCount);
    }

    [Fact]
    public void RecordError_AddsError()
    {
        // Arrange
        var service = new DryRunService();
        var error = "Test error";

        // Act
        service.RecordError(error);
        var report = service.GetReport();

        // Assert
        Assert.Single(report.Errors);
        Assert.Equal(error, report.Errors[0]);
        Assert.Equal(1, report.Summary.ErrorCount);
        Assert.False(report.WouldSucceed);
    }

    [Fact]
    public void GetReport_CalculatesSummaryCorrectly()
    {
        // Arrange
        var service = new DryRunService();

        // Act
        service.RecordChange("create.txt", ChangeType.Create, "new file");
        service.RecordChange("modify.txt", ChangeType.Modify, "modified");
        service.RecordChange("delete.txt", ChangeType.Delete, "deleted");
        service.RecordChange("backup.txt", ChangeType.Backup, "backup");
        service.RecordWarning("Warning 1");
        service.RecordWarning("Warning 2");

        var report = service.GetReport();

        // Assert
        Assert.Equal(1, report.Summary.FilesToCreate);
        Assert.Equal(1, report.Summary.FilesToModify);
        Assert.Equal(1, report.Summary.FilesToDelete);
        Assert.Equal(1, report.Summary.FilesToBackup);
        Assert.Equal(4, report.Summary.TotalFilesAffected);
        Assert.Equal(2, report.Summary.WarningCount);
        Assert.Equal(0, report.Summary.ErrorCount);
        Assert.True(report.WouldSucceed);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        var service = new DryRunService();
        service.RecordChange("test.txt", ChangeType.Modify, "test");
        service.RecordWarning("warning");
        service.RecordError("error");

        // Act
        service.Reset();
        var report = service.GetReport();

        // Assert
        Assert.Empty(report.FileDiffs);
        Assert.Empty(report.Warnings);
        Assert.Empty(report.Errors);
        Assert.Equal(0, report.Summary.TotalFilesAffected);
    }

    [Fact]
    public void DiffGeneration_HandlesIdenticalContent()
    {
        // Arrange
        var service = new DryRunService();
        var content = "Line 1\nLine 2\nLine 3";

        // Act
        service.RecordChange("test.txt", ChangeType.Modify, content, content);
        var report = service.GetReport();

        // Assert
        var diff = report.FileDiffs[0];
        Assert.Empty(diff.Hunks);
        Assert.Equal("No changes", diff.Preview);
    }

    [Fact]
    public void DiffGeneration_HandlesAdditions()
    {
        // Arrange
        var service = new DryRunService();
        var original = "Line 1\nLine 2";
        var modified = "Line 1\nLine 2\nLine 3\nLine 4";

        // Act
        service.RecordChange("test.txt", ChangeType.Modify, original, modified);
        var report = service.GetReport();

        // Assert
        var diff = report.FileDiffs[0];
        Assert.NotEmpty(diff.Hunks);
        Assert.Contains("+2", diff.Preview);
    }

    [Fact]
    public void DiffGeneration_HandlesDeletions()
    {
        // Arrange
        var service = new DryRunService();
        var original = "Line 1\nLine 2\nLine 3\nLine 4";
        var modified = "Line 1\nLine 2";

        // Act
        service.RecordChange("test.txt", ChangeType.Modify, original, modified);
        var report = service.GetReport();

        // Assert
        var diff = report.FileDiffs[0];
        Assert.NotEmpty(diff.Hunks);
        Assert.Contains("-2", diff.Preview);
    }

    [Fact]
    public void DiffGeneration_HandlesMixedChanges()
    {
        // Arrange
        var service = new DryRunService();
        var original = "Line 1\nLine 2\nLine 3";
        var modified = "Line 1\nLine 2 Modified\nLine 3\nLine 4";

        // Act
        service.RecordChange("test.txt", ChangeType.Modify, original, modified);
        var report = service.GetReport();

        // Assert
        var diff = report.FileDiffs[0];
        Assert.NotEmpty(diff.Hunks);
        var hunk = diff.Hunks[0];
        Assert.Contains(hunk.Lines, l => l.Type == DiffLineType.Addition);
        Assert.Contains(hunk.Lines, l => l.Type == DiffLineType.Deletion);
        Assert.Contains(hunk.Lines, l => l.Type == DiffLineType.Context);
    }

    [Fact]
    public void MultipleChanges_PreservesOrder()
    {
        // Arrange
        var service = new DryRunService();

        // Act
        service.RecordChange("file1.txt", ChangeType.Create, "content1");
        service.RecordChange("file2.txt", ChangeType.Modify, "content2");
        service.RecordChange("file3.txt", ChangeType.Delete, "content3");

        var report = service.GetReport();

        // Assert
        Assert.Equal(3, report.FileDiffs.Count);
        Assert.Equal("file1.txt", report.FileDiffs[0].FilePath);
        Assert.Equal("file2.txt", report.FileDiffs[1].FilePath);
        Assert.Equal("file3.txt", report.FileDiffs[2].FilePath);
    }
}
