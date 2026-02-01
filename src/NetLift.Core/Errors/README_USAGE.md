# Error Handling Usage

## Quick Example

```csharp
// Constructor injection
public class MyMigrationService
{
    private readonly IErrorHandler _errorHandler;

    public MyMigrationService(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler;
    }

    public void MigrateSolution(string solutionPath)
    {
        // Check if file exists
        if (!File.Exists(solutionPath))
        {
            _errorHandler.HandleError(new MigrationError
            {
                Code = "NETLIFT001",
                Message = $"Solution file not found: {solutionPath}",
                Category = ErrorCategory.Analysis,
                FilePath = solutionPath
            });
            return;
        }

        // Handle warnings
        _errorHandler.HandleWarning(new MigrationError
        {
            Code = "NETLIFT100",
            Message = "Legacy package detected - consider manual review",
            Category = ErrorCategory.Transformation,
            FilePath = "packages.config"
        });

        // Check for critical errors before continuing
        if (_errorHandler.HasCriticalErrors)
        {
            var errors = _errorHandler.GetErrors();
            foreach (var error in errors)
            {
                Console.WriteLine($"{error.Code}: {error.Message}");
                foreach (var suggestion in error.RecoverySuggestions)
                {
                    Console.WriteLine($"  - {suggestion}");
                }
            }
            return;
        }
    }
}
```

## Error Codes

- **NETLIFT001**: Solution file not found
- **NETLIFT002**: Package incompatible with target framework
- **NETLIFT003**: API removed in target framework

## Categories

- `Analysis`: Parsing/analysis phase errors
- `Transformation`: Code transformation errors
- `Compilation`: Build/compilation errors
- `FileSystem`: File I/O errors
- `Configuration`: Config file errors
- `Validation`: Post-migration validation errors
- `External`: External tool errors
