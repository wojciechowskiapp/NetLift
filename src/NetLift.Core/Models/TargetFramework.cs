namespace NetLift.Core.Models;

/// <summary>
/// Represents a target framework for a .NET project.
/// </summary>
public class TargetFramework
{
    /// <summary>
    /// Gets or sets the target framework moniker (e.g., "net48", "net6.0").
    /// </summary>
    public string Moniker { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of framework.
    /// </summary>
    public FrameworkType Type { get; set; }

    /// <summary>
    /// Gets or sets the framework version.
    /// </summary>
    public Version? Version { get; set; }

    /// <summary>
    /// Gets or sets the original framework version string (e.g., "v4.8").
    /// </summary>
    public string? OriginalVersion { get; set; }
}
