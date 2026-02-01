using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates Protocol Buffer (.proto) files from WCF service and data contracts.
/// </summary>
public interface IProtoGenerator
{
    /// <summary>
    /// Generates a .proto file from a WCF service contract and its associated data contracts.
    /// </summary>
    /// <param name="serviceContract">The WCF service contract to convert.</param>
    /// <param name="dataContracts">The data contracts used by the service.</param>
    /// <returns>A ProtoFileInfo containing the generated .proto file content.</returns>
    ProtoFileInfo Generate(WcfServiceContract serviceContract, IReadOnlyList<WcfDataContract> dataContracts);

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
