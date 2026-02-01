namespace NetLift.Core.Interfaces;

using NetLift.Core.Models;

/// <summary>
/// Validates migrated projects by executing dotnet build and parsing output.
/// </summary>
public interface IBuildValidator
{
    /// <summary>
    /// Validates a solution or project by running dotnet build.
    /// </summary>
    /// <param name="solutionOrProjectPath">Path to the solution or project file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The build validation result.</returns>
    Task<BuildResult> ValidateAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default);
}
