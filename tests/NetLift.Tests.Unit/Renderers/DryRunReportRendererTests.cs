using NetLift.Cli.Renderers;
using NetLift.Core.Models;
using Xunit;

namespace NetLift.Tests.Unit.Renderers;

/// <summary>
/// Unit tests for the DryRunReportRenderer.
/// </summary>
public sealed class DryRunReportRendererTests
{
    [Fact]
    public async Task WriteToFileAsync_CreatesValidMarkdownReport()
    {
        // Arrange
        var renderer = new DryRunReportRenderer();
        var report = CreateSampleReport();
        var outputPath = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid()}.md");

        try
        {
            // Act
            await renderer.WriteToFileAsync(report, outputPath);

            // Assert
            Assert.True(File.Exists(outputPath));
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("# NetLift Dry Run Report", content);
            Assert.Contains("## Summary", content);
            Assert.Contains("## File Changes", content);
            Assert.Contains("Files to create: 1", content);
            Assert.Contains("Files to modify: 1", content);
            Assert.Contains("Files to delete: 1", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task WriteToFileAsync_IncludesWarningsAndErrors()
    {
        // Arrange
        var renderer = new DryRunReportRenderer();
        var report = new DryRunReport
        {
            WouldSucceed = false,
            Warnings = new List<string> { "Warning 1", "Warning 2" },
            Errors = new List<string> { "Error 1" },
            Summary = new DryRunSummary
            {
                WarningCount = 2,
                ErrorCount = 1
            }
        };
        var outputPath = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid()}.md");

        try
        {
            // Act
            await renderer.WriteToFileAsync(report, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("## Warnings", content);
            Assert.Contains("Warning 1", content);
            Assert.Contains("Warning 2", content);
            Assert.Contains("## Errors", content);
            Assert.Contains("Error 1", content);
            Assert.Contains("Migration would fail", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task WriteToFileAsync_IncludesFileDiffs()
    {
        // Arrange
        var renderer = new DryRunReportRenderer();
        var report = new DryRunReport
        {
            WouldSucceed = true,
            FileDiffs = new List<FileDiff>
            {
                new FileDiff
                {
                    FilePath = "test.csproj",
                    ChangeType = ChangeType.Modify,
                    Hunks = new List<DiffHunk>
                    {
                        new DiffHunk
                        {
                            OldStart = 1,
                            OldCount = 3,
                            NewStart = 1,
                            NewCount = 4,
                            Lines = new List<DiffLine>
                            {
                                new DiffLine { Type = DiffLineType.Context, Content = "Line 1" },
                                new DiffLine { Type = DiffLineType.Deletion, Content = "Line 2" },
                                new DiffLine { Type = DiffLineType.Addition, Content = "Line 2 Modified" },
                                new DiffLine { Type = DiffLineType.Context, Content = "Line 3" }
                            }
                        }
                    }
                }
            },
            Summary = new DryRunSummary { FilesToModify = 1, TotalFilesAffected = 1 }
        };
        var outputPath = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid()}.md");

        try
        {
            // Act
            await renderer.WriteToFileAsync(report, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("### MODIFY: test.csproj", content);
            Assert.Contains("@@ -1,3 +1,4 @@", content);
            Assert.Contains(" Line 1", content);
            Assert.Contains("-Line 2", content);
            Assert.Contains("+Line 2 Modified", content);
            Assert.Contains(" Line 3", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task WriteToFileAsync_HandlesEmptyReport()
    {
        // Arrange
        var renderer = new DryRunReportRenderer();
        var report = new DryRunReport
        {
            WouldSucceed = true,
            Summary = new DryRunSummary()
        };
        var outputPath = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid()}.md");

        try
        {
            // Act
            await renderer.WriteToFileAsync(report, outputPath);

            // Assert
            Assert.True(File.Exists(outputPath));
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("# NetLift Dry Run Report", content);
            Assert.Contains("Migration would complete successfully", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static DryRunReport CreateSampleReport()
    {
        return new DryRunReport
        {
            WouldSucceed = true,
            FileDiffs = new List<FileDiff>
            {
                new FileDiff
                {
                    FilePath = "new.csproj",
                    ChangeType = ChangeType.Create,
                    Preview = "New file with 10 lines"
                },
                new FileDiff
                {
                    FilePath = "existing.csproj",
                    ChangeType = ChangeType.Modify,
                    Preview = "+5 -2 lines"
                },
                new FileDiff
                {
                    FilePath = "packages.config",
                    ChangeType = ChangeType.Delete,
                    Preview = "Delete file with 20 lines"
                }
            },
            Warnings = new List<string> { "Sample warning" },
            Summary = new DryRunSummary
            {
                FilesToCreate = 1,
                FilesToModify = 1,
                FilesToDelete = 1,
                TotalFilesAffected = 3,
                WarningCount = 1,
                ErrorCount = 0
            }
        };
    }
}
