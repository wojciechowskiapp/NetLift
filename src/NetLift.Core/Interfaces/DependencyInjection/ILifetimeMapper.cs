using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Maps legacy DI framework lifetimes to Microsoft.Extensions.DependencyInjection.ServiceLifetime.
/// </summary>
public interface ILifetimeMapper
{
    /// <summary>
    /// Maps a source lifetime to the target MEDI lifetime.
    /// </summary>
    /// <param name="sourceLifetime">The source lifetime name (e.g., "SingleInstance", "InstancePerLifetimeScope").</param>
    /// <param name="framework">The DI framework the lifetime belongs to.</param>
    /// <returns>The lifetime mapping with confidence score.</returns>
    LifetimeMapping MapLifetime(string sourceLifetime, DIFrameworkType framework);

    /// <summary>
    /// Gets the ServiceLifetime from a mapping.
    /// </summary>
    /// <param name="mapping">The lifetime mapping.</param>
    /// <returns>The target ServiceLifetime.</returns>
    ServiceLifetime GetServiceLifetime(LifetimeMapping mapping);

    /// <summary>
    /// Loads lifetime mappings from a YAML configuration file.
    /// </summary>
    /// <param name="yamlPath">Path to the YAML file.</param>
    Task LoadMappingsFromYamlAsync(string yamlPath);

    /// <summary>
    /// Gets all mappings for a specific framework.
    /// </summary>
    /// <param name="framework">The DI framework.</param>
    /// <returns>List of lifetime mappings.</returns>
    IReadOnlyList<LifetimeMapping> GetMappingsForFramework(DIFrameworkType framework);
}
