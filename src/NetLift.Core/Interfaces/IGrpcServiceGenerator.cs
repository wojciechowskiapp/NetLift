using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates gRPC service implementation classes from WCF service contracts.
/// </summary>
public interface IGrpcServiceGenerator
{
    /// <summary>
    /// Generates a gRPC service implementation from a WCF service contract.
    /// </summary>
    /// <param name="serviceContract">The WCF service contract to convert.</param>
    /// <param name="targetNamespace">The target namespace for the generated service class.</param>
    /// <returns>A GrpcServiceInfo containing the generated service implementation and extension methods.</returns>
    GrpcServiceInfo Generate(WcfServiceContract serviceContract, string targetNamespace);

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
