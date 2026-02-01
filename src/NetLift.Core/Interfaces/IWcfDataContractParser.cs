using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses WCF DataContract and DataMember attributes from C# source code using Roslyn.
/// </summary>
public interface IWcfDataContractParser
{
    /// <summary>
    /// Parses the source code and extracts all DataContract types.
    /// </summary>
    /// <param name="sourceCode">The C# source code to parse.</param>
    /// <returns>A list of parsed WCF data contracts.</returns>
    IReadOnlyList<WcfDataContract> Parse(string sourceCode);

    /// <summary>
    /// Gets the collection of diagnostic messages from the last parse operation.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
