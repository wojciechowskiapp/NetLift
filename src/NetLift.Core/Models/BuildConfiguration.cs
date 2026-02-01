namespace NetLift.Core.Models;

/// <summary>
/// Represents a build configuration in a Visual Studio solution.
/// </summary>
public class BuildConfiguration
{
    /// <summary>
    /// Gets or sets the configuration name (e.g., Debug, Release).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the platform (e.g., "Any CPU", "x86", "x64").
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full configuration string (e.g., "Debug|Any CPU").
    /// </summary>
    public string FullName { get; set; } = string.Empty;
}
