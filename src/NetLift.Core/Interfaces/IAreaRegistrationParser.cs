using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses AreaRegistration classes to extract area definitions and their routes.
/// </summary>
public interface IAreaRegistrationParser
{
    /// <summary>
    /// Parses C# source code containing an AreaRegistration class.
    /// </summary>
    /// <param name="sourceCode">The AreaRegistration class source code.</param>
    /// <returns>A list of parsed area definitions (typically one per file).</returns>
    IReadOnlyList<AreaDefinition> Parse(string sourceCode);
}
