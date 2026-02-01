using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates ASP.NET Core REST API controllers from WCF service contracts.
/// </summary>
public interface IRestControllerGenerator
{
    /// <summary>
    /// Generates a REST API controller from a WCF service contract.
    /// </summary>
    /// <param name="serviceContract">The WCF service contract to convert.</param>
    /// <param name="targetNamespace">The target namespace for the generated controller.</param>
    /// <returns>A RestControllerInfo containing the generated controller code and metadata.</returns>
    RestControllerInfo Generate(WcfServiceContract serviceContract, string targetNamespace);

    /// <summary>
    /// Gets the confidence score (0-100) for the last generation operation.
    /// Higher scores indicate more reliable transformations.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets the diagnostic messages (warnings, notes) from the last generation operation.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
