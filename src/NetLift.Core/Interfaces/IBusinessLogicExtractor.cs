using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Extracts business logic from WCF service implementations into clean service layer code.
/// Converts constructor dependencies to DI, methods to async, and generates interface-based abstractions.
/// </summary>
public interface IBusinessLogicExtractor
{
    /// <summary>
    /// Extracts business logic from a WCF service implementation.
    /// </summary>
    /// <param name="sourceCode">The C# source code containing the WCF service implementation.</param>
    /// <param name="contract">The WCF service contract interface that the implementation should implement.</param>
    /// <returns>Extracted service information including interface and implementation code.</returns>
    ExtractedServiceInfo Extract(string sourceCode, WcfServiceContract contract);

    /// <summary>
    /// Gets the confidence score (0-100) for the last extraction.
    /// Higher scores indicate more reliable transformations.
    /// </summary>
    int ConfidenceScore { get; }

    /// <summary>
    /// Gets the diagnostics (warnings, errors) from the last extraction operation.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
