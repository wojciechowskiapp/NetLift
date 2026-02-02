namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information about a private helper method in a controller.
/// Used to track private methods that are called by action methods and need to be extracted.
/// </summary>
public sealed record PrivateMethodInfo
{
    /// <summary>
    /// Gets the name of the private method.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the full source code of the method, including signature and body.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets the parameters of the private method.
    /// </summary>
    public IReadOnlyList<ActionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the return type of the private method.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets the list of action method names that call this private method.
    /// </summary>
    public IReadOnlyList<string> CallingActions { get; init; } = [];

    /// <summary>
    /// Gets whether this private method is asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets whether this private method is static.
    /// </summary>
    public bool IsStatic { get; init; }
}
