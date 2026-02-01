namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a generated gRPC service implementation.
/// </summary>
public sealed record GrpcServiceInfo
{
    /// <summary>
    /// Gets the service class name (e.g., "CustomerServiceGrpc").
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the namespace for the generated service class (e.g., "Company.Services.Grpc").
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the base class name from proto-generated code (e.g., "CustomerService.CustomerServiceBase").
    /// </summary>
    public required string BaseClassName { get; init; }

    /// <summary>
    /// Gets the complete C# source code for the service implementation.
    /// </summary>
    public required string ServiceCode { get; init; }

    /// <summary>
    /// Gets the C# source code for DI registration and endpoint mapping extension methods.
    /// </summary>
    public required string ExtensionsCode { get; init; }

    /// <summary>
    /// Gets the list of gRPC methods (RPCs) in the service.
    /// </summary>
    public IReadOnlyList<GrpcMethodInfo> Methods { get; init; } = [];
}

/// <summary>
/// Represents a gRPC method (RPC) in a service.
/// </summary>
public sealed record GrpcMethodInfo
{
    /// <summary>
    /// Gets the method name (e.g., "GetCustomer").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the request message type (e.g., "GetCustomerRequest").
    /// </summary>
    public required string RequestType { get; init; }

    /// <summary>
    /// Gets the response message type (e.g., "GetCustomerResponse").
    /// </summary>
    public required string ResponseType { get; init; }

    /// <summary>
    /// Gets the XML documentation comment for this method, if present.
    /// </summary>
    public string? Documentation { get; init; }
}
