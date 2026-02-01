using FluentAssertions;
using NetLift.Core.Errors;
using NetLift.Core.Interfaces;
using NetLift.Validation;
using Xunit;

namespace NetLift.Tests.Unit.Errors;

public sealed class RecoverySuggestionProviderTests
{
    private readonly IRecoverySuggestionProvider _provider;

    public RecoverySuggestionProviderTests()
    {
        _provider = new RecoverySuggestionProvider();
    }

    [Fact]
    public void GetSuggestions_WithNullError_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _provider.GetSuggestions(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSuggestions_ForNETLIFT001_ShouldReturnPathVerificationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "NETLIFT001",
            Message = "Solution file not found",
            Category = ErrorCategory.Analysis
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("path exists"));
        suggestions.Should().Contain(s => s.Contains("absolute path"));
    }

    [Fact]
    public void GetSuggestions_ForNETLIFT002_ShouldReturnPackageAlternativeSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "NETLIFT002",
            Message = "Package incompatible with target framework",
            Category = ErrorCategory.Transformation
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("nuget.org"));
        suggestions.Should().Contain(s => s.Contains("alternative"));
    }

    [Fact]
    public void GetSuggestions_ForNETLIFT003_ShouldReturnApiMigrationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "NETLIFT003",
            Message = "API no longer available in target framework",
            Category = ErrorCategory.Transformation
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("migration documentation"));
        suggestions.Should().Contain(s => s.Contains("breaking changes"));
    }

    [Fact]
    public void GetSuggestions_ForAnalysisCategory_ShouldReturnAnalysisSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN001",
            Message = "Analysis failed",
            Category = ErrorCategory.Analysis
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("project files"));
    }

    [Fact]
    public void GetSuggestions_ForTransformationCategory_ShouldReturnTransformationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN002",
            Message = "Transformation failed",
            Category = ErrorCategory.Transformation
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("transformation logs"));
    }

    [Fact]
    public void GetSuggestions_ForCompilationCategory_ShouldReturnCompilationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN003",
            Message = "Build failed",
            Category = ErrorCategory.Compilation
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("dotnet restore"));
        suggestions.Should().Contain(s => s.Contains("compiler errors"));
    }

    [Fact]
    public void GetSuggestions_ForFileSystemCategory_ShouldReturnFileSystemSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN004",
            Message = "File access denied",
            Category = ErrorCategory.FileSystem
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("permissions"));
        suggestions.Should().Contain(s => s.Contains("disk space"));
    }

    [Fact]
    public void GetSuggestions_ForConfigurationCategory_ShouldReturnConfigurationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN005",
            Message = "Invalid configuration",
            Category = ErrorCategory.Configuration
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("configuration file"));
    }

    [Fact]
    public void GetSuggestions_ForValidationCategory_ShouldReturnValidationSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN006",
            Message = "Validation failed",
            Category = ErrorCategory.Validation
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("validation"));
    }

    [Fact]
    public void GetSuggestions_ForExternalCategory_ShouldReturnExternalSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN007",
            Message = "External tool failed",
            Category = ErrorCategory.External
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("external tools"));
    }

    [Fact]
    public void GetSuggestions_WithUnknownCodeAndCategory_ShouldReturnDefaultSuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN999",
            Message = "Unknown error",
            Category = (ErrorCategory)999 // Invalid category
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Contains("documentation"));
        suggestions.Should().Contain(s => s.Contains("verbose"));
    }

    [Fact]
    public void GetSuggestions_ErrorCodeTakesPrecedenceOverCategory()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "NETLIFT001",
            Message = "Error",
            Category = ErrorCategory.Compilation // Different category
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert - Should get NETLIFT001 suggestions, not Compilation suggestions
        suggestions.Should().Contain(s => s.Contains("path exists"));
        suggestions.Should().NotContain(s => s.Contains("dotnet restore"));
    }

    [Fact]
    public void GetSuggestions_IsCaseInsensitiveForErrorCodes()
    {
        // Arrange
        var error1 = new MigrationError
        {
            Code = "NETLIFT001",
            Message = "Error",
            Category = ErrorCategory.Analysis
        };
        var error2 = new MigrationError
        {
            Code = "netlift001",
            Message = "Error",
            Category = ErrorCategory.Analysis
        };

        // Act
        var suggestions1 = _provider.GetSuggestions(error1);
        var suggestions2 = _provider.GetSuggestions(error2);

        // Assert
        suggestions1.Should().BeEquivalentTo(suggestions2);
    }

    [Fact]
    public void GetSuggestions_WithEmptyErrorCode_ShouldUseCategorySuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "",
            Message = "Error",
            Category = ErrorCategory.Analysis
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().Contain(s => s.Contains("project files"));
    }

    [Fact]
    public void GetSuggestions_WithWhitespaceErrorCode_ShouldUseCategorySuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "   ",
            Message = "Error",
            Category = ErrorCategory.FileSystem
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().Contain(s => s.Contains("permissions"));
    }

    [Theory]
    [InlineData(ErrorCategory.Analysis)]
    [InlineData(ErrorCategory.Transformation)]
    [InlineData(ErrorCategory.Compilation)]
    [InlineData(ErrorCategory.FileSystem)]
    [InlineData(ErrorCategory.Configuration)]
    [InlineData(ErrorCategory.Validation)]
    [InlineData(ErrorCategory.External)]
    public void GetSuggestions_AllCategoriesReturnNonEmptySuggestions(ErrorCategory category)
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "UNKNOWN",
            Message = "Test error",
            Category = category
        };

        // Act
        var suggestions = _provider.GetSuggestions(error);

        // Assert
        suggestions.Should().NotBeEmpty();
    }
}
