using System.Xml.Linq;
using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses appSettings section from web.config files.
/// </summary>
public interface IWebConfigAppSettingsParser
{
    /// <summary>
    /// Parses the appSettings section from a web.config XDocument.
    /// </summary>
    /// <param name="webConfig">The web.config XML document.</param>
    /// <returns>The parsed appSettings section.</returns>
    AppSettingsSection Parse(XDocument webConfig);

    /// <summary>
    /// Parses the appSettings section with XDT transform support.
    /// </summary>
    /// <param name="webConfig">The base web.config XML document.</param>
    /// <param name="transformConfig">The transform XML document (e.g., Web.Release.config).</param>
    /// <returns>The parsed appSettings section with transforms applied.</returns>
    AppSettingsSection ParseWithTransforms(XDocument webConfig, XDocument? transformConfig);

    /// <summary>
    /// Builds a hierarchical dictionary structure from flat appSettings.
    /// Useful for converting to appsettings.json format.
    /// </summary>
    /// <param name="section">The appSettings section.</param>
    /// <returns>A hierarchical dictionary suitable for JSON serialization.</returns>
    Dictionary<string, object> BuildHierarchy(AppSettingsSection section);
}
