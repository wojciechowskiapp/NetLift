using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates client proxy code for consuming gRPC and REST APIs migrated from WCF.
/// </summary>
public interface IClientProxyGenerator
{
    /// <summary>
    /// Generates client proxy code from a WCF service contract.
    /// </summary>
    /// <param name="serviceContract">The WCF service contract to generate clients for.</param>
    /// <param name="targetNamespace">The target namespace for the generated client code.</param>
    /// <returns>A ClientProxyInfo containing the generated interface and client implementations.</returns>
    ClientProxyInfo Generate(WcfServiceContract serviceContract, string targetNamespace);

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
