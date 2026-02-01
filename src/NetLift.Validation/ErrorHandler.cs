using System.Collections.Concurrent;
using NetLift.Core.Errors;
using NetLift.Core.Interfaces;

namespace NetLift.Validation;

/// <summary>
/// Thread-safe error handler for tracking migration errors and warnings.
/// </summary>
public sealed class ErrorHandler : IErrorHandler
{
    private readonly ConcurrentBag<MigrationError> _errors = new();
    private readonly ConcurrentBag<MigrationError> _warnings = new();
    private readonly IRecoverySuggestionProvider _suggestionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorHandler"/> class.
    /// </summary>
    /// <param name="suggestionProvider">Provider for recovery suggestions.</param>
    public ErrorHandler(IRecoverySuggestionProvider suggestionProvider)
    {
        _suggestionProvider = suggestionProvider ?? throw new ArgumentNullException(nameof(suggestionProvider));
    }

    /// <inheritdoc/>
    public void HandleError(MigrationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // Enrich error with recovery suggestions if not already present
        if (error.RecoverySuggestions.Count == 0)
        {
            var suggestions = _suggestionProvider.GetSuggestions(error);
            if (suggestions.Count > 0)
            {
                error = error with { RecoverySuggestions = suggestions };
            }
        }

        _errors.Add(error);
    }

    /// <inheritdoc/>
    public void HandleWarning(MigrationError warning)
    {
        ArgumentNullException.ThrowIfNull(warning);

        // Enrich warning with recovery suggestions if not already present
        if (warning.RecoverySuggestions.Count == 0)
        {
            var suggestions = _suggestionProvider.GetSuggestions(warning);
            if (suggestions.Count > 0)
            {
                warning = warning with { RecoverySuggestions = suggestions };
            }
        }

        _warnings.Add(warning);
    }

    /// <inheritdoc/>
    public IReadOnlyList<MigrationError> GetErrors()
    {
        return _errors.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<MigrationError> GetWarnings()
    {
        return _warnings.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public bool HasCriticalErrors => !_errors.IsEmpty;

    /// <inheritdoc/>
    public void Clear()
    {
        _errors.Clear();
        _warnings.Clear();
    }
}
