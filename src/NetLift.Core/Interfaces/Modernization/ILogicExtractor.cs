using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Extracts business logic from method bodies for transformation into CQRS handlers.
/// </summary>
public interface ILogicExtractor
{
    /// <summary>
    /// Extracts logic from a method body source code.
    /// </summary>
    /// <param name="methodBody">The method body source code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted logic information.</returns>
    Task<ExtractedLogic> ExtractAsync(
        string methodBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts logic from a full method declaration.
    /// </summary>
    /// <param name="methodSource">The full method source code including signature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted logic information.</returns>
    Task<ExtractedLogic> ExtractFromMethodAsync(
        string methodSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Combines extracted logic from a controller action and its service methods.
    /// </summary>
    /// <param name="actionLogic">Logic from the controller action.</param>
    /// <param name="serviceMethods">Service methods called by the action with their logic.</param>
    /// <returns>Combined logic ready for handler generation.</returns>
    ExtractedLogic CombineLogic(
        ExtractedLogic actionLogic,
        IReadOnlyList<ServiceMethodLink> serviceMethods);

    /// <summary>
    /// Transforms extracted logic for async execution.
    /// </summary>
    /// <param name="logic">The extracted logic.</param>
    /// <returns>Logic with sync calls transformed to async.</returns>
    ExtractedLogic TransformToAsync(ExtractedLogic logic);
}
