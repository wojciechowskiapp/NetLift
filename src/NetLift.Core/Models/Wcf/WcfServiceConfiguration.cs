namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents the complete WCF service configuration from system.serviceModel section.
/// </summary>
public sealed record WcfServiceConfiguration
{
    /// <summary>
    /// Gets the list of WCF service definitions.
    /// </summary>
    public IReadOnlyList<WcfServiceDefinition> Services { get; init; } = [];

    /// <summary>
    /// Gets the list of binding configurations.
    /// </summary>
    public IReadOnlyList<WcfBindingConfig> Bindings { get; init; } = [];

    /// <summary>
    /// Gets the list of behavior configurations.
    /// </summary>
    public IReadOnlyList<WcfBehaviorConfig> Behaviors { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether multiple site bindings are enabled in serviceHostingEnvironment.
    /// </summary>
    public bool MultipleSiteBindingsEnabled { get; init; }
}

/// <summary>
/// Represents a WCF service definition.
/// </summary>
public sealed record WcfServiceDefinition
{
    /// <summary>
    /// Gets the fully qualified service type name.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Gets the behavior configuration name reference.
    /// </summary>
    public string? BehaviorConfiguration { get; init; }

    /// <summary>
    /// Gets the list of service endpoints.
    /// </summary>
    public IReadOnlyList<WcfEndpoint> Endpoints { get; init; } = [];
}

/// <summary>
/// Represents a WCF service endpoint.
/// </summary>
public sealed record WcfEndpoint
{
    /// <summary>
    /// Gets the endpoint address (relative or absolute).
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets the binding type (e.g., basicHttpBinding, wsHttpBinding).
    /// </summary>
    public required string Binding { get; init; }

    /// <summary>
    /// Gets the binding configuration name reference.
    /// </summary>
    public string? BindingConfiguration { get; init; }

    /// <summary>
    /// Gets the fully qualified contract interface name.
    /// </summary>
    public required string Contract { get; init; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is a metadata exchange (MEX) endpoint.
    /// </summary>
    public bool IsMetadataExchange { get; init; }
}

/// <summary>
/// Represents a WCF binding configuration.
/// </summary>
public sealed record WcfBindingConfig
{
    /// <summary>
    /// Gets the binding type (e.g., basicHttpBinding, wsHttpBinding, netTcpBinding).
    /// </summary>
    public required string BindingType { get; init; }

    /// <summary>
    /// Gets the binding configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the maximum message size in bytes. Default is 65536.
    /// </summary>
    public long MaxReceivedMessageSize { get; init; } = 65536;

    /// <summary>
    /// Gets the maximum buffer size in bytes. Default is 65536.
    /// </summary>
    public long MaxBufferSize { get; init; } = 65536;

    /// <summary>
    /// Gets the receive timeout. Default is 10 minutes.
    /// </summary>
    public TimeSpan ReceiveTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the send timeout. Default is 1 minute.
    /// </summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the security mode.
    /// </summary>
    public WcfSecurityMode SecurityMode { get; init; }

    /// <summary>
    /// Gets the client credential type (e.g., None, Windows, UserName, Certificate).
    /// </summary>
    public string? ClientCredentialType { get; init; }
}

/// <summary>
/// Represents a WCF service behavior configuration.
/// </summary>
public sealed record WcfBehaviorConfig
{
    /// <summary>
    /// Gets the behavior configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether HTTP GET metadata is enabled.
    /// </summary>
    public bool MetadataHttpGetEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether HTTPS GET metadata is enabled.
    /// </summary>
    public bool MetadataHttpsGetEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether exception details are included in faults.
    /// </summary>
    public bool IncludeExceptionDetailInFaults { get; init; }

    /// <summary>
    /// Gets the maximum number of concurrent calls. Null if not specified.
    /// </summary>
    public int? MaxConcurrentCalls { get; init; }

    /// <summary>
    /// Gets the maximum number of concurrent instances. Null if not specified.
    /// </summary>
    public int? MaxConcurrentInstances { get; init; }

    /// <summary>
    /// Gets the maximum number of concurrent sessions. Null if not specified.
    /// </summary>
    public int? MaxConcurrentSessions { get; init; }
}

/// <summary>
/// Represents WCF security modes.
/// </summary>
public enum WcfSecurityMode
{
    /// <summary>
    /// No security.
    /// </summary>
    None,

    /// <summary>
    /// Transport-level security (HTTPS, etc.).
    /// </summary>
    Transport,

    /// <summary>
    /// Message-level security.
    /// </summary>
    Message,

    /// <summary>
    /// Transport security with message credentials.
    /// </summary>
    TransportWithMessageCredential
}
