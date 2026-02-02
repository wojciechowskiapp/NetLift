namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents detected authentication patterns in a .NET Framework project.
/// </summary>
public sealed record AuthenticationInfo
{
    /// <summary>
    /// Gets the project path being analyzed.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets the list of roles detected in [Authorize] attributes and User.IsInRole calls.
    /// </summary>
    public IReadOnlyList<RoleUsage> RolesDetected { get; init; } = [];

    /// <summary>
    /// Gets the list of custom claims detected in the codebase.
    /// </summary>
    public IReadOnlyList<CustomClaimUsage> CustomClaims { get; init; } = [];

    /// <summary>
    /// Gets the list of Membership API method calls detected.
    /// </summary>
    public IReadOnlyList<MembershipUsage> MembershipCalls { get; init; } = [];

    /// <summary>
    /// Gets the list of FormsAuthentication method calls detected.
    /// </summary>
    public IReadOnlyList<FormsAuthUsage> FormsAuthCalls { get; init; } = [];

    /// <summary>
    /// Gets whether the project has custom IIdentity or IPrincipal implementations.
    /// </summary>
    public bool HasCustomIdentity { get; init; }

    /// <summary>
    /// Gets whether the project contains API controllers requiring JWT authentication.
    /// </summary>
    public bool RequiresJwt { get; init; }

    /// <summary>
    /// Gets the custom identity class name if one was detected.
    /// </summary>
    public string? CustomIdentityClassName { get; init; }

    /// <summary>
    /// Gets the custom principal class name if one was detected.
    /// </summary>
    public string? CustomPrincipalClassName { get; init; }

    /// <summary>
    /// Gets the confidence score for the analysis (0-100).
    /// Higher confidence indicates clearer, more standard authentication patterns.
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Represents a detected role usage in the codebase.
/// </summary>
public sealed record RoleUsage
{
    /// <summary>
    /// Gets the role name (e.g., "Admin", "User", "Manager").
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the location where this role was found (file path and line number).
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the type of role usage.
    /// </summary>
    public RoleUsageType Type { get; init; }

    /// <summary>
    /// Gets the controller or class name where this role is used.
    /// </summary>
    public string? ClassName { get; init; }

    /// <summary>
    /// Gets the method or action name where this role is used.
    /// </summary>
    public string? MethodName { get; init; }
}

/// <summary>
/// Types of role usage detection.
/// </summary>
public enum RoleUsageType
{
    /// <summary>
    /// Role found in [Authorize(Roles = "...")] attribute.
    /// </summary>
    AuthorizeAttribute,

    /// <summary>
    /// Role found in User.IsInRole("...") method call.
    /// </summary>
    IsInRoleCall,

    /// <summary>
    /// Role found in Roles.IsUserInRole("...") method call.
    /// </summary>
    RolesApiCall,

    /// <summary>
    /// Role found in custom authorization logic.
    /// </summary>
    Custom
}

/// <summary>
/// Represents a detected custom claim usage.
/// </summary>
public sealed record CustomClaimUsage
{
    /// <summary>
    /// Gets the claim name or property name.
    /// </summary>
    public required string ClaimName { get; init; }

    /// <summary>
    /// Gets the claim type if specified (e.g., ClaimTypes.Email).
    /// </summary>
    public string? ClaimType { get; init; }

    /// <summary>
    /// Gets the location where this claim was found.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the data type of the claim value.
    /// </summary>
    public string? DataType { get; init; }
}

/// <summary>
/// Represents a detected Membership API usage.
/// </summary>
public sealed record MembershipUsage
{
    /// <summary>
    /// Gets the Membership method called (e.g., "GetUser", "CreateUser", "ValidateUser").
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the location where this call was found.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the class containing this call.
    /// </summary>
    public string? ClassName { get; init; }
}

/// <summary>
/// Represents a detected FormsAuthentication usage.
/// </summary>
public sealed record FormsAuthUsage
{
    /// <summary>
    /// Gets the FormsAuthentication method called (e.g., "SetAuthCookie", "SignOut", "RedirectFromLoginPage").
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the location where this call was found.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the class containing this call.
    /// </summary>
    public string? ClassName { get; init; }
}
