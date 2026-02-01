namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a parameter in a method signature.
/// Used by various WCF-related models for parameter information.
/// </summary>
public sealed record MethodParameter
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public required string Type { get; init; }
}
