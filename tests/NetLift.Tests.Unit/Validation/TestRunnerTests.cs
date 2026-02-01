namespace NetLift.Tests.Unit.Validation;

using NetLift.Core.Models;
using NetLift.Validation;
using Xunit;
using FluentAssertions;
using System.Xml.Linq;

/// <summary>
/// Unit tests for <see cref="TestRunner"/>.
/// </summary>
public class TestRunnerTests
{
    [Fact]
    public void ParseTrxFile_WithAllPassingTests_ShouldParseCorrectly()
    {
        // Arrange
        var trxContent = CreateSampleTrx(total: 5, passed: 5, failed: 0, skipped: 0, failedTests: []);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 0, TimeSpan.FromSeconds(10), "test output");

            // Assert
            result.Success.Should().BeTrue();
            result.ExitCode.Should().Be(0);
            result.TotalTests.Should().Be(5);
            result.PassedTests.Should().Be(5);
            result.FailedTests.Should().Be(0);
            result.SkippedTests.Should().Be(0);
            result.Failures.Should().BeEmpty();
            result.Duration.Should().Be(TimeSpan.FromSeconds(10));
            result.RawOutput.Should().Be("test output");
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithFailedTests_ShouldParseCorrectly()
    {
        // Arrange
        var failedTests = new[]
        {
            new { TestId = "test-1", TestName = "TestMethod1", ClassName = "MyTests.UnitTests", Message = "Assert failed", StackTrace = "at MyTests.UnitTests.TestMethod1()" }
        };
        var trxContent = CreateSampleTrx(total: 3, passed: 2, failed: 1, skipped: 0, failedTests);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 1, TimeSpan.FromSeconds(15), "test output");

            // Assert
            result.Success.Should().BeFalse();
            result.ExitCode.Should().Be(1);
            result.TotalTests.Should().Be(3);
            result.PassedTests.Should().Be(2);
            result.FailedTests.Should().Be(1);
            result.SkippedTests.Should().Be(0);
            result.Failures.Should().HaveCount(1);
            result.Failures[0].TestName.Should().Be("TestMethod1");
            result.Failures[0].ClassName.Should().Be("MyTests.UnitTests");
            result.Failures[0].ErrorMessage.Should().Be("Assert failed");
            result.Failures[0].StackTrace.Should().Be("at MyTests.UnitTests.TestMethod1()");
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithSkippedTests_ShouldParseCorrectly()
    {
        // Arrange
        var trxContent = CreateSampleTrx(total: 10, passed: 7, failed: 0, skipped: 3, failedTests: []);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 0, TimeSpan.FromSeconds(20), "test output");

            // Assert
            result.Success.Should().BeTrue();
            result.TotalTests.Should().Be(10);
            result.PassedTests.Should().Be(7);
            result.FailedTests.Should().Be(0);
            result.SkippedTests.Should().Be(3);
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithMultipleFailures_ShouldParseAll()
    {
        // Arrange
        var failedTests = new[]
        {
            new { TestId = "test-1", TestName = "Test1", ClassName = "Class1", Message = "Error 1", StackTrace = "Stack 1" },
            new { TestId = "test-2", TestName = "Test2", ClassName = "Class2", Message = "Error 2", StackTrace = "Stack 2" },
            new { TestId = "test-3", TestName = "Test3", ClassName = "Class3", Message = "Error 3", StackTrace = "Stack 3" }
        };
        var trxContent = CreateSampleTrx(total: 5, passed: 2, failed: 3, skipped: 0, failedTests);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 1, TimeSpan.FromSeconds(30), "output");

            // Assert
            result.Failures.Should().HaveCount(3);
            result.Failures[0].TestName.Should().Be("Test1");
            result.Failures[0].ErrorMessage.Should().Be("Error 1");
            result.Failures[1].TestName.Should().Be("Test2");
            result.Failures[1].ErrorMessage.Should().Be("Error 2");
            result.Failures[2].TestName.Should().Be("Test3");
            result.Failures[2].ErrorMessage.Should().Be("Error 3");
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithNoTests_ShouldReturnZeroCounts()
    {
        // Arrange
        var trxContent = CreateSampleTrx(total: 0, passed: 0, failed: 0, skipped: 0, failedTests: []);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 0, TimeSpan.FromSeconds(1), "no tests");

            // Assert
            result.Success.Should().BeTrue();
            result.TotalTests.Should().Be(0);
            result.PassedTests.Should().Be(0);
            result.FailedTests.Should().Be(0);
            result.SkippedTests.Should().Be(0);
            result.Failures.Should().BeEmpty();
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithMissingCounters_ShouldReturnZeroCounts()
    {
        // Arrange
        var trxContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TestRun xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <Results>
  </Results>
</TestRun>";
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 0, TimeSpan.FromSeconds(1), "output");

            // Assert
            result.TotalTests.Should().Be(0);
            result.PassedTests.Should().Be(0);
            result.FailedTests.Should().Be(0);
            result.SkippedTests.Should().Be(0);
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithMalformedXml_ShouldReturnBasicResult()
    {
        // Arrange
        var trxContent = "This is not valid XML";
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 0, TimeSpan.FromSeconds(5), "output");

            // Assert
            result.ExitCode.Should().Be(0);
            result.TotalTests.Should().Be(0);
            result.RawOutput.Should().Contain("TRX Parsing Error");
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithFailureWithoutStackTrace_ShouldHandleGracefully()
    {
        // Arrange
        var trxContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TestRun xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <TestDefinitions>
    <UnitTest id=""test-1"">
      <TestMethod name=""TestMethod1"" className=""MyTests.UnitTests"" />
    </UnitTest>
  </TestDefinitions>
  <Results>
    <UnitTestResult testId=""test-1"" testName=""TestMethod1"" outcome=""Failed"" duration=""00:00:01.5000000"">
      <Output>
        <ErrorInfo>
          <Message>Test failed without stack trace</Message>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
  </Results>
  <ResultSummary>
    <Counters total=""1"" passed=""0"" failed=""1"" notExecuted=""0"" />
  </ResultSummary>
</TestRun>";
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 1, TimeSpan.FromSeconds(2), "output");

            // Assert
            result.Failures.Should().HaveCount(1);
            result.Failures[0].ErrorMessage.Should().Be("Test failed without stack trace");
            result.Failures[0].StackTrace.Should().BeNull();
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public void ParseTrxFile_WithWhitespaceInErrorMessage_ShouldTrim()
    {
        // Arrange
        var failedTests = new[]
        {
            new { TestId = "test-1", TestName = "Test1", ClassName = "Class1", Message = "  \n  Error message  \n  ", StackTrace = "  \n  Stack trace  \n  " }
        };
        var trxContent = CreateSampleTrx(total: 1, passed: 0, failed: 1, skipped: 0, failedTests);
        var trxPath = WriteTempTrxFile(trxContent);

        try
        {
            // Act
            var result = TestRunner.ParseTrxFile(trxPath, exitCode: 1, TimeSpan.FromSeconds(1), "output");

            // Assert
            result.Failures[0].ErrorMessage.Should().Be("Error message");
            result.Failures[0].StackTrace.Should().Be("Stack trace");
        }
        finally
        {
            File.Delete(trxPath);
        }
    }

    [Fact]
    public async Task RunTestsAsync_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange
        var runner = new TestRunner();

        // Act
        var act = () => runner.RunTestsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task RunTestsAsync_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var runner = new TestRunner();

        // Act
        var act = () => runner.RunTestsAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task RunTestsAsync_WithWhitespacePath_ShouldThrowArgumentException()
    {
        // Arrange
        var runner = new TestRunner();

        // Act
        var act = () => runner.RunTestsAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task RunTestsAsync_WithNonExistentPath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var runner = new TestRunner();
        var nonExistentPath = "C:\\NonExistent\\Solution.sln";

        // Act
        var act = () => runner.RunTestsAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    [Fact]
    public void TestResult_ShouldBeRecord()
    {
        // Arrange
        var result1 = new TestResult { Success = true, ExitCode = 0, TotalTests = 5 };
        var result2 = new TestResult { Success = true, ExitCode = 0, TotalTests = 5 };

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void TestFailure_ShouldBeRecord()
    {
        // Arrange
        var failure1 = new TestFailure
        {
            TestName = "Test1",
            ClassName = "Class1",
            ErrorMessage = "Error"
        };
        var failure2 = new TestFailure
        {
            TestName = "Test1",
            ClassName = "Class1",
            ErrorMessage = "Error"
        };

        // Assert
        failure1.Should().Be(failure2);
    }

    [Fact]
    public void TestResult_WithDefaultValues_ShouldHaveEmptyCollections()
    {
        // Arrange & Act
        var result = new TestResult();

        // Assert
        result.Failures.Should().BeEmpty();
        result.RawOutput.Should().BeEmpty();
    }

    [Fact]
    public void TestRunnerOptions_WithDefaultValues_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new TestRunnerOptions();

        // Assert
        options.TimeoutSeconds.Should().Be(600);
        options.StopOnFirstFailure.Should().BeFalse();
        options.Filter.Should().BeNull();
        options.Configuration.Should().Be("Debug");
    }

    [Fact]
    public void TestRunnerOptions_ShouldSupportCustomValues()
    {
        // Arrange & Act
        var options = new TestRunnerOptions
        {
            TimeoutSeconds = 300,
            StopOnFirstFailure = true,
            Filter = "FullyQualifiedName~MyTests",
            Configuration = "Release"
        };

        // Assert
        options.TimeoutSeconds.Should().Be(300);
        options.StopOnFirstFailure.Should().BeTrue();
        options.Filter.Should().Be("FullyQualifiedName~MyTests");
        options.Configuration.Should().Be("Release");
    }

    /// <summary>
    /// Creates a sample TRX file content with the specified test results.
    /// </summary>
    private static string CreateSampleTrx(
        int total,
        int passed,
        int failed,
        int skipped,
        IEnumerable<dynamic> failedTests)
    {
        var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");
        var doc = new XDocument(
            new XElement(ns + "TestRun",
                new XElement(ns + "TestDefinitions",
                    failedTests.Select(t => new XElement(ns + "UnitTest",
                        new XAttribute("id", t.TestId),
                        new XElement(ns + "TestMethod",
                            new XAttribute("name", t.TestName),
                            new XAttribute("className", t.ClassName)
                        )
                    ))
                ),
                new XElement(ns + "Results",
                    failedTests.Select(t => new XElement(ns + "UnitTestResult",
                        new XAttribute("testId", t.TestId),
                        new XAttribute("testName", t.TestName),
                        new XAttribute("outcome", "Failed"),
                        new XAttribute("duration", "00:00:01.5000000"),
                        new XElement(ns + "Output",
                            new XElement(ns + "ErrorInfo",
                                new XElement(ns + "Message", t.Message),
                                new XElement(ns + "StackTrace", t.StackTrace)
                            )
                        )
                    ))
                ),
                new XElement(ns + "ResultSummary",
                    new XElement(ns + "Counters",
                        new XAttribute("total", total),
                        new XAttribute("passed", passed),
                        new XAttribute("failed", failed),
                        new XAttribute("notExecuted", skipped)
                    )
                )
            )
        );

        return doc.ToString();
    }

    /// <summary>
    /// Writes TRX content to a temporary file and returns the path.
    /// </summary>
    private static string WriteTempTrxFile(string content)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.trx");
        File.WriteAllText(tempPath, content);
        return tempPath;
    }
}
