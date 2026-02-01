namespace NetLift.Core.Models.Config;

/// <summary>
/// Represents the authentication mode from system.web/authentication.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// No authentication specified (mode="None").
    /// </summary>
    None,

    /// <summary>
    /// Forms-based authentication (mode="Forms").
    /// </summary>
    Forms,

    /// <summary>
    /// Windows/Integrated authentication (mode="Windows").
    /// </summary>
    Windows,

    /// <summary>
    /// Passport authentication (mode="Passport") - deprecated.
    /// </summary>
    Passport
}

/// <summary>
/// Represents Forms authentication settings from system.web/authentication/forms.
/// </summary>
public sealed record FormsAuthSettings
{
    /// <summary>
    /// Gets the login URL (loginUrl attribute).
    /// </summary>
    public string? LoginUrl { get; init; }

    /// <summary>
    /// Gets the authentication timeout in minutes (timeout attribute).
    /// Default: 30 minutes.
    /// </summary>
    public int TimeoutMinutes { get; init; } = 30;

    /// <summary>
    /// Gets whether sliding expiration is enabled (slidingExpiration attribute).
    /// Default: true.
    /// </summary>
    public bool SlidingExpiration { get; init; } = true;

    /// <summary>
    /// Gets whether SSL is required for the cookie (requireSSL attribute).
    /// Default: false.
    /// </summary>
    public bool RequireSsl { get; init; }

    /// <summary>
    /// Gets the authentication cookie name (name attribute).
    /// Default: ".ASPXAUTH".
    /// </summary>
    public string CookieName { get; init; } = ".ASPXAUTH";

    /// <summary>
    /// Gets the default redirect URL after login (defaultUrl attribute).
    /// </summary>
    public string? DefaultUrl { get; init; }

    /// <summary>
    /// Gets the cookie domain (domain attribute).
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets whether cross-application redirects are enabled (enableCrossAppRedirects attribute).
    /// Default: false.
    /// </summary>
    public bool EnableCrossAppRedirects { get; init; }

    /// <summary>
    /// Gets the cookie path (path attribute).
    /// Default: "/".
    /// </summary>
    public string CookiePath { get; init; } = "/";

    /// <summary>
    /// Gets the protection mode (protection attribute).
    /// Default: "All".
    /// </summary>
    public string Protection { get; init; } = "All";
}

/// <summary>
/// Represents an authorization rule from system.web/authorization.
/// </summary>
public sealed record AuthorizationRule
{
    /// <summary>
    /// Gets whether this is an allow rule (true) or deny rule (false).
    /// </summary>
    public bool IsAllow { get; init; }

    /// <summary>
    /// Gets the comma-separated list of users (users attribute).
    /// Special values: "*" (all users), "?" (anonymous users).
    /// </summary>
    public string? Users { get; init; }

    /// <summary>
    /// Gets the comma-separated list of roles (roles attribute).
    /// </summary>
    public string? Roles { get; init; }

    /// <summary>
    /// Gets the comma-separated list of HTTP verbs (verbs attribute).
    /// </summary>
    public string? Verbs { get; init; }
}

/// <summary>
/// Represents the parsed authentication section from web.config.
/// </summary>
public sealed record AuthenticationSection
{
    /// <summary>
    /// Gets the authentication mode.
    /// Default: None.
    /// </summary>
    public AuthenticationMode Mode { get; init; } = AuthenticationMode.None;

    /// <summary>
    /// Gets the Forms authentication settings, or null if not using Forms auth.
    /// </summary>
    public FormsAuthSettings? FormsSettings { get; init; }

    /// <summary>
    /// Gets the list of authorization rules from system.web/authorization.
    /// </summary>
    public IReadOnlyList<AuthorizationRule> AuthorizationRules { get; init; } = [];
}
