namespace NetLift.Validation;

using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Runs tests on .NET projects and solutions using dotnet test with TRX output parsing.
/// </summary>
public class TestRunner : ITestRunner
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Runs tests for a solution or project using dotnet test.
    /// </summary>
    /// <param name="solutionOrProjectPath">The absolute path to the solution (.sln) or project (.csproj) file.</param>
    /// <param name="options">Optional configuration for test execution.</param>
    /// <param name="cancellationToken">Cancellation token to abort the test run.</param>
    /// <returns>A task that completes with the test execution results.</returns>
    public async Task<TestResult> RunTestsAsync(
        string solutionOrProjectPath,
        TestRunnerOptions? options = null,
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

        options ??= new TestRunnerOptions();

        var stopwatch = Stopwatch.StartNew();
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Generate unique TRX file name in temp directory
        var trxFileName = $"test-results-{Guid.NewGuid():N}.trx";
        var testResultsDir = Path.Combine(Path.GetTempPath(), "NetLift", "TestResults");
        Directory.CreateDirectory(testResultsDir);
        var trxPath = Path.Combine(testResultsDir, trxFileName);

        try
        {
            // Build dotnet test arguments
            var arguments = BuildTestArguments(solutionOrProjectPath, trxFileName, testResultsDir, options);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
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

            // Wait for process with timeout
            var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var processTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw new TimeoutException($"Test operation timed out after {options.TimeoutSeconds} seconds.");
            }

            stopwatch.Stop();

            var rawOutput = outputBuilder.ToString() + errorBuilder.ToString();

            // Parse TRX file if it exists
            TestResult result;
            if (File.Exists(trxPath))
            {
                result = ParseTrxFile(trxPath, process.ExitCode, stopwatch.Elapsed, rawOutput);
            }
            else
            {
                // Fallback if TRX file wasn't generated (e.g., no tests found)
                result = new TestResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    Duration = stopwatch.Elapsed,
                    TotalTests = 0,
                    PassedTests = 0,
                    FailedTests = 0,
                    SkippedTests = 0,
                    Failures = [],
                    RawOutput = rawOutput
                };
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            // Cleanup TRX file
            if (File.Exists(trxPath))
            {
                try
                {
                    File.Delete(trxPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Builds the command-line arguments for dotnet test.
    /// </summary>
    private static string BuildTestArguments(
        string solutionOrProjectPath,
        string trxFileName,
        string testResultsDir,
        TestRunnerOptions options)
    {
        var args = new StringBuilder();
        args.Append($"test \"{solutionOrProjectPath}\"");
        args.Append($" --logger \"trx;LogFileName={trxFileName}\"");
        args.Append($" --results-directory \"{testResultsDir}\"");
        args.Append($" --configuration {options.Configuration}");
        args.Append(" --no-build");

        if (options.StopOnFirstFailure)
        {
            args.Append(" -- RunConfiguration.StopOnFailure=true");
        }

        if (!string.IsNullOrWhiteSpace(options.Filter))
        {
            args.Append($" --filter \"{options.Filter}\"");
        }

        return args.ToString();
    }

    /// <summary>
    /// Parses a TRX (Test Results XML) file to extract test results.
    /// </summary>
    /// <param name="trxPath">Path to the TRX file.</param>
    /// <param name="exitCode">Exit code from the test process.</param>
    /// <param name="duration">Duration of the test run.</param>
    /// <param name="rawOutput">Raw console output from the test run.</param>
    /// <returns>Parsed test result.</returns>
    public static TestResult ParseTrxFile(string trxPath, int exitCode, TimeSpan duration, string rawOutput)
    {
        try
        {
            var doc = XDocument.Load(trxPath);
            var ns = XNamespace.Get(TrxNamespace);

            // Parse test result summary from Counters element
            var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
            var total = ParseInt(counters?.Attribute("total")?.Value);
            var passed = ParseInt(counters?.Attribute("passed")?.Value);
            var failed = ParseInt(counters?.Attribute("failed")?.Value);
            var skipped = ParseInt(counters?.Attribute("notExecuted")?.Value);

            // Parse individual test failures
            var failures = ParseTestFailures(doc, ns);

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
        catch (Exception ex)
        {
            // If TRX parsing fails, return a basic result
            return new TestResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                Duration = duration,
                TotalTests = 0,
                PassedTests = 0,
                FailedTests = 0,
                SkippedTests = 0,
                Failures = [],
                RawOutput = rawOutput + $"\n\nTRX Parsing Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses test failure details from TRX XML.
    /// </summary>
    private static IReadOnlyList<TestFailure> ParseTestFailures(XDocument doc, XNamespace ns)
    {
        var failures = new List<TestFailure>();

        // Get test definitions for mapping execution IDs to test names
        var testDefinitions = doc.Descendants(ns + "UnitTest")
            .ToDictionary(
                t => t.Attribute("id")?.Value ?? "",
                t => new
                {
                    TestName = t.Descendants(ns + "TestMethod")
                        .FirstOrDefault()?.Attribute("name")?.Value ?? "Unknown",
                    ClassName = t.Descendants(ns + "TestMethod")
                        .FirstOrDefault()?.Attribute("className")?.Value ?? "Unknown"
                }
            );

        // Parse failed test results
        var failedResults = doc.Descendants(ns + "UnitTestResult")
            .Where(r => r.Attribute("outcome")?.Value == "Failed");

        foreach (var result in failedResults)
        {
            var testId = result.Attribute("testId")?.Value ?? "";
            var durationStr = result.Attribute("duration")?.Value;
            var testDuration = ParseDuration(durationStr);

            // Get test name and class from definition
            var testDef = testDefinitions.GetValueOrDefault(testId);
            var testName = result.Attribute("testName")?.Value ?? testDef?.TestName ?? "Unknown";
            var className = testDef?.ClassName ?? "Unknown";

            // Extract error message and stack trace
            var output = result.Descendants(ns + "Output").FirstOrDefault();
            var errorInfo = output?.Descendants(ns + "ErrorInfo").FirstOrDefault();
            var message = errorInfo?.Element(ns + "Message")?.Value ?? "No error message";
            var stackTrace = errorInfo?.Element(ns + "StackTrace")?.Value;

            failures.Add(new TestFailure
            {
                TestName = testName,
                ClassName = className,
                ErrorMessage = message.Trim(),
                StackTrace = stackTrace?.Trim(),
                Duration = testDuration
            });
        }

        return failures;
    }

    /// <summary>
    /// Parses an integer from a string, returning 0 if parsing fails.
    /// </summary>
    private static int ParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    /// <summary>
    /// Parses a TimeSpan from TRX duration format (HH:MM:SS.mmmmmmm).
    /// </summary>
    private static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.TryParse(value, out var result) ? result : TimeSpan.Zero;
    }
}
