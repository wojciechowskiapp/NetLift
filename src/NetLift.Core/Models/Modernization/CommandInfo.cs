namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information needed to generate a CQRS Command and CommandHandler.
/// </summary>
public sealed record CommandInfo
{
    /// <summary>
    /// Gets the name of the command (e.g., "CreateProduct").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the namespace for the command.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the properties of the command.
    /// </summary>
    public IReadOnlyList<CommandProperty> Properties { get; init; } = [];

    /// <summary>
    /// Gets the return type of the command handler.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets whether the handler is asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets the source controller and action that this command was generated from.
    /// </summary>
    public required SourceReference Source { get; init; }

    /// <summary>
    /// Gets the confidence score for this command generation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets whether this command requires validation.
    /// </summary>
    public bool RequiresValidation { get; init; }

    /// <summary>
    /// Gets the business logic extracted from the original action method.
    /// </summary>
    public string? BusinessLogic { get; init; }

    /// <summary>
    /// Gets the ViewBag/ViewData mutations from the original action method.
    /// Used to generate response DTO properties.
    /// </summary>
    public IReadOnlyList<ViewModelMutation>? ViewModelMutations { get; init; }
}

/// <summary>
/// Represents a property of a Command or Query.
/// </summary>
public sealed record CommandProperty
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets whether the property is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets whether the property is required (for validation).
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets validation constraints for this property.
    /// </summary>
    public IReadOnlyList<string> ValidationRules { get; init; } = [];
}

/// <summary>
/// Represents a reference to the source code element.
/// </summary>
public sealed record SourceReference
{
    /// <summary>
    /// Gets the file path of the source.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the controller name.
    /// </summary>
    public required string ControllerName { get; init; }

    /// <summary>
    /// Gets the action method name.
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// Gets the line number in the source file, if available.
    /// </summary>
    public int? LineNumber { get; init; }
}
