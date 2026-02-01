using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides package mapping services for migrating legacy NuGet packages to modern equivalents.
/// </summary>
public interface IPackageMappingService
{
    /// <summary>
    /// Gets the mapped package information for a given package ID and version.
    /// </summary>
    /// <param name="packageId">The original package identifier.</param>
    /// <param name="version">The original package version.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A <see cref="PackageMappingResult"/> containing the mapping information.</returns>
    PackageMappingResult GetMappedPackage(string packageId, string version, string targetFramework);

    /// <summary>
    /// Determines whether a package requires mapping.
    /// </summary>
    /// <param name="packageId">The package identifier to check.</param>
    /// <returns><c>true</c> if the package has a mapping rule; otherwise, <c>false</c>.</returns>
    bool RequiresMapping(string packageId);

    /// <summary>
    /// Gets all available mapping rules.
    /// </summary>
    /// <returns>A collection of all package mapping rules.</returns>
    IReadOnlyCollection<PackageMappingRule> GetAllMappingRules();

    /// <summary>
    /// Checks if a package is obsolete and should be removed.
    /// </summary>
    /// <param name="packageId">The package identifier to check.</param>
    /// <param name="targetFramework">The target framework moniker.</param>
    /// <returns><c>true</c> if the package is obsolete; otherwise, <c>false</c>.</returns>
    bool IsObsolete(string packageId, string targetFramework);
}
