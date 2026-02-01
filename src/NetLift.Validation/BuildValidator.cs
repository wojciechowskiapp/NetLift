namespace NetLift.Validation;

using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Validates migrated projects by executing dotnet build and parsing MSBuild output.
/// </summary>
public partial class BuildValidator : IBuildValidator
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    [GeneratedRegex(@"^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildDiagnosticRegex();

    /// <summary>
    /// Validates a solution or project by running dotnet build.
    /// </summary>
    /// <param name="solutionOrProjectPath">Path to the solution or project file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The build validation result.</returns>
    public async Task<BuildResult> ValidateAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(solutionOrProjectPath))
        {
            throw new ArgumentException("Solution or project path cannot be null or empty.", nameof(solutionOrProjectPath));
        }

        if (!File.Exists(solutionOrProjectPath))
        {
            throw new FileNotFoundException($"Solution or project file not found: {solutionOrProjectPath}", solutionOrProjectPath);
        }

        var stopwatch = Stopwatch.StartNew();
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{solutionOrProjectPath}\" --no-incremental",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            // Additional timeout check
            var timeoutTask = Task.Delay(DefaultTimeout, cancellationToken);
            var processTask = Task.Run(() =>
            {
                process.WaitForExit();
            }, cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw new TimeoutException($"Build operation timed out after {DefaultTimeout.TotalMinutes} minutes.");
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        stopwatch.Stop();

        var rawOutput = outputBuilder.ToString() + errorBuilder.ToString();
        var diagnostics = ParseDiagnostics(rawOutput);

        return new BuildResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Duration = stopwatch.Elapsed,
            Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList(),
            Warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList(),
            RawOutput = rawOutput
        };
    }

    /// <summary>
    /// Parses MSBuild diagnostics from build output.
    /// </summary>
    /// <param name="output">The raw build output.</param>
    /// <returns>List of parsed diagnostics.</returns>
    public static List<BuildDiagnostic> ParseDiagnostics(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var diagnostics = new List<BuildDiagnostic>();
        var regex = MsBuildDiagnosticRegex();
        var matches = regex.Matches(output);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 7)
            {
                var severityText = match.Groups[4].Value.ToLowerInvariant();
                var severity = severityText == "error"
                    ? DiagnosticSeverity.Error
                    : DiagnosticSeverity.Warning;

                diagnostics.Add(new BuildDiagnostic
                {
                    File = match.Groups[1].Value.Trim(),
                    Line = int.TryParse(match.Groups[2].Value, out var line) ? line : 0,
                    Column = int.TryParse(match.Groups[3].Value, out var column) ? column : 0,
                    Severity = severity,
                    Code = match.Groups[5].Value.Trim(),
                    Message = match.Groups[6].Value.Trim()
                });
            }
        }

        return diagnostics;
    }
}
