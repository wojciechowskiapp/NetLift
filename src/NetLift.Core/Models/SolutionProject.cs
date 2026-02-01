namespace NetLift.Core.Models;

/// <summary>
/// Represents a project entry within a Visual Studio solution file.
/// </summary>
public class SolutionProject
{
    /// <summary>
    /// Gets or sets the unique identifier of the project.
    /// </summary>
    public Guid ProjectGuid { get; set; }

    /// <summary>
    /// Gets or sets the project type GUID that identifies the project's type.
    /// </summary>
    public Guid TypeGuid { get; set; }

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative path to the project file from the solution.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the project file.
    /// </summary>
    public string AbsolutePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detected project type.
    /// </summary>
    public ProjectType DetectedType { get; set; }
}
