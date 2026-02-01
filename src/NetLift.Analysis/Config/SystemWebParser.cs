using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Analysis.Config;

/// <summary>
/// Parses the system.web section from web.config files to extract ASP.NET configuration.
/// </summary>
public class SystemWebParser : ISystemWebParser
{
    /// <inheritdoc />
    public SystemWebSection Parse(XDocument webConfig)
    {
        if (webConfig?.Root == null)
        {
            return new SystemWebSection();
        }

        var systemWeb = webConfig.Root
            .Elements("system.web")
            .FirstOrDefault();

        if (systemWeb == null)
        {
            return new SystemWebSection();
        }

        return new SystemWebSection
        {
            Compilation = ParseCompilation(systemWeb),
            HttpRuntime = ParseHttpRuntime(systemWeb),
            CustomErrors = ParseCustomErrors(systemWeb)
        };
    }

    private static CompilationSettings? ParseCompilation(XElement systemWeb)
    {
        var compilation = systemWeb.Element("compilation");
        if (compilation == null)
        {
            return null;
        }

        var assemblies = ParseAssemblies(compilation);

        return new CompilationSettings
        {
            Debug = ParseBoolAttribute(compilation, "debug", false),
            TargetFramework = compilation.Attribute("targetFramework")?.Value,
            OptimizeCompilations = ParseBoolAttribute(compilation, "optimizeCompilations", false),
            Assemblies = assemblies
        };
    }

    private static IReadOnlyList<string> ParseAssemblies(XElement compilation)
    {
        var assembliesElement = compilation.Element("assemblies");
        if (assembliesElement == null)
        {
            return Array.Empty<string>();
        }

        return assembliesElement
            .Elements("add")
            .Select(e => e.Attribute("assembly")?.Value)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Cast<string>()
            .ToList();
    }

    private static HttpRuntimeSettings? ParseHttpRuntime(XElement systemWeb)
    {
        var httpRuntime = systemWeb.Element("httpRuntime");
        if (httpRuntime == null)
        {
            return null;
        }

        return new HttpRuntimeSettings
        {
            TargetFramework = httpRuntime.Attribute("targetFramework")?.Value,
            MaxRequestLengthKb = ParseIntAttribute(httpRuntime, "maxRequestLength"),
            ExecutionTimeoutSeconds = ParseIntAttribute(httpRuntime, "executionTimeout"),
            EnableVersionHeader = ParseBoolAttribute(httpRuntime, "enableVersionHeader", true)
        };
    }

    private static CustomErrorSettings? ParseCustomErrors(XElement systemWeb)
    {
        var customErrors = systemWeb.Element("customErrors");
        if (customErrors == null)
        {
            return null;
        }

        var mode = ParseCustomErrorMode(customErrors.Attribute("mode")?.Value);
        var defaultRedirect = customErrors.Attribute("defaultRedirect")?.Value;
        var errorPages = ParseErrorPages(customErrors);

        return new CustomErrorSettings
        {
            Mode = mode,
            DefaultRedirect = defaultRedirect,
            ErrorPages = errorPages
        };
    }

    private static IReadOnlyList<CustomErrorPage> ParseErrorPages(XElement customErrors)
    {
        return customErrors
            .Elements("error")
            .Select(ParseErrorPage)
            .Where(e => e != null)
            .Cast<CustomErrorPage>()
            .ToList();
    }

    private static CustomErrorPage? ParseErrorPage(XElement errorElement)
    {
        var statusCodeAttr = errorElement.Attribute("statusCode")?.Value;
        var redirect = errorElement.Attribute("redirect")?.Value;

        if (string.IsNullOrWhiteSpace(statusCodeAttr) || string.IsNullOrWhiteSpace(redirect))
        {
            return null;
        }

        if (!int.TryParse(statusCodeAttr, out var statusCode))
        {
            return null;
        }

        return new CustomErrorPage
        {
            StatusCode = statusCode,
            Redirect = redirect
        };
    }

    private static CustomErrorMode ParseCustomErrorMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return CustomErrorMode.RemoteOnly;
        }

        return mode.ToLowerInvariant() switch
        {
            "off" => CustomErrorMode.Off,
            "on" => CustomErrorMode.On,
            "remoteonly" => CustomErrorMode.RemoteOnly,
            _ => CustomErrorMode.RemoteOnly
        };
    }

    private static bool ParseBoolAttribute(XElement element, string attributeName, bool defaultValue)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var result) ? result : null;
    }
}
