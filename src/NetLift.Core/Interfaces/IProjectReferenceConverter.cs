using System.Xml.Linq;
using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Converts old-style project references to SDK-style format.
/// </summary>
public interface IProjectReferenceConverter
{
    /// <summary>
    /// Converts a list of project references to SDK-style ItemGroup element.
    /// </summary>
    /// <param name="references">The project references to convert.</param>
    /// <param name="sourceProjectPath">The path to the source project file for resolving relative paths.</param>
    /// <returns>An ItemGroup XElement containing the converted project references, or null if no references.</returns>
    XElement? ConvertProjectReferences(List<ProjectReference> references, string sourceProjectPath);
}
