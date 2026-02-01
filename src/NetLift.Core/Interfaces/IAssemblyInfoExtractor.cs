using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Interface for extracting assembly information from AssemblyInfo.cs files.
/// Used during migration to convert legacy AssemblyInfo.cs attributes to SDK-style .csproj properties.
/// </summary>
public interface IAssemblyInfoExtractor
{
    /// <summary>
    /// Extracts assembly information from an AssemblyInfo.cs file.
    /// </summary>
    /// <param name="assemblyInfoPath">The absolute path to the AssemblyInfo.cs file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An AssemblyInfoData object containing all extracted attributes.</returns>
    Task<AssemblyInfoData> ExtractAsync(string assemblyInfoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts and merges assembly information from multiple AssemblyInfo.cs files.
    /// This is useful when projects have both shared and project-specific AssemblyInfo files.
    /// </summary>
    /// <param name="assemblyInfoPaths">The absolute paths to the AssemblyInfo.cs files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A merged AssemblyInfoData object containing all extracted attributes.</returns>
    Task<AssemblyInfoData> ExtractAndMergeAsync(IEnumerable<string> assemblyInfoPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the specified file is a valid AssemblyInfo.cs file that can be parsed.
    /// </summary>
    /// <param name="filePath">The absolute path to the file.</param>
    /// <returns>True if the file can be parsed as an AssemblyInfo.cs file; otherwise, false.</returns>
    bool CanExtract(string filePath);
}
