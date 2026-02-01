using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Generators;

/// <summary>
/// Generates environment-specific appsettings.json files with appropriate configurations
/// for Development and Production environments.
/// </summary>
public partial class EnvironmentAppSettingsGenerator : IEnvironmentAppSettingsGenerator
{
    private readonly IWebConfigAppSettingsParser _appSettingsParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentAppSettingsGenerator"/> class.
    /// </summary>
    /// <param name="appSettingsParser">The parser for building hierarchical app settings.</param>
    public EnvironmentAppSettingsGenerator(IWebConfigAppSettingsParser appSettingsParser)
    {
        _appSettingsParser = appSettingsParser ?? throw new ArgumentNullException(nameof(appSettingsParser));
    }

    /// <inheritdoc />
    public string GenerateDevelopment(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null)
    {
        var root = new Dictionary<string, object>();

        // Apply debug transform to connection strings if provided
        var transformedConnectionStrings = ApplyConnectionStringTransform(connectionStrings, debugTransform);

        // Add ConnectionStrings section with actual values for development
        if (transformedConnectionStrings.Count > 0)
        {
            var connectionStringsDict = new Dictionary<string, string>();
            foreach (var connString in transformedConnectionStrings)
            {
                connectionStringsDict[connString.Name] = connString.ConnectionString;
            }
            root["ConnectionStrings"] = connectionStringsDict;
        }

        // Build hierarchical app settings
        var hierarchy = _appSettingsParser.BuildHierarchy(appSettings);
        foreach (var kvp in hierarchy)
        {
            root[kvp.Key] = kvp.Value;
        }

        // Add Development logging configuration with Debug level and EF Core logging
        root["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = "Debug",
                ["Microsoft.AspNetCore"] = "Information",
                ["Microsoft.EntityFrameworkCore"] = "Information"
            }
        };

        // Enable detailed errors for development
        root["DetailedErrors"] = true;

        // Add AllowedHosts
        root["AllowedHosts"] = "*";

        return SerializeToJson(root);
    }

    /// <inheritdoc />
    public string GenerateProduction(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? releaseTransform = null)
    {
        var root = new Dictionary<string, object>();

        // Apply release transform to connection strings if provided
        var transformedConnectionStrings = ApplyConnectionStringTransform(connectionStrings, releaseTransform);

        // Add ConnectionStrings section with environment variable placeholders for production
        if (transformedConnectionStrings.Count > 0)
        {
            var connectionStringsDict = new Dictionary<string, string>();
            foreach (var connString in transformedConnectionStrings)
            {
                var envVarName = SanitizeConnectionStringName(connString.Name);
                connectionStringsDict[connString.Name] = $"${{{envVarName}}}";
            }
            root["ConnectionStrings"] = connectionStringsDict;
        }

        // Build hierarchical app settings
        var hierarchy = _appSettingsParser.BuildHierarchy(appSettings);
        foreach (var kvp in hierarchy)
        {
            root[kvp.Key] = kvp.Value;
        }

        // Add Production logging configuration with Warning level
        root["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = "Warning",
                ["Microsoft.AspNetCore"] = "Warning"
            }
        };

        // Add Kestrel HTTPS endpoint configuration
        root["Kestrel"] = new Dictionary<string, object>
        {
            ["Endpoints"] = new Dictionary<string, object>
            {
                ["Https"] = new Dictionary<string, string>
                {
                    ["Url"] = "https://*:443"
                }
            }
        };

        // Add ApplicationInsights placeholder
        root["ApplicationInsights"] = new Dictionary<string, string>
        {
            ["InstrumentationKey"] = "${APPLICATIONINSIGHTS_INSTRUMENTATIONKEY}"
        };

        // Add AllowedHosts
        root["AllowedHosts"] = "*";

        return SerializeToJson(root);
    }

    /// <inheritdoc />
    public async Task WriteEnvironmentFilesAsync(
        string outputDirectory,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null,
        XDocument? releaseTransform = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or whitespace.", nameof(outputDirectory));
        }

        // Ensure directory exists
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Generate Development file
        var developmentJson = GenerateDevelopment(connectionStrings, appSettings, systemWeb, debugTransform);
        var developmentPath = Path.Combine(outputDirectory, "appsettings.Development.json");
        await File.WriteAllTextAsync(developmentPath, developmentJson, cancellationToken);

        // Generate Production file
        var productionJson = GenerateProduction(connectionStrings, appSettings, systemWeb, releaseTransform);
        var productionPath = Path.Combine(outputDirectory, "appsettings.Production.json");
        await File.WriteAllTextAsync(productionPath, productionJson, cancellationToken);
    }

    /// <summary>
    /// Applies XDT transforms to connection strings if transform document is provided.
    /// Supports basic SetAttributes transformations commonly used in web.Debug.config and web.Release.config.
    /// </summary>
    /// <param name="connectionStrings">The original connection strings section.</param>
    /// <param name="transform">Optional XDT transform document.</param>
    /// <returns>The list of connection strings with transforms applied.</returns>
    private static List<ConnectionStringInfo> ApplyConnectionStringTransform(
        ConnectionStringsSection connectionStrings,
        XDocument? transform)
    {
        var result = new List<ConnectionStringInfo>(connectionStrings.ConnectionStrings);

        if (transform == null)
        {
            return result;
        }

        // Find connectionStrings section in transform
        var xdtNamespace = XNamespace.Get("http://schemas.microsoft.com/XML-Document-Transform");
        var connectionStringsElement = transform.Descendants("connectionStrings").FirstOrDefault();

        if (connectionStringsElement == null)
        {
            return result;
        }

        // Process each add element in the transform
        foreach (var addElement in connectionStringsElement.Elements("add"))
        {
            var transformAttr = addElement.Attribute(xdtNamespace + "Transform")?.Value;
            var locatorAttr = addElement.Attribute(xdtNamespace + "Locator")?.Value;
            var name = addElement.Attribute("name")?.Value;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Handle SetAttributes transform (most common)
            if (transformAttr?.Contains("SetAttributes", StringComparison.OrdinalIgnoreCase) == true)
            {
                var existingIndex = result.FindIndex(cs => cs.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    var existing = result[existingIndex];
                    result[existingIndex] = existing with
                    {
                        ConnectionString = addElement.Attribute("connectionString")?.Value ?? existing.ConnectionString,
                        ProviderName = addElement.Attribute("providerName")?.Value ?? existing.ProviderName
                    };
                }
            }
            // Handle Insert transform
            else if (transformAttr?.Contains("Insert", StringComparison.OrdinalIgnoreCase) == true)
            {
                var connectionString = addElement.Attribute("connectionString")?.Value;
                var providerName = addElement.Attribute("providerName")?.Value ?? "System.Data.SqlClient";

                if (!string.IsNullOrEmpty(connectionString))
                {
                    result.Add(new ConnectionStringInfo
                    {
                        Name = name,
                        ConnectionString = connectionString,
                        ProviderName = providerName
                    });
                }
            }
            // Handle Remove transform
            else if (transformAttr?.Contains("Remove", StringComparison.OrdinalIgnoreCase) == true)
            {
                result.RemoveAll(cs => cs.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
        }

        return result;
    }

    /// <summary>
    /// Sanitizes a connection string name to a valid environment variable name.
    /// Converts to uppercase and replaces special characters with underscores.
    /// </summary>
    /// <param name="name">The connection string name to sanitize.</param>
    /// <returns>A sanitized environment variable name with CONNECTION_STRING_ prefix.</returns>
    private static string SanitizeConnectionStringName(string name)
    {
        // Replace any non-alphanumeric character with underscore
        var sanitized = ConnectionStringNameRegex().Replace(name, "_").ToUpperInvariant();
        return $"CONNECTION_STRING_{sanitized}";
    }

    /// <summary>
    /// Serializes a dictionary to formatted JSON.
    /// </summary>
    private static string SerializeToJson(Dictionary<string, object> root)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(root, options);
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex ConnectionStringNameRegex();
}
