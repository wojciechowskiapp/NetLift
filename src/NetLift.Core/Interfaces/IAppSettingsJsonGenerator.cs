using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates appsettings.json content from web.config sections.
/// </summary>
public interface IAppSettingsJsonGenerator
{
    /// <summary>
    /// Generates a valid appsettings.json string from web.config sections.
    /// </summary>
    /// <param name="connectionStrings">The parsed connection strings section.</param>
    /// <param name="appSettings">The parsed application settings section.</param>
    /// <param name="systemWeb">The parsed system.web section.</param>
    /// <returns>A formatted JSON string suitable for appsettings.json.</returns>
    string Generate(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb);

    /// <summary>
    /// Generates and writes appsettings.json to a file.
    /// </summary>
    /// <param name="outputPath">The path where the appsettings.json file should be written.</param>
    /// <param name="connectionStrings">The parsed connection strings section.</param>
    /// <param name="appSettings">The parsed application settings section.</param>
    /// <param name="systemWeb">The parsed system.web section.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteToFileAsync(
        string outputPath,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        CancellationToken cancellationToken = default);
}
