using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms WCF FaultContracts to custom exceptions with gRPC and REST error handling.
/// </summary>
public interface IFaultContractTransformer
{
    /// <summary>
    /// Transforms a list of WCF fault data contracts to custom exceptions and error handlers.
    /// </summary>
    /// <param name="faultContracts">The list of data contracts that represent faults.</param>
    /// <returns>The transformation result containing exceptions, interceptor, and handlers.</returns>
    FaultTransformResult Transform(IReadOnlyList<WcfDataContract> faultContracts);

    /// <summary>
    /// Gets the confidence score of the last transformation (0-100).
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets diagnostic messages from the last transformation.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
