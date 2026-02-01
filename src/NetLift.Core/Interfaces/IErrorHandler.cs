using NetLift.Core.Errors;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Handles errors and warnings during migration, providing error tracking and recovery suggestions.
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Registers an error that occurred during migration.
    /// </summary>
    /// <param name="error">The error to register.</param>
    void HandleError(MigrationError error);

    /// <summary>
    /// Registers a warning that occurred during migration.
    /// </summary>
    /// <param name="warning">The warning to register.</param>
    void HandleWarning(MigrationError warning);

    /// <summary>
    /// Gets all errors that have been registered.
    /// </summary>
    /// <returns>Read-only list of all errors.</returns>
    IReadOnlyList<MigrationError> GetErrors();

    /// <summary>
    /// Gets all warnings that have been registered.
    /// </summary>
    /// <returns>Read-only list of all warnings.</returns>
    IReadOnlyList<MigrationError> GetWarnings();

    /// <summary>
    /// Indicates whether any critical errors have been registered.
    /// Critical errors prevent successful migration completion.
    /// </summary>
    bool HasCriticalErrors { get; }

    /// <summary>
    /// Clears all registered errors and warnings.
    /// </summary>
    void Clear();
}

/// <summary>
/// Provides context-aware recovery suggestions for migration errors.
/// </summary>
public interface IRecoverySuggestionProvider
{
    /// <summary>
    /// Gets recovery suggestions for a specific error.
    /// </summary>
    /// <param name="error">The error to get suggestions for.</param>
    /// <returns>List of actionable recovery suggestions.</returns>
    IReadOnlyList<string> GetSuggestions(MigrationError error);
}
