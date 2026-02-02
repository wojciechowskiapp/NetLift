using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Analyzes service classes to extract method information and dependencies.
/// </summary>
public interface IServiceAnalyzer
{
    /// <summary>
    /// Analyzes service classes in the specified project directory.
    /// </summary>
    /// <param name="projectPath">Path to the project directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of analyzed service classes.</returns>
    Task<IReadOnlyList<ServiceInfo>> AnalyzeServicesAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a single service file.
    /// </summary>
    /// <param name="filePath">Path to the service file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service information or null if not a valid service.</returns>
    Task<ServiceInfo?> AnalyzeServiceFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the service method that corresponds to a method call expression.
    /// </summary>
    /// <param name="services">List of analyzed services.</param>
    /// <param name="callExpression">The method call expression (e.g., "service.GetItem(id)").</param>
    /// <returns>The matching service method or null.</returns>
    ServiceMethodInfo? FindServiceMethod(
        IReadOnlyList<ServiceInfo> services,
        string callExpression);
}
