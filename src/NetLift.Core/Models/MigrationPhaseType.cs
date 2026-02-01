namespace NetLift.Core.Models;

/// <summary>
/// Represents the different types of migration phases.
/// </summary>
public enum MigrationPhaseType
{
    /// <summary>
    /// Convert .csproj files to SDK-style format.
    /// </summary>
    ProjectFiles,

    /// <summary>
    /// Migrate configuration files (Web.config to appsettings.json).
    /// </summary>
    Configuration,

    /// <summary>
    /// Migrate ASP.NET MVC controllers to ASP.NET Core.
    /// </summary>
    Controllers,

    /// <summary>
    /// Migrate Entity Framework to Entity Framework Core.
    /// </summary>
    EntityFramework,

    /// <summary>
    /// Convert WCF services to gRPC or REST APIs.
    /// </summary>
    Wcf,

    /// <summary>
    /// Migrate validation logic to ASP.NET Core validation.
    /// </summary>
    Validation
}
