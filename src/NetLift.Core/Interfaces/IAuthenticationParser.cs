using System.Xml.Linq;
using NetLift.Core.Models.Config;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Parses authentication and authorization sections from web.config.
/// </summary>
public interface IAuthenticationParser
{
    /// <summary>
    /// Parses the authentication and authorization sections from a web.config document.
    /// </summary>
    /// <param name="webConfig">The web.config XDocument to parse.</param>
    /// <returns>An <see cref="AuthenticationSection"/> containing parsed authentication settings.</returns>
    AuthenticationSection Parse(XDocument webConfig);
}
