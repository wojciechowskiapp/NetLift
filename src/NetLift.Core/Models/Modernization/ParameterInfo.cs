namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents information about a method parameter.
/// </summary>
public sealed record ParameterInfo
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the fully qualified type name of the parameter.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the list of attributes applied to the parameter.
    /// </summary>
    public IReadOnlyList<string> Attributes { get; init; } = [];

    /// <summary>
    /// Gets the default value of the parameter, if any.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is passed by reference.
    /// </summary>
    public bool IsByRef { get; init; }

    /// <summary>
    /// Gets the binding source (e.g., "FromBody", "FromQuery", "FromRoute").
    /// </summary>
    public string? BindingSource { get; init; }
}
