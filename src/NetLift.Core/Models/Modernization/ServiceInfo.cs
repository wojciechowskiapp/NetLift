namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information about a service class extracted from source code.
/// </summary>
public sealed record ServiceInfo
{
    /// <summary>
    /// Gets the file path of the service source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the name of the service class.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the namespace of the service.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the interface(s) implemented by the service.
    /// </summary>
    public IReadOnlyList<string> ImplementedInterfaces { get; init; } = [];

    /// <summary>
    /// Gets the list of methods in the service.
    /// </summary>
    public IReadOnlyList<ServiceMethodInfo> Methods { get; init; } = [];

    /// <summary>
    /// Gets the injected dependencies (constructor parameters).
    /// </summary>
    public IReadOnlyList<ServiceDependency> Dependencies { get; init; } = [];

    /// <summary>
    /// Gets whether this service uses DbContext directly.
    /// </summary>
    public bool UsesDbContext { get; init; }

    /// <summary>
    /// Gets the DbContext type name if used.
    /// </summary>
    public string? DbContextTypeName { get; init; }
}

/// <summary>
/// Represents information about a method in a service class.
/// </summary>
public sealed record ServiceMethodInfo
{
    /// <summary>
    /// Gets the name of the method.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the return type of the method.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets the parameters of the method.
    /// </summary>
    public IReadOnlyList<MethodParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Gets whether the method is asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets the extracted logic from the method body.
    /// </summary>
    public ExtractedLogic? ExtractedLogic { get; init; }

    /// <summary>
    /// Gets whether this method modifies state (Create, Update, Delete, Save).
    /// </summary>
    public bool ModifiesState { get; init; }
}

/// <summary>
/// Represents a parameter of a service method.
/// </summary>
public sealed record MethodParameter
{
    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of the parameter.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets whether the parameter is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets whether the parameter has a default value.
    /// </summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>
    /// Gets the default value expression if any.
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Represents an injected dependency in a service class.
/// </summary>
public sealed record ServiceDependency
{
    /// <summary>
    /// Gets the name of the dependency field/property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of the dependency.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets whether this is a DbContext dependency.
    /// </summary>
    public bool IsDbContext { get; init; }

    /// <summary>
    /// Gets whether this is a repository dependency.
    /// </summary>
    public bool IsRepository { get; init; }

    /// <summary>
    /// Gets whether this is a logger dependency.
    /// </summary>
    public bool IsLogger { get; init; }
}
