namespace NetLift.Core.Models.Config;

/// <summary>
/// Represents a single connection string entry from web.config.
/// </summary>
public sealed record ConnectionStringInfo
{
    /// <summary>
    /// Gets the connection string name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the connection string value.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Gets the provider name (e.g., System.Data.SqlClient).
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets a value indicating whether this connection string is encrypted.
    /// </summary>
    public bool IsEncrypted { get; init; }
}

/// <summary>
/// Represents the connectionStrings section from web.config.
/// </summary>
public sealed record ConnectionStringsSection
{
    /// <summary>
    /// Gets the collection of connection strings.
    /// </summary>
    public IReadOnlyList<ConnectionStringInfo> ConnectionStrings { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the section contains encrypted connection strings.
    /// </summary>
    public bool HasEncryptedStrings { get; init; }
}
