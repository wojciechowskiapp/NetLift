namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information about an action method in an MVC controller.
/// </summary>
public sealed record ActionInfo
{
    /// <summary>
    /// Gets the name of the action method.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the HTTP method(s) this action responds to.
    /// </summary>
    public IReadOnlyList<string> HttpMethods { get; init; } = [];

    /// <summary>
    /// Gets the route template for this action, if specified.
    /// </summary>
    public string? RouteTemplate { get; init; }

    /// <summary>
    /// Gets the parameters of the action method.
    /// </summary>
    public IReadOnlyList<ActionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the return type of the action method.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets whether this action is asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets whether this action modifies state (POST, PUT, DELETE, PATCH).
    /// </summary>
    public bool IsCommand { get; init; }

    /// <summary>
    /// Gets whether this action only reads data (GET, HEAD).
    /// </summary>
    public bool IsQuery { get; init; }

    /// <summary>
    /// Gets the list of filter attributes applied to this action.
    /// </summary>
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>
    /// Gets the confidence score for extracting this action (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets whether this action has an overload (another method with the same name but different parameters).
    /// </summary>
    public bool HasOverload { get; init; }

    /// <summary>
    /// Gets whether this action is trivial and should not generate CQRS handlers.
    /// Trivial actions are simple methods that only return a view with no business logic.
    /// </summary>
    public bool IsTrivial { get; init; }
}

/// <summary>
/// Represents a parameter of an action method.
/// </summary>
public sealed record ActionParameter
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
    /// Gets the binding source (FromBody, FromQuery, FromRoute, etc.).
    /// </summary>
    public string? BindingSource { get; init; }
}
