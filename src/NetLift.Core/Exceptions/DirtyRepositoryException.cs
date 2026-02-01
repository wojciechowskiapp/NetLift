namespace NetLift.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to perform an operation on a Git repository with uncommitted changes.
/// </summary>
public class DirtyRepositoryException : Exception
{
    /// <summary>
    /// Gets the path to the repository.
    /// </summary>
    public string RepositoryPath { get; }

    /// <summary>
    /// Gets the list of modified files in the repository.
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirtyRepositoryException"/> class.
    /// </summary>
    /// <param name="repositoryPath">The path to the repository.</param>
    /// <param name="modifiedFiles">The list of modified files.</param>
    public DirtyRepositoryException(string repositoryPath, IEnumerable<string> modifiedFiles)
        : base($"Repository at '{repositoryPath}' has uncommitted changes. Please commit or stash your changes before proceeding.")
    {
        RepositoryPath = repositoryPath;
        ModifiedFiles = modifiedFiles.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirtyRepositoryException"/> class with a custom message.
    /// </summary>
    /// <param name="repositoryPath">The path to the repository.</param>
    /// <param name="modifiedFiles">The list of modified files.</param>
    /// <param name="message">The custom error message.</param>
    public DirtyRepositoryException(string repositoryPath, IEnumerable<string> modifiedFiles, string message)
        : base(message)
    {
        RepositoryPath = repositoryPath;
        ModifiedFiles = modifiedFiles.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirtyRepositoryException"/> class with an inner exception.
    /// </summary>
    /// <param name="repositoryPath">The path to the repository.</param>
    /// <param name="modifiedFiles">The list of modified files.</param>
    /// <param name="innerException">The inner exception.</param>
    public DirtyRepositoryException(string repositoryPath, IEnumerable<string> modifiedFiles, Exception innerException)
        : base($"Repository at '{repositoryPath}' has uncommitted changes. Please commit or stash your changes before proceeding.", innerException)
    {
        RepositoryPath = repositoryPath;
        ModifiedFiles = modifiedFiles.ToList();
    }
}
