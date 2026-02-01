using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses WCF system.serviceModel configuration from Web.config or App.config files.
/// </summary>
public interface IServiceModelParser
{
    /// <summary>
    /// Parses the system.serviceModel section from a configuration file content.
    /// </summary>
    /// <param name="configContent">The XML configuration file content.</param>
    /// <returns>The parsed WCF service configuration, or null if no system.serviceModel section exists.</returns>
    WcfServiceConfiguration? Parse(string configContent);

    /// <summary>
    /// Gets diagnostic messages generated during parsing.
    /// Useful for identifying parsing issues or warnings.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
