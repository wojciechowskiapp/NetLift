using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Service for tracking and reporting changes during a dry-run migration.
/// </summary>
public interface IDryRunService
{
    /// <summary>
    /// Records a planned change to a file.
    /// </summary>
    /// <param name="filePath">The path to the file that will be changed.</param>
    /// <param name="changeType">The type of change.</param>
    /// <param name="preview">A preview of the change content.</param>
    void RecordChange(string filePath, ChangeType changeType, string preview);

    /// <summary>
    /// Records a planned change to a file with original and new content.
    /// </summary>
    /// <param name="filePath">The path to the file that will be changed.</param>
    /// <param name="changeType">The type of change.</param>
    /// <param name="originalContent">The original content of the file.</param>
    /// <param name="newContent">The new content of the file.</param>
    void RecordChange(string filePath, ChangeType changeType, string? originalContent, string? newContent);

    /// <summary>
    /// Records a warning message.
    /// </summary>
    /// <param name="warning">The warning message.</param>
    void RecordWarning(string warning);

    /// <summary>
    /// Records an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    void RecordError(string error);

    /// <summary>
    /// Generates a comprehensive report of all recorded changes.
    /// </summary>
    /// <returns>A dry-run report containing all changes and statistics.</returns>
    DryRunReport GetReport();

    /// <summary>
    /// Clears all recorded changes and resets the service.
    /// </summary>
    void Reset();
}
