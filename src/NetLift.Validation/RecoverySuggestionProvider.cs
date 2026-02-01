using NetLift.Core.Errors;
using NetLift.Core.Interfaces;

namespace NetLift.Validation;

/// <summary>
/// Provides context-aware recovery suggestions based on error codes and categories.
/// </summary>
public sealed class RecoverySuggestionProvider : IRecoverySuggestionProvider
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ErrorCodeSuggestions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["NETLIFT001"] = new[]
            {
                "Verify the solution file path exists and is accessible",
                "Check for typos in the file path",
                "Ensure you have read permissions for the directory",
                "Try using an absolute path instead of a relative path"
            },
            ["NETLIFT002"] = new[]
            {
                "Search for alternative packages on nuget.org",
                "Check the package migration guide at https://docs.microsoft.com/en-us/dotnet/core/porting/",
                "Consider implementing the functionality directly if no alternative exists",
                "Review the package's GitHub repository for .NET Core/5+ versions"
            },
            ["NETLIFT003"] = new[]
            {
                "Consult the .NET migration documentation at https://docs.microsoft.com/en-us/aspnet/core/migration/",
                "Search for the API name in the .NET API browser",
                "Check if there's a modern equivalent in ASP.NET Core",
                "Review breaking changes documentation for your target framework"
            }
        };

    private static readonly IReadOnlyDictionary<ErrorCategory, IReadOnlyList<string>> CategorySuggestions =
        new Dictionary<ErrorCategory, IReadOnlyList<string>>
        {
            [ErrorCategory.Analysis] = new[]
            {
                "Ensure all project files are valid and not corrupted",
                "Check that all referenced files exist in the solution",
                "Verify the solution structure matches expected format"
            },
            [ErrorCategory.Transformation] = new[]
            {
                "Review the transformation logs for detailed error information",
                "Check if the source code uses unsupported patterns",
                "Consider manually migrating complex code sections"
            },
            [ErrorCategory.Compilation] = new[]
            {
                "Run 'dotnet restore' to ensure all packages are restored",
                "Check for missing package references",
                "Verify the target framework is correctly specified",
                "Review compiler errors in the build output"
            },
            [ErrorCategory.FileSystem] = new[]
            {
                "Check file and directory permissions",
                "Ensure sufficient disk space is available",
                "Verify the file path length is within OS limits",
                "Close any applications that may have locked the files"
            },
            [ErrorCategory.Configuration] = new[]
            {
                "Verify configuration file syntax is valid",
                "Check for missing required configuration sections",
                "Ensure configuration values match expected types",
                "Review the configuration documentation"
            },
            [ErrorCategory.Validation] = new[]
            {
                "Run the validation command with --verbose for detailed output",
                "Check the validation report for specific failures",
                "Ensure all tests pass after migration"
            },
            [ErrorCategory.External] = new[]
            {
                "Verify external tools are installed and accessible",
                "Check network connectivity if the error involves downloads",
                "Ensure required SDKs are installed",
                "Review external tool documentation for troubleshooting"
            }
        };

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSuggestions(MigrationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // First, try to get suggestions by error code
        if (!string.IsNullOrWhiteSpace(error.Code) &&
            ErrorCodeSuggestions.TryGetValue(error.Code, out var codeSuggestions))
        {
            return codeSuggestions;
        }

        // Fall back to category-based suggestions
        if (CategorySuggestions.TryGetValue(error.Category, out var categorySuggestions))
        {
            return categorySuggestions;
        }

        // Default suggestions
        return new[]
        {
            "Consult the NetLift documentation for troubleshooting guidance",
            "Run the command with --verbose for detailed diagnostic information",
            "Check the NetLift GitHub issues for similar problems",
            "Consider reporting this error if it persists"
        };
    }
}
