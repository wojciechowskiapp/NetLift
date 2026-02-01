namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents the complete duplex warning report for WCF services.
/// </summary>
public sealed record DuplexWarningReport
{
    /// <summary>
    /// Gets the list of duplex warnings detected in the service contracts.
    /// </summary>
    public IReadOnlyList<DuplexWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the markdown-formatted migration guidance document.
    /// Provides recommendations for migrating duplex contracts to modern alternatives.
    /// </summary>
    public required string MigrationGuidanceMarkdown { get; init; }

    /// <summary>
    /// Gets a value indicating whether any duplex contracts were detected.
    /// </summary>
    public bool HasDuplexContracts => Warnings.Count > 0;
}

/// <summary>
/// Represents a warning about a specific duplex contract.
/// </summary>
public sealed record DuplexWarning
{
    /// <summary>
    /// Gets the name of the service interface that uses a callback contract.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Gets the name of the callback contract interface.
    /// </summary>
    public required string CallbackContractName { get; init; }

    /// <summary>
    /// Gets the severity level of this duplex warning.
    /// </summary>
    public required DuplexWarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets the list of callback methods defined in the callback contract.
    /// </summary>
    public IReadOnlyList<CallbackMethod> CallbackMethods { get; init; } = [];

    /// <summary>
    /// Gets the list of duplex bindings used by endpoints implementing this service.
    /// </summary>
    public IReadOnlyList<string> DuplexBindings { get; init; } = [];
}

/// <summary>
/// Represents a callback method in a duplex contract.
/// </summary>
public sealed record CallbackMethod
{
    /// <summary>
    /// Gets the name of the callback method.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a one-way operation (fire-and-forget).
    /// </summary>
    public bool IsOneWay { get; init; }

    /// <summary>
    /// Gets the list of method parameters.
    /// </summary>
    public IReadOnlyList<MethodParameter> Parameters { get; init; } = [];
}

/// <summary>
/// Represents the severity level of a duplex warning.
/// </summary>
public enum DuplexWarningSeverity
{
    /// <summary>
    /// High severity - CallbackContract attribute detected on service interface.
    /// Requires manual migration as callbacks cannot be auto-migrated.
    /// </summary>
    High,

    /// <summary>
    /// Medium severity - Duplex binding detected in configuration (wsDualHttpBinding, netTcpBinding).
    /// May require manual review to ensure proper migration.
    /// </summary>
    Medium
}
