using NetLift.Core.Models;
using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Detects which DI framework is being used in a project or solution.
/// </summary>
public interface IDIContainerDetector
{
    /// <summary>
    /// Detects DI container configuration in a solution.
    /// </summary>
    /// <param name="solution">The solution to analyze.</param>
    /// <returns>DI container information.</returns>
    Task<DIContainerInfo> DetectAsync(SolutionInfo solution);

    /// <summary>
    /// Gets the DI frameworks used in a specific project directory.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="packages">Package references from the project.</param>
    /// <returns>List of detected frameworks.</returns>
    Task<List<DIFrameworkType>> GetUsedFrameworksAsync(string projectPath, IEnumerable<PackageReference>? packages = null);

    /// <summary>
    /// Finds configuration files for a specific DI framework.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="framework">The DI framework to find files for.</param>
    /// <returns>List of configuration file paths.</returns>
    Task<List<string>> FindConfigurationFilesAsync(string projectPath, DIFrameworkType framework);

    /// <summary>
    /// Detects DI framework from package references.
    /// </summary>
    /// <param name="packages">List of package references.</param>
    /// <returns>The detected framework type.</returns>
    DIFrameworkType DetectFromPackages(IEnumerable<PackageReference> packages);
}
