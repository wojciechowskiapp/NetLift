using NetLift.Core.Models;

namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents the result of generating modern observability code.
/// </summary>
public sealed record ObservabilityResult
{
    /// <summary>
    /// Gets the generated files with their content (file path to content mapping).
    /// </summary>
    public IReadOnlyDictionary<string, string> GeneratedFiles { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the NuGet packages that need to be added.
    /// </summary>
    public IReadOnlyList<PackageReference> PackagesToAdd { get; init; } = [];

    /// <summary>
    /// Gets the configuration changes needed in appsettings.json.
    /// </summary>
    public IReadOnlyList<ConfigurationChange> ConfigurationChanges { get; init; } = [];

    /// <summary>
    /// Gets the code changes to add to Program.cs for logging configuration.
    /// </summary>
    public string? ProgramCsChanges { get; init; }

    /// <summary>
    /// Gets the health check endpoints that were generated.
    /// </summary>
    public IReadOnlyList<string> HealthCheckEndpoints { get; init; } = [];

    /// <summary>
    /// Gets the OpenTelemetry configuration code, if applicable.
    /// </summary>
    public string? OpenTelemetryConfig { get; init; }

    /// <summary>
    /// Gets the confidence score for this observability generation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets warnings about the observability generation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets manual steps that cannot be automated and require developer action.
    /// </summary>
    public IReadOnlyList<string> ManualSteps { get; init; } = [];

    /// <summary>
    /// Gets the source files that were modified with logger replacements.
    /// </summary>
    public IReadOnlyList<string> ModifiedSourceFiles { get; init; } = [];

    /// <summary>
    /// Gets whether OpenTelemetry was included in the generation.
    /// </summary>
    public bool IncludesOpenTelemetry { get; init; }
}

/// <summary>
/// Represents a configuration change needed in appsettings.json or other config files.
/// </summary>
public sealed record ConfigurationChange
{
    /// <summary>
    /// Gets the JSON path or section name for the configuration change.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the value to set at the configuration path.
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Gets a description of what this configuration change does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets whether this configuration change is required for the application to function.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the environment this configuration applies to (null for all environments).
    /// </summary>
    public string? Environment { get; init; }
}
