# [TASK-029] Generate appsettings.Development.json / Production.json

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 3 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-028
- **Blocks:** (none)

---

## Description

Generate environment-specific appsettings files (appsettings.Development.json, appsettings.Production.json) from web.config transformations (Web.Debug.config, Web.Release.config). Include placeholders for secrets and environment-specific overrides.

---

## Acceptance Criteria

- [ ] Generate appsettings.Development.json from Web.Debug.config transformations
- [ ] Generate appsettings.Production.json from Web.Release.config transformations
- [ ] Include secrets placeholders with comments for sensitive values
- [ ] Apply XDT transformations (SetAttributes, Replace, Insert, Remove)
- [ ] Configure appropriate log levels per environment
- [ ] Include environment-specific Kestrel configuration
- [ ] Generate placeholder format compatible with Azure App Configuration / Key Vault
- [ ] Unit tests verify correct environment-specific generation

---

## Technical Notes

### Target Development JSON:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyDb_Dev;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "DetailedErrors": true
}
```

### Target Production JSON:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "${CONNECTION_STRING_DEFAULT}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443"
      }
    }
  },
  "ApplicationInsights": {
    "ConnectionString": "${APPLICATIONINSIGHTS_CONNECTION_STRING}"
  }
}
```

### Generator Implementation:

```csharp
namespace NetLift.Generation.Config;

public sealed class EnvironmentAppSettingsGenerator
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    public string GenerateDevelopment(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null)
    {
        var root = new Dictionary<string, object>();

        // Apply debug transformations if available
        if (debugTransform != null)
        {
            var transformedConnStrings = ApplyConnectionStringTransforms(
                connectionStrings, debugTransform);
            if (transformedConnStrings.Count > 0)
            {
                root["ConnectionStrings"] = transformedConnStrings;
            }
        }

        // Development-specific logging
        root["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = "Debug",
                ["Microsoft.AspNetCore"] = "Information",
                ["Microsoft.EntityFrameworkCore"] = "Information"
            }
        };

        // Enable detailed errors in development
        root["DetailedErrors"] = true;

        return JsonSerializer.Serialize(root, _jsonOptions);
    }

    public string GenerateProduction(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? releaseTransform = null)
    {
        var root = new Dictionary<string, object>();

        // Generate secrets placeholders for connection strings
        var connStringsWithPlaceholders = new Dictionary<string, string>();
        foreach (var conn in connectionStrings.ConnectionStrings)
        {
            var placeholder = GenerateSecretPlaceholder(
                "CONNECTION_STRING",
                SanitizeForEnvVar(conn.Name));
            connStringsWithPlaceholders[conn.Name] = placeholder;
        }

        if (connStringsWithPlaceholders.Count > 0)
        {
            root["ConnectionStrings"] = connStringsWithPlaceholders;
        }

        // Production-specific logging
        root["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = "Warning",
                ["Microsoft.AspNetCore"] = "Warning"
            }
        };

        // Production Kestrel configuration
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

        // Add Application Insights placeholder
        root["ApplicationInsights"] = new Dictionary<string, string>
        {
            ["ConnectionString"] = GenerateSecretPlaceholder(
                "APPLICATIONINSIGHTS", "CONNECTION_STRING")
        };

        return JsonSerializer.Serialize(root, _jsonOptions);
    }

    private string GenerateSecretPlaceholder(string prefix, string name)
    {
        return $"${{{prefix}_{name}}}";
    }

    private string SanitizeForEnvVar(string name)
    {
        return name.ToUpperInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_");
    }

    private Dictionary<string, string> ApplyConnectionStringTransforms(
        ConnectionStringsSection baseSection,
        XDocument transformConfig)
    {
        var result = new Dictionary<string, string>();

        var transformElement = transformConfig
            .Descendants("connectionStrings")
            .FirstOrDefault();

        if (transformElement == null)
        {
            // Return base connection strings if no transform
            foreach (var conn in baseSection.ConnectionStrings)
            {
                result[conn.Name] = conn.ConnectionString;
            }
            return result;
        }

        // Start with base
        foreach (var conn in baseSection.ConnectionStrings)
        {
            result[conn.Name] = conn.ConnectionString;
        }

        // Apply transforms
        foreach (var add in transformElement.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var connStr = add.Attribute("connectionString")?.Value;
            var transform = add.Attribute(XName.Get("Transform",
                "http://schemas.microsoft.com/XML-Document-Transform"))?.Value;

            if (string.IsNullOrEmpty(name)) continue;

            if (transform is "SetAttributes" or "Replace" && !string.IsNullOrEmpty(connStr))
            {
                result[name] = connStr;
            }
            else if (transform == "Insert" && !string.IsNullOrEmpty(connStr))
            {
                result.TryAdd(name, connStr);
            }
            else if (transform == "Remove")
            {
                result.Remove(name);
            }
        }

        return result;
    }

    public async Task WriteEnvironmentFilesAsync(
        string outputDirectory,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        XDocument? debugTransform = null,
        XDocument? releaseTransform = null,
        CancellationToken cancellationToken = default)
    {
        var devJson = GenerateDevelopment(
            connectionStrings, appSettings, systemWeb, debugTransform);
        var prodJson = GenerateProduction(
            connectionStrings, appSettings, systemWeb, releaseTransform);

        var devPath = Path.Combine(outputDirectory, "appsettings.Development.json");
        var prodPath = Path.Combine(outputDirectory, "appsettings.Production.json");

        await Task.WhenAll(
            File.WriteAllTextAsync(devPath, devJson, cancellationToken),
            File.WriteAllTextAsync(prodPath, prodJson, cancellationToken));
    }
}
```

### Secrets Placeholder Format:

```csharp
public static class SecretsPlaceholderFormat
{
    // Environment variable style: ${ENV_VAR_NAME}
    public const string EnvVarPattern = @"\$\{([A-Z_]+)\}";

    // Azure Key Vault reference style
    public static string ToKeyVaultReference(string vaultName, string secretName)
    {
        return $"@Microsoft.KeyVault(VaultName={vaultName};SecretName={secretName})";
    }

    // Azure App Configuration reference style
    public static string ToAppConfigReference(string key)
    {
        return $"@AppConfiguration(Key={key})";
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Generation.Config;

public sealed class EnvironmentAppSettingsGeneratorTests
{
    private readonly EnvironmentAppSettingsGenerator _generator = new();

    [Fact]
    public void GenerateDevelopment_SetsDebugLogLevel()
    {
        var json = _generator.GenerateDevelopment(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Default")
            .GetString()
            .Should().Be("Debug");
    }

    [Fact]
    public void GenerateDevelopment_EnablesDetailedErrors()
    {
        var json = _generator.GenerateDevelopment(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("DetailedErrors")
            .GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public void GenerateProduction_UsesSecretsPlaceholders()
    {
        var connStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=prod;Database=MyDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var json = _generator.GenerateProduction(
            connStrings,
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            .Should().Be("${CONNECTION_STRING_DEFAULTCONNECTION}");
    }

    [Fact]
    public void GenerateProduction_SetsWarningLogLevel()
    {
        var json = _generator.GenerateProduction(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Default")
            .GetString()
            .Should().Be("Warning");
    }

    [Fact]
    public void GenerateProduction_IncludesKestrelHttpsEndpoint()
    {
        var json = _generator.GenerateProduction(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Kestrel")
            .GetProperty("Endpoints")
            .GetProperty("Https")
            .GetProperty("Url")
            .GetString()
            .Should().Be("https://*:443");
    }

    [Fact]
    public void GenerateDevelopment_AppliesDebugTransforms()
    {
        var connStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=base;Database=MyDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var debugTransform = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb_Dev;"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        var json = _generator.GenerateDevelopment(
            connStrings,
            new AppSettingsSection(),
            new SystemWebSection(),
            debugTransform);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            .Should().Contain("localhost");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
