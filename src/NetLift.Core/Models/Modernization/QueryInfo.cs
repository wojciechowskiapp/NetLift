namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information needed to generate a CQRS Query and QueryHandler.
/// </summary>
public sealed record QueryInfo
{
    /// <summary>
    /// Gets the name of the query (e.g., "GetProductById").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the namespace for the query.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the properties of the query (query parameters).
    /// </summary>
    public IReadOnlyList<CommandProperty> Properties { get; init; } = [];

    /// <summary>
    /// Gets the return type of the query handler.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets whether the handler is asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets the source controller and action that this query was generated from.
    /// </summary>
    public required SourceReference Source { get; init; }

    /// <summary>
    /// Gets the confidence score for this query generation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets whether this query requires validation.
    /// </summary>
    public bool RequiresValidation { get; init; }

    /// <summary>
    /// Gets the business logic extracted from the original action method.
    /// </summary>
    public string? BusinessLogic { get; init; }

    /// <summary>
    /// Gets whether this query supports pagination.
    /// </summary>
    public bool SupportsPagination { get; init; }

    /// <summary>
    /// Gets whether this query supports filtering.
    /// </summary>
    public bool SupportsFiltering { get; init; }

    /// <summary>
    /// Gets the ViewBag/ViewData mutations from the original action method.
    /// Used to generate response DTO properties.
    /// </summary>
    public IReadOnlyList<ViewModelMutation>? ViewModelMutations { get; init; }
}
