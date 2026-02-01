using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Analysis.Config;

/// <summary>
/// Parses authentication and authorization configuration from web.config files.
/// </summary>
public class AuthenticationParser : IAuthenticationParser
{
    /// <inheritdoc />
    public AuthenticationSection Parse(XDocument webConfig)
    {
        if (webConfig?.Root == null)
        {
            return new AuthenticationSection();
        }

        var systemWeb = webConfig.Root.Element("system.web");
        if (systemWeb == null)
        {
            return new AuthenticationSection();
        }

        var authenticationElement = systemWeb.Element("authentication");
        var authorizationElement = systemWeb.Element("authorization");

        var mode = ParseAuthenticationMode(authenticationElement);
        var formsSettings = mode == AuthenticationMode.Forms
            ? ParseFormsAuthSettings(authenticationElement)
            : null;

        var authorizationRules = ParseAuthorizationRules(authorizationElement);

        return new AuthenticationSection
        {
            Mode = mode,
            FormsSettings = formsSettings,
            AuthorizationRules = authorizationRules
        };
    }

    private static AuthenticationMode ParseAuthenticationMode(XElement? authenticationElement)
    {
        if (authenticationElement == null)
        {
            return AuthenticationMode.None;
        }

        var modeAttr = authenticationElement.Attribute("mode")?.Value;
        if (string.IsNullOrWhiteSpace(modeAttr))
        {
            return AuthenticationMode.None;
        }

        return modeAttr.Trim() switch
        {
            "Forms" => AuthenticationMode.Forms,
            "Windows" => AuthenticationMode.Windows,
            "Passport" => AuthenticationMode.Passport,
            "None" => AuthenticationMode.None,
            _ => AuthenticationMode.None
        };
    }

    private static FormsAuthSettings? ParseFormsAuthSettings(XElement? authenticationElement)
    {
        var formsElement = authenticationElement?.Element("forms");
        if (formsElement == null)
        {
            // Return default settings if Forms mode is specified but no <forms> element
            return new FormsAuthSettings();
        }

        var loginUrl = formsElement.Attribute("loginUrl")?.Value;
        var timeout = ParseInt(formsElement.Attribute("timeout")?.Value, 30);
        var slidingExpiration = ParseBool(formsElement.Attribute("slidingExpiration")?.Value, true);
        var requireSsl = ParseBool(formsElement.Attribute("requireSSL")?.Value, false);
        var cookieName = formsElement.Attribute("name")?.Value ?? ".ASPXAUTH";
        var defaultUrl = formsElement.Attribute("defaultUrl")?.Value;
        var domain = formsElement.Attribute("domain")?.Value;
        var enableCrossAppRedirects = ParseBool(formsElement.Attribute("enableCrossAppRedirects")?.Value, false);
        var cookiePath = formsElement.Attribute("path")?.Value ?? "/";
        var protection = formsElement.Attribute("protection")?.Value ?? "All";

        return new FormsAuthSettings
        {
            LoginUrl = loginUrl,
            TimeoutMinutes = timeout,
            SlidingExpiration = slidingExpiration,
            RequireSsl = requireSsl,
            CookieName = cookieName,
            DefaultUrl = defaultUrl,
            Domain = domain,
            EnableCrossAppRedirects = enableCrossAppRedirects,
            CookiePath = cookiePath,
            Protection = protection
        };
    }

    private static IReadOnlyList<AuthorizationRule> ParseAuthorizationRules(XElement? authorizationElement)
    {
        if (authorizationElement == null)
        {
            return [];
        }

        var rules = new List<AuthorizationRule>();

        foreach (var element in authorizationElement.Elements())
        {
            var isAllow = element.Name.LocalName.Equals("allow", StringComparison.OrdinalIgnoreCase);
            var isDeny = element.Name.LocalName.Equals("deny", StringComparison.OrdinalIgnoreCase);

            if (!isAllow && !isDeny)
            {
                continue;
            }

            var users = element.Attribute("users")?.Value;
            var roles = element.Attribute("roles")?.Value;
            var verbs = element.Attribute("verbs")?.Value;

            rules.Add(new AuthorizationRule
            {
                IsAllow = isAllow,
                Users = users,
                Roles = roles,
                Verbs = verbs
            });
        }

        return rules.AsReadOnly();
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }
}
