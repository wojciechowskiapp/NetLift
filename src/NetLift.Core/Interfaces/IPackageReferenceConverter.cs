using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides functionality to convert packages.config references to modern PackageReference format.
/// </summary>
public interface IPackageReferenceConverter
{
    /// <summary>
    /// Converts packages from packages.config to PackageReference elements for SDK-style projects.
    /// </summary>
    /// <param name="packagesConfig">The parsed packages.config file.</param>
    /// <param name="targetFramework">The target framework for the conversion (e.g., "net8.0").</param>
    /// <returns>A conversion result containing the converted packages and any warnings or actions taken.</returns>
    PackageConversionResult Convert(PackagesConfig packagesConfig, string targetFramework);
}
