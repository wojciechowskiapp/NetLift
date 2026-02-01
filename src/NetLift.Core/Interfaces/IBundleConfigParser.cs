using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses BundleConfig.cs files to extract bundle definitions.
/// </summary>
public interface IBundleConfigParser
{
    /// <summary>
    /// Parses C# source code to extract bundle definitions.
    /// </summary>
    /// <param name="sourceCode">The BundleConfig.cs source code.</param>
    /// <returns>A list of parsed bundle definitions.</returns>
    IReadOnlyList<BundleDefinition> Parse(string sourceCode);
}
