using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Provides parsing capabilities for Visual Studio solution files.
/// </summary>
public interface ISolutionParser
{
    /// <summary>
    /// Parses a Visual Studio solution file and extracts its metadata.
    /// </summary>
    /// <param name="solutionPath">The absolute path to the .sln file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A SolutionInfo object containing parsed solution data.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the solution file doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the solution file is malformed.</exception>
    Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a file is a valid Visual Studio solution file.
    /// </summary>
    /// <param name="solutionPath">The absolute path to the .sln file.</param>
    /// <returns>True if the file is a valid solution file; otherwise, false.</returns>
    bool IsValidSolutionFile(string solutionPath);
}
