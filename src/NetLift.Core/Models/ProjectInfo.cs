namespace NetLift.Core.Models;

/// <summary>
/// Represents comprehensive information about a .NET project.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Gets or sets the absolute path to the project file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project name (derived from file name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the root namespace.
    /// </summary>
    public string? RootNamespace { get; set; }

    /// <summary>
    /// Gets or sets the output type (Library, Exe, WinExe, etc.).
    /// </summary>
    public string? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the target framework information.
    /// </summary>
    public TargetFramework? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the project format (old-style or SDK-style).
    /// </summary>
    public ProjectFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the project GUID (old-style projects only).
    /// </summary>
    public string? ProjectGuid { get; set; }

    /// <summary>
    /// Gets or sets the project type GUIDs (old-style projects only).
    /// </summary>
    public List<string> ProjectTypeGuids { get; set; } = new();

    /// <summary>
    /// Gets or sets the assembly references.
    /// </summary>
    public List<AssemblyReference> References { get; set; } = new();

    /// <summary>
    /// Gets or sets the project references.
    /// </summary>
    public List<ProjectReference> ProjectReferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the NuGet package references.
    /// </summary>
    public List<PackageReference> PackageReferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the compile items (source code files).
    /// </summary>
    public List<CompileItem> CompileItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the content items.
    /// </summary>
    public List<ContentItem> ContentItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the embedded resources.
    /// </summary>
    public List<EmbeddedResource> EmbeddedResources { get; set; } = new();

    /// <summary>
    /// Gets or sets additional properties from the project file.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}
