namespace NetLift.Core.Models.Config;

/// <summary>
/// Represents compilation settings from the system.web/compilation element.
/// </summary>
public sealed record CompilationSettings
{
    /// <summary>
    /// Gets whether debug mode is enabled (debug="true").
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Gets the target framework version (targetFramework="4.8").
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets whether optimizeCompilations is enabled.
    /// </summary>
    public bool OptimizeCompilations { get; init; }

    /// <summary>
    /// Gets the list of referenced assemblies from the assemblies collection.
    /// </summary>
    public IReadOnlyList<string> Assemblies { get; init; } = [];
}

/// <summary>
/// Represents HTTP runtime settings from the system.web/httpRuntime element.
/// </summary>
public sealed record HttpRuntimeSettings
{
    /// <summary>
    /// Gets the target framework version (targetFramework="4.8").
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets the maximum request length in kilobytes (maxRequestLength).
    /// </summary>
    public int? MaxRequestLengthKb { get; init; }

    /// <summary>
    /// Gets the execution timeout in seconds (executionTimeout).
    /// </summary>
    public int? ExecutionTimeoutSeconds { get; init; }

    /// <summary>
    /// Gets whether the version header is enabled (enableVersionHeader).
    /// Defaults to true as per ASP.NET defaults.
    /// </summary>
    public bool EnableVersionHeader { get; init; } = true;
}

/// <summary>
/// Represents custom error settings from the system.web/customErrors element.
/// </summary>
public sealed record CustomErrorSettings
{
    /// <summary>
    /// Gets the custom error mode (off, on, remoteOnly).
    /// </summary>
    public CustomErrorMode Mode { get; init; } = CustomErrorMode.RemoteOnly;

    /// <summary>
    /// Gets the default redirect page (defaultRedirect attribute).
    /// </summary>
    public string? DefaultRedirect { get; init; }

    /// <summary>
    /// Gets the list of custom error pages for specific status codes.
    /// </summary>
    public IReadOnlyList<CustomErrorPage> ErrorPages { get; init; } = [];
}

/// <summary>
/// Represents the custom error mode values.
/// </summary>
public enum CustomErrorMode
{
    /// <summary>
    /// Custom errors are disabled (mode="Off").
    /// </summary>
    Off,

    /// <summary>
    /// Custom errors are enabled for all requests (mode="On").
    /// </summary>
    On,

    /// <summary>
    /// Custom errors are shown only for remote requests (mode="RemoteOnly").
    /// </summary>
    RemoteOnly
}

/// <summary>
/// Represents a custom error page mapping for a specific HTTP status code.
/// </summary>
public sealed record CustomErrorPage
{
    /// <summary>
    /// Gets the HTTP status code (statusCode attribute).
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the redirect URL for this status code (redirect attribute).
    /// </summary>
    public required string Redirect { get; init; }
}

/// <summary>
/// Represents the parsed system.web section from web.config.
/// </summary>
public sealed record SystemWebSection
{
    /// <summary>
    /// Gets the compilation settings, or null if not present.
    /// </summary>
    public CompilationSettings? Compilation { get; init; }

    /// <summary>
    /// Gets the HTTP runtime settings, or null if not present.
    /// </summary>
    public HttpRuntimeSettings? HttpRuntime { get; init; }

    /// <summary>
    /// Gets the custom error settings, or null if not present.
    /// </summary>
    public CustomErrorSettings? CustomErrors { get; init; }
}
