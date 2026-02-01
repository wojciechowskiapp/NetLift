namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a transformed WCF FaultContract as a custom exception.
/// </summary>
public sealed record FaultContractInfo
{
    /// <summary>
    /// Gets the original WCF fault type name (e.g., "CustomerNotFoundFault").
    /// </summary>
    public required string FaultTypeName { get; init; }

    /// <summary>
    /// Gets the generated exception class name (e.g., "CustomerNotFoundException").
    /// </summary>
    public required string ExceptionClassName { get; init; }

    /// <summary>
    /// Gets the exception error code for categorization.
    /// </summary>
    public required string ExceptionCode { get; init; }

    /// <summary>
    /// Gets the gRPC status code mapping.
    /// </summary>
    public required GrpcStatusCode GrpcStatus { get; init; }

    /// <summary>
    /// Gets the HTTP status code mapping for REST APIs.
    /// </summary>
    public required int HttpStatusCode { get; init; }

    /// <summary>
    /// Gets the list of properties from the original fault contract.
    /// </summary>
    public IReadOnlyList<FaultProperty> Properties { get; init; } = [];
}

/// <summary>
/// Represents a property from a WCF fault contract.
/// </summary>
public sealed record FaultProperty
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the property type.
    /// </summary>
    public required string Type { get; init; }
}

/// <summary>
/// gRPC status codes based on the gRPC specification.
/// See: https://grpc.io/docs/guides/status-codes/
/// </summary>
public enum GrpcStatusCode
{
    /// <summary>
    /// Success (0).
    /// </summary>
    OK = 0,

    /// <summary>
    /// Operation cancelled (1).
    /// </summary>
    Cancelled = 1,

    /// <summary>
    /// Unknown error (2).
    /// </summary>
    Unknown = 2,

    /// <summary>
    /// Client specified an invalid argument (3).
    /// </summary>
    InvalidArgument = 3,

    /// <summary>
    /// Some requested entity was not found (5).
    /// </summary>
    NotFound = 5,

    /// <summary>
    /// Entity already exists (6).
    /// </summary>
    AlreadyExists = 6,

    /// <summary>
    /// Caller does not have permission (7).
    /// </summary>
    PermissionDenied = 7,

    /// <summary>
    /// Internal server error (13).
    /// </summary>
    Internal = 13,

    /// <summary>
    /// Request not authenticated (16).
    /// </summary>
    Unauthenticated = 16
}

/// <summary>
/// Contains the result of transforming WCF FaultContracts to modern error handling.
/// </summary>
public sealed record FaultTransformResult
{
    /// <summary>
    /// Gets the list of generated custom exceptions.
    /// </summary>
    public IReadOnlyList<FaultContractInfo> Exceptions { get; init; } = [];

    /// <summary>
    /// Gets the generated gRPC interceptor code for exception handling.
    /// </summary>
    public required string InterceptorCode { get; init; }

    /// <summary>
    /// Gets the generated REST exception handler code (ProblemDetails).
    /// </summary>
    public required string ExceptionHandlerCode { get; init; }

    /// <summary>
    /// Gets the generated custom exception classes code.
    /// </summary>
    public string ExceptionClassesCode { get; init; } = string.Empty;
}
