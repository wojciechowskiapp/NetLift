using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates authentication and authorization code for ASP.NET Core.
/// </summary>
public interface IAuthenticationCodeGenerator
{
    /// <summary>
    /// Generates authentication services registration code for Program.cs.
    /// </summary>
    /// <param name="auth">The authentication section containing configuration.</param>
    /// <returns>C# code string for authentication services registration.</returns>
    string GenerateServicesCode(AuthenticationSection auth);

    /// <summary>
    /// Generates authorization policy definitions for Program.cs.
    /// </summary>
    /// <param name="auth">The authentication section containing authorization rules.</param>
    /// <returns>C# code string for authorization policy configuration.</returns>
    string GenerateAuthorizationPolicies(AuthenticationSection auth);

    /// <summary>
    /// Generates JWT Bearer authentication alternative scaffolding code.
    /// </summary>
    /// <returns>C# code string with JWT Bearer configuration template.</returns>
    string GenerateJwtAlternative();
}
