# [TASK-028] Generate appsettings.json from web.config

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 3 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-025, TASK-026, TASK-027
- **Blocks:** TASK-029, TASK-030

---

## Description

Generate a valid appsettings.json file from parsed web.config sections. Combine connection strings, app settings, and system.web configuration into the standard ASP.NET Core configuration format with proper Kestrel and logging configuration.

---

## Acceptance Criteria

- [ ] Generate valid JSON structure matching appsettings.json schema
- [ ] Include ConnectionStrings section from parsed connection strings
- [ ] Build hierarchical structure from parsed app settings
- [ ] Configure Kestrel settings based on httpRuntime (maxRequestLength, timeouts)
- [ ] Configure Logging section based on compilation debug flag
- [ ] Generate AllowedHosts configuration
- [ ] Handle type inference for boolean, integer, and string values
- [ ] Output properly formatted JSON with indentation
- [ ] Unit tests verify correct JSON generation

---

## Technical Notes

### Target JSON Structure:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyDb;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 33554432,
      "RequestHeadersTimeout": "00:05:00"
    }
  },
  "Azure": {
    "Storage": {
      "AccountName": "mystorageaccount",
      "ContainerName": "uploads"
    }
  },
  "Features": {
    "EnableNewUI": true
  }
}
```

### Generator Implementation:

```csharp
namespace NetLift.Generation.Config;

public sealed class AppSettingsJsonGenerator
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // Preserve casing
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Generate(
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb)
    {
        var root = new Dictionary<string, object>();

        // Add ConnectionStrings
        if (connectionStrings.ConnectionStrings.Count > 0)
        {
            var connStrings = new Dictionary<string, string>();
            foreach (var conn in connectionStrings.ConnectionStrings)
            {
                connStrings[conn.Name] = conn.ConnectionString;
            }
            root["ConnectionStrings"] = connStrings;
        }

        // Add Logging configuration
        root["Logging"] = BuildLoggingConfig(systemWeb);

        // Add AllowedHosts
        root["AllowedHosts"] = "*";

        // Add Kestrel configuration from httpRuntime
        var kestrelConfig = BuildKestrelConfig(systemWeb);
        if (kestrelConfig != null)
        {
            root["Kestrel"] = kestrelConfig;
        }

        // Add hierarchical app settings
        var appSettingsParser = new WebConfigAppSettingsParser();
        var hierarchy = appSettingsParser.BuildHierarchy(appSettings);
        foreach (var kvp in hierarchy)
        {
            root[kvp.Key] = kvp.Value;
        }

        return JsonSerializer.Serialize(root, _jsonOptions);
    }

    private Dictionary<string, object> BuildLoggingConfig(SystemWebSection systemWeb)
    {
        var isDebug = systemWeb.Compilation?.Debug ?? false;

        return new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, string>
            {
                ["Default"] = isDebug ? "Debug" : "Information",
                ["Microsoft.AspNetCore"] = "Warning"
            }
        };
    }

    private Dictionary<string, object>? BuildKestrelConfig(SystemWebSection systemWeb)
    {
        var httpRuntime = systemWeb.HttpRuntime;
        if (httpRuntime == null)
        {
            return null;
        }

        var limits = new Dictionary<string, object>();

        // Convert maxRequestLength (KB) to MaxRequestBodySize (bytes)
        if (httpRuntime.MaxRequestLengthKb.HasValue)
        {
            limits["MaxRequestBodySize"] = httpRuntime.MaxRequestLengthKb.Value * 1024;
        }

        // Convert executionTimeout to RequestHeadersTimeout
        if (httpRuntime.ExecutionTimeoutSeconds.HasValue)
        {
            var timeout = TimeSpan.FromSeconds(httpRuntime.ExecutionTimeoutSeconds.Value);
            limits["RequestHeadersTimeout"] = timeout.ToString(@"hh\:mm\:ss");
        }

        if (limits.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            ["Limits"] = limits
        };
    }

    public async Task WriteToFileAsync(
        string outputPath,
        ConnectionStringsSection connectionStrings,
        AppSettingsSection appSettings,
        SystemWebSection systemWeb,
        CancellationToken cancellationToken = default)
    {
        var json = Generate(connectionStrings, appSettings, systemWeb);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
}
```

### Connection String Provider Mapping:

```csharp
public sealed class ConnectionStringTransformer
{
    public string TransformConnectionString(ConnectionStringInfo connInfo)
    {
        var connectionString = connInfo.ConnectionString;

        // Handle LocalDB path transformation
        if (connectionString.Contains("|DataDirectory|"))
        {
            // Replace with relative path for ASP.NET Core
            connectionString = connectionString.Replace(
                "|DataDirectory|",
                "./App_Data/");
        }

        // Handle AttachDbFilename for LocalDB
        if (connectionString.Contains("AttachDbFilename"))
        {
            // Consider using a proper connection string for Docker/production
            // This is a migration warning
        }

        return connectionString;
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Generation.Config;

public sealed class AppSettingsJsonGeneratorTests
{
    private readonly AppSettingsJsonGenerator _generator = new();

    [Fact]
    public void Generate_CreatesValidJsonWithConnectionStrings()
    {
        var connStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=localhost;Database=MyDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var json = _generator.Generate(
            connStrings,
            new AppSettingsSection(),
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            .Should().Contain("Server=localhost");
    }

    [Fact]
    public void Generate_ConfiguresLoggingFromDebugFlag()
    {
        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings { Debug = true }
        };

        var json = _generator.Generate(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            systemWeb);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Default")
            .GetString()
            .Should().Be("Debug");
    }

    [Fact]
    public void Generate_ConfiguresKestrelFromHttpRuntime()
    {
        var systemWeb = new SystemWebSection
        {
            HttpRuntime = new HttpRuntimeSettings
            {
                MaxRequestLengthKb = 32768,
                ExecutionTimeoutSeconds = 300
            }
        };

        var json = _generator.Generate(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            systemWeb);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Kestrel")
            .GetProperty("Limits")
            .GetProperty("MaxRequestBodySize")
            .GetInt64()
            .Should().Be(32768 * 1024);
    }

    [Fact]
    public void Generate_BuildsHierarchicalAppSettings()
    {
        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting
                {
                    Key = "Azure:Storage:AccountName",
                    Value = "mystorageaccount",
                    KeyPath = ["Azure", "Storage", "AccountName"]
                }
            ]
        };

        var json = _generator.Generate(
            new ConnectionStringsSection(),
            appSettings,
            new SystemWebSection());

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Azure")
            .GetProperty("Storage")
            .GetProperty("AccountName")
            .GetString()
            .Should().Be("mystorageaccount");
    }

    [Fact]
    public void Generate_OutputsFormattedJson()
    {
        var json = _generator.Generate(
            new ConnectionStringsSection(),
            new AppSettingsSection(),
            new SystemWebSection());

        // Verify indentation
        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
