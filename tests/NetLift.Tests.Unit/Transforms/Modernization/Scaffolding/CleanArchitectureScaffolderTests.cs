using FluentAssertions;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Transforms.Modernization.Generators;
using NetLift.Transforms.Modernization.Scaffolding;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Scaffolding;

public sealed class CleanArchitectureScaffolderTests : IDisposable
{
    private readonly string _tempPath;
    private readonly IProjectScaffolder _scaffolder;

    public CleanArchitectureScaffolderTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"NetLift_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);

        var handlerGenerator = new HandlerGenerator();
        _scaffolder = new CleanArchitectureScaffolder(handlerGenerator);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [Fact]
    public void Scaffold_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Scaffold_WithNullProjectPath_ReturnsFailure()
    {
        // Arrange
        var options = new ScaffoldOptions();

        // Act
        var result = _scaffolder.Scaffold(null!, "TestApp", options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Project path cannot be null or whitespace");
    }

    [Fact]
    public void Scaffold_WithEmptyProjectPath_ReturnsFailure()
    {
        // Arrange
        var options = new ScaffoldOptions();

        // Act
        var result = _scaffolder.Scaffold(string.Empty, "TestApp", options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Project path cannot be null or whitespace");
    }

    [Fact]
    public void Scaffold_WithNullRootNamespace_ReturnsFailure()
    {
        // Arrange
        var options = new ScaffoldOptions();

        // Act
        var result = _scaffolder.Scaffold(_tempPath, null!, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Root namespace cannot be null or whitespace");
    }

    [Fact]
    public void Scaffold_WithEmptyRootNamespace_ReturnsFailure()
    {
        // Arrange
        var options = new ScaffoldOptions();

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "   ", options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Root namespace cannot be null or whitespace");
    }

    [Fact]
    public void Scaffold_WithDomainLayer_CreatesDomainFolders()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = false
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain", "Common")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain", "Entities")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain", "Enums")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain", "Events")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Domain", "Exceptions")).Should().BeTrue();
    }

    [Fact]
    public void Scaffold_WithApplicationLayer_CreatesApplicationFolders()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = false
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common", "Behaviors")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common", "Exceptions")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common", "Interfaces")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common", "Mappings")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Application", "Common", "Models")).Should().BeTrue();
    }

    [Fact]
    public void Scaffold_WithInfrastructureLayer_CreatesInfrastructureFolders()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = false
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Infrastructure")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Infrastructure", "Persistence")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Infrastructure", "Persistence", "Configurations")).Should().BeTrue();
        Directory.Exists(Path.Combine(_tempPath, "Infrastructure", "Services")).Should().BeTrue();
    }

    [Fact]
    public void Scaffold_WithDomainLayerAndCommonFiles_GeneratesBaseEntity()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var baseEntityPath = Path.Combine(_tempPath, "Domain", "Common", "BaseEntity.cs");
        File.Exists(baseEntityPath).Should().BeTrue();

        var content = File.ReadAllText(baseEntityPath);
        content.Should().Contain("namespace TestApp.Domain.Common");
        content.Should().Contain("public abstract class BaseEntity");
        content.Should().Contain("public int Id { get; set; }");
        content.Should().Contain("public IReadOnlyCollection<BaseEvent> DomainEvents");
    }

    [Fact]
    public void Scaffold_WithDomainLayerAndCommonFiles_GeneratesBaseEvent()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var baseEventPath = Path.Combine(_tempPath, "Domain", "Common", "BaseEvent.cs");
        File.Exists(baseEventPath).Should().BeTrue();

        var content = File.ReadAllText(baseEventPath);
        content.Should().Contain("namespace TestApp.Domain.Common");
        content.Should().Contain("using MediatR");
        content.Should().Contain("public abstract class BaseEvent : INotification");
        content.Should().Contain("public DateTimeOffset OccurredOn");
    }

    [Fact]
    public void Scaffold_WithDomainLayerAndCommonFiles_GeneratesValueObject()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var valueObjectPath = Path.Combine(_tempPath, "Domain", "Common", "ValueObject.cs");
        File.Exists(valueObjectPath).Should().BeTrue();

        var content = File.ReadAllText(valueObjectPath);
        content.Should().Contain("namespace TestApp.Domain.Common");
        content.Should().Contain("public abstract class ValueObject");
        content.Should().Contain("protected abstract IEnumerable<object?> GetEqualityComponents()");
        content.Should().Contain("public static bool operator ==");
        content.Should().Contain("public static bool operator !=");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesValidationBehavior()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var behaviorPath = Path.Combine(_tempPath, "Application", "Common", "Behaviors", "ValidationBehavior.cs");
        File.Exists(behaviorPath).Should().BeTrue();

        var content = File.ReadAllText(behaviorPath);
        content.Should().Contain("namespace TestApp.Application.Common.Behaviors");
        content.Should().Contain("using FluentValidation");
        content.Should().Contain("using MediatR");
        content.Should().Contain("public class ValidationBehavior<TRequest, TResponse>");
        content.Should().Contain("IPipelineBehavior<TRequest, TResponse>");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesLoggingBehavior()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var behaviorPath = Path.Combine(_tempPath, "Application", "Common", "Behaviors", "LoggingBehavior.cs");
        File.Exists(behaviorPath).Should().BeTrue();

        var content = File.ReadAllText(behaviorPath);
        content.Should().Contain("namespace TestApp.Application.Common.Behaviors");
        content.Should().Contain("using Microsoft.Extensions.Logging");
        content.Should().Contain("public class LoggingBehavior<TRequest, TResponse>");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesUnhandledExceptionBehavior()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var behaviorPath = Path.Combine(_tempPath, "Application", "Common", "Behaviors", "UnhandledExceptionBehavior.cs");
        File.Exists(behaviorPath).Should().BeTrue();

        var content = File.ReadAllText(behaviorPath);
        content.Should().Contain("namespace TestApp.Application.Common.Behaviors");
        content.Should().Contain("public class UnhandledExceptionBehavior<TRequest, TResponse>");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesNotFoundException()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var exceptionPath = Path.Combine(_tempPath, "Application", "Common", "Exceptions", "NotFoundException.cs");
        File.Exists(exceptionPath).Should().BeTrue();

        var content = File.ReadAllText(exceptionPath);
        content.Should().Contain("namespace TestApp.Application.Common.Exceptions");
        content.Should().Contain("public class NotFoundException : Exception");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesValidationException()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var exceptionPath = Path.Combine(_tempPath, "Application", "Common", "Exceptions", "ValidationException.cs");
        File.Exists(exceptionPath).Should().BeTrue();

        var content = File.ReadAllText(exceptionPath);
        content.Should().Contain("namespace TestApp.Application.Common.Exceptions");
        content.Should().Contain("using FluentValidation.Results");
        content.Should().Contain("public class ValidationException : Exception");
        content.Should().Contain("IDictionary<string, string[]> Errors");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesIApplicationDbContext()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var interfacePath = Path.Combine(_tempPath, "Application", "Common", "Interfaces", "IApplicationDbContext.cs");
        File.Exists(interfacePath).Should().BeTrue();

        var content = File.ReadAllText(interfacePath);
        content.Should().Contain("namespace TestApp.Application.Common.Interfaces");
        content.Should().Contain("public interface IApplicationDbContext");
        content.Should().Contain("Task<int> SaveChangesAsync");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesIMapFrom()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var mappingPath = Path.Combine(_tempPath, "Application", "Common", "Mappings", "IMapFrom.cs");
        File.Exists(mappingPath).Should().BeTrue();

        var content = File.ReadAllText(mappingPath);
        content.Should().Contain("namespace TestApp.Application.Common.Mappings");
        content.Should().Contain("using AutoMapper");
        content.Should().Contain("public interface IMapFrom<T>");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesResult()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var resultPath = Path.Combine(_tempPath, "Application", "Common", "Models", "Result.cs");
        File.Exists(resultPath).Should().BeTrue();

        var content = File.ReadAllText(resultPath);
        content.Should().Contain("namespace TestApp.Application.Common.Models");
        content.Should().Contain("public class Result<T>");
        content.Should().Contain("public bool IsSuccess");
        content.Should().Contain("public static Result<T> Success");
        content.Should().Contain("public static Result<T> Failure");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesPaginatedList()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var paginatedPath = Path.Combine(_tempPath, "Application", "Common", "Models", "PaginatedList.cs");
        File.Exists(paginatedPath).Should().BeTrue();

        var content = File.ReadAllText(paginatedPath);
        content.Should().Contain("namespace TestApp.Application.Common.Models");
        content.Should().Contain("public class PaginatedList<T>");
        content.Should().Contain("public IReadOnlyCollection<T> Items");
        content.Should().Contain("public int PageNumber");
        content.Should().Contain("public int TotalPages");
        content.Should().Contain("public int TotalCount");
        content.Should().Contain("public bool HasPreviousPage");
        content.Should().Contain("public bool HasNextPage");
    }

    [Fact]
    public void Scaffold_WithApplicationLayerAndCommonFiles_GeneratesDependencyInjection()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var diPath = Path.Combine(_tempPath, "Application", "DependencyInjection.cs");
        File.Exists(diPath).Should().BeTrue();

        var content = File.ReadAllText(diPath);
        content.Should().Contain("namespace TestApp.Application");
        content.Should().Contain("using MediatR");
        content.Should().Contain("using FluentValidation");
        content.Should().Contain("public static IServiceCollection AddApplication");
        content.Should().Contain("AddMediatR");
        content.Should().Contain("AddValidatorsFromAssembly");
        content.Should().Contain("IPipelineBehavior");
    }

    [Fact]
    public void Scaffold_WithInfrastructureLayerAndCommonFiles_GeneratesApplicationDbContext()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var dbContextPath = Path.Combine(_tempPath, "Infrastructure", "Persistence", "ApplicationDbContext.cs");
        File.Exists(dbContextPath).Should().BeTrue();

        var content = File.ReadAllText(dbContextPath);
        content.Should().Contain("namespace TestApp.Infrastructure.Persistence");
        content.Should().Contain("using TestApp.Application.Common.Interfaces");
        content.Should().Contain("public class ApplicationDbContext : DbContext, IApplicationDbContext");
        content.Should().Contain("protected override void OnModelCreating");
    }

    [Fact]
    public void Scaffold_WithInfrastructureLayerAndCommonFiles_GeneratesApplicationDbContextInitializer()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var initializerPath = Path.Combine(_tempPath, "Infrastructure", "Persistence", "ApplicationDbContextInitializer.cs");
        File.Exists(initializerPath).Should().BeTrue();

        var content = File.ReadAllText(initializerPath);
        content.Should().Contain("namespace TestApp.Infrastructure.Persistence");
        content.Should().Contain("public class ApplicationDbContextInitializer");
        content.Should().Contain("public async Task InitializeAsync()");
        content.Should().Contain("public async Task SeedAsync()");
    }

    [Fact]
    public void Scaffold_WithInfrastructureLayerAndCommonFiles_GeneratesInfrastructureDependencyInjection()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = false,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        var diPath = Path.Combine(_tempPath, "Infrastructure", "DependencyInjection.cs");
        File.Exists(diPath).Should().BeTrue();

        var content = File.ReadAllText(diPath);
        content.Should().Contain("namespace TestApp.Infrastructure");
        content.Should().Contain("using TestApp.Application.Common.Interfaces");
        content.Should().Contain("public static IServiceCollection AddInfrastructure");
        content.Should().Contain("AddDbContext<ApplicationDbContext>");
        content.Should().Contain("UseSqlServer");
        content.Should().Contain("AddScoped<IApplicationDbContext>");
    }

    [Fact]
    public void Scaffold_WithAllLayers_ReportsAllCreatedDirectories()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = false
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        result.CreatedDirectories.Should().NotBeEmpty();
        result.CreatedDirectories.Should().Contain(d => d.EndsWith("Domain"));
        result.CreatedDirectories.Should().Contain(d => d.EndsWith("Application"));
        result.CreatedDirectories.Should().Contain(d => d.EndsWith("Infrastructure"));
    }

    [Fact]
    public void Scaffold_WithCommonFiles_ReportsAllCreatedFiles()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        result.CreatedFiles.Should().NotBeEmpty();

        // Verify domain files
        result.CreatedFiles.Should().Contain(f => f.FileType == "BaseEntity");
        result.CreatedFiles.Should().Contain(f => f.FileType == "BaseEvent");
        result.CreatedFiles.Should().Contain(f => f.FileType == "ValueObject");

        // Verify application files
        result.CreatedFiles.Should().Contain(f => f.FileType == "ValidationBehavior");
        result.CreatedFiles.Should().Contain(f => f.FileType == "Result");
        result.CreatedFiles.Should().Contain(f => f.FileType == "PaginatedList");

        // Verify infrastructure files
        result.CreatedFiles.Should().Contain(f => f.FileType == "ApplicationDbContext");
    }

    [Fact]
    public void Scaffold_WithCommonFiles_AllFilesHave100PercentConfidence()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        result.CreatedFiles.Should().NotBeEmpty();
        result.CreatedFiles.Should().AllSatisfy(f => f.Confidence.Should().Be(100));
    }

    [Fact]
    public void Scaffold_WithoutCommonFiles_DoesNotGenerateFiles()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = true,
            CreateInfrastructureLayer = true,
            GenerateCommonFiles = false
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "TestApp", options);

        // Assert
        result.Success.Should().BeTrue();
        result.CreatedFiles.Should().BeEmpty();
    }

    [Fact]
    public void Scaffold_WithComplexNamespace_UsesCorrectNamespaceInFiles()
    {
        // Arrange
        var options = new ScaffoldOptions
        {
            CreateDomainLayer = true,
            CreateApplicationLayer = false,
            CreateInfrastructureLayer = false,
            GenerateCommonFiles = true
        };

        // Act
        var result = _scaffolder.Scaffold(_tempPath, "Company.Product.Module", options);

        // Assert
        result.Success.Should().BeTrue();
        var baseEntityPath = Path.Combine(_tempPath, "Domain", "Common", "BaseEntity.cs");
        var content = File.ReadAllText(baseEntityPath);
        content.Should().Contain("namespace Company.Product.Module.Domain.Common");
    }
}
