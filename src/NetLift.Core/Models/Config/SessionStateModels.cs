namespace NetLift.Core.Models.Config;

/// <summary>
/// Represents the session state mode from ASP.NET Framework web.config.
/// </summary>
public enum SessionStateMode
{
    /// <summary>
    /// Session state is disabled.
    /// </summary>
    Off,

    /// <summary>
    /// Session state is stored in-process in the ASP.NET worker process.
    /// </summary>
    InProc,

    /// <summary>
    /// Session state is stored in a remote state server.
    /// </summary>
    StateServer,

    /// <summary>
    /// Session state is stored in a SQL Server database.
    /// </summary>
    SQLServer,

    /// <summary>
    /// Session state is stored using a custom provider.
    /// </summary>
    Custom
}

/// <summary>
/// Represents session state configuration extracted from ASP.NET Framework web.config.
/// </summary>
public sealed record SessionStateSettings
{
    /// <summary>
    /// Gets the session state mode.
    /// </summary>
    public SessionStateMode Mode { get; init; } = SessionStateMode.InProc;

    /// <summary>
    /// Gets the session timeout in minutes.
    /// </summary>
    public int TimeoutMinutes { get; init; } = 20;

    /// <summary>
    /// Gets the name of the session cookie.
    /// </summary>
    public string CookieName { get; init; } = "ASP.NET_SessionId";

    /// <summary>
    /// Gets a value indicating whether cookieless sessions are enabled.
    /// </summary>
    public bool Cookieless { get; init; }

    /// <summary>
    /// Gets a value indicating whether to regenerate expired session IDs.
    /// </summary>
    public bool RegenerateExpiredSessionId { get; init; } = true;

    /// <summary>
    /// Gets the connection string for StateServer mode (tcpip=server:port).
    /// </summary>
    public string? StateConnectionString { get; init; }

    /// <summary>
    /// Gets the SQL Server connection string name for SQLServer mode.
    /// </summary>
    public string? SqlConnectionString { get; init; }

    /// <summary>
    /// Gets the custom provider type name for Custom mode.
    /// </summary>
    public string? CustomProvider { get; init; }
}
