# [TASK-012] Setup xUnit Tests

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | S |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001
- **Blocks:** All future tests

---

## Description

Setup xUnit testing infrastructure with FluentAssertions and proper test organization.

---

## Acceptance Criteria

- [ ] xUnit packages added to test projects
- [ ] FluentAssertions configured
- [ ] Sample test passes
- [ ] Tests can be run with `dotnet test`
- [ ] Test output is readable
- [ ] Code coverage report can be generated (optional)

---

## Technical Notes

### Packages for test projects:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  <PackageReference Include="xunit" Version="2.6.4" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
  <PackageReference Include="NSubstitute" Version="5.1.0" />
  <PackageReference Include="Verify.Xunit" Version="23.0.0" />
</ItemGroup>
```

### Test project structure:

```
tests/
├── NetLift.Tests.Unit/
│   ├── NetLift.Tests.Unit.csproj
│   ├── Analysis/
│   │   ├── SolutionAnalyzerTests.cs
│   │   ├── ProjectAnalyzerTests.cs
│   │   └── DependencyGraphTests.cs
│   ├── Transforms/
│   │   └── (future)
│   └── TestHelpers/
│       └── TestDataBuilder.cs
│
└── NetLift.Tests.Integration/
    ├── NetLift.Tests.Integration.csproj
    ├── EndToEnd/
    │   └── AnalyzeCommandTests.cs
    └── TestFixtures/
        └── (symlink or copy of test-fixtures)
```

### Sample unit test:

```csharp
using FluentAssertions;
using Xunit;

namespace NetLift.Tests.Unit.Analysis;

public class SolutionAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_WithValidSolution_ReturnsSolutionInfo()
    {
        // Arrange
        var analyzer = new SolutionAnalyzer();
        var solutionPath = GetTestFixturePath("mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await analyzer.AnalyzeAsync(solutionPath);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Mvc5Basic");
        result.Projects.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var analyzer = new SolutionAnalyzer();

        // Act
        var act = () => analyzer.AnalyzeAsync("nonexistent.sln");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    private string GetTestFixturePath(string fixture, string file)
    {
        // Navigate from test output to test-fixtures
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "..", "..", "..", "..", "..",
            "test-fixtures", fixture, file);
    }
}
```

### Test naming convention:

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
- `AnalyzeAsync_WithValidSolution_ReturnsSolutionInfo`
- `Detect_WithMvcReferences_ReturnsDetectedTrue`
- `Parse_WithEmptyConfig_ReturnsEmptyList`

### Run tests:

```bash
dotnet test
dotnet test --filter "Category=Unit"
dotnet test --collect:"XPlat Code Coverage"
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
