namespace NetLift.Core.Models;

/// <summary>
/// Represents an assembly reference in a .NET project.
/// </summary>
public class AssemblyReference
{
    /// <summary>
    /// Gets or sets the assembly name (e.g., "System.Web.Mvc").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hint path to the assembly DLL (for NuGet packages).
    /// </summary>
    public string? HintPath { get; set; }

    /// <summary>
    /// Gets or sets the assembly version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the culture.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets the public key token.
    /// </summary>
    public string? PublicKeyToken { get; set; }

    /// <summary>
    /// Gets or sets the processor architecture.
    /// </summary>
    public string? ProcessorArchitecture { get; set; }

    /// <summary>
    /// Gets or sets whether this is a private assembly.
    /// </summary>
    public bool? IsPrivate { get; set; }

    /// <summary>
    /// Gets a value indicating whether this reference appears to be a NuGet package (has HintPath).
    /// </summary>
    public bool IsNuGetPackage => !string.IsNullOrEmpty(HintPath);
}
