namespace NetLift.Core.Models;

/// <summary>
/// Represents a parsed packages.config file.
/// </summary>
public class PackagesConfig
{
    /// <summary>
    /// Gets or sets the list of package references.
    /// </summary>
    public List<PackageReference> Packages { get; set; } = new();
}
