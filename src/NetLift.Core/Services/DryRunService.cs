using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Core.Services;

/// <summary>
/// Service for tracking and reporting changes during a dry-run migration.
/// </summary>
public sealed class DryRunService : IDryRunService
{
    private readonly List<FileDiff> _fileDiffs = new();
    private readonly List<string> _warnings = new();
    private readonly List<string> _errors = new();

    /// <inheritdoc />
    public void RecordChange(string filePath, ChangeType changeType, string preview)
    {
        _fileDiffs.Add(new FileDiff
        {
            FilePath = filePath,
            ChangeType = changeType,
            Preview = preview
        });
    }

    /// <inheritdoc />
    public void RecordChange(string filePath, ChangeType changeType, string? originalContent, string? newContent)
    {
        var fileDiff = new FileDiff
        {
            FilePath = filePath,
            ChangeType = changeType,
            OriginalContent = originalContent,
            NewContent = newContent
        };

        // Generate diff hunks if both contents are provided
        if (originalContent != null && newContent != null)
        {
            fileDiff.Hunks = GenerateDiffHunks(originalContent, newContent);
            fileDiff.Preview = GeneratePreview(fileDiff.Hunks);
        }
        else if (newContent != null)
        {
            fileDiff.Preview = $"New file with {newContent.Split('\n').Length} lines";
        }
        else if (originalContent != null)
        {
            fileDiff.Preview = $"Delete file with {originalContent.Split('\n').Length} lines";
        }

        _fileDiffs.Add(fileDiff);
    }

    /// <inheritdoc />
    public void RecordWarning(string warning)
    {
        _warnings.Add(warning);
    }

    /// <inheritdoc />
    public void RecordError(string error)
    {
        _errors.Add(error);
    }

    /// <inheritdoc />
    public DryRunReport GetReport()
    {
        var summary = new DryRunSummary
        {
            FilesToCreate = _fileDiffs.Count(d => d.ChangeType == ChangeType.Create),
            FilesToModify = _fileDiffs.Count(d => d.ChangeType == ChangeType.Modify),
            FilesToDelete = _fileDiffs.Count(d => d.ChangeType == ChangeType.Delete),
            FilesToBackup = _fileDiffs.Count(d => d.ChangeType == ChangeType.Backup),
            TotalFilesAffected = _fileDiffs.Count,
            WarningCount = _warnings.Count,
            ErrorCount = _errors.Count
        };

        return new DryRunReport
        {
            WouldSucceed = _errors.Count == 0,
            FileDiffs = new List<FileDiff>(_fileDiffs),
            Warnings = new List<string>(_warnings),
            Errors = new List<string>(_errors),
            Summary = summary
        };
    }

    /// <inheritdoc />
    public void Reset()
    {
        _fileDiffs.Clear();
        _warnings.Clear();
        _errors.Clear();
    }

    /// <summary>
    /// Generates diff hunks between original and new content.
    /// </summary>
    private static List<DiffHunk> GenerateDiffHunks(string originalContent, string newContent)
    {
        var oldLines = originalContent.Split('\n');
        var newLines = newContent.Split('\n');
        var hunks = new List<DiffHunk>();

        // Simple line-by-line diff algorithm
        var differences = ComputeDifferences(oldLines, newLines);

        if (differences.Count == 0)
        {
            return hunks;
        }

        // Group consecutive changes into hunks
        var currentHunk = new DiffHunk();
        var hunkLines = new List<DiffLine>();
        var oldLineNum = 1;
        var newLineNum = 1;
        var inHunk = false;
        var contextBefore = 3;
        var contextAfter = 3;

        for (int i = 0; i < differences.Count; i++)
        {
            var diff = differences[i];

            if (diff.Type != DiffLineType.Context)
            {
                if (!inHunk)
                {
                    // Start new hunk
                    currentHunk = new DiffHunk
                    {
                        OldStart = Math.Max(1, diff.OldLineNumber ?? oldLineNum - contextBefore),
                        NewStart = Math.Max(1, diff.NewLineNumber ?? newLineNum - contextBefore)
                    };
                    inHunk = true;

                    // Add context lines before
                    var startIndex = Math.Max(0, i - contextBefore);
                    for (int j = startIndex; j < i; j++)
                    {
                        hunkLines.Add(differences[j]);
                    }
                }

                hunkLines.Add(diff);
            }
            else if (inHunk)
            {
                hunkLines.Add(diff);

                // Check if this is the last context line or if next line is a change
                var isLastLine = i == differences.Count - 1;
                var nextIsChange = !isLastLine && differences[i + 1].Type != DiffLineType.Context;
                var contextCount = 0;

                // Count consecutive context lines
                for (int j = i; j < differences.Count && differences[j].Type == DiffLineType.Context; j++)
                {
                    contextCount++;
                }

                if (contextCount > contextAfter && !nextIsChange)
                {
                    // End current hunk
                    currentHunk.Lines = new List<DiffLine>(hunkLines);
                    currentHunk.OldCount = hunkLines.Count(l => l.Type != DiffLineType.Addition);
                    currentHunk.NewCount = hunkLines.Count(l => l.Type != DiffLineType.Deletion);
                    hunks.Add(currentHunk);
                    hunkLines.Clear();
                    inHunk = false;
                }
            }

            if (diff.OldLineNumber.HasValue) oldLineNum = diff.OldLineNumber.Value + 1;
            if (diff.NewLineNumber.HasValue) newLineNum = diff.NewLineNumber.Value + 1;
        }

        // Add final hunk if still building one
        if (inHunk && hunkLines.Count > 0)
        {
            currentHunk.Lines = new List<DiffLine>(hunkLines);
            currentHunk.OldCount = hunkLines.Count(l => l.Type != DiffLineType.Addition);
            currentHunk.NewCount = hunkLines.Count(l => l.Type != DiffLineType.Deletion);
            hunks.Add(currentHunk);
        }

        return hunks;
    }

    /// <summary>
    /// Computes line-by-line differences using a simple LCS-based algorithm.
    /// </summary>
    private static List<DiffLine> ComputeDifferences(string[] oldLines, string[] newLines)
    {
        var differences = new List<DiffLine>();
        var lcs = LongestCommonSubsequence(oldLines, newLines);

        int oldIndex = 0;
        int newIndex = 0;
        int lcsIndex = 0;

        while (oldIndex < oldLines.Length || newIndex < newLines.Length)
        {
            if (lcsIndex < lcs.Count &&
                oldIndex < oldLines.Length &&
                newIndex < newLines.Length &&
                oldLines[oldIndex] == lcs[lcsIndex] &&
                newLines[newIndex] == lcs[lcsIndex])
            {
                // Common line
                differences.Add(new DiffLine
                {
                    Type = DiffLineType.Context,
                    Content = oldLines[oldIndex],
                    OldLineNumber = oldIndex + 1,
                    NewLineNumber = newIndex + 1
                });
                oldIndex++;
                newIndex++;
                lcsIndex++;
            }
            else if (oldIndex < oldLines.Length &&
                     (lcsIndex >= lcs.Count || oldLines[oldIndex] != lcs[lcsIndex]))
            {
                // Deletion
                differences.Add(new DiffLine
                {
                    Type = DiffLineType.Deletion,
                    Content = oldLines[oldIndex],
                    OldLineNumber = oldIndex + 1,
                    NewLineNumber = null
                });
                oldIndex++;
            }
            else if (newIndex < newLines.Length)
            {
                // Addition
                differences.Add(new DiffLine
                {
                    Type = DiffLineType.Addition,
                    Content = newLines[newIndex],
                    OldLineNumber = null,
                    NewLineNumber = newIndex + 1
                });
                newIndex++;
            }
        }

        return differences;
    }

    /// <summary>
    /// Computes the longest common subsequence of two arrays.
    /// </summary>
    private static List<string> LongestCommonSubsequence(string[] a, string[] b)
    {
        int m = a.Length;
        int n = b.Length;
        var dp = new int[m + 1, n + 1];

        // Build LCS table
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        // Reconstruct LCS
        var lcs = new List<string>();
        int x = m, y = n;

        while (x > 0 && y > 0)
        {
            if (a[x - 1] == b[y - 1])
            {
                lcs.Insert(0, a[x - 1]);
                x--;
                y--;
            }
            else if (dp[x - 1, y] > dp[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }

        return lcs;
    }

    /// <summary>
    /// Generates a preview string from diff hunks.
    /// </summary>
    private static string GeneratePreview(List<DiffHunk> hunks)
    {
        var additions = hunks.SelectMany(h => h.Lines).Count(l => l.Type == DiffLineType.Addition);
        var deletions = hunks.SelectMany(h => h.Lines).Count(l => l.Type == DiffLineType.Deletion);

        if (additions > 0 && deletions > 0)
        {
            return $"+{additions} -{deletions} lines";
        }
        else if (additions > 0)
        {
            return $"+{additions} lines";
        }
        else if (deletions > 0)
        {
            return $"-{deletions} lines";
        }

        return "No changes";
    }
}
