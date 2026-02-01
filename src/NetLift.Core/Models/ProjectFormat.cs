namespace NetLift.Core.Models;

/// <summary>
/// Represents the format of a .csproj file.
/// </summary>
public enum ProjectFormat
{
    /// <summary>
    /// Old-style .csproj format (pre-.NET Core, verbose XML).
    /// </summary>
    OldStyle,

    /// <summary>
    /// Modern SDK-style .csproj format (simplified, .NET Core/5+).
    /// </summary>
    SdkStyle
}
