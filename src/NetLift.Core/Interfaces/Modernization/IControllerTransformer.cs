using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Transforms controller classes to use MediatR instead of direct service calls.
/// </summary>
public interface IControllerTransformer
{
    /// <summary>
    /// Transforms a controller to use MediatR for all actions.
    /// </summary>
    /// <param name="controllerSource">The original controller source code.</param>
    /// <param name="actionContexts">The action logic contexts with generated command/query names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed controller source code.</returns>
    Task<ControllerTransformResult> TransformAsync(
        string controllerSource,
        IReadOnlyList<ActionLogicContext> actionContexts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms a single action method to use MediatR.
    /// </summary>
    /// <param name="actionSource">The original action source code.</param>
    /// <param name="context">The action context with command/query information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed action source code.</returns>
    Task<string> TransformActionAsync(
        string actionSource,
        ActionLogicContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of controller transformation.
/// </summary>
public sealed record ControllerTransformResult
{
    /// <summary>
    /// Gets the transformed controller source code.
    /// </summary>
    public required string TransformedSource { get; init; }

    /// <summary>
    /// Gets the list of namespaces that need to be added.
    /// </summary>
    public IReadOnlyList<string> RequiredUsings { get; init; } = [];

    /// <summary>
    /// Gets the list of actions that were transformed.
    /// </summary>
    public IReadOnlyList<string> TransformedActions { get; init; } = [];

    /// <summary>
    /// Gets the list of actions that could not be transformed.
    /// </summary>
    public IReadOnlyList<TransformWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the overall confidence for this transformation (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Warning about a transformation issue.
/// </summary>
public sealed record TransformWarning
{
    /// <summary>
    /// Gets the action name.
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the severity (Info, Warning, Error).
    /// </summary>
    public required string Severity { get; init; }
}
