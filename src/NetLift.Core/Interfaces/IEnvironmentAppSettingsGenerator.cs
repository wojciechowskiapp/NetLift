using NetLift.Core.Models.Config;
using System.Xml.Linq;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates environment-specific appsettings.json files (Development, Production)
/// from web.config sections with optional XDT transform support.
/// </summary>
public interface IEnvironmentAppSettingsGenerator
{
    /// <summary>
    /// Generates appsettings.Development.json content with debug settings.
    /// </summary>
    /// <param name="connectionStrings">The parsed connection strings section.</param>
    /// <param name="appSettings">The parsed application settings section.</param>
    /// <param name="systemWeb">The parsed system.web section.</param>
    /// <param name="debugTransform">Optional web.Debug.config XDT transform document.</param>
    /// <returns>A formatted JSON string for Development environment.</returns>
    string GenerateDevelopment(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null);

    /// <summary>
    /// Generates appsettings.Production.json content with production settings and environment variable placeholders.
    /// </summary>
    /// <param name="connectionStrings">The parsed connection strings section.</param>
    /// <param name="appSettings">The parsed application settings section.</param>
    /// <param name="systemWeb">The parsed system.web section.</param>
    /// <param name="releaseTransform">Optional web.Release.config XDT transform document.</param>
    /// <returns>A formatted JSON string for Production environment.</returns>
    string GenerateProduction(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? releaseTransform = null);

    /// <summary>
    /// Generates and writes both Development and Production appsettings files.
    /// </summary>
    /// <param name="outputDirectory">The directory where the files should be written.</param>
    /// <param name="connectionStrings">The parsed connection strings section.</param>
    /// <param name="appSettings">The parsed application settings section.</param>
    /// <param name="systemWeb">The parsed system.web section.</param>
    /// <param name="debugTransform">Optional web.Debug.config XDT transform document.</param>
    /// <param name="releaseTransform">Optional web.Release.config XDT transform document.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteEnvironmentFilesAsync(
        string outputDirectory,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null,
        XDocument? releaseTransform = null,
        CancellationToken cancellationToken = default);
}
