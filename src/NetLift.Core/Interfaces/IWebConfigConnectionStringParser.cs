using System.Xml.Linq;
using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parser for connection strings section in web.config files.
/// </summary>
public interface IWebConfigConnectionStringParser
{
    /// <summary>
    /// Parses the connectionStrings section from a web.config file.
    /// </summary>
    /// <param name="webConfig">The web.config XDocument.</param>
    /// <returns>The parsed connection strings section.</returns>
    ConnectionStringsSection Parse(XDocument webConfig);

    /// <summary>
    /// Parses the connectionStrings section with XDT transformations applied.
    /// </summary>
    /// <param name="webConfig">The base web.config XDocument.</param>
    /// <param name="transformConfig">The transform config XDocument (e.g., Web.Release.config).</param>
    /// <returns>The parsed connection strings section with transformations applied.</returns>
    ConnectionStringsSection ParseWithTransforms(XDocument webConfig, XDocument? transformConfig);
}
