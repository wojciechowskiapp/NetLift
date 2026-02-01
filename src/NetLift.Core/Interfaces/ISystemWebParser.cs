using System.Xml.Linq;
using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides functionality to parse the system.web section from web.config files.
/// </summary>
public interface ISystemWebParser
{
    /// <summary>
    /// Parses the system.web section from a web.config XML document.
    /// </summary>
    /// <param name="webConfig">The XDocument containing the web.config XML.</param>
    /// <returns>
    /// A SystemWebSection containing the parsed configuration settings.
    /// Returns an empty section if system.web is not present.
    /// </returns>
    SystemWebSection Parse(XDocument webConfig);
}
