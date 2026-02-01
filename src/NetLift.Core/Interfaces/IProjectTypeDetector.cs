using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Interface for detecting project types and features.
/// </summary>
public interface IProjectTypeDetector
{
    /// <summary>
    /// Detects the type and features of a .NET project.
    /// </summary>
    /// <param name="project">The project information to analyze.</param>
    /// <returns>A comprehensive detection result with confidence scores.</returns>
    ProjectTypeResult Detect(ProjectInfo project);
}
