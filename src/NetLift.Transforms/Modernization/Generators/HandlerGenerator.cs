using System.Text;
using NetLift.Core.Interfaces.Modernization;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates supplementary handler components for MediatR-based architecture.
/// Produces Result wrapper classes, DTOs, and IApplicationDbContext interface using clean code generation.
/// </summary>
public sealed class HandlerGenerator : IHandlerGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <inheritdoc />
    public string GenerateResultClass(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Generate non-generic Result class first (for commands that don't return data)
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents the result of an operation that can either succeed or fail with an error.");
        sb.AppendLine("/// Used for commands that don't return data.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class Result");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a value indicating whether the operation was successful.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public bool IsSuccess {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the error message. Only available when IsSuccess is false.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public string? Error {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a value indicating whether the operation failed.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public bool IsFailure => !IsSuccess;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}protected Result(bool isSuccess, string? error)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}IsSuccess = isSuccess;");
        sb.AppendLine($"{DoubleIndent}Error = error;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a successful result.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <returns>A successful result.</returns>");
        sb.AppendLine($"{Indent}public static Result Success() => new(true, null);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a successful result with the specified value.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <typeparam name=\"T\">The type of the result value.</typeparam>");
        sb.AppendLine($"{Indent}/// <param name=\"value\">The result value.</param>");
        sb.AppendLine($"{Indent}/// <returns>A successful result with value.</returns>");
        sb.AppendLine($"{Indent}public static Result<T> Success<T>(T value) => Result<T>.Success(value);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a failed result with the specified error message.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"error\">The error message.</param>");
        sb.AppendLine($"{Indent}/// <returns>A failed result.</returns>");
        sb.AppendLine($"{Indent}public static Result Failure(string error) => new(false, error);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a failed result with the specified error message.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <typeparam name=\"T\">The type of the result value.</typeparam>");
        sb.AppendLine($"{Indent}/// <param name=\"error\">The error message.</param>");
        sb.AppendLine($"{Indent}/// <returns>A failed result.</returns>");
        sb.AppendLine($"{Indent}public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate generic Result<T> class
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents the result of an operation that can either succeed with a value or fail with an error.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The type of the result value.</typeparam>");
        sb.AppendLine("public class Result<T> : Result");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the result value. Only available when IsSuccess is true.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public T? Value {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}private Result(bool isSuccess, T? value, string? error)");
        sb.AppendLine($"{Indent}    : base(isSuccess, error)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Value = value;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a successful result with the specified value.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"value\">The result value.</param>");
        sb.AppendLine($"{Indent}/// <returns>A successful result.</returns>");
        sb.AppendLine($"{Indent}public static new Result<T> Success(T value) => new(true, value, null);");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a failed result with the specified error message.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"error\">The error message.</param>");
        sb.AppendLine($"{Indent}/// <returns>A failed result.</returns>");
        sb.AppendLine($"{Indent}public static new Result<T> Failure(string error) => new(false, default, error);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateDto(
        string entityName,
        IEnumerable<(string Name, string Type)> properties,
        string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Entity name cannot be null or whitespace.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        var propertyList = properties?.ToList() ?? throw new ArgumentNullException(nameof(properties));

        if (propertyList.Count == 0)
        {
            throw new ArgumentException("Properties collection cannot be empty.", nameof(properties));
        }

        var sb = new StringBuilder();
        var dtoName = $"{entityName}Dto";

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Data transfer object for {entityName} entity.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public record {dtoName}");
        sb.AppendLine("{");

        // Generate properties
        for (int i = 0; i < propertyList.Count; i++)
        {
            var (name, type) = propertyList[i];

            sb.AppendLine($"{Indent}/// <summary>");
            sb.AppendLine($"{Indent}/// Gets or initializes the {name}.");
            sb.AppendLine($"{Indent}/// </summary>");

            // Use init-only properties with appropriate defaults
            var defaultValue = GetDefaultValue(type);
            sb.AppendLine($"{Indent}public {type} {name} {{ get; init; }}{defaultValue}");

            // Add blank line between properties except after the last one
            if (i < propertyList.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateDbContextInterface(IEnumerable<string> entityNames, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        var entityList = entityNames?.ToList() ?? throw new ArgumentNullException(nameof(entityNames));

        if (entityList.Count == 0)
        {
            throw new ArgumentException("Entity names collection cannot be empty.", nameof(entityNames));
        }

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        // Add Models namespace - extract root namespace from interface namespace
        var rootNamespace = ExtractRootNamespace(namespaceName);
        sb.AppendLine($"using {rootNamespace}.Models;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines the application database context contract for dependency injection.");
        sb.AppendLine("/// Implemented by the infrastructure layer's DbContext.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IApplicationDbContext");
        sb.AppendLine("{");

        // Generate DbSet properties for each entity
        for (int i = 0; i < entityList.Count; i++)
        {
            var entityName = entityList[i];

            if (string.IsNullOrWhiteSpace(entityName))
            {
                continue;
            }

            sb.AppendLine($"{Indent}/// <summary>");
            sb.AppendLine($"{Indent}/// Gets the DbSet for {entityName} entities.");
            sb.AppendLine($"{Indent}/// </summary>");
            sb.AppendLine($"{Indent}DbSet<{entityName}> {MakePlural(entityName)} {{ get; }}");

            // Add blank line between properties except after the last one
            if (i < entityList.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Asynchronously saves all changes made in this context to the database.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">A token to cancel the operation.</param>");
        sb.AppendLine($"{Indent}/// <returns>The number of state entries written to the database.</returns>");
        sb.AppendLine($"{Indent}Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the default value initializer for a property type.
    /// </summary>
    private static string GetDefaultValue(string type)
    {
        // For nullable types, no default needed
        if (type.EndsWith("?", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        // For string, use string.Empty
        if (type.Equals("string", StringComparison.Ordinal))
        {
            return " = string.Empty;";
        }

        // For collections, initialize to empty
        if (type.StartsWith("ICollection<", StringComparison.Ordinal) ||
            type.StartsWith("IList<", StringComparison.Ordinal) ||
            type.StartsWith("List<", StringComparison.Ordinal) ||
            type.StartsWith("IEnumerable<", StringComparison.Ordinal) ||
            type.StartsWith("IReadOnlyList<", StringComparison.Ordinal) ||
            type.StartsWith("IReadOnlyCollection<", StringComparison.Ordinal))
        {
            return " = [];";
        }

        // For value types and reference types, let compiler handle
        return string.Empty;
    }

    /// <summary>
    /// Makes a simple plural form of an entity name.
    /// Uses basic English pluralization rules.
    /// </summary>
    private static string MakePlural(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return entityName;
        }

        // Handle common irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Person", "People" },
            { "Child", "Children" },
            { "Man", "Men" },
            { "Woman", "Women" },
            { "Tooth", "Teeth" },
            { "Foot", "Feet" },
            { "Mouse", "Mice" },
            { "Goose", "Geese" }
        };

        if (irregulars.TryGetValue(entityName, out var irregular))
        {
            return irregular;
        }

        // Words ending in 'y' preceded by consonant -> 'ies'
        if (entityName.Length > 1 &&
            entityName.EndsWith("y", StringComparison.Ordinal) &&
            !IsVowel(entityName[entityName.Length - 2]))
        {
            return entityName.Substring(0, entityName.Length - 1) + "ies";
        }

        // Words ending in 's', 'ss', 'sh', 'ch', 'x', 'z' -> add 'es'
        if (entityName.EndsWith("s", StringComparison.Ordinal) ||
            entityName.EndsWith("ss", StringComparison.Ordinal) ||
            entityName.EndsWith("sh", StringComparison.Ordinal) ||
            entityName.EndsWith("ch", StringComparison.Ordinal) ||
            entityName.EndsWith("x", StringComparison.Ordinal) ||
            entityName.EndsWith("z", StringComparison.Ordinal))
        {
            return entityName + "es";
        }

        // Words ending in 'f' or 'fe' -> 'ves'
        if (entityName.EndsWith("f", StringComparison.Ordinal))
        {
            return entityName.Substring(0, entityName.Length - 1) + "ves";
        }

        if (entityName.EndsWith("fe", StringComparison.Ordinal))
        {
            return entityName.Substring(0, entityName.Length - 2) + "ves";
        }

        // Default: just add 's'
        return entityName + "s";
    }

    /// <summary>
    /// Checks if a character is a vowel.
    /// </summary>
    private static bool IsVowel(char c)
    {
        return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ||
               c == 'A' || c == 'E' || c == 'I' || c == 'O' || c == 'U';
    }

    /// <summary>
    /// Extracts the root namespace from a full namespace.
    /// For example: "MyApp.Application.Common.Interfaces" -> "MyApp"
    /// </summary>
    private static string ExtractRootNamespace(string fullNamespace)
    {
        if (string.IsNullOrWhiteSpace(fullNamespace))
            return "Application";

        var parts = fullNamespace.Split('.');
        // Find where "Application" starts and return everything before it
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("Application", StringComparison.OrdinalIgnoreCase))
            {
                return i > 0 ? string.Join(".", parts.Take(i)) : parts[0];
            }
        }

        // If no Application found, return first part
        return parts[0];
    }

    /// <inheritdoc />
    public string GenerateMediatorInterfaces(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        // Generate IRequest<TResponse>
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for requests that return a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TResponse\">The type of the response.</typeparam>");
        sb.AppendLine("public interface IRequest<out TResponse>");
        sb.AppendLine("{");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate IRequest (non-generic)
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Marker interface for requests that don't return a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IRequest : IRequest<Unit>");
        sb.AppendLine("{");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate IRequestHandler<TRequest, TResponse>
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a handler for a request.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TRequest\">The type of the request.</typeparam>");
        sb.AppendLine("/// <typeparam name=\"TResponse\">The type of the response.</typeparam>");
        sb.AppendLine("public interface IRequestHandler<in TRequest, TResponse>");
        sb.AppendLine($"{Indent}where TRequest : IRequest<TResponse>");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Handles the request.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"request\">The request to handle.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"{Indent}/// <returns>The response.</returns>");
        sb.AppendLine($"{Indent}Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate IRequestHandler<TRequest> (non-generic)
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a handler for a request that doesn't return a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TRequest\">The type of the request.</typeparam>");
        sb.AppendLine("public interface IRequestHandler<in TRequest>");
        sb.AppendLine($"{Indent}where TRequest : IRequest<Unit>");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Handles the request.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"request\">The request to handle.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"{Indent}/// <returns>Unit value.</returns>");
        sb.AppendLine($"{Indent}Task<Unit> Handle(TRequest request, CancellationToken cancellationToken);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate Unit struct
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Represents a void type. Used when a handler doesn't return data.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets the default Unit value.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static readonly Unit Value = new();");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public bool Equals(Unit other) => true;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public override bool Equals(object? obj) => obj is Unit;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public override int GetHashCode() => 0;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public int CompareTo(Unit other) => 0;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public int CompareTo(object? obj) => 0;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Equality operator.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static bool operator ==(Unit left, Unit right) => true;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Inequality operator.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}public static bool operator !=(Unit left, Unit right) => false;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public override string ToString() => \"()\";");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateMediatorInterface(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Extract root namespace to reference Application.Common
        var rootNamespace = ExtractRootNamespace(namespaceName);

        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Defines a mediator for sending requests to handlers.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IMediator");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Sends a request to the appropriate handler.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <typeparam name=\"TResponse\">The type of the response.</typeparam>");
        sb.AppendLine($"{Indent}/// <param name=\"request\">The request to send.</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"{Indent}/// <returns>The response from the handler.</returns>");
        sb.AppendLine($"{Indent}Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateMediatorImplementation(string namespaceName, string rootNamespace)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName));
        }

        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            throw new ArgumentException("Root namespace cannot be null or whitespace.", nameof(rootNamespace));
        }

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Concurrent;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// High-performance mediator implementation with cached delegates.");
        sb.AppendLine("/// Resolves handlers from DI and caches compiled invoke delegates for performance.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class Mediator : IMediator");
        sb.AppendLine("{");
        sb.AppendLine($"{Indent}private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}// Cache for compiled handler invokers - avoids reflection on every call");
        sb.AppendLine($"{Indent}private static readonly ConcurrentDictionary<Type, HandlerInvokerInfo> _handlerCache = new();");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"Mediator\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"serviceProvider\">The service provider to resolve handlers.</param>");
        sb.AppendLine($"{Indent}public Mediator(IServiceProvider serviceProvider)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_serviceProvider = serviceProvider;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}ArgumentNullException.ThrowIfNull(request);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var requestType = request.GetType();");
        sb.AppendLine($"{DoubleIndent}var invokerInfo = _handlerCache.GetOrAdd(requestType, CreateHandlerInvoker<TResponse>);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var handler = _serviceProvider.GetRequiredService(invokerInfo.HandlerType);");
        sb.AppendLine($"{DoubleIndent}return await ((Func<object, object, CancellationToken, Task<TResponse>>)invokerInfo.Invoker)(handler, request, cancellationToken);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Creates a compiled invoker delegate for a handler type.");
        sb.AppendLine($"{Indent}/// This is called once per request type and cached for subsequent calls.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}private static HandlerInvokerInfo CreateHandlerInvoker<TResponse>(Type requestType)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}var responseType = typeof(TResponse);");
        sb.AppendLine($"{DoubleIndent}var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Get the Handle method");
        sb.AppendLine($"{DoubleIndent}var handleMethod = handlerType.GetMethod(\"Handle\")!;");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Build compiled expression: (handler, request, ct) => ((IRequestHandler<TReq, TRes>)handler).Handle((TReq)request, ct)");
        sb.AppendLine($"{DoubleIndent}var handlerParam = Expression.Parameter(typeof(object), \"handler\");");
        sb.AppendLine($"{DoubleIndent}var requestParam = Expression.Parameter(typeof(object), \"request\");");
        sb.AppendLine($"{DoubleIndent}var ctParam = Expression.Parameter(typeof(CancellationToken), \"cancellationToken\");");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var castHandler = Expression.Convert(handlerParam, handlerType);");
        sb.AppendLine($"{DoubleIndent}var castRequest = Expression.Convert(requestParam, requestType);");
        sb.AppendLine($"{DoubleIndent}var callHandle = Expression.Call(castHandler, handleMethod, castRequest, ctParam);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<TResponse>>>(");
        sb.AppendLine($"{TripleIndent}callHandle, handlerParam, requestParam, ctParam);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}return new HandlerInvokerInfo(handlerType, lambda.Compile());");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Cached handler invoker information.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}private sealed record HandlerInvokerInfo(Type HandlerType, Delegate Invoker);");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
