using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates Webpack configuration files from bundle definitions.
/// </summary>
public interface IWebpackConfigGenerator
{
    /// <summary>
    /// Generates a webpack.config.js file from bundle definitions.
    /// </summary>
    /// <param name="bundles">The bundle definitions to convert.</param>
    /// <returns>The generated webpack.config.js content.</returns>
    string Generate(IEnumerable<BundleDefinition> bundles);
}
