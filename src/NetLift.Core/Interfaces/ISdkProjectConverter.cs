using System.Xml.Linq;
using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Converts old-format .csproj files to modern SDK-style format.
/// </summary>
public interface ISdkProjectConverter
{
    /// <summary>
    /// Converts a ProjectInfo from old-format to SDK-style XML.
    /// </summary>
    /// <param name="projectInfo">The parsed old-format project information.</param>
    /// <returns>The generated SDK-style project XML document.</returns>
    XDocument Convert(ProjectInfo projectInfo);

    /// <summary>
    /// Converts a ProjectInfo from old-format to SDK-style XML with options.
    /// </summary>
    /// <param name="projectInfo">The parsed old-format project information.</param>
    /// <param name="targetFramework">Optional target framework override (e.g., "net8.0").</param>
    /// <returns>The generated SDK-style project XML document.</returns>
    XDocument Convert(ProjectInfo projectInfo, string? targetFramework = null);
}
