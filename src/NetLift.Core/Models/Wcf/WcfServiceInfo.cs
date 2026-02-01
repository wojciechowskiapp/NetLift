namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a WCF service contract interface with [ServiceContract] attribute.
/// </summary>
public sealed record WcfServiceContract
{
    /// <summary>
    /// The interface name (e.g., "IProductService").
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// The fully qualified type name including namespace (e.g., "MyApp.Services.IProductService").
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// The ServiceContract namespace attribute value, if specified.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// The ServiceContract name attribute value, if specified.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether the service contract requires a session (SessionMode = SessionMode.Required).
    /// </summary>
    public bool SessionRequired { get; init; }

    /// <summary>
    /// The callback contract type, if specified (duplex pattern).
    /// </summary>
    public string? CallbackContract { get; init; }

    /// <summary>
    /// The list of operations (methods with [OperationContract] attribute).
    /// </summary>
    public IReadOnlyList<WcfOperation> Operations { get; init; } = [];
}

/// <summary>
/// Represents a WCF operation (method with [OperationContract] attribute).
/// </summary>
public sealed record WcfOperation
{
    /// <summary>
    /// The method name (e.g., "GetProduct").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The return type (e.g., "Product", "Task&lt;Product&gt;", "void").
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Whether this is a one-way operation (fire-and-forget, no response).
    /// </summary>
    public bool IsOneWay { get; init; }

    /// <summary>
    /// Whether this is an async operation (returns Task or Task&lt;T&gt;).
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// The Action attribute value, if specified (SOAP action).
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// The ReplyAction attribute value, if specified (SOAP reply action).
    /// </summary>
    public string? ReplyAction { get; init; }

    /// <summary>
    /// The XML documentation comment for this operation, if present.
    /// </summary>
    public string? XmlDocumentation { get; init; }

    /// <summary>
    /// The list of method parameters.
    /// </summary>
    public IReadOnlyList<WcfParameter> Parameters { get; init; } = [];
}

/// <summary>
/// Represents a parameter of a WCF operation.
/// </summary>
public sealed record WcfParameter
{
    /// <summary>
    /// The parameter name (e.g., "productId").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The simple type name (e.g., "int", "Product", "List&lt;string&gt;").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The fully qualified type name including namespace (e.g., "System.Int32", "MyApp.Models.Product").
    /// </summary>
    public required string FullTypeName { get; init; }

    /// <summary>
    /// Whether the type is an array (e.g., "string[]", "Product[]").
    /// </summary>
    public bool IsArray { get; init; }

    /// <summary>
    /// Whether the type is nullable (e.g., "int?", "Nullable&lt;DateTime&gt;").
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether the type is generic (e.g., "List&lt;Product&gt;", "Task&lt;int&gt;").
    /// </summary>
    public bool IsGeneric { get; init; }
}
