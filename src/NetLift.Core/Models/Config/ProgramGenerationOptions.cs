namespace NetLift.Core.Models.Config;

/// <summary>
/// Options for controlling the generation of Program.cs.
/// </summary>
public sealed class ProgramGenerationOptions
{
    /// <summary>
    /// Gets a value indicating whether to include Swagger/OpenAPI configuration.
    /// Defaults to true.
    /// </summary>
    public bool IncludeSwagger { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to include authentication middleware.
    /// Defaults to false.
    /// </summary>
    public bool IncludeAuthentication { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include session middleware.
    /// Defaults to false.
    /// </summary>
    public bool IncludeSession { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include health checks.
    /// Defaults to true.
    /// </summary>
    public bool IncludeHealthChecks { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether this is an MVC app with Razor views.
    /// If true, uses AddControllersWithViews() instead of AddControllers().
    /// Defaults to false (API-style).
    /// </summary>
    public bool IsMvcWithViews { get; init; }

    /// <summary>
    /// Gets the DbContext type name to register, if any.
    /// When set, generates DbContext registration with connection string.
    /// </summary>
    public string? DbContextName { get; init; }

    /// <summary>
    /// Gets the connection string name to use for DbContext registration.
    /// Defaults to "DefaultConnection".
    /// </summary>
    public string ConnectionStringName { get; init; } = "DefaultConnection";
}
