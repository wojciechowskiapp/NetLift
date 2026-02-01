using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Analysis.Config;

/// <summary>
/// Parses session state configuration from ASP.NET Framework web.config.
/// </summary>
public sealed class SessionStateParser : ISessionStateParser
{
    /// <inheritdoc />
    public SessionStateSettings Parse(XDocument webConfig)
    {
        var sessionStateElement = webConfig.Root?
            .Element("system.web")?
            .Element("sessionState");

        if (sessionStateElement == null)
        {
            return new SessionStateSettings(); // Use defaults
        }

        var mode = ParseMode(sessionStateElement.Attribute("mode")?.Value);
        var timeout = ParseInt(sessionStateElement.Attribute("timeout")?.Value, 20);
        var cookieName = sessionStateElement.Attribute("cookieName")?.Value ?? "ASP.NET_SessionId";
        var cookieless = ParseCookieless(sessionStateElement.Attribute("cookieless")?.Value);
        var regenerateExpiredSessionId = ParseBool(sessionStateElement.Attribute("regenerateExpiredSessionId")?.Value, true);
        var stateConnectionString = sessionStateElement.Attribute("stateConnectionString")?.Value;
        var sqlConnectionStringName = sessionStateElement.Attribute("sqlConnectionStringName")?.Value;
        var customProvider = sessionStateElement.Attribute("customProvider")?.Value;

        // Resolve SQL connection string from connectionStrings section
        string? sqlConnectionString = null;
        if (!string.IsNullOrEmpty(sqlConnectionStringName))
        {
            sqlConnectionString = ResolveConnectionString(webConfig, sqlConnectionStringName);
        }

        return new SessionStateSettings
        {
            Mode = mode,
            TimeoutMinutes = timeout,
            CookieName = cookieName,
            Cookieless = cookieless,
            RegenerateExpiredSessionId = regenerateExpiredSessionId,
            StateConnectionString = stateConnectionString,
            SqlConnectionString = sqlConnectionString,
            CustomProvider = customProvider
        };
    }

    private static SessionStateMode ParseMode(string? modeValue)
    {
        if (string.IsNullOrEmpty(modeValue))
        {
            return SessionStateMode.InProc; // Default
        }

        return modeValue.ToLowerInvariant() switch
        {
            "off" => SessionStateMode.Off,
            "inproc" => SessionStateMode.InProc,
            "stateserver" => SessionStateMode.StateServer,
            "sqlserver" => SessionStateMode.SQLServer,
            "custom" => SessionStateMode.Custom,
            _ => SessionStateMode.InProc
        };
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static bool ParseCookieless(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false; // Default
        }

        // Cookieless can be: true/false or UseCookies/UseUri/AutoDetect/UseDeviceProfile
        return value.ToLowerInvariant() switch
        {
            "true" => true,
            "useuri" => true,
            "autodetect" => true,
            "usedeviceprofile" => true,
            _ => false
        };
    }

    private static string? ResolveConnectionString(XDocument webConfig, string connectionStringName)
    {
        var connectionString = webConfig.Root?
            .Element("connectionStrings")?
            .Elements("add")
            .FirstOrDefault(e => e.Attribute("name")?.Value == connectionStringName)?
            .Attribute("connectionString")?.Value;

        return connectionString;
    }
}
