namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents extracted business logic from a WCF service implementation.
/// </summary>
public sealed record ExtractedServiceInfo
{
    /// <summary>
    /// The interface name for the extracted service (e.g., "ICustomerService").
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// The implementation class name (e.g., "CustomerService").
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// The namespace for the extracted service.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// The generated interface code with async methods and CancellationToken parameters.
    /// </summary>
    public required string InterfaceCode { get; init; }

    /// <summary>
    /// The generated implementation code with DI constructor and async methods.
    /// </summary>
    public required string ImplementationCode { get; init; }

    /// <summary>
    /// The list of dependencies detected from the implementation.
    /// </summary>
    public IReadOnlyList<ExtractedDependency> Dependencies { get; init; } = [];

    /// <summary>
    /// The list of extracted methods with async signatures.
    /// </summary>
    public IReadOnlyList<ExtractedMethod> Methods { get; init; } = [];

    /// <summary>
    /// Warnings and notes about the extraction (e.g., TransactionScope usage).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Represents a dependency detected from a WCF service implementation.
/// </summary>
public sealed record ExtractedDependency
{
    /// <summary>
    /// The type name of the dependency (e.g., "CustomerRepository", "ICustomerRepository").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The parameter name for constructor injection (e.g., "customerRepository").
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// The interface type to use for DI (e.g., "ICustomerRepository").
    /// If the original was a concrete type, this will be the generated interface name.
    /// </summary>
    public required string InterfaceType { get; init; }

    /// <summary>
    /// Whether this dependency is a logger (ILogger, NLog, Log4Net).
    /// </summary>
    public bool IsLogger { get; init; }
}

/// <summary>
/// Represents an extracted method with async signature.
/// </summary>
public sealed record ExtractedMethod
{
    /// <summary>
    /// The original method name (e.g., "GetCustomer").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The async method name (e.g., "GetCustomerAsync").
    /// </summary>
    public required string AsyncName { get; init; }

    /// <summary>
    /// The original return type (e.g., "CustomerDto", "void").
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// The async return type (e.g., "Task&lt;CustomerDto&gt;", "Task").
    /// </summary>
    public required string AsyncReturnType { get; init; }

    /// <summary>
    /// The XML documentation comment for this method, if present.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The list of method parameters.
    /// </summary>
    public IReadOnlyList<MethodParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Whether the method uses TransactionScope (requires manual review).
    /// </summary>
    public bool HasTransactionScope { get; init; }

    /// <summary>
    /// Whether the method throws FaultException (should be mapped to custom exceptions).
    /// </summary>
    public bool HasFaultException { get; init; }
}
