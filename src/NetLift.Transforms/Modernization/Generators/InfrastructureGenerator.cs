using System.Text;
using NetLift.Core.Interfaces.Modernization;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates production-ready CQRS infrastructure code.
/// Creates pipeline behaviors, Result pattern, and supporting services.
/// </summary>
public sealed class InfrastructureGenerator : IInfrastructureGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";
    private const string QuadIndent = "                ";

    /// <inheritdoc />
    public Dictionary<string, string> GenerateAll(string rootNamespace, InfrastructureOptions options)
    {
        var files = new Dictionary<string, string>();

        // Core interfaces (always generated)
        files["Application/Common/IRequest.cs"] = GenerateMediatRInterfaces(rootNamespace);
        files["Application/Common/Result.cs"] = GenerateResultPattern(rootNamespace);
        files["Application/Common/PagedList.cs"] = GeneratePagedList(rootNamespace);
        files["Application/Common/Extensions/QueryableExtensions.cs"] = GenerateQueryableExtensions(rootNamespace);

        // Service interfaces
        files["Application/Common/Interfaces/ICurrentUserService.cs"] = GenerateCurrentUserService(rootNamespace);
        files["Application/Common/Interfaces/IDateTime.cs"] = GenerateDateTimeService(rootNamespace);
        files["Application/Common/Interfaces/IApplicationDbContext.cs"] = GenerateApplicationDbContext(rootNamespace);

        // Pipeline behaviors
        if (options.IncludeValidationBehavior)
            files["Application/Common/Behaviors/ValidationBehavior.cs"] = GenerateValidationBehavior(rootNamespace);

        if (options.IncludeLoggingBehavior)
            files["Application/Common/Behaviors/LoggingBehavior.cs"] = GenerateLoggingBehavior(rootNamespace);

        if (options.IncludeTransactionBehavior)
            files["Application/Common/Behaviors/TransactionBehavior.cs"] = GenerateTransactionBehavior(rootNamespace);

        if (options.IncludePerformanceBehavior)
            files["Application/Common/Behaviors/PerformanceBehavior.cs"] = GeneratePerformanceBehavior(rootNamespace, options.SlowRequestThresholdMs);

        if (options.IncludeUnhandledExceptionBehavior)
            files["Application/Common/Behaviors/UnhandledExceptionBehavior.cs"] = GenerateUnhandledExceptionBehavior(rootNamespace);

        // Mapping profile if AutoMapper included
        if (options.IncludeAutoMapper)
            files["Application/Common/Mappings/MappingExtensions.cs"] = GenerateMappingExtensions(rootNamespace);

        // Caching infrastructure
        if (options.IncludeCaching)
        {
            files["Application/Common/Interfaces/ICacheService.cs"] = GenerateCacheServiceInterface(rootNamespace);
            files["Application/Common/Behaviors/CachingBehavior.cs"] = GenerateCachingBehavior(rootNamespace);
        }

        // DI registration
        files["Application/DependencyInjection.cs"] = GenerateDependencyInjection(rootNamespace, options);

        return files;
    }

    /// <inheritdoc />
    public string GenerateValidationBehavior(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that validates requests using FluentValidation.");
        sb.AppendLine("/// Runs all validators for the request type and throws ValidationException on failure.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly IEnumerable<IValidator<TRequest>> _validators;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_validators = validators;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (!_validators.Any())");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return await next().ConfigureAwait(false);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var context = new ValidationContext<TRequest>(request);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var validationResults = await Task.WhenAll(");
        sb.AppendLine($"{TripleIndent}_validators.Select(v => v.ValidateAsync(context, cancellationToken)));");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var failures = validationResults");
        sb.AppendLine($"{TripleIndent}.SelectMany(r => r.Errors)");
        sb.AppendLine($"{TripleIndent}.Where(f => f is not null)");
        sb.AppendLine($"{TripleIndent}.ToList();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}if (failures.Count != 0)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}throw new ValidationException(failures);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return await next().ConfigureAwait(false);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateLoggingBehavior(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that logs request execution with correlation IDs and timing.");
        sb.AppendLine("/// Provides structured logging for all CQRS operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine($"{Indent}private readonly ICurrentUserService _currentUser;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public LoggingBehavior(");
        sb.AppendLine($"{DoubleIndent}ILogger<LoggingBehavior<TRequest, TResponse>> logger,");
        sb.AppendLine($"{DoubleIndent}ICurrentUserService currentUser)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{DoubleIndent}_currentUser = currentUser;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine($"{DoubleIndent}var userId = _currentUser.UserId;");
        sb.AppendLine($"{DoubleIndent}var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_logger.LogInformation(");
        sb.AppendLine($"{TripleIndent}\"[START] {{RequestName}} by User {{UserId}} | CorrelationId: {{CorrelationId}}\",");
        sb.AppendLine($"{TripleIndent}requestName, userId, correlationId);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var stopwatch = Stopwatch.StartNew();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}var response = await next().ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}stopwatch.Stop();");
        sb.AppendLine($"{TripleIndent}_logger.LogInformation(");
        sb.AppendLine($"{QuadIndent}\"[END] {{RequestName}} completed in {{ElapsedMs}}ms | CorrelationId: {{CorrelationId}}\",");
        sb.AppendLine($"{QuadIndent}requestName, stopwatch.ElapsedMilliseconds, correlationId);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}return response;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}stopwatch.Stop();");
        sb.AppendLine($"{TripleIndent}_logger.LogError(");
        sb.AppendLine($"{QuadIndent}ex,");
        sb.AppendLine($"{QuadIndent}\"[ERROR] {{RequestName}} failed after {{ElapsedMs}}ms | CorrelationId: {{CorrelationId}}\",");
        sb.AppendLine($"{QuadIndent}requestName, stopwatch.ElapsedMilliseconds, correlationId);");
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateTransactionBehavior(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that wraps command handlers in a database transaction.");
        sb.AppendLine("/// Implements Unit of Work pattern - commits on success, rolls back on failure.");
        sb.AppendLine("/// Only applies to commands (requests ending with 'Command').");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly IApplicationDbContext _context;");
        sb.AppendLine($"{Indent}private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public TransactionBehavior(");
        sb.AppendLine($"{DoubleIndent}IApplicationDbContext context,");
        sb.AppendLine($"{DoubleIndent}ILogger<TransactionBehavior<TRequest, TResponse>> logger)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_context = context;");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Only wrap commands in transactions, not queries");
        sb.AppendLine($"{DoubleIndent}if (!requestName.EndsWith(\"Command\", StringComparison.Ordinal))");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return await next().ConfigureAwait(false);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}await using var transaction = await _context.Database");
        sb.AppendLine($"{TripleIndent}.BeginTransactionAsync(cancellationToken)");
        sb.AppendLine($"{TripleIndent}.ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}_logger.LogDebug(\"Beginning transaction for {{RequestName}}\", requestName);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}var response = await next().ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine($"{TripleIndent}_logger.LogDebug(\"Transaction committed for {{RequestName}}\", requestName);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}return response;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}_logger.LogError(ex, \"Transaction rolled back for {{RequestName}}\", requestName);");
        sb.AppendLine($"{TripleIndent}await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GeneratePerformanceBehavior(string rootNamespace, int slowRequestThresholdMs = 500)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that detects slow requests and logs warnings.");
        sb.AppendLine($"/// Requests taking longer than {slowRequestThresholdMs}ms are flagged for review.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine($"{Indent}private readonly ICurrentUserService _currentUser;");
        sb.AppendLine($"{Indent}private const int SlowRequestThresholdMs = {slowRequestThresholdMs};");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public PerformanceBehavior(");
        sb.AppendLine($"{DoubleIndent}ILogger<PerformanceBehavior<TRequest, TResponse>> logger,");
        sb.AppendLine($"{DoubleIndent}ICurrentUserService currentUser)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{DoubleIndent}_currentUser = currentUser;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var stopwatch = Stopwatch.StartNew();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var response = await next().ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}stopwatch.Stop();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var elapsedMs = stopwatch.ElapsedMilliseconds;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}if (elapsedMs > SlowRequestThresholdMs)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine($"{TripleIndent}var userId = _currentUser.UserId;");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}_logger.LogWarning(");
        sb.AppendLine($"{QuadIndent}\"[SLOW] {{RequestName}} took {{ElapsedMs}}ms (threshold: {{ThresholdMs}}ms) | User: {{UserId}}\",");
        sb.AppendLine($"{QuadIndent}requestName, elapsedMs, SlowRequestThresholdMs, userId);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return response;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateUnhandledExceptionBehavior(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that catches and logs unhandled exceptions.");
        sb.AppendLine("/// Provides global error handling for all CQRS requests.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}try");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return await next().ConfigureAwait(false);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{DoubleIndent}catch (Exception ex)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}var requestName = typeof(TRequest).Name;");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}_logger.LogError(");
        sb.AppendLine($"{QuadIndent}ex,");
        sb.AppendLine($"{QuadIndent}\"Unhandled exception for request {{RequestName}} {{@Request}}\",");
        sb.AppendLine($"{QuadIndent}requestName, request);");
        sb.AppendLine();
        sb.AppendLine($"{TripleIndent}throw;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateResultPattern(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents the result of an operation with success/failure status.");
        sb.AppendLine("/// Provides a clean alternative to throwing exceptions for business logic errors.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class Result");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}protected Result(bool isSuccess, Error error)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (isSuccess && error != Error.None)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}throw new InvalidOperationException(\"Success result cannot have an error.\");");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}if (!isSuccess && error == Error.None)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}throw new InvalidOperationException(\"Failure result must have an error.\");");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}IsSuccess = isSuccess;");
        sb.AppendLine($"{DoubleIndent}Error = error;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public bool IsSuccess {{ get; }}");
        sb.AppendLine($"{Indent}public bool IsFailure => !IsSuccess;");
        sb.AppendLine($"{Indent}public Error Error {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static Result Success() => new(true, Error.None);");
        sb.AppendLine($"{Indent}public static Result Failure(Error error) => new(false, error);");
        sb.AppendLine($"{Indent}public static Result Failure(string message) => new(false, new Error(\"General.Failure\", message));");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static Result<T> Success<T>(T value) => new(value, true, Error.None);");
        sb.AppendLine($"{Indent}public static Result<T> Failure<T>(Error error) => new(default!, false, error);");
        sb.AppendLine($"{Indent}public static Result<T> Failure<T>(string message) => new(default!, false, new Error(\"General.Failure\", message));");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static Result<T> Create<T>(T? value) =>");
        sb.AppendLine($"{DoubleIndent}value is not null ? Success(value) : Failure<T>(Error.NullValue);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents the result of an operation that returns a value.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class Result<T> : Result");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly T _value;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}protected internal Result(T value, bool isSuccess, Error error)");
        sb.AppendLine($"{DoubleIndent}: base(isSuccess, error)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_value = value;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public T Value => IsSuccess");
        sb.AppendLine($"{DoubleIndent}? _value");
        sb.AppendLine($"{DoubleIndent}: throw new InvalidOperationException(\"Cannot access value of a failed result.\");");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static implicit operator Result<T>(T value) => Create(value);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static Result<T> Success(T value) => new(value, true, Error.None);");
        sb.AppendLine($"{Indent}public new static Result<T> Failure(Error error) => new(default!, false, error);");
        sb.AppendLine($"{Indent}public new static Result<T> Failure(string message) => new(default!, false, new Error(\"General.Failure\", message));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents an error with a code and message.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed record Error(string Code, string Message)");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}public static readonly Error None = new(string.Empty, string.Empty);");
        sb.AppendLine($"{Indent}public static readonly Error NullValue = new(\"Error.NullValue\", \"The specified value was null.\");");
        sb.AppendLine($"{Indent}public static readonly Error NotFound = new(\"Error.NotFound\", \"The requested resource was not found.\");");
        sb.AppendLine($"{Indent}public static readonly Error Validation = new(\"Error.Validation\", \"A validation error occurred.\");");
        sb.AppendLine($"{Indent}public static readonly Error Conflict = new(\"Error.Conflict\", \"A conflict occurred.\");");
        sb.AppendLine($"{Indent}public static readonly Error Unauthorized = new(\"Error.Unauthorized\", \"User is not authorized.\");");
        sb.AppendLine($"{Indent}public static readonly Error Forbidden = new(\"Error.Forbidden\", \"Access is forbidden.\");");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static Error Custom(string code, string message) => new(code, message);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents a validation error for a specific property.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed record ValidationError(string PropertyName, string ErrorMessage) : Error(\"Validation.Error\", ErrorMessage);");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GeneratePagedList(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents a paginated list of items with metadata.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class PagedList<T>");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}public IReadOnlyList<T> Items {{ get; }}");
        sb.AppendLine($"{Indent}public int PageNumber {{ get; }}");
        sb.AppendLine($"{Indent}public int PageSize {{ get; }}");
        sb.AppendLine($"{Indent}public int TotalCount {{ get; }}");
        sb.AppendLine($"{Indent}public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);");
        sb.AppendLine($"{Indent}public bool HasPreviousPage => PageNumber > 1;");
        sb.AppendLine($"{Indent}public bool HasNextPage => PageNumber < TotalPages;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public PagedList(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Items = items;");
        sb.AppendLine($"{DoubleIndent}PageNumber = pageNumber;");
        sb.AppendLine($"{DoubleIndent}PageSize = pageSize;");
        sb.AppendLine($"{DoubleIndent}TotalCount = totalCount;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public static PagedList<T> Empty => new(Array.Empty<T>(), 1, 10, 0);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public PagedList<TResult> Map<TResult>(Func<T, TResult> selector)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return new PagedList<TResult>(");
        sb.AppendLine($"{TripleIndent}Items.Select(selector).ToList(),");
        sb.AppendLine($"{TripleIndent}PageNumber,");
        sb.AppendLine($"{TripleIndent}PageSize,");
        sb.AppendLine($"{TripleIndent}TotalCount);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateQueryableExtensions(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Extensions;");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for IQueryable to support pagination and common operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class QueryableExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a paginated list from the query.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static async Task<PagedList<T>> ToPagedListAsync<T>(");
        sb.AppendLine($"{DoubleIndent}this IQueryable<T> source,");
        sb.AppendLine($"{DoubleIndent}int pageNumber,");
        sb.AppendLine($"{DoubleIndent}int pageSize,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var totalCount = await source.CountAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var items = await source");
        sb.AppendLine($"{TripleIndent}.Skip((pageNumber - 1) * pageSize)");
        sb.AppendLine($"{TripleIndent}.Take(pageSize)");
        sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken)");
        sb.AppendLine($"{TripleIndent}.ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new PagedList<T>(items, pageNumber, pageSize, totalCount);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a paginated list from the query (synchronous).");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static PagedList<T> ToPagedList<T>(");
        sb.AppendLine($"{DoubleIndent}this IQueryable<T> source,");
        sb.AppendLine($"{DoubleIndent}int pageNumber,");
        sb.AppendLine($"{DoubleIndent}int pageSize)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var totalCount = source.Count();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var items = source");
        sb.AppendLine($"{TripleIndent}.Skip((pageNumber - 1) * pageSize)");
        sb.AppendLine($"{TripleIndent}.Take(pageSize)");
        sb.AppendLine($"{TripleIndent}.ToList();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new PagedList<T>(items, pageNumber, pageSize, totalCount);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Applies AsNoTracking for read-only queries (optimization).");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static IQueryable<T> ReadOnly<T>(this IQueryable<T> source) where T : class");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return source.AsNoTracking();");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Applies conditional Where clause.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static IQueryable<T> WhereIf<T>(");
        sb.AppendLine($"{DoubleIndent}this IQueryable<T> source,");
        sb.AppendLine($"{DoubleIndent}bool condition,");
        sb.AppendLine($"{DoubleIndent}System.Linq.Expressions.Expression<Func<T, bool>> predicate)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return condition ? source.Where(predicate) : source;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateMediatRInterfaces(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common;");
        sb.AppendLine();
        sb.AppendLine("// Re-export MediatR interfaces for convenience");
        sb.AppendLine("// This allows commands/queries to reference interfaces without direct MediatR dependency");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for a request with a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IRequest<out TResponse> : MediatR.IRequest<TResponse> { }");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for a request without a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IRequest : MediatR.IRequest { }");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a handler for a request.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IRequestHandler<in TRequest, TResponse> : MediatR.IRequestHandler<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : MediatR.IRequest<TResponse> {{ }}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a handler for a request without a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IRequestHandler<in TRequest> : MediatR.IRequestHandler<TRequest>");
        sb.AppendLine($"{Indent}where TRequest : MediatR.IRequest {{ }}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateCurrentUserService(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides access to the current user's identity for audit trails.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface ICurrentUserService");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the current user's ID, or null if not authenticated.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}string? UserId {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the current user's name, or null if not authenticated.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}string? UserName {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets whether the current user is authenticated.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}bool IsAuthenticated {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Checks if the current user has a specific role.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}bool IsInRole(string role);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateDateTimeService(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides testable access to date/time values.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IDateTime");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the current UTC date and time.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}DateTime UtcNow {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the current local date and time.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}DateTime Now {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets today's date (local).");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}DateOnly Today {{ get; }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateDependencyInjection(string rootNamespace, InfrastructureOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application;");
        sb.AppendLine();
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for registering Application layer services.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Adds Application layer services to the DI container.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static IServiceCollection AddApplicationServices(this IServiceCollection services)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var assembly = Assembly.GetExecutingAssembly();");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Register MediatR handlers");
        sb.AppendLine($"{DoubleIndent}services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Register FluentValidation validators");
        sb.AppendLine($"{DoubleIndent}services.AddValidatorsFromAssembly(assembly);");
        sb.AppendLine();

        if (options.IncludeAutoMapper)
        {
            sb.AppendLine($"{DoubleIndent}// Register AutoMapper profiles");
            sb.AppendLine($"{DoubleIndent}services.AddAutoMapper(assembly);");
            sb.AppendLine();
        }

        sb.AppendLine($"{DoubleIndent}// Register pipeline behaviors (order matters!)");

        if (options.IncludeUnhandledExceptionBehavior)
            sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));");

        if (options.IncludeLoggingBehavior)
            sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));");

        if (options.IncludeValidationBehavior)
            sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));");

        if (options.IncludePerformanceBehavior)
            sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));");

        if (options.IncludeTransactionBehavior)
            sb.AppendLine($"{DoubleIndent}services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));");

        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return services;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates IApplicationDbContext interface.
    /// </summary>
    private string GenerateApplicationDbContext(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Infrastructure;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for the application's database context.");
        sb.AppendLine("/// Defines DbSet properties and common operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IApplicationDbContext");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}// TODO: Add DbSet<TEntity> properties for your entities");
        sb.AppendLine($"{Indent}// Example: DbSet<Student> Students {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the database facade for transaction management.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}DatabaseFacade Database {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Saves all pending changes to the database.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates mapping extensions for AutoMapper.
    /// </summary>
    private string GenerateMappingExtensions(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Mappings;");
        sb.AppendLine();
        sb.AppendLine("using AutoMapper;");
        sb.AppendLine("using AutoMapper.QueryableExtensions;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for mapping and projection.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class MappingExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Projects the query to DTOs and creates a paged list.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static async Task<PagedList<TDestination>> ToPagedListAsync<TDestination>(");
        sb.AppendLine($"{DoubleIndent}this IQueryable<TDestination> queryable,");
        sb.AppendLine($"{DoubleIndent}int pageNumber,");
        sb.AppendLine($"{DoubleIndent}int pageSize,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var items = await queryable");
        sb.AppendLine($"{TripleIndent}.Skip((pageNumber - 1) * pageSize)");
        sb.AppendLine($"{TripleIndent}.Take(pageSize)");
        sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken)");
        sb.AppendLine($"{TripleIndent}.ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new PagedList<TDestination>(items, pageNumber, pageSize, totalCount);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Projects the query to DTOs using AutoMapper and creates a paged list.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static async Task<PagedList<TDestination>> ProjectToPagedListAsync<TDestination>(");
        sb.AppendLine($"{DoubleIndent}this IQueryable queryable,");
        sb.AppendLine($"{DoubleIndent}IConfigurationProvider configuration,");
        sb.AppendLine($"{DoubleIndent}int pageNumber,");
        sb.AppendLine($"{DoubleIndent}int pageSize,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var totalCount = await queryable.Cast<object>().CountAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var items = await queryable");
        sb.AppendLine($"{TripleIndent}.ProjectTo<TDestination>(configuration)");
        sb.AppendLine($"{TripleIndent}.Skip((pageNumber - 1) * pageSize)");
        sb.AppendLine($"{TripleIndent}.Take(pageSize)");
        sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken)");
        sb.AppendLine($"{TripleIndent}.ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new PagedList<TDestination>(items, pageNumber, pageSize, totalCount);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates ICacheService interface.
    /// </summary>
    private string GenerateCacheServiceInterface(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for caching operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface ICacheService");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a cached item by key.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Sets a cached item with optional expiration.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Removes a cached item by key.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}Task RemoveAsync(string key, CancellationToken cancellationToken = default);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets or creates a cached item.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}Task<T> GetOrCreateAsync<T>(");
        sb.AppendLine($"{DoubleIndent}string key,");
        sb.AppendLine($"{DoubleIndent}Func<CancellationToken, Task<T>> factory,");
        sb.AppendLine($"{DoubleIndent}TimeSpan? expiration = null,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken = default);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates CachingBehavior.
    /// </summary>
    private string GenerateCachingBehavior(string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {rootNamespace}.Application.Common.Behaviors;");
        sb.AppendLine();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for cacheable queries.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface ICacheableQuery");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}string CacheKey {{ get; }}");
        sb.AppendLine($"{Indent}TimeSpan? CacheDuration {{ get; }}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline behavior that caches query responses.");
        sb.AppendLine("/// Only applies to queries implementing ICacheableQuery.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : notnull");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly ICacheService _cache;");
        sb.AppendLine($"{Indent}private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_cache = cache;");
        sb.AppendLine($"{DoubleIndent}_logger = logger;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}public async Task<TResponse> Handle(");
        sb.AppendLine($"{DoubleIndent}TRequest request,");
        sb.AppendLine($"{DoubleIndent}RequestHandlerDelegate<TResponse> next,");
        sb.AppendLine($"{DoubleIndent}CancellationToken cancellationToken)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}if (request is not ICacheableQuery cacheableQuery)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}return await next().ConfigureAwait(false);");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var cacheKey = cacheableQuery.CacheKey;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var cachedResponse = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);");
        sb.AppendLine($"{DoubleIndent}if (cachedResponse is not null)");
        sb.AppendLine($"{DoubleIndent}{{");
        sb.AppendLine($"{TripleIndent}_logger.LogDebug(\"Cache hit for {{CacheKey}}\", cacheKey);");
        sb.AppendLine($"{TripleIndent}return cachedResponse;");
        sb.AppendLine($"{DoubleIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_logger.LogDebug(\"Cache miss for {{CacheKey}}\", cacheKey);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var response = await next().ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}await _cache.SetAsync(cacheKey, response, cacheableQuery.CacheDuration, cancellationToken);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return response;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
