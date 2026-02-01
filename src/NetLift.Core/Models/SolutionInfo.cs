namespace NetLift.Core.Models;

/// <summary>
/// Represents information about a Visual Studio solution file.
/// </summary>
public class SolutionInfo
{
    /// <summary>
    /// Gets or sets the absolute path to the solution file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the solution name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the solution format version.
    /// </summary>
    public string FormatVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Visual Studio version.
    /// </summary>
    public string VisualStudioVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects in the solution.
    /// </summary>
    public List<SolutionProject> Projects { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of solution folders.
    /// </summary>
    public List<SolutionFolder> Folders { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of build configurations.
    /// </summary>
    public List<BuildConfiguration> Configurations { get; set; } = new();

    /// <summary>
    /// Gets the directory containing the solution file.
    /// </summary>
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;
}
