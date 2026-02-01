# [TASK-061] Implement Build Validator

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | M |
| **Sprint** | 7 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-050 (Migration Executor)
- **Blocks:** TASK-063, TASK-064

---

## Description

Implement a build validator that runs `dotnet build` on migrated projects and captures compilation results, errors, and warnings. This validates that the migration produced compilable code.

---

## Acceptance Criteria

- [ ] `IBuildValidator` interface created
- [ ] Executes `dotnet build` on migrated solution/projects
- [ ] Captures exit code, stdout, and stderr
- [ ] Parses MSBuild errors and warnings
- [ ] Returns structured `BuildResult` with diagnostic info
- [ ] Handles build timeouts gracefully
- [ ] Unit tests with mocked process execution
- [ ] Integration test with actual build

---

## Technical Notes

### Interface:

```csharp
namespace NetLift.Validation;

public interface IBuildValidator
{
    Task<BuildResult> ValidateAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default);
}

public record BuildResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<BuildDiagnostic> Errors { get; init; } = [];
    public IReadOnlyList<BuildDiagnostic> Warnings { get; init; } = [];
    public string RawOutput { get; init; } = "";
}

public record BuildDiagnostic
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public DiagnosticSeverity Severity { get; init; }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}
```

### Implementation:

```csharp
public class BuildValidator : IBuildValidator
{
    private readonly ILogger<BuildValidator> _logger;
    private const int DefaultTimeoutSeconds = 300; // 5 minutes

    public async Task<BuildResult> ValidateAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
        {
            throw new FileNotFoundException(
                "Solution or project file not found",
                solutionOrProjectPath);
        }

        var startTime = DateTime.UtcNow;
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{solutionOrProjectPath}\" --no-incremental",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(solutionOrProjectPath)
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogDebug("Build output: {Line}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("Build error: {Line}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(DefaultTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Build exceeded timeout of {DefaultTimeoutSeconds}s");
        }

        var duration = DateTime.UtcNow - startTime;
        var rawOutput = outputBuilder.ToString();
        var diagnostics = ParseDiagnostics(rawOutput);

        return new BuildResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Duration = duration,
            Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList(),
            Warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList(),
            RawOutput = rawOutput
        };
    }

    private static List<BuildDiagnostic> ParseDiagnostics(string output)
    {
        var diagnostics = new List<BuildDiagnostic>();

        // Parse MSBuild diagnostic format:
        // path\file.cs(line,col): error CS1234: Message
        var pattern = @"^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.+)$";
        var regex = new Regex(pattern, RegexOptions.Multiline);

        foreach (Match match in regex.Matches(output))
        {
            diagnostics.Add(new BuildDiagnostic
            {
                File = match.Groups[1].Value.Trim(),
                Line = int.Parse(match.Groups[2].Value),
                Column = int.Parse(match.Groups[3].Value),
                Severity = match.Groups[4].Value.ToLowerInvariant() == "error"
                    ? DiagnosticSeverity.Error
                    : DiagnosticSeverity.Warning,
                Code = match.Groups[5].Value,
                Message = match.Groups[6].Value.Trim()
            });
        }

        return diagnostics;
    }
}
```

### Usage in migrate command:

```csharp
var buildResult = await _buildValidator.ValidateAsync(migratedSolutionPath);

if (!buildResult.Success)
{
    AnsiConsole.MarkupLine("[red]Build failed after migration[/]");

    var table = new Table();
    table.AddColumn("File");
    table.AddColumn("Line");
    table.AddColumn("Code");
    table.AddColumn("Message");

    foreach (var error in buildResult.Errors.Take(10))
    {
        table.AddRow(
            Path.GetFileName(error.File),
            error.Line.ToString(),
            error.Code,
            error.Message.EscapeMarkup());
    }

    AnsiConsole.Write(table);

    if (buildResult.Errors.Count > 10)
    {
        AnsiConsole.MarkupLine(
            $"[yellow]... and {buildResult.Errors.Count - 10} more errors[/]");
    }
}
else
{
    AnsiConsole.MarkupLine(
        $"[green]✓[/] Build succeeded in {buildResult.Duration.TotalSeconds:F1}s");
}
```

### Unit tests:

```csharp
public class BuildValidatorTests
{
    [Fact]
    public async Task ValidateAsync_SuccessfulBuild_ReturnsSuccess()
    {
        // Test with actual minimal .csproj that builds
        var projectPath = CreateTestProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var validator = new BuildValidator(NullLogger<BuildValidator>.Instance);
        var result = await validator.ValidateAsync(projectPath);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_CompilationError_ReturnsFailureWithDiagnostics()
    {
        // Test with project containing syntax error
        var projectPath = CreateTestProjectWithError();

        var validator = new BuildValidator(NullLogger<BuildValidator>.Instance);
        var result = await validator.ValidateAsync(projectPath);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ParseDiagnostics_ExtractsFileLineColumn()
    {
        var output = """
            Program.cs(10,5): error CS0103: The name 'invalid' does not exist
            Helper.cs(25,12): warning CS0219: Variable is assigned but never used
            """;

        // Test diagnostic parsing logic
        var diagnostics = BuildValidator.ParseDiagnostics(output);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal("Program.cs", diagnostics[0].File);
        Assert.Equal(10, diagnostics[0].Line);
        Assert.Equal(5, diagnostics[0].Column);
        Assert.Equal("CS0103", diagnostics[0].Code);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
