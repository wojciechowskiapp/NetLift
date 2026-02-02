namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents the full context linking a controller action to its service method and extracted logic.
/// This is the main input for generating handlers with actual business logic.
/// </summary>
public sealed record ActionLogicContext
{
    /// <summary>
    /// Gets the controller information.
    /// </summary>
    public required ControllerInfo Controller { get; init; }

    /// <summary>
    /// Gets the action information.
    /// </summary>
    public required ActionInfo Action { get; init; }

    /// <summary>
    /// Gets the logic extracted from the controller action body.
    /// </summary>
    public ExtractedLogic? ActionLogic { get; init; }

    /// <summary>
    /// Gets the service method(s) called by this action.
    /// </summary>
    public IReadOnlyList<ServiceMethodLink> ServiceMethods { get; init; } = [];

    /// <summary>
    /// Gets the combined logic from action and service methods.
    /// </summary>
    public ExtractedLogic? CombinedLogic { get; init; }

    /// <summary>
    /// Gets the target namespace for generated code.
    /// </summary>
    public required string TargetNamespace { get; init; }

    /// <summary>
    /// Gets whether this action should generate a Command (write operation).
    /// </summary>
    public bool GenerateCommand { get; init; }

    /// <summary>
    /// Gets whether this action should generate a Query (read operation).
    /// </summary>
    public bool GenerateQuery { get; init; }

    /// <summary>
    /// Gets the overall confidence for this transformation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets any transformation warnings or notes.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Links an action to a service method it calls.
/// </summary>
public sealed record ServiceMethodLink
{
    /// <summary>
    /// Gets the service containing the method.
    /// </summary>
    public required ServiceInfo Service { get; init; }

    /// <summary>
    /// Gets the method being called.
    /// </summary>
    public required ServiceMethodInfo Method { get; init; }

    /// <summary>
    /// Gets the call expression in the action (e.g., "service.GetItem(id)").
    /// </summary>
    public required string CallExpression { get; init; }

    /// <summary>
    /// Gets the variable the result is assigned to, if any.
    /// </summary>
    public string? ResultVariable { get; init; }

    /// <summary>
    /// Gets the argument mappings (action param → service param).
    /// </summary>
    public IReadOnlyDictionary<string, string> ArgumentMappings { get; init; } =
        new Dictionary<string, string>();
}
