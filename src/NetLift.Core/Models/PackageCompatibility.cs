namespace NetLift.Core.Models;

/// <summary>
/// Represents the compatibility status of a NuGet package with .NET Core/.NET.
/// </summary>
public enum PackageCompatibility
{
    /// <summary>
    /// Compatibility status is unknown or has not been determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Package is compatible with .NET Core/.NET as-is.
    /// </summary>
    Compatible,

    /// <summary>
    /// Package has a different replacement package for .NET Core/.NET.
    /// </summary>
    HasReplacement,

    /// <summary>
    /// Package is incompatible with .NET Core/.NET.
    /// </summary>
    Incompatible,

    /// <summary>
    /// Package is deprecated and should not be used.
    /// </summary>
    Deprecated
}
