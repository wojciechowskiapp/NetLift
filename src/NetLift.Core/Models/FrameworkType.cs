namespace NetLift.Core.Models;

/// <summary>
/// Represents the type of .NET framework.
/// </summary>
public enum FrameworkType
{
    /// <summary>
    /// .NET Framework (e.g., net48, net472).
    /// </summary>
    Framework,

    /// <summary>
    /// .NET Core (e.g., netcoreapp3.1).
    /// </summary>
    Core,

    /// <summary>
    /// .NET Standard (e.g., netstandard2.0).
    /// </summary>
    Standard,

    /// <summary>
    /// Modern .NET (e.g., net6.0, net8.0).
    /// </summary>
    Net,

    /// <summary>
    /// Unknown or unrecognized framework type.
    /// </summary>
    Unknown
}
