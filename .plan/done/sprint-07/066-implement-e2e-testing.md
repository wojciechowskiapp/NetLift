# [TASK-066] Implement End-to-End Testing

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 7 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-065
- **Blocks:** -

---

## Description

Implement end-to-end testing using a real MVC5 project fixture. Run the full migration pipeline, verify the migrated project builds successfully with `dotnet build`, and compare output structure to expected results. Tests should run on CI and validate the complete migration workflow.

---

## Acceptance Criteria

- [ ] E2E test suite passes on CI (GitHub Actions)
- [ ] Uses test fixture from TASK-011
- [ ] Full migration pipeline executes end-to-end
- [ ] Migrated project builds successfully with `dotnet build`
- [ ] Output directory structure matches expected layout
- [ ] Generated .csproj files are valid SDK-style projects
- [ ] NuGet package references resolve correctly
- [ ] Test coverage for common MVC5 patterns
- [ ] Test reports generated with detailed results
- [ ] Cleanup of test artifacts after run

---

## Technical Notes

### Test Fixture Structure (from TASK-011):

```
tests/
  fixtures/
    mvc5-sample/
      MvcSample.sln
      MvcSample/
        MvcSample.csproj
        Web.config
        Global.asax
        Controllers/
          HomeController.cs
        Models/
          User.cs
        Views/
          Home/
            Index.cshtml
        packages.config
```

### E2E Test Class:

```csharp
namespace NetLift.Tests.E2E;

[Collection("E2E")]
public class FullMigrationE2ETests : IAsyncLifetime
{
    private readonly string _fixtureSourcePath;
    private readonly string _workingDirectory;
    private readonly ITestOutputHelper _output;

    public FullMigrationE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _fixtureSourcePath = Path.Combine(
            TestContext.SolutionDirectory,
            "tests", "fixtures", "mvc5-sample");
        _workingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"netlift-e2e-{Guid.NewGuid():N}");
    }

    public Task InitializeAsync()
    {
        // Copy fixture to working directory
        CopyDirectory(_fixtureSourcePath, _workingDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Cleanup working directory
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FullMigration_Mvc5ToNet8_BuildsSuccessfully()
    {
        // Arrange
        var solutionPath = Path.Combine(_workingDirectory, "MvcSample.sln");
        var migrator = CreateMigrator();

        // Act
        var result = await migrator.MigrateAsync(solutionPath, new MigrationOptions
        {
            TargetFramework = "net8.0",
            OutputDirectory = Path.Combine(_workingDirectory, "migrated")
        });

        // Assert
        Assert.True(result.Success, $"Migration failed: {string.Join(", ", result.Errors)}");

        // Verify build
        var buildResult = await RunDotnetBuild(result.OutputPath);
        Assert.True(buildResult.Success, $"Build failed: {buildResult.Output}");
    }

    [Fact]
    public async Task FullMigration_GeneratesExpectedStructure()
    {
        // Arrange
        var solutionPath = Path.Combine(_workingDirectory, "MvcSample.sln");
        var migrator = CreateMigrator();
        var outputDir = Path.Combine(_workingDirectory, "migrated");

        // Act
        await migrator.MigrateAsync(solutionPath, new MigrationOptions
        {
            TargetFramework = "net8.0",
            OutputDirectory = outputDir
        });

        // Assert expected structure
        Assert.True(File.Exists(Path.Combine(outputDir, "MvcSample.sln")));
        Assert.True(File.Exists(Path.Combine(outputDir, "MvcSample", "MvcSample.csproj")));
        Assert.True(File.Exists(Path.Combine(outputDir, "MvcSample", "Program.cs")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "MvcSample", "Controllers")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "MvcSample", "Views")));

        // Verify SDK-style csproj
        var csproj = await File.ReadAllTextAsync(
            Path.Combine(outputDir, "MvcSample", "MvcSample.csproj"));
        Assert.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">", csproj);
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", csproj);
    }

    [Fact]
    public async Task FullMigration_GeneratesValidHtmlReport()
    {
        // Arrange
        var solutionPath = Path.Combine(_workingDirectory, "MvcSample.sln");
        var migrator = CreateMigrator();
        var reportPath = Path.Combine(_workingDirectory, "report.html");

        // Act
        await migrator.MigrateAsync(solutionPath, new MigrationOptions
        {
            TargetFramework = "net8.0",
            OutputDirectory = Path.Combine(_workingDirectory, "migrated"),
            ReportPath = reportPath
        });

        // Assert
        Assert.True(File.Exists(reportPath));
        var html = await File.ReadAllTextAsync(reportPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Migration Confidence Score", html);
    }

    private async Task<(bool Success, string Output)> RunDotnetBuild(string projectPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectPath}\" --no-restore",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        _output.WriteLine(output);
        if (!string.IsNullOrEmpty(error))
            _output.WriteLine($"STDERR: {error}");

        return (process.ExitCode == 0, output + error);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
```

### CI Configuration (GitHub Actions):

```yaml
e2e-tests:
  runs-on: windows-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    - name: Restore
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore
    - name: Run E2E Tests
      run: dotnet test tests/NetLift.Tests.E2E --no-build --logger "trx;LogFileName=e2e-results.trx"
    - name: Upload Test Results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: e2e-test-results
        path: '**/e2e-results.trx'
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
