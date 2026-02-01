using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses C# source code to extract WCF service contracts with [ServiceContract] and [OperationContract] attributes.
/// </summary>
public interface IWcfServiceParser
{
    /// <summary>
    /// Parses C# source code to extract all service contracts.
    /// </summary>
    /// <param name="sourceCode">The C# source code containing WCF service interfaces.</param>
    /// <returns>A list of parsed WCF service contracts.</returns>
    IReadOnlyList<WcfServiceContract> Parse(string sourceCode);

    /// <summary>
    /// Gets the diagnostics (warnings, errors) from the last parse operation.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
