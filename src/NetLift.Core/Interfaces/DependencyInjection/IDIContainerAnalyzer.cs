using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Base analyzer interface for DI container registration parsing.
/// </summary>
public interface IDIContainerAnalyzer
{
    /// <summary>
    /// The DI framework this analyzer supports.
    /// </summary>
    DIFrameworkType SupportedFramework { get; }

    /// <summary>
    /// Analyzes service registrations in a file.
    /// </summary>
    /// <param name="filePath">The file to analyze.</param>
    /// <returns>List of service registrations found.</returns>
    Task<List<ServiceRegistrationInfo>> AnalyzeRegistrationsAsync(string filePath);

    /// <summary>
    /// Analyzes service registrations from source code content.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>List of service registrations found.</returns>
    Task<List<ServiceRegistrationInfo>> AnalyzeRegistrationsFromContentAsync(string content, string filePath);

    /// <summary>
    /// Maps a source lifetime to the MEDI lifetime.
    /// </summary>
    /// <param name="sourceLifetime">The source lifetime name.</param>
    /// <returns>The lifetime mapping.</returns>
    Task<LifetimeMapping> MapLifetimeAsync(string sourceLifetime);

    /// <summary>
    /// Calculates the confidence score for a registration.
    /// </summary>
    /// <param name="registration">The registration to score.</param>
    /// <returns>Confidence score (0-100).</returns>
    int CalculateConfidence(ServiceRegistrationInfo registration);
}

/// <summary>
/// Autofac-specific analyzer.
/// </summary>
public interface IAutofacAnalyzer : IDIContainerAnalyzer
{
    /// <summary>
    /// Parses Autofac modules in a project.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <returns>List of modules found.</returns>
    Task<List<ModuleInfo>> ParseModulesAsync(string projectPath);

    /// <summary>
    /// Parses registrations from an Autofac module file.
    /// </summary>
    /// <param name="filePath">The module file path.</param>
    /// <returns>List of service registrations.</returns>
    Task<List<ServiceRegistrationInfo>> ParseModuleAsync(string filePath);

    /// <summary>
    /// Parses ContainerBuilder registrations from source code.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <returns>List of service registrations.</returns>
    List<ServiceRegistrationInfo> ParseContainerBuilder(string content);
}

/// <summary>
/// Unity-specific analyzer.
/// </summary>
public interface IUnityAnalyzer : IDIContainerAnalyzer
{
    /// <summary>
    /// Parses UnityConfig.cs file.
    /// </summary>
    /// <param name="filePath">The config file path.</param>
    /// <returns>List of service registrations.</returns>
    Task<List<ServiceRegistrationInfo>> ParseUnityConfigAsync(string filePath);

    /// <summary>
    /// Parses property injection from source code.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="typeName">The type name to analyze.</param>
    /// <returns>Property injection information.</returns>
    PropertyInjectionInfo ParseInjectionProperty(string content, string typeName);
}

/// <summary>
/// Ninject-specific analyzer.
/// </summary>
public interface INinjectAnalyzer : IDIContainerAnalyzer
{
    /// <summary>
    /// Parses Ninject bindings from source code.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <returns>List of service registrations.</returns>
    List<ServiceRegistrationInfo> ParseBindings(string content);

    /// <summary>
    /// Parses Ninject modules in a project.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <returns>List of modules found.</returns>
    Task<List<ModuleInfo>> ParseNinjectModulesAsync(string projectPath);
}

/// <summary>
/// StructureMap-specific analyzer.
/// </summary>
public interface IStructureMapAnalyzer : IDIContainerAnalyzer
{
    /// <summary>
    /// Parses StructureMap registry file.
    /// </summary>
    /// <param name="filePath">The registry file path.</param>
    /// <returns>List of service registrations.</returns>
    Task<List<ServiceRegistrationInfo>> ParseRegistryAsync(string filePath);

    /// <summary>
    /// Parses assembly scanning from source code.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <returns>List of service registrations.</returns>
    List<ServiceRegistrationInfo> ParseScan(string content);
}
