using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides functionality to parse packages.config files.
/// </summary>
public interface IPackagesConfigParser
{
    /// <summary>
    /// Parses a packages.config file and extracts package references.
    /// </summary>
    /// <param name="filePath">The absolute path to the packages.config file.</param>
    /// <returns>A list of package references. Returns an empty list if the file doesn't exist.</returns>
    List<PackageReference> Parse(string filePath);
}
