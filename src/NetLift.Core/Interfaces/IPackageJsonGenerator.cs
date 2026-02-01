namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates package.json files for modern JavaScript build tools.
/// </summary>
public interface IPackageJsonGenerator
{
    /// <summary>
    /// Generates a package.json file with dependencies and scripts.
    /// </summary>
    /// <param name="useVite">If true, generates for Vite; otherwise generates for Webpack.</param>
    /// <returns>The generated package.json content.</returns>
    string Generate(bool useVite = true);
}
