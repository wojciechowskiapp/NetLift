using NetLift.Core.Models.StaticFiles;

namespace NetLift.Core.Interfaces.StaticFiles;

/// <summary>
/// Migrates static files to the wwwroot folder structure.
/// </summary>
public interface IStaticFilesMigrator
{
    /// <summary>
    /// Migrates static files to wwwroot.
    /// </summary>
    /// <param name="staticFilesInfo">The analyzed static files info.</param>
    /// <param name="dryRun">If true, don't make actual changes.</param>
    /// <returns>The migration result.</returns>
    Task<StaticFilesMigrationResult> MigrateAsync(StaticFilesInfo staticFilesInfo, bool dryRun = false);

    /// <summary>
    /// Creates the wwwroot folder structure.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    Task CreateWwwrootStructureAsync(string projectPath);

    /// <summary>
    /// Moves a static folder to wwwroot.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <param name="folder">The folder to move.</param>
    Task MoveFolderAsync(string projectPath, StaticFolder folder);

    /// <summary>
    /// Updates static file references in code and views.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <param name="references">The references to update.</param>
    /// <param name="dryRun">If true, don't make actual changes.</param>
    /// <returns>Number of references updated.</returns>
    Task<int> UpdateReferencesAsync(string projectPath, IReadOnlyList<StaticFileReference> references, bool dryRun = false);

    /// <summary>
    /// Generates the static files middleware configuration for Program.cs.
    /// </summary>
    /// <returns>The middleware configuration code.</returns>
    string GenerateStaticFilesMiddleware();
}
