using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates Vite configuration files from bundle definitions.
/// </summary>
public interface IViteConfigGenerator
{
    /// <summary>
    /// Generates a vite.config.js file from bundle definitions.
    /// </summary>
    /// <param name="bundles">The bundle definitions to convert.</param>
    /// <returns>The generated vite.config.js content.</returns>
    string Generate(IEnumerable<BundleDefinition> bundles);
}
