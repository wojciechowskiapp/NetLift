using System.Xml.Linq;
using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses session state configuration from ASP.NET Framework web.config.
/// </summary>
public interface ISessionStateParser
{
    /// <summary>
    /// Parses the sessionState element from a web.config XML document.
    /// </summary>
    /// <param name="webConfig">The web.config XML document.</param>
    /// <returns>The parsed session state settings.</returns>
    SessionStateSettings Parse(XDocument webConfig);
}

/// <summary>
/// Generates ASP.NET Core session configuration code.
/// </summary>
public interface ISessionCodeGenerator
{
    /// <summary>
    /// Generates service registration code for ConfigureServices/Program.cs.
    /// </summary>
    /// <param name="session">The session state settings.</param>
    /// <returns>C# code to add to service configuration.</returns>
    string GenerateServicesCode(SessionStateSettings session);

    /// <summary>
    /// Generates middleware registration code for Configure/Program.cs.
    /// </summary>
    /// <returns>C# code to add session middleware.</returns>
    string GenerateMiddlewareCode();
}
