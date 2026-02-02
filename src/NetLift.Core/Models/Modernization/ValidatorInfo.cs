namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information needed to generate a FluentValidation validator.
/// </summary>
public sealed record ValidatorInfo
{
    /// <summary>
    /// Gets the name of the validator class (e.g., "CreateProductCommandValidator").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the namespace for the validator.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the type being validated (Command or Query class name).
    /// </summary>
    public required string ValidatedType { get; init; }

    /// <summary>
    /// Gets the validation rules to be applied.
    /// </summary>
    public IReadOnlyList<ValidationRule> Rules { get; init; } = [];

    /// <summary>
    /// Gets the confidence score for this validator generation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the source that this validator was generated from.
    /// </summary>
    public string? SourceReference { get; init; }
}

/// <summary>
/// Represents a single FluentValidation rule.
/// </summary>
public sealed record ValidationRule
{
    /// <summary>
    /// Gets the property name being validated.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the validation method (e.g., "NotEmpty", "MaxLength", "EmailAddress").
    /// </summary>
    public required string ValidationMethod { get; init; }

    /// <summary>
    /// Gets optional parameters for the validation method.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the custom error message, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
