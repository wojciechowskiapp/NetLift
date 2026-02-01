using FluentAssertions;
using NetLift.Core.Errors;
using NetLift.Core.Interfaces;
using NetLift.Validation;
using Xunit;

namespace NetLift.Tests.Unit.Errors;

public sealed class ErrorHandlerTests
{
    private readonly IRecoverySuggestionProvider _suggestionProvider;
    private readonly IErrorHandler _errorHandler;

    public ErrorHandlerTests()
    {
        _suggestionProvider = new RecoverySuggestionProvider();
        _errorHandler = new ErrorHandler(_suggestionProvider);
    }

    [Fact]
    public void HandleError_ShouldAddErrorToCollection()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Analysis
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        _errorHandler.GetErrors().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(error, options => options.Excluding(e => e.RecoverySuggestions));
    }

    [Fact]
    public void HandleWarning_ShouldAddWarningToCollection()
    {
        // Arrange
        var warning = new MigrationError
        {
            Code = "TEST002",
            Message = "Test warning",
            Category = ErrorCategory.Transformation
        };

        // Act
        _errorHandler.HandleWarning(warning);

        // Assert
        _errorHandler.GetWarnings().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(warning, options => options.Excluding(w => w.RecoverySuggestions));
    }

    [Fact]
    public void HandleError_WithNullError_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _errorHandler.HandleError(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HandleWarning_WithNullWarning_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _errorHandler.HandleWarning(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasCriticalErrors_WithNoErrors_ShouldReturnFalse()
    {
        // Assert
        _errorHandler.HasCriticalErrors.Should().BeFalse();
    }

    [Fact]
    public void HasCriticalErrors_WithErrors_ShouldReturnTrue()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Compilation
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        _errorHandler.HasCriticalErrors.Should().BeTrue();
    }

    [Fact]
    public void HasCriticalErrors_WithOnlyWarnings_ShouldReturnFalse()
    {
        // Arrange
        var warning = new MigrationError
        {
            Code = "TEST002",
            Message = "Test warning",
            Category = ErrorCategory.Validation
        };

        // Act
        _errorHandler.HandleWarning(warning);

        // Assert
        _errorHandler.HasCriticalErrors.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllErrorsAndWarnings()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.FileSystem
        };
        var warning = new MigrationError
        {
            Code = "TEST002",
            Message = "Test warning",
            Category = ErrorCategory.Configuration
        };

        _errorHandler.HandleError(error);
        _errorHandler.HandleWarning(warning);

        // Act
        _errorHandler.Clear();

        // Assert
        _errorHandler.GetErrors().Should().BeEmpty();
        _errorHandler.GetWarnings().Should().BeEmpty();
        _errorHandler.HasCriticalErrors.Should().BeFalse();
    }

    [Fact]
    public void HandleError_ShouldEnrichWithRecoverySuggestions()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "NETLIFT001",
            Message = "Solution not found",
            Category = ErrorCategory.Analysis
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        var storedError = _errorHandler.GetErrors().Single();
        storedError.RecoverySuggestions.Should().NotBeEmpty();
        storedError.RecoverySuggestions.Should().Contain(s => s.Contains("path exists"));
    }

    [Fact]
    public void HandleError_WithExistingSuggestions_ShouldNotOverwrite()
    {
        // Arrange
        var customSuggestions = new[] { "Custom suggestion 1", "Custom suggestion 2" };
        var error = new MigrationError
        {
            Code = "NETLIFT001",
            Message = "Solution not found",
            Category = ErrorCategory.Analysis,
            RecoverySuggestions = customSuggestions
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        var storedError = _errorHandler.GetErrors().Single();
        storedError.RecoverySuggestions.Should().BeEquivalentTo(customSuggestions);
    }

    [Fact]
    public void GetErrors_ShouldReturnReadOnlyList()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.External
        };
        _errorHandler.HandleError(error);

        // Act
        var errors = _errorHandler.GetErrors();

        // Assert
        errors.Should().BeAssignableTo<IReadOnlyList<MigrationError>>();
    }

    [Fact]
    public void GetWarnings_ShouldReturnReadOnlyList()
    {
        // Arrange
        var warning = new MigrationError
        {
            Code = "TEST002",
            Message = "Test warning",
            Category = ErrorCategory.Validation
        };
        _errorHandler.HandleWarning(warning);

        // Act
        var warnings = _errorHandler.GetWarnings();

        // Assert
        warnings.Should().BeAssignableTo<IReadOnlyList<MigrationError>>();
    }

    [Fact]
    public void HandleError_MultipleErrors_ShouldMaintainAll()
    {
        // Arrange
        var error1 = new MigrationError
        {
            Code = "TEST001",
            Message = "First error",
            Category = ErrorCategory.Analysis
        };
        var error2 = new MigrationError
        {
            Code = "TEST002",
            Message = "Second error",
            Category = ErrorCategory.Transformation
        };

        // Act
        _errorHandler.HandleError(error1);
        _errorHandler.HandleError(error2);

        // Assert
        _errorHandler.GetErrors().Should().HaveCount(2);
    }

    [Fact]
    public void HandleError_WithFileLocation_ShouldPreserveLocationInfo()
    {
        // Arrange
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.Transformation,
            FilePath = "C:\\test\\project.cs",
            Line = 42,
            Column = 10
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        var storedError = _errorHandler.GetErrors().Single();
        storedError.FilePath.Should().Be("C:\\test\\project.cs");
        storedError.Line.Should().Be(42);
        storedError.Column.Should().Be(10);
    }

    [Fact]
    public void HandleError_WithException_ShouldPreserveException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner exception");
        var error = new MigrationError
        {
            Code = "TEST001",
            Message = "Test error",
            Category = ErrorCategory.External,
            InnerException = innerException,
            StackTrace = innerException.StackTrace
        };

        // Act
        _errorHandler.HandleError(error);

        // Assert
        var storedError = _errorHandler.GetErrors().Single();
        storedError.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public async Task ErrorHandler_IsThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int errorsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < errorsPerThread; j++)
                {
                    var error = new MigrationError
                    {
                        Code = $"TEST{threadIndex:D3}-{j:D3}",
                        Message = $"Error from thread {threadIndex}, iteration {j}",
                        Category = ErrorCategory.Analysis
                    };
                    _errorHandler.HandleError(error);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        _errorHandler.GetErrors().Should().HaveCount(threadCount * errorsPerThread);
    }
}
