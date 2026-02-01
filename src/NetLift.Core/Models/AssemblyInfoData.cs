namespace NetLift.Core.Models;

/// <summary>
/// Represents extracted assembly information from AssemblyInfo.cs files.
/// This data will be migrated to .csproj properties in SDK-style projects.
/// </summary>
public sealed class AssemblyInfoData
{
    /// <summary>
    /// Gets or sets the file path of the source AssemblyInfo.cs file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly title (AssemblyTitle attribute).
    /// Maps to AssemblyTitle property in .csproj.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the assembly description (AssemblyDescription attribute).
    /// Maps to Description property in .csproj.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the company name (AssemblyCompany attribute).
    /// Maps to Company and Authors properties in .csproj.
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the product name (AssemblyProduct attribute).
    /// Maps to Product property in .csproj.
    /// </summary>
    public string? Product { get; set; }

    /// <summary>
    /// Gets or sets the copyright information (AssemblyCopyright attribute).
    /// Maps to Copyright property in .csproj.
    /// </summary>
    public string? Copyright { get; set; }

    /// <summary>
    /// Gets or sets the assembly version (AssemblyVersion attribute).
    /// Maps to AssemblyVersion property in .csproj.
    /// </summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// Gets or sets the file version (AssemblyFileVersion attribute).
    /// Maps to FileVersion property in .csproj.
    /// </summary>
    public string? FileVersion { get; set; }

    /// <summary>
    /// Gets or sets the informational version (AssemblyInformationalVersion attribute).
    /// Maps to InformationalVersion property in .csproj.
    /// </summary>
    public string? InformationalVersion { get; set; }

    /// <summary>
    /// Gets or sets the assembly configuration (AssemblyConfiguration attribute).
    /// Maps to Configuration property in .csproj.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the GUID (Guid attribute).
    /// Used for COM interop.
    /// </summary>
    public string? Guid { get; set; }

    /// <summary>
    /// Gets or sets the COM visibility flag (ComVisible attribute).
    /// Maps to ComVisible property in .csproj.
    /// </summary>
    public bool? ComVisible { get; set; }

    /// <summary>
    /// Gets or sets the neutral resources language (NeutralResourcesLanguage attribute).
    /// Maps to NeutralLanguage property in .csproj.
    /// </summary>
    public string? NeutralLanguage { get; set; }

    /// <summary>
    /// Gets the list of assemblies that have access to internal types (InternalsVisibleTo attribute).
    /// Maps to InternalsVisibleTo ItemGroup in .csproj.
    /// </summary>
    public List<string> InternalsVisibleTo { get; init; } = new();

    /// <summary>
    /// Gets the list of custom attributes that cannot be automatically mapped to .csproj properties.
    /// These may need to be preserved in a partial AssemblyInfo.cs file.
    /// </summary>
    public List<CustomAttribute> CustomAttributes { get; init; } = new();

    /// <summary>
    /// Gets or sets the trademark information (AssemblyTrademark attribute).
    /// </summary>
    public string? Trademark { get; set; }

    /// <summary>
    /// Gets or sets the culture information (AssemblyCulture attribute).
    /// </summary>
    public string? Culture { get; set; }
}

/// <summary>
/// Represents a custom assembly attribute that cannot be automatically mapped to .csproj properties.
/// </summary>
public sealed class CustomAttribute
{
    /// <summary>
    /// Gets or sets the name of the attribute (without "Attribute" suffix).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of attribute arguments.
    /// </summary>
    public List<string> Arguments { get; init; } = new();

    /// <summary>
    /// Gets or sets the full attribute syntax text for preservation.
    /// </summary>
    public string? FullSyntax { get; set; }
}
