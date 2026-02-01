# [TASK-062] Implement Test Runner

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
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

Implement a test runner that executes `dotnet test` on migrated test projects and reports test results. This validates that existing tests still pass after migration.

---

## Acceptance Criteria

- [ ] `ITestRunner` interface created
- [ ] Executes `dotnet test` with appropriate options
- [ ] Parses test results (TRX format)
- [ ] Returns structured `TestResult` with pass/fail counts
- [ ] Handles projects without tests gracefully
- [ ] Supports test timeout configuration
- [ ] Unit tests with mocked test execution
- [ ] Integration test with real test project

---

## Technical Notes

### Interface:

```csharp
namespace NetLift.Validation;

public interface ITestRunner
{
    Task<TestResult> RunTestsAsync(
        string solutionOrProjectPath,
        TestRunnerOptions? options = null,
        CancellationToken cancellationToken = default);
}

public record TestRunnerOptions
{
    public int TimeoutSeconds { get; init; } = 600; // 10 minutes
    public bool StopOnFirstFailure { get; init; } = false;
    public string? Filter { get; init; }
    public string? Configuration { get; init; } = "Debug";
}

public record TestResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public TimeSpan Duration { get; init; }
    public int TotalTests { get; init; }
    public int PassedTests { get; init; }
    public int FailedTests { get; init; }
    public int SkippedTests { get; init; }
    public IReadOnlyList<TestFailure> Failures { get; init; } = [];
    public string RawOutput { get; init; } = "";
}

public record TestFailure
{
    public string TestName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
    public string? StackTrace { get; init; }
    public TimeSpan Duration { get; init; }
}
```

### Implementation:

```csharp
public class TestRunner : ITestRunner
{
    private readonly ILogger<TestRunner> _logger;

    public async Task<TestResult> RunTestsAsync(
        string solutionOrProjectPath,
        TestRunnerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TestRunnerOptions();

        if (!File.Exists(solutionOrProjectPath))
        {
            throw new FileNotFoundException(
                "Solution or project file not found",
                solutionOrProjectPath);
        }

        var startTime = DateTime.UtcNow;
        var trxPath = Path.Combine(
            Path.GetTempPath(),
            $"netlift-test-{Guid.NewGuid()}.trx");

        var args = new List<string>
        {
            "test",
            $"\"{solutionOrProjectPath}\"",
            $"--logger:\"trx;LogFileName={trxPath}\"",
            "--no-build",
            $"--configuration {options.Configuration}"
        };

        if (options.Filter != null)
        {
            args.Add($"--filter \"{options.Filter}\"");
        }

        if (options.StopOnFirstFailure)
        {
            args.Add("--blame-hang-timeout 30s");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(solutionOrProjectPath)
        };

        var outputBuilder = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogDebug("Test output: {Line}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Tests exceeded timeout of {options.TimeoutSeconds}s");
        }

        var duration = DateTime.UtcNow - startTime;
        var rawOutput = outputBuilder.ToString();

        // Parse TRX file if it exists
        TestResult result;
        if (File.Exists(trxPath))
        {
            result = ParseTrxFile(trxPath, process.ExitCode, duration, rawOutput);

            // Cleanup
            try { File.Delete(trxPath); } catch { /* Best effort */ }
        }
        else
        {
            // No tests found or test discovery failed
            result = new TestResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Duration = duration,
                RawOutput = rawOutput
            };
        }

        return result;
    }

    private static TestResult ParseTrxFile(
        string trxPath,
        int exitCode,
        TimeSpan duration,
        string rawOutput)
    {
        var doc = XDocument.Load(trxPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
        var total = int.Parse(counters?.Attribute("total")?.Value ?? "0");
        var passed = int.Parse(counters?.Attribute("passed")?.Value ?? "0");
        var failed = int.Parse(counters?.Attribute("failed")?.Value ?? "0");
        var skipped = int.Parse(counters?.Attribute("notExecuted")?.Value ?? "0");

        var failures = new List<TestFailure>();

        foreach (var result in doc.Descendants(ns + "UnitTestResult")
            .Where(r => r.Attribute("outcome")?.Value == "Failed"))
        {
            var testName = result.Attribute("testName")?.Value ?? "";
            var className = result.Descendants(ns + "ClassName")
                .FirstOrDefault()?.Value ?? "";
            var errorMessage = result.Descendants(ns + "Message")
                .FirstOrDefault()?.Value ?? "";
            var stackTrace = result.Descendants(ns + "StackTrace")
                .FirstOrDefault()?.Value;
            var durationAttr = result.Attribute("duration")?.Value;
            var testDuration = durationAttr != null
                ? TimeSpan.Parse(durationAttr)
                : TimeSpan.Zero;

            failures.Add(new TestFailure
            {
                TestName = testName,
                ClassName = className,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                Duration = testDuration
            });
        }

        return new TestResult
        {
            Success = exitCode == 0 && failed == 0,
            ExitCode = exitCode,
            Duration = duration,
            TotalTests = total,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            Failures = failures,
            RawOutput = rawOutput
        };
    }
}
```

### Usage in migrate command:

```csharp
var testResult = await _testRunner.RunTestsAsync(
    migratedSolutionPath,
    new TestRunnerOptions { TimeoutSeconds = 300 });

if (testResult.TotalTests == 0)
{
    AnsiConsole.MarkupLine("[yellow]No tests found[/]");
}
else if (testResult.Success)
{
    AnsiConsole.MarkupLine(
        $"[green]✓[/] All {testResult.PassedTests} tests passed " +
        $"in {testResult.Duration.TotalSeconds:F1}s");
}
else
{
    AnsiConsole.MarkupLine(
        $"[red]✗[/] {testResult.FailedTests} of {testResult.TotalTests} tests failed");

    foreach (var failure in testResult.Failures.Take(5))
    {
        AnsiConsole.MarkupLine($"\n[red]FAIL:[/] {failure.TestName}");
        AnsiConsole.MarkupLine($"  {failure.ErrorMessage.EscapeMarkup()}");
    }

    if (testResult.Failures.Count > 5)
    {
        AnsiConsole.MarkupLine(
            $"\n[yellow]... and {testResult.Failures.Count - 5} more failures[/]");
    }
}
```

### Unit tests:

```csharp
public class TestRunnerTests
{
    [Fact]
    public async Task RunTestsAsync_AllTestsPass_ReturnsSuccess()
    {
        // Create test project with passing tests
        var projectPath = CreateTestProjectWithPassingTests();

        var runner = new TestRunner(NullLogger<TestRunner>.Instance);
        var result = await runner.RunTestsAsync(projectPath);

        Assert.True(result.Success);
        Assert.True(result.TotalTests > 0);
        Assert.Equal(result.TotalTests, result.PassedTests);
        Assert.Equal(0, result.FailedTests);
    }

    [Fact]
    public async Task RunTestsAsync_TestFailures_ReturnsFailuresWithDetails()
    {
        var projectPath = CreateTestProjectWithFailures();

        var runner = new TestRunner(NullLogger<TestRunner>.Instance);
        var result = await runner.RunTestsAsync(projectPath);

        Assert.False(result.Success);
        Assert.True(result.FailedTests > 0);
        Assert.NotEmpty(result.Failures);
        Assert.All(result.Failures, f =>
        {
            Assert.NotEmpty(f.TestName);
            Assert.NotEmpty(f.ErrorMessage);
        });
    }

    [Fact]
    public async Task RunTestsAsync_NoTests_ReturnsZeroTests()
    {
        var projectPath = CreateProjectWithoutTests();

        var runner = new TestRunner(NullLogger<TestRunner>.Instance);
        var result = await runner.RunTestsAsync(projectPath);

        Assert.Equal(0, result.TotalTests);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
