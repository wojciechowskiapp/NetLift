using System.Text.Json;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Generators;

/// <summary>
/// Generates appsettings.json content from web.config sections.
/// </summary>
public class AppSettingsJsonGenerator : IAppSettingsJsonGenerator
{
    private readonly IWebConfigAppSettingsParser _appSettingsParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsJsonGenerator"/> class.
    /// </summary>
    /// <param name="appSettingsParser">The parser for building hierarchical app settings.</param>
    public AppSettingsJsonGenerator(IWebConfigAppSettingsParser appSettingsParser)
    {
        _appSettingsParser = appSettingsParser ?? throw new ArgumentNullException(nameof(appSettingsParser));
    }

    /// <inheritdoc />
    public string Generate(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb)
    {
        var root = new Dictionary<string, object>();

        // Add ConnectionStrings section
        if (connectionStrings.ConnectionStrings.Count > 0)
        {
            var connectionStringsDict = new Dictionary<string, string>();
            foreach (var connString in connectionStrings.ConnectionStrings)
            {
                connectionStringsDict[connString.Name] = connString.ConnectionString;
            }
            root["ConnectionStrings"] = connectionStringsDict;
        }

        // Build hierarchical app settings using the parser
        var hierarchy = _appSettingsParser.BuildHierarchy(appSettings);
        foreach (var kvp in hierarchy)
        {
            root[kvp.Key] = kvp.Value;
        }

        // Add Logging section based on compilation debug flag
        var isDebugMode = systemWeb.Compilation?.Debug ?? false;
        root["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = isDebugMode ? "Debug" : "Information",
                ["Microsoft.AspNetCore"] = isDebugMode ? "Debug" : "Warning"
            }
        };

        // Add Kestrel configuration from httpRuntime
        if (systemWeb.HttpRuntime != null)
        {
            var kestrelLimits = new Dictionary<string, object>();

            // Convert maxRequestLength from KB to bytes
            if (systemWeb.HttpRuntime.MaxRequestLengthKb.HasValue)
            {
                // ASP.NET Core uses bytes, web.config uses KB
                var maxRequestBodySizeBytes = systemWeb.HttpRuntime.MaxRequestLengthKb.Value * 1024L;
                kestrelLimits["MaxRequestBodySize"] = maxRequestBodySizeBytes;
            }

            // Convert executionTimeout to RequestHeadersTimeout
            if (systemWeb.HttpRuntime.ExecutionTimeoutSeconds.HasValue)
            {
                // Format as TimeSpan string (e.g., "00:01:50" for 110 seconds)
                var timeout = TimeSpan.FromSeconds(systemWeb.HttpRuntime.ExecutionTimeoutSeconds.Value);
                kestrelLimits["RequestHeadersTimeout"] = timeout.ToString(@"hh\:mm\:ss");
            }

            if (kestrelLimits.Count > 0)
            {
                root["Kestrel"] = new Dictionary<string, object>
                {
                    ["Limits"] = kestrelLimits
                };
            }
        }

        // Add AllowedHosts
        root["AllowedHosts"] = "*";

        // Serialize to JSON with indentation
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(root, options);
    }

    /// <inheritdoc />
    public async Task WriteToFileAsync(
        string outputPath,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or whitespace.", nameof(outputPath));
        }

        var json = Generate(connectionStrings, appSettings, systemWeb);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
}
