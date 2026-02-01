# [TASK-065] Implement Comprehensive Error Handling

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

- **Depends on:** TASK-063, TASK-064
- **Blocks:** TASK-066

---

## Description

Implement comprehensive error handling with recovery suggestions throughout the migration pipeline. Catch Roslyn compilation errors gracefully, provide actionable error messages, suggest manual fixes when auto-migration fails, and log errors with context for debugging.

---

## Acceptance Criteria

- [ ] No unhandled exceptions escape the migration pipeline
- [ ] All error messages are meaningful and actionable
- [ ] Recovery guidance provided for common failure scenarios
- [ ] Roslyn compilation errors caught and wrapped with context
- [ ] Error logging includes file path, line number, and stack trace
- [ ] User-friendly error summaries distinct from debug logs
- [ ] Retry mechanisms for transient failures (file locks, etc.)
- [ ] Graceful degradation when partial migration is possible
- [ ] Unit tests for error handling paths
- [ ] Integration tests verify error recovery suggestions

---

## Technical Notes

### Error Categories:

```csharp
namespace NetLift.Core.Errors;

public enum ErrorCategory
{
    Analysis,           // Solution/project parsing errors
    Transformation,     // Code transformation failures
    Compilation,        // Roslyn compilation errors
    FileSystem,         // IO errors (access, locks, etc.)
    Configuration,      // Invalid settings or options
    Validation,         // Post-migration validation failures
    External            // NuGet, dotnet CLI, etc.
}

public record MigrationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required ErrorCategory Category { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
    public string? StackTrace { get; init; }
    public IReadOnlyList<string> RecoverySuggestions { get; init; } = [];
    public Exception? InnerException { get; init; }
}
```

### Error Handler Interface:

```csharp
public interface IErrorHandler
{
    void HandleError(MigrationError error);
    void HandleWarning(MigrationError warning);
    IReadOnlyList<MigrationError> GetErrors();
    IReadOnlyList<MigrationError> GetWarnings();
    bool HasCriticalErrors { get; }
}

public interface IRecoverySuggestionProvider
{
    IReadOnlyList<string> GetSuggestions(MigrationError error);
}
```

### Recovery Suggestions Example:

```csharp
public class RecoverySuggestionProvider : IRecoverySuggestionProvider
{
    public IReadOnlyList<string> GetSuggestions(MigrationError error)
    {
        return error.Code switch
        {
            "NETLIFT001" => [
                "Ensure the solution file exists and is not corrupted",
                "Try opening the solution in Visual Studio to verify it loads correctly",
                "Check that all project references are valid"
            ],
            "NETLIFT002" => [
                "The referenced NuGet package may not support .NET 8",
                "Search for an alternative package on nuget.org",
                "Consider removing the dependency if it's not critical"
            ],
            "NETLIFT003" => [
                "This API was removed in .NET Core/.NET 5+",
                "See migration guide: https://docs.microsoft.com/dotnet/core/porting/",
                "Manual code changes required for this pattern"
            ],
            _ => ["Review the error details and consult the NetLift documentation"]
        };
    }
}
```

### Roslyn Error Wrapping:

```csharp
public static class RoslynErrorExtensions
{
    public static MigrationError ToMigrationError(this Diagnostic diagnostic)
    {
        return new MigrationError
        {
            Code = diagnostic.Id,
            Message = diagnostic.GetMessage(),
            Category = ErrorCategory.Compilation,
            FilePath = diagnostic.Location.SourceTree?.FilePath,
            Line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
            Column = diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1,
            RecoverySuggestions = GetRoslynRecoverySuggestions(diagnostic)
        };
    }
}
```

### Unit Tests:

```csharp
public class ErrorHandlerTests
{
    [Fact]
    public void HandleError_AddsToErrorList()
    {
        var handler = new ErrorHandler();
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Transformation
        };

        handler.HandleError(error);

        Assert.Single(handler.GetErrors());
        Assert.True(handler.HasCriticalErrors);
    }

    [Fact]
    public void GetSuggestions_ReturnsRelevantSuggestions()
    {
        var provider = new RecoverySuggestionProvider();
        var error = new MigrationError
        {
            Code = "NETLIFT002",
            Message = "Package not compatible",
            Category = ErrorCategory.External
        };

        var suggestions = provider.GetSuggestions(error);

        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Contains("NuGet"));
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
