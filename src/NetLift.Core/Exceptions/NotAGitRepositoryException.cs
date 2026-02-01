namespace NetLift.Core.Exceptions;

/// <summary>
/// Exception thrown when a path is not a valid Git repository.
/// </summary>
public class NotAGitRepositoryException : Exception
{
    /// <summary>
    /// Gets the path that was not a valid Git repository.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotAGitRepositoryException"/> class.
    /// </summary>
    /// <param name="path">The path that is not a Git repository.</param>
    public NotAGitRepositoryException(string path)
        : base($"'{path}' is not a Git repository")
    {
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotAGitRepositoryException"/> class.
    /// </summary>
    /// <param name="path">The path that is not a Git repository.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NotAGitRepositoryException(string path, Exception innerException)
        : base($"'{path}' is not a Git repository", innerException)
    {
        Path = path;
    }
}
