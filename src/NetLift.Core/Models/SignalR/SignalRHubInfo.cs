using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Models.SignalR;

/// <summary>
/// Contains information about an analyzed SignalR Hub class.
/// </summary>
public record SignalRHubInfo
{
    /// <summary>
    /// The name of the Hub class.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// The full namespace of the Hub class.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// The file path containing the Hub.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Lifecycle methods found in the Hub.
    /// </summary>
    public IReadOnlyList<HubLifecycleInfo> LifecycleMethods { get; init; } = [];

    /// <summary>
    /// Client invocations found in the Hub.
    /// </summary>
    public IReadOnlyList<ClientInvocationInfo> ClientInvocations { get; init; } = [];

    /// <summary>
    /// Hub methods (public methods that can be called by clients).
    /// </summary>
    public IReadOnlyList<HubMethodInfo> HubMethods { get; init; } = [];

    /// <summary>
    /// Groups operations found in the Hub.
    /// </summary>
    public IReadOnlyList<GroupsOperationInfo> GroupsOperations { get; init; } = [];

    /// <summary>
    /// Whether the Hub uses custom authorization.
    /// </summary>
    public bool HasCustomAuthorization { get; init; }

    /// <summary>
    /// The hub path/route if specified via HubName attribute.
    /// </summary>
    public string? HubRoute { get; init; }

    /// <summary>
    /// Overall confidence score for migration (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Information about a hub lifecycle method (OnConnected, OnDisconnected, etc.).
/// </summary>
public record HubLifecycleInfo
{
    /// <summary>
    /// The method name (e.g., "OnConnected", "OnDisconnected", "OnReconnected").
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The line number where the method is defined.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Whether this method can be automatically transformed.
    /// </summary>
    public bool CanAutoTransform { get; init; }

    /// <summary>
    /// Transformation notes or warnings.
    /// </summary>
    public string? TransformationNote { get; init; }
}

/// <summary>
/// Information about a client invocation in a Hub.
/// </summary>
public record ClientInvocationInfo
{
    /// <summary>
    /// The invocation pattern (e.g., "Clients.All", "Clients.Caller", "Clients.Group").
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// The method being invoked on the client.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The line number of the invocation.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The transformed code snippet.
    /// </summary>
    public string? TransformedCode { get; init; }
}

/// <summary>
/// Information about a public Hub method.
/// </summary>
public record HubMethodInfo
{
    /// <summary>
    /// The method name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The return type.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Method parameters.
    /// </summary>
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>
    /// Whether the method is async.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// The line number where the method is defined.
    /// </summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Information about Groups operations in a Hub.
/// </summary>
public record GroupsOperationInfo
{
    /// <summary>
    /// The operation type (Add, Remove).
    /// </summary>
    public required GroupsOperationType OperationType { get; init; }

    /// <summary>
    /// The line number of the operation.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }
}

/// <summary>
/// Types of Groups operations.
/// </summary>
public enum GroupsOperationType
{
    /// <summary>
    /// Adding a connection to a group.
    /// </summary>
    Add,

    /// <summary>
    /// Removing a connection from a group.
    /// </summary>
    Remove
}
