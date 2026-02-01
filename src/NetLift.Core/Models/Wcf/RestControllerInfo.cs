namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a generated REST API controller from a WCF service contract.
/// </summary>
public sealed record RestControllerInfo
{
    /// <summary>
    /// The controller class name (e.g., "CustomerController").
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// The namespace for the controller (e.g., "MyApp.Api.Controllers").
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// The complete generated C# controller code.
    /// </summary>
    public required string ControllerCode { get; init; }

    /// <summary>
    /// The route prefix for the controller (e.g., "api/customers").
    /// </summary>
    public required string RoutePrefix { get; init; }

    /// <summary>
    /// The list of REST API actions/endpoints in this controller.
    /// </summary>
    public IReadOnlyList<RestActionInfo> Actions { get; init; } = [];
}

/// <summary>
/// Represents a single REST API action method in a controller.
/// </summary>
public sealed record RestActionInfo
{
    /// <summary>
    /// The action method name (e.g., "GetCustomer").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The HTTP method (e.g., "GET", "POST", "PUT", "DELETE").
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// The route template for this action (e.g., "{customerId:int}").
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// The return type (e.g., "CustomerDto", "IEnumerable&lt;CustomerDto&gt;").
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// The XML documentation comment for this action, if present.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The list of parameters for this action.
    /// </summary>
    public IReadOnlyList<RestParameterInfo> Parameters { get; init; } = [];
}

/// <summary>
/// Represents a parameter in a REST API action method.
/// </summary>
public sealed record RestParameterInfo
{
    /// <summary>
    /// The parameter name (e.g., "customerId").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The parameter type (e.g., "int", "CustomerDto").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The parameter source (e.g., "FromRoute", "FromQuery", "FromBody").
    /// </summary>
    public required string Source { get; init; }
}
