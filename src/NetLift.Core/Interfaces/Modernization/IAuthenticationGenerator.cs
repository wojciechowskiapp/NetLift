using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates modern ASP.NET Core Identity authentication code from detected legacy patterns.
/// </summary>
public interface IAuthenticationGenerator
{
    /// <summary>
    /// Generates a complete authentication modernization result with all required components.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns from the legacy codebase.</param>
    /// <returns>Complete modernization result including generated code and configuration.</returns>
    AuthModernizationResult Generate(AuthenticationInfo authInfo);

    /// <summary>
    /// Generates a custom ApplicationUser class if custom claims or properties are needed.
    /// Returns null if the default IdentityUser is sufficient.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns.</param>
    /// <returns>C# code for ApplicationUser class, or null if not needed.</returns>
    string? GenerateIdentityUser(AuthenticationInfo authInfo);

    /// <summary>
    /// Generates an ApplicationDbContext for Identity.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns.</param>
    /// <param name="dbContextNamespace">Namespace for the DbContext.</param>
    /// <returns>C# code for ApplicationDbContext class.</returns>
    string GenerateIdentityDbContext(AuthenticationInfo authInfo, string dbContextNamespace);

    /// <summary>
    /// Generates authentication service registration code for Program.cs.
    /// Chooses between Cookie authentication, JWT Bearer, or both based on project type.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns.</param>
    /// <returns>C# code for authentication service registration.</returns>
    string GenerateProgramCsAuth(AuthenticationInfo authInfo);

    /// <summary>
    /// Generates authorization policy configuration code for Program.cs.
    /// Converts role-based [Authorize] attributes to policy-based authorization.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns.</param>
    /// <returns>C# code for authorization policy configuration.</returns>
    string GenerateAuthorizationPolicies(AuthenticationInfo authInfo);

    /// <summary>
    /// Generates JWT configuration section for appsettings.json.
    /// Only generates if the project requires JWT authentication.
    /// </summary>
    /// <param name="authInfo">Detected authentication patterns.</param>
    /// <returns>JSON configuration for JWT, or null if not needed.</returns>
    string? GenerateJwtConfiguration(AuthenticationInfo authInfo);
}
