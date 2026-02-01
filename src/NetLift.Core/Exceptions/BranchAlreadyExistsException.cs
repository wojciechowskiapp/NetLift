namespace NetLift.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a branch that already exists.
/// </summary>
public class BranchAlreadyExistsException : Exception
{
    /// <summary>
    /// Gets the name of the branch that already exists.
    /// </summary>
    public string BranchName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchAlreadyExistsException"/> class.
    /// </summary>
    /// <param name="branchName">The name of the branch that already exists.</param>
    public BranchAlreadyExistsException(string branchName)
        : base($"Branch '{branchName}' already exists.")
    {
        BranchName = branchName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchAlreadyExistsException"/> class with a custom message.
    /// </summary>
    /// <param name="branchName">The name of the branch that already exists.</param>
    /// <param name="message">The custom error message.</param>
    public BranchAlreadyExistsException(string branchName, string message)
        : base(message)
    {
        BranchName = branchName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchAlreadyExistsException"/> class with an inner exception.
    /// </summary>
    /// <param name="branchName">The name of the branch that already exists.</param>
    /// <param name="innerException">The inner exception.</param>
    public BranchAlreadyExistsException(string branchName, Exception innerException)
        : base($"Branch '{branchName}' already exists.", innerException)
    {
        BranchName = branchName;
    }
}
