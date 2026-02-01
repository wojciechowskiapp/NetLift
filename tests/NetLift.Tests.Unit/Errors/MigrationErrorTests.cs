using FluentAssertions;
using NetLift.Core.Errors;
using Xunit;

namespace NetLift.Tests.Unit.Errors;

public sealed class MigrationErrorTests
{
    [Fact]
    public void MigrationError_WithRequiredProperties_ShouldCreate()
    {
        // Act
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error message",
            Category = ErrorCategory.Analysis
        };

        // Assert
        error.Code.Should().Be("TEST001");
        error.Message.Should().Be("Test error message");
        error.Category.Should().Be(ErrorCategory.Analysis);
    }

    [Fact]
    public void MigrationError_WithOptionalProperties_ShouldCreate()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var suggestions = new[] { "Suggestion 1", "Suggestion 2" };

        // Act
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error message",
            Category = ErrorCategory.Transformation,
            FilePath = "C:\\test\\file.cs",
            Line = 42,
            Column = 10,
            StackTrace = "Stack trace here",
            RecoverySuggestions = suggestions,
            InnerException = exception
        };

        // Assert
        error.FilePath.Should().Be("C:\\test\\file.cs");
        error.Line.Should().Be(42);
        error.Column.Should().Be(10);
        error.StackTrace.Should().Be("Stack trace here");
        error.RecoverySuggestions.Should().BeEquivalentTo(suggestions);
        error.InnerException.Should().BeSameAs(exception);
    }

    [Fact]
    public void MigrationError_DefaultRecoverySuggestions_ShouldBeEmpty()
    {
        // Act
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Compilation
        };

        // Assert
        error.RecoverySuggestions.Should().BeEmpty();
    }

    [Fact]
    public void MigrationError_IsRecord_ShouldSupportWith()
    {
        // Arrange
        var original = new MigrationError
        {
            Code = "TEST001",
            Message = "Original message",
            Category = ErrorCategory.FileSystem
        };

        // Act
        var modified = original with { Message = "Modified message" };

        // Assert
        modified.Code.Should().Be(original.Code);
        modified.Message.Should().Be("Modified message");
        modified.Category.Should().Be(original.Category);
    }

    [Fact]
    public void MigrationError_IsRecord_ShouldSupportEquality()
    {
        // Arrange
        var error1 = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Configuration
        };
        var error2 = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Configuration
        };

        // Assert
        error1.Should().Be(error2);
    }

    [Fact]
    public void MigrationError_WithDifferentProperties_ShouldNotBeEqual()
    {
        // Arrange
        var error1 = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Validation
        };
        var error2 = new MigrationError
        {
            Code = "TEST002",
            Message = "Test error",
            Category = ErrorCategory.Validation
        };

        // Assert
        error1.Should().NotBe(error2);
    }

    [Fact]
    public void ErrorCategory_Analysis_ShouldHaveCorrectValue()
    {
        // Assert
        ErrorCategory.Analysis.Should().Be(ErrorCategory.Analysis);
    }

    [Fact]
    public void ErrorCategory_AllValues_ShouldBeDefined()
    {
        // Act
        var categories = Enum.GetValues<ErrorCategory>();

        // Assert
        categories.Should().Contain(new[]
        {
            ErrorCategory.Analysis,
            ErrorCategory.Transformation,
            ErrorCategory.Compilation,
            ErrorCategory.FileSystem,
            ErrorCategory.Configuration,
            ErrorCategory.Validation,
            ErrorCategory.External
        });
    }

    [Fact]
    public void MigrationError_WithFileLocation_ShouldFormatCorrectly()
    {
        // Act
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Syntax error",
            Category = ErrorCategory.Transformation,
            FilePath = "C:\\src\\Project\\File.cs",
            Line = 100,
            Column = 25
        };

        // Assert
        error.FilePath.Should().Be("C:\\src\\Project\\File.cs");
        error.Line.Should().Be(100);
        error.Column.Should().Be(25);
    }

    [Fact]
    public void MigrationError_NullableProperties_CanBeNull()
    {
        // Act
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.External,
            FilePath = null,
            Line = null,
            Column = null,
            StackTrace = null,
            InnerException = null
        };

        // Assert
        error.FilePath.Should().BeNull();
        error.Line.Should().BeNull();
        error.Column.Should().BeNull();
        error.StackTrace.Should().BeNull();
        error.InnerException.Should().BeNull();
    }

    [Fact]
    public void MigrationError_RecoverySuggestions_IsReadOnly()
    {
        // Arrange
        var suggestions = new[] { "Suggestion 1" };
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Analysis,
            RecoverySuggestions = suggestions
        };

        // Assert
        error.RecoverySuggestions.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public void MigrationError_ToString_ShouldContainRelevantInfo()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error message",
            Category = ErrorCategory.Compilation
        };

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Contain("TEST001");
        result.Should().Contain("Test error message");
    }
}
