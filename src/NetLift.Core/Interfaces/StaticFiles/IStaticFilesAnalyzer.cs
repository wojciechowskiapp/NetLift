using NetLift.Core.Models.StaticFiles;

namespace NetLift.Core.Interfaces.StaticFiles;

/// <summary>
/// Analyzes static file folders and references in a project.
/// </summary>
public interface IStaticFilesAnalyzer
{
    /// <summary>
    /// Analyzes static files in a project.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <returns>Information about static files.</returns>
    Task<StaticFilesInfo> AnalyzeAsync(string projectPath);

    /// <summary>
    /// Detects static file folders (Content, Scripts, Images, etc.).
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <returns>List of detected static folders.</returns>
    IReadOnlyList<StaticFolder> DetectStaticFolders(string projectPath);

    /// <summary>
    /// Detects static file references in code and views.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <returns>List of static file references.</returns>
    Task<IReadOnlyList<StaticFileReference>> DetectReferencesAsync(string projectPath);

    /// <summary>
    /// Maps a static path to its wwwroot equivalent.
    /// </summary>
    /// <param name="originalPath">The original path (e.g., "~/Content/site.css").</param>
    /// <returns>The mapped path (e.g., "~/css/site.css").</returns>
    string MapToWwwroot(string originalPath);
}
