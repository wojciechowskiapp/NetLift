namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents the result of modernizing authentication to ASP.NET Core Identity.
/// </summary>
public sealed record AuthModernizationResult
{
    /// <summary>
    /// Gets the source authentication information that was analyzed.
    /// </summary>
    public required AuthenticationInfo SourceInfo { get; init; }

    /// <summary>
    /// Gets the generated ApplicationUser class code (null if default IdentityUser is sufficient).
    /// </summary>
    public string? IdentityUserCode { get; init; }

    /// <summary>
    /// Gets the generated ApplicationDbContext class code.
    /// </summary>
    public string? IdentityDbContextCode { get; init; }

    /// <summary>
    /// Gets the authentication registration code for Program.cs (AddIdentity or AddJwtBearer).
    /// </summary>
    public string? ProgramCsAuthCode { get; init; }

    /// <summary>
    /// Gets the authorization policy configuration code for Program.cs.
    /// </summary>
    public string? ProgramCsAuthorizationCode { get; init; }

    /// <summary>
    /// Gets the JWT configuration code for appsettings.json (null if not needed).
    /// </summary>
    public string? JwtConfigurationCode { get; init; }

    /// <summary>
    /// Gets the list of generated authorization policies.
    /// </summary>
    public IReadOnlyList<PolicyDefinition> GeneratedPolicies { get; init; } = [];

    /// <summary>
    /// Gets the list of required NuGet packages for the modernized authentication.
    /// </summary>
    public IReadOnlyList<string> RequiredPackages { get; init; } = [];

    /// <summary>
    /// Gets the migration guide with manual steps if any.
    /// </summary>
    public string? MigrationGuide { get; init; }

    /// <summary>
    /// Gets the confidence score for the generated code (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets any warnings or notes about the modernization.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Represents a generated authorization policy definition.
/// </summary>
public sealed record PolicyDefinition
{
    /// <summary>
    /// Gets the policy name (e.g., "AdminPolicy", "UserPolicy").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the roles required by this policy.
    /// </summary>
    public required string Roles { get; init; }

    /// <summary>
    /// Gets the description of what this policy authorizes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the original [Authorize] attribute syntax that this policy replaces.
    /// </summary>
    public string? OriginalAttribute { get; init; }

    /// <summary>
    /// Gets the recommended new [Authorize] attribute syntax.
    /// </summary>
    public string? RecommendedAttribute { get; init; }
}
