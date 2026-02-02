namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information about an ASP.NET MVC controller extracted from source code.
/// </summary>
public sealed record ControllerInfo
{
    /// <summary>
    /// Gets the file path of the controller source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the name of the controller class.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the namespace of the controller.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the base class of the controller (e.g., "Controller", "ApiController").
    /// </summary>
    public string? BaseClass { get; init; }

    /// <summary>
    /// Gets the list of action methods in the controller.
    /// </summary>
    public IReadOnlyList<ActionInfo> Actions { get; init; } = [];

    /// <summary>
    /// Gets the list of private helper methods in the controller that are called by action methods.
    /// </summary>
    public IReadOnlyList<PrivateMethodInfo> PrivateMethods { get; init; } = [];

    /// <summary>
    /// Gets the list of route attributes applied to the controller.
    /// </summary>
    public IReadOnlyList<string> RouteAttributes { get; init; } = [];

    /// <summary>
    /// Gets whether this is an API controller (derives from ApiController).
    /// </summary>
    public bool IsApiController { get; init; }

    /// <summary>
    /// Gets the confidence score for parsing this controller (0-100).
    /// </summary>
    public int Confidence { get; init; }
}
