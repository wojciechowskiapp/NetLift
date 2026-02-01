namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents generated client proxy code for consuming a migrated service.
/// </summary>
public sealed record ClientProxyInfo
{
    /// <summary>
    /// Gets the client interface name (e.g., "ICustomerServiceClient").
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// Gets the complete C# code for the client interface.
    /// </summary>
    public required string InterfaceCode { get; init; }

    /// <summary>
    /// Gets the complete C# code for the gRPC client implementation.
    /// </summary>
    public required string GrpcClientCode { get; init; }

    /// <summary>
    /// Gets the complete C# code for the HTTP client implementation.
    /// </summary>
    public required string HttpClientCode { get; init; }

    /// <summary>
    /// Gets the C# code for DI registration extension methods.
    /// </summary>
    public required string ExtensionsCode { get; init; }

    /// <summary>
    /// Gets the namespace for the generated client code.
    /// </summary>
    public required string Namespace { get; init; }
}

/// <summary>
/// Configuration options for client proxy behavior.
/// </summary>
public sealed record ClientOptions
{
    /// <summary>
    /// Gets or initializes the gRPC service address.
    /// </summary>
    public string GrpcAddress { get; init; } = "https://localhost:5001";

    /// <summary>
    /// Gets or initializes the HTTP API base address.
    /// </summary>
    public string HttpBaseAddress { get; init; } = "https://localhost:5000";

    /// <summary>
    /// Gets or initializes the request timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or initializes the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
