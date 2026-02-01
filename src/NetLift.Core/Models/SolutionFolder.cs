namespace NetLift.Core.Models;

/// <summary>
/// Represents a solution folder in a Visual Studio solution.
/// </summary>
public class SolutionFolder
{
    /// <summary>
    /// Gets or sets the unique identifier of the solution folder.
    /// </summary>
    public Guid FolderGuid { get; set; }

    /// <summary>
    /// Gets or sets the folder name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects contained in this folder.
    /// </summary>
    public List<Guid> ProjectGuids { get; set; } = new();
}
