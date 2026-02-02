using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates modern observability code for ASP.NET Core applications.
/// </summary>
public interface IObservabilityGenerator
{
    /// <summary>
    /// Generates modern observability code based on detected logging information.
    /// </summary>
    /// <param name="loggingInfo">Detected logging information</param>
    /// <param name="projectInfo">Project information</param>
    /// <param name="includeOpenTelemetry">Whether to include OpenTelemetry setup</param>
    /// <returns>Generated observability code and configuration</returns>
    ObservabilityResult Generate(
        LoggingInfo loggingInfo,
        ProjectInfo projectInfo,
        bool includeOpenTelemetry = false);

    /// <summary>
    /// Generates an ILogger field declaration with dependency injection.
    /// </summary>
    /// <param name="className">The class name for ILogger&lt;T&gt;</param>
    /// <param name="fieldName">The field name (default: _logger)</param>
    /// <returns>Generated C# field declaration and constructor parameter</returns>
    string GenerateLoggerField(string className, string fieldName = "_logger");

    /// <summary>
    /// Generates a structured logging method call.
    /// </summary>
    /// <param name="level">Log level (Information, Warning, Error, etc.)</param>
    /// <param name="message">Log message template</param>
    /// <param name="properties">Property names for structured logging</param>
    /// <returns>Generated logging method call</returns>
    string GenerateStructuredLoggingCall(
        string level,
        string message,
        params string[] properties);

    /// <summary>
    /// Generates a health check endpoint configuration.
    /// </summary>
    /// <param name="includeDatabase">Whether to include database health checks</param>
    /// <param name="includeCustomChecks">Whether to include custom health checks</param>
    /// <returns>Generated health check endpoint code</returns>
    string GenerateHealthCheckEndpoint(
        bool includeDatabase = false,
        bool includeCustomChecks = false);

    /// <summary>
    /// Generates OpenTelemetry setup code for Program.cs.
    /// </summary>
    /// <param name="serviceName">The service name for OpenTelemetry</param>
    /// <param name="includeTracing">Whether to include distributed tracing</param>
    /// <param name="includeMetrics">Whether to include metrics collection</param>
    /// <returns>Generated OpenTelemetry configuration code</returns>
    string GenerateOpenTelemetrySetup(
        string serviceName,
        bool includeTracing = true,
        bool includeMetrics = true);

    /// <summary>
    /// Generates logging configuration for appsettings.json.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level (Information, Warning, etc.)</param>
    /// <returns>JSON configuration object for logging</returns>
    string GenerateLoggingConfiguration(string minimumLevel = "Information");
}
