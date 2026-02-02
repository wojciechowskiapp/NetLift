using System.Text;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Scaffolding;

/// <summary>
/// Scaffolds Clean Architecture folder structure and generates common infrastructure files.
/// Creates Domain, Application, and Infrastructure layers with standard patterns and helpers.
/// </summary>
public sealed class CleanArchitectureScaffolder : IProjectScaffolder
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    private readonly IHandlerGenerator _handlerGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanArchitectureScaffolder"/> class.
    /// </summary>
    /// <param name="handlerGenerator">The handler generator for creating common classes like Result.</param>
    public CleanArchitectureScaffolder(IHandlerGenerator handlerGenerator)
    {
        _handlerGenerator = handlerGenerator ?? throw new ArgumentNullException(nameof(handlerGenerator));
    }

    /// <inheritdoc />
    public ScaffoldResult Scaffold(string projectPath, string rootNamespace, ScaffoldOptions options)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return new ScaffoldResult
            {
                Success = false,
                Errors = ["Project path cannot be null or whitespace."]
            };
        }

        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            return new ScaffoldResult
            {
                Success = false,
                Errors = ["Root namespace cannot be null or whitespace."]
            };
        }

        var createdDirectories = new List<string>();
        var createdFiles = new List<GeneratedFileInfo>();
        var errors = new List<string>();

        try
        {
            // Create Domain layer
            if (options.CreateDomainLayer)
            {
                CreateDomainLayer(projectPath, rootNamespace, createdDirectories, createdFiles, errors, options.GenerateCommonFiles);
            }

            // Create Application layer
            if (options.CreateApplicationLayer)
            {
                CreateApplicationLayer(projectPath, rootNamespace, createdDirectories, createdFiles, errors, options.GenerateCommonFiles);
            }

            // Create Infrastructure layer
            if (options.CreateInfrastructureLayer)
            {
                CreateInfrastructureLayer(projectPath, rootNamespace, createdDirectories, createdFiles, errors, options.GenerateCommonFiles);
            }

            return new ScaffoldResult
            {
                Success = errors.Count == 0,
                CreatedDirectories = createdDirectories,
                CreatedFiles = createdFiles,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Scaffolding failed: {ex.Message}");
            return new ScaffoldResult
            {
                Success = false,
                CreatedDirectories = createdDirectories,
                CreatedFiles = createdFiles,
                Errors = errors
            };
        }
    }

    private void CreateDomainLayer(
        string projectPath,
        string rootNamespace,
        List<string> createdDirectories,
        List<GeneratedFileInfo> createdFiles,
        List<string> errors,
        bool generateCommonFiles)
    {
        var domainPath = Path.Combine(projectPath, "Domain");

        // Create folder structure
        var folders = new[]
        {
            domainPath,
            Path.Combine(domainPath, "Common"),
            Path.Combine(domainPath, "Entities"),
            Path.Combine(domainPath, "Enums"),
            Path.Combine(domainPath, "Events"),
            Path.Combine(domainPath, "Exceptions")
        };

        foreach (var folder in folders)
        {
            CreateDirectory(folder, createdDirectories, errors);
        }

        if (!generateCommonFiles) return;

        // Generate common base classes
        var commonPath = Path.Combine(domainPath, "Common");

        // BaseEntity.cs
        var baseEntityContent = GenerateBaseEntity($"{rootNamespace}.Domain.Common");
        WriteFile(Path.Combine(commonPath, "BaseEntity.cs"), baseEntityContent, "BaseEntity", createdFiles, errors);

        // BaseEvent.cs
        var baseEventContent = GenerateBaseEvent($"{rootNamespace}.Domain.Common");
        WriteFile(Path.Combine(commonPath, "BaseEvent.cs"), baseEventContent, "BaseEvent", createdFiles, errors);

        // ValueObject.cs
        var valueObjectContent = GenerateValueObject($"{rootNamespace}.Domain.Common");
        WriteFile(Path.Combine(commonPath, "ValueObject.cs"), valueObjectContent, "ValueObject", createdFiles, errors);
    }

    private void CreateApplicationLayer(
        string projectPath,
        string rootNamespace,
        List<string> createdDirectories,
        List<GeneratedFileInfo> createdFiles,
        List<string> errors,
        bool generateCommonFiles)
    {
        var applicationPath = Path.Combine(projectPath, "Application");

        // Create folder structure
        var folders = new[]
        {
            applicationPath,
            Path.Combine(applicationPath, "Common"),
            Path.Combine(applicationPath, "Common", "Behaviors"),
            Path.Combine(applicationPath, "Common", "Exceptions"),
            Path.Combine(applicationPath, "Common", "Interfaces"),
            Path.Combine(applicationPath, "Common", "Mappings"),
            Path.Combine(applicationPath, "Common", "Models")
        };

        foreach (var folder in folders)
        {
            CreateDirectory(folder, createdDirectories, errors);
        }

        if (!generateCommonFiles) return;

        var commonPath = Path.Combine(applicationPath, "Common");

        // Behaviors
        var behaviorsPath = Path.Combine(commonPath, "Behaviors");
        var validationBehaviorContent = GenerateValidationBehavior($"{rootNamespace}.Application.Common.Behaviors");
        WriteFile(Path.Combine(behaviorsPath, "ValidationBehavior.cs"), validationBehaviorContent, "ValidationBehavior", createdFiles, errors);

        var loggingBehaviorContent = GenerateLoggingBehavior($"{rootNamespace}.Application.Common.Behaviors");
        WriteFile(Path.Combine(behaviorsPath, "LoggingBehavior.cs"), loggingBehaviorContent, "LoggingBehavior", createdFiles, errors);

        var unhandledExceptionBehaviorContent = GenerateUnhandledExceptionBehavior($"{rootNamespace}.Application.Common.Behaviors");
        WriteFile(Path.Combine(behaviorsPath, "UnhandledExceptionBehavior.cs"), unhandledExceptionBehaviorContent, "UnhandledExceptionBehavior", createdFiles, errors);

        // Exceptions
        var exceptionsPath = Path.Combine(commonPath, "Exceptions");
        var notFoundExceptionContent = GenerateNotFoundException($"{rootNamespace}.Application.Common.Exceptions");
        WriteFile(Path.Combine(exceptionsPath, "NotFoundException.cs"), notFoundExceptionContent, "NotFoundException", createdFiles, errors);

        var validationExceptionContent = GenerateValidationException($"{rootNamespace}.Application.Common.Exceptions");
        WriteFile(Path.Combine(exceptionsPath, "ValidationException.cs"), validationExceptionContent, "ValidationException", createdFiles, errors);

        // Interfaces
        var interfacesPath = Path.Combine(commonPath, "Interfaces");
        var dbContextInterfaceContent = GenerateApplicationDbContextInterface($"{rootNamespace}.Application.Common.Interfaces");
        WriteFile(Path.Combine(interfacesPath, "IApplicationDbContext.cs"), dbContextInterfaceContent, "IApplicationDbContext", createdFiles, errors);

        // Mappings
        var mappingsPath = Path.Combine(commonPath, "Mappings");
        var mapFromContent = GenerateIMapFrom($"{rootNamespace}.Application.Common.Mappings");
        WriteFile(Path.Combine(mappingsPath, "IMapFrom.cs"), mapFromContent, "IMapFrom", createdFiles, errors);

        // Models
        var modelsPath = Path.Combine(commonPath, "Models");
        var resultContent = _handlerGenerator.GenerateResultClass($"{rootNamespace}.Application.Common.Models");
        WriteFile(Path.Combine(modelsPath, "Result.cs"), resultContent, "Result", createdFiles, errors);

        var paginatedListContent = GeneratePaginatedList($"{rootNamespace}.Application.Common.Models");
        WriteFile(Path.Combine(modelsPath, "PaginatedList.cs"), paginatedListContent, "PaginatedList", createdFiles, errors);

        // DependencyInjection.cs
        var dependencyInjectionContent = GenerateApplicationDependencyInjection($"{rootNamespace}.Application");
        WriteFile(Path.Combine(applicationPath, "DependencyInjection.cs"), dependencyInjectionContent, "DependencyInjection", createdFiles, errors);
    }

    private void CreateInfrastructureLayer(
        string projectPath,
        string rootNamespace,
        List<string> createdDirectories,
        List<GeneratedFileInfo> createdFiles,
        List<string> errors,
        bool generateCommonFiles)
    {
        var infrastructurePath = Path.Combine(projectPath, "Infrastructure");

        // Create folder structure
        var folders = new[]
        {
            infrastructurePath,
            Path.Combine(infrastructurePath, "Persistence"),
            Path.Combine(infrastructurePath, "Persistence", "Configurations"),
            Path.Combine(infrastructurePath, "Services")
        };

        foreach (var folder in folders)
        {
            CreateDirectory(folder, createdDirectories, errors);
        }

        if (!generateCommonFiles) return;

        var persistencePath = Path.Combine(infrastructurePath, "Persistence");

        // ApplicationDbContext.cs
        var dbContextContent = GenerateApplicationDbContext($"{rootNamespace}.Infrastructure.Persistence", rootNamespace);
        WriteFile(Path.Combine(persistencePath, "ApplicationDbContext.cs"), dbContextContent, "ApplicationDbContext", createdFiles, errors);

        // ApplicationDbContextInitializer.cs
        var initializerContent = GenerateApplicationDbContextInitializer($"{rootNamespace}.Infrastructure.Persistence");
        WriteFile(Path.Combine(persistencePath, "ApplicationDbContextInitializer.cs"), initializerContent, "ApplicationDbContextInitializer", createdFiles, errors);

        // DependencyInjection.cs
        var dependencyInjectionContent = GenerateInfrastructureDependencyInjection($"{rootNamespace}.Infrastructure", rootNamespace);
        WriteFile(Path.Combine(infrastructurePath, "DependencyInjection.cs"), dependencyInjectionContent, "DependencyInjection", createdFiles, errors);
    }

    private static void CreateDirectory(string path, List<string> createdDirectories, List<string> errors)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                createdDirectories.Add(path);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to create directory '{path}': {ex.Message}");
        }
    }

    private static void WriteFile(
        string filePath,
        string content,
        string fileType,
        List<GeneratedFileInfo> createdFiles,
        List<string> errors)
    {
        try
        {
            File.WriteAllText(filePath, content);
            createdFiles.Add(new GeneratedFileInfo
            {
                FilePath = filePath,
                FileType = fileType,
                Confidence = 100
            });
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to write file '{filePath}': {ex.Message}");
        }
    }

    #region Domain Layer Generators

    private static string GenerateBaseEntity(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base class for all entities with identity.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public abstract class BaseEntity");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets or sets the unique identifier for this entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public int Id {{ get; set; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}private readonly List<BaseEvent> _domainEvents = [];");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the domain events for this entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Adds a domain event to this entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public void AddDomainEvent(BaseEvent domainEvent)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_domainEvents.Add(domainEvent);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Removes a domain event from this entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public void RemoveDomainEvent(BaseEvent domainEvent)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_domainEvents.Remove(domainEvent);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Clears all domain events from this entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public void ClearDomainEvents()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_domainEvents.Clear();");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateBaseEvent(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base class for all domain events.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public abstract class BaseEvent : INotification");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the date and time when this event occurred.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public DateTimeOffset OccurredOn {{ get; }} = DateTimeOffset.UtcNow;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateValueObject(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base class for value objects with value-based equality.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public abstract class ValueObject");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the atomic values that make up this value object.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}protected abstract IEnumerable<object?> GetEqualityComponents();");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public override bool Equals(object? obj)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (obj == null || obj.GetType() != GetType())");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return false;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var other = (ValueObject)obj;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public override int GetHashCode()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return GetEqualityComponents()");
        sb.AppendLine($"{TripleIndent}.Select(x => x?.GetHashCode() ?? 0)");
        sb.AppendLine($"{TripleIndent}.Aggregate((x, y) => x ^ y);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Equality operator for value objects.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static bool operator ==(ValueObject? left, ValueObject? right)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (left is null ^ right is null)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return false;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return left is null || left.Equals(right);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Inequality operator for value objects.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static bool operator !=(ValueObject? left, ValueObject? right)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return !(left == right);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Application Layer Generators

    private static string GenerateValidationBehavior(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that validates requests using FluentValidation validators.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TRequest\">The request type.</typeparam>");
        sb.AppendLine("/// <typeparam name=\"TResponse\">The response type.</typeparam>");
        sb.AppendLine("public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly IEnumerable<IValidator<TRequest>> _validators;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"ValidationBehavior{{TRequest, TResponse}}\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"validators\">The validators for the request.</param>");
        sb.AppendLine($"{Indent}public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_validators = validators;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (_validators.Any())");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}var context = new ValidationContext<TRequest>(request);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}var validationResults = await Task.WhenAll(");
        sb.AppendLine($"{TripleIndent}{Indent}_validators.Select(v => v.ValidateAsync(context, cancellationToken)));");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}var failures = validationResults");
        sb.AppendLine($"{TripleIndent}{Indent}.SelectMany(r => r.Errors)");
        sb.AppendLine($"{TripleIndent}{Indent}.Where(f => f != null)");
        sb.AppendLine($"{TripleIndent}{Indent}.ToList();");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}if (failures.Count != 0)");
        sb.AppendLine($"{TripleIndent}{{");
        sb.AppendLine($"{TripleIndent}{Indent}throw new ValidationException(failures);");
        sb.AppendLine($"{TripleIndent}}}");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return await next();");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateLoggingBehavior(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that logs request execution.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TRequest\">The request type.</typeparam>");
        sb.AppendLine("/// <typeparam name=\"TResponse\">The response type.</typeparam>");
        sb.AppendLine("public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"LoggingBehavior{{TRequest, TResponse}}\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"logger\">The logger.</param>");
        sb.AppendLine($"{Indent}public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Handling {{RequestName}}\", requestName);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var response = await next();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Handled {{RequestName}}\", requestName);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return response;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateUnhandledExceptionBehavior(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that catches and logs unhandled exceptions.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TRequest\">The request type.</typeparam>");
        sb.AppendLine("/// <typeparam name=\"TResponse\">The response type.</typeparam>");
        sb.AppendLine("public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"UnhandledExceptionBehavior{{TRequest, TResponse}}\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"logger\">The logger.</param>");
        sb.AppendLine($"{Indent}public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return await next();");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}_logger.LogError(ex, \"Unhandled exception for request {{RequestName}}\", requestName);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateNotFoundException(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Exception thrown when a requested entity is not found.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NotFoundException : Exception");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"NotFoundException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public NotFoundException()");
        sb.AppendLine($"{Indent}{Indent}: base()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"NotFoundException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"message\">The error message.</param>");
        sb.AppendLine($"{Indent}public NotFoundException(string message)");
        sb.AppendLine($"{Indent}{Indent}: base(message)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"NotFoundException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"message\">The error message.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"innerException\">The inner exception.</param>");
        sb.AppendLine($"{Indent}public NotFoundException(string message, Exception innerException)");
        sb.AppendLine($"{Indent}{Indent}: base(message, innerException)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"NotFoundException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"name\">The name of the entity.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"key\">The key of the entity.</param>");
        sb.AppendLine($"{Indent}public NotFoundException(string name, object key)");
        sb.AppendLine($"{Indent}{Indent}: base($\"Entity \\\"{{name}}\\\" ({{key}}) was not found.\")");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateValidationException(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using FluentValidation.Results;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Exception thrown when one or more validation failures occur.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ValidationException : Exception");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"ValidationException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public ValidationException()");
        sb.AppendLine($"{Indent}{Indent}: base(\"One or more validation failures have occurred.\")");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Errors = new Dictionary<string, string[]>();");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"ValidationException\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"failures\">The validation failures.</param>");
        sb.AppendLine($"{Indent}public ValidationException(IEnumerable<ValidationFailure> failures)");
        sb.AppendLine($"{Indent}{Indent}: this()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Errors = failures");
        sb.AppendLine($"{TripleIndent}.GroupBy(e => e.PropertyName, e => e.ErrorMessage)");
        sb.AppendLine($"{TripleIndent}.ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the validation errors.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public IDictionary<string, string[]> Errors {{ get; }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateApplicationDbContextInterface(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines the application database context contract for dependency injection.");
        sb.AppendLine("/// Implemented by the infrastructure layer's DbContext.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IApplicationDbContext");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Asynchronously saves all changes made in this context to the database.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">A token to cancel the operation.</param>");
        sb.AppendLine($"{Indent}/// <returns>The number of state entries written to the database.</returns>");
        sb.AppendLine($"{Indent}Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateIMapFrom(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using AutoMapper;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for AutoMapper profile configuration.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The source type to map from.</typeparam>");
        sb.AppendLine("public interface IMapFrom<T>");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Configures the mapping from the source type to this type.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"profile\">The AutoMapper profile.</param>");
        sb.AppendLine($"{Indent}void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GeneratePaginatedList(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents a paginated list of items.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The type of items in the list.</typeparam>");
        sb.AppendLine("public class PaginatedList<T>");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the items in the current page.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public IReadOnlyCollection<T> Items {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the current page number.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public int PageNumber {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the total number of pages.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public int TotalPages {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the total number of items across all pages.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public int TotalCount {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a value indicating whether there is a previous page.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public bool HasPreviousPage => PageNumber > 1;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a value indicating whether there is a next page.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public bool HasNextPage => PageNumber < TotalPages;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}private PaginatedList(IReadOnlyCollection<T> items, int count, int pageNumber, int pageSize)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Items = items;");
        sb.AppendLine($"{DoubleIndent}PageNumber = pageNumber;");
        sb.AppendLine($"{DoubleIndent}TotalPages = (int)Math.Ceiling(count / (double)pageSize);");
        sb.AppendLine($"{DoubleIndent}TotalCount = count;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a paginated list from an IQueryable source.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"source\">The queryable source.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"pageNumber\">The page number (1-based).</param>");
        sb.AppendLine($"{Indent}/// <param name=\"pageSize\">The page size.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">A token to cancel the operation.</param>");
        sb.AppendLine($"{Indent}/// <returns>A paginated list.</returns>");
        sb.AppendLine($"{Indent}public static async Task<PaginatedList<T>> CreateAsync(");
        sb.AppendLine($"{DoubleIndent}IQueryable<T> source,");
        sb.AppendLine($"{DoubleIndent}int pageNumber,");
        sb.AppendLine($"{DoubleIndent}int pageSize,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var count = await source.CountAsync(cancellationToken);");
        sb.AppendLine($"{DoubleIndent}var items = await source");
        sb.AppendLine($"{TripleIndent}.Skip((pageNumber - 1) * pageSize)");
        sb.AppendLine($"{TripleIndent}.Take(pageSize)");
        sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new PaginatedList<T>(items, count, pageNumber, pageSize);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateApplicationDependencyInjection(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dependency injection configuration for the Application layer.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Adds Application layer services to the service collection.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine($"{Indent}/// <returns>The service collection for chaining.</returns>");
        sb.AppendLine($"{Indent}public static IServiceCollection AddApplication(this IServiceCollection services)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var assembly = Assembly.GetExecutingAssembly();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));");
        sb.AppendLine($"{DoubleIndent}services.AddValidatorsFromAssembly(assembly);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Common.Behaviors.ValidationBehavior<,>));");
        sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Common.Behaviors.UnhandledExceptionBehavior<,>));");
        sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Common.Behaviors.LoggingBehavior<,>));");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return services;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Infrastructure Layer Generators

    private static string GenerateApplicationDbContext(string namespaceName, string rootNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {rootNamespace}.Domain.Common;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// The application database context.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ApplicationDbContext : DbContext, IApplicationDbContext");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"ApplicationDbContext\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"options\">The DbContext options.</param>");
        sb.AppendLine($"{Indent}public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)");
        sb.AppendLine($"{Indent}{Indent}: base(options)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}// TODO: Add DbSet properties for your entities here");
        sb.AppendLine($"{Indent}// Example: public DbSet<Student> Students => Set<Student>();");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}protected override void OnModelCreating(ModelBuilder builder)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}base.OnModelCreating(builder);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateApplicationDbContextInitializer(string namespaceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Initializes and seeds the application database.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ApplicationDbContextInitializer");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<ApplicationDbContextInitializer> _logger;");
        sb.AppendLine($"{Indent}private readonly ApplicationDbContext _context;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"ApplicationDbContextInitializer\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"logger\">The logger.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"context\">The database context.</param>");
        sb.AppendLine($"{Indent}public ApplicationDbContextInitializer(");
        sb.AppendLine($"{DoubleIndent}ILogger<ApplicationDbContextInitializer> logger,");
        sb.AppendLine($"{DoubleIndent}ApplicationDbContext context)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{DoubleIndent}_context = context;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes the database by applying pending migrations.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public async Task InitializeAsync()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}await _context.Database.MigrateAsync();");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}_logger.LogError(ex, \"An error occurred while initializing the database.\");");
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Seeds the database with initial data.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public async Task SeedAsync()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}await TrySeedAsync();");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}_logger.LogError(ex, \"An error occurred while seeding the database.\");");
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}private async Task TrySeedAsync()");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}// TODO: Add seed data here");
        sb.AppendLine($"{DoubleIndent}await Task.CompletedTask;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateInfrastructureDependencyInjection(string namespaceName, string rootNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {namespaceName}.Persistence;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dependency injection configuration for the Infrastructure layer.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Adds Infrastructure layer services to the service collection.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"configuration\">The configuration.</param>");
        sb.AppendLine($"{Indent}/// <returns>The service collection for chaining.</returns>");
        sb.AppendLine($"{Indent}public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var connectionString = configuration.GetConnectionString(\"DefaultConnection\")");
        sb.AppendLine($"{TripleIndent}?? throw new InvalidOperationException(\"Connection string 'DefaultConnection' not found.\");");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}services.AddDbContext<ApplicationDbContext>(options =>");
        sb.AppendLine($"{TripleIndent}options.UseSqlServer(connectionString));");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}services.AddScoped<ApplicationDbContextInitializer>();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return services;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion
}
