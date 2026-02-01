namespace NetLift.Core.Models.Mvc;

/// <summary>
/// Represents a migration plan for converting an MVC Area to ASP.NET Core conventions.
/// </summary>
public sealed record AreaMigrationPlan
{
    /// <summary>
    /// Gets the area name.
    /// </summary>
    public string AreaName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of folders to create for the area structure.
    /// Example: Areas/{AreaName}/Controllers, Areas/{AreaName}/Views, Areas/{AreaName}/Models
    /// </summary>
    public List<string> FoldersToCreate { get; init; } = new();

    /// <summary>
    /// Gets the files to generate with their relative paths and content.
    /// Key: Relative file path (e.g., "Areas/Admin/Views/_ViewImports.cshtml")
    /// Value: File content
    /// </summary>
    public Dictionary<string, string> FilesToGenerate { get; init; } = new();

    /// <summary>
    /// Gets the list of controller file paths that need [Area] attribute added.
    /// </summary>
    public List<string> ControllersToUpdate { get; init; } = new();

    /// <summary>
    /// Gets the route registration code to be added to Program.cs for this area.
    /// </summary>
    public string RouteRegistration { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence score for this migration (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; } = 100;

    /// <summary>
    /// Gets diagnostic messages for this migration plan.
    /// </summary>
    public List<string> Diagnostics { get; init; } = new();
}
