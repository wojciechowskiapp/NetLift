using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates Program.cs files with WebApplicationBuilder pattern for ASP.NET Core.
/// </summary>
public interface IProgramCsGenerator
{
    /// <summary>
    /// Generates a Program.cs file content based on web.config settings.
    /// </summary>
    /// <param name="systemWeb">The system.web section from web.config.</param>
    /// <param name="appSettings">The appSettings section from web.config.</param>
    /// <param name="options">Options controlling the generation behavior.</param>
    /// <returns>The generated Program.cs file content as a string.</returns>
    string Generate(
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options);

    /// <summary>
    /// Generates and writes a Program.cs file to the specified output path.
    /// </summary>
    /// <param name="outputPath">The file path where Program.cs should be written.</param>
    /// <param name="systemWeb">The system.web section from web.config.</param>
    /// <param name="appSettings">The appSettings section from web.config.</param>
    /// <param name="options">Options controlling the generation behavior.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteToFileAsync(
        string outputPath,
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options,
        CancellationToken cancellationToken = default);
}
