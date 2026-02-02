using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Analyzes .NET Framework projects to detect legacy logging patterns and configurations.
/// </summary>
public interface ILoggingAnalyzer
{
    /// <summary>
    /// Analyzes a project to detect logging configuration and usage.
    /// </summary>
    /// <param name="projectInfo">The project information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LoggingInfo if logging is detected, null otherwise</returns>
    Task<LoggingInfo?> AnalyzeProjectAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds logger usages in a C# source file.
    /// </summary>
    /// <param name="filePath">Path to the source file</param>
    /// <param name="sourceCode">Source code content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected logger usages</returns>
    Task<IReadOnlyList<LoggerUsage>> FindLoggerUsagesAsync(
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects which logging framework is in use in the project.
    /// </summary>
    /// <param name="projectInfo">The project information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The detected logging framework</returns>
    Task<LoggingFramework> DetectFrameworkAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a logging configuration file (log4net.config, nlog.config, etc.).
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file</param>
    /// <param name="framework">The logging framework type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed configuration XML content</returns>
    Task<string?> ParseLoggingConfigAsync(
        string configFilePath,
        LoggingFramework framework,
        CancellationToken cancellationToken = default);
}
