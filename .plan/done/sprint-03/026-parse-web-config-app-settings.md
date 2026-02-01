# [TASK-026] Parse web.config AppSettings

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | M |
| **Sprint** | 3 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001, TASK-004
- **Blocks:** TASK-028, TASK-029

---

## Description

Implement parsing of the `<appSettings>` section from web.config files. Handle flat key-value pairs and convert nested keys (using colon or dot notation) for hierarchical appsettings.json structure.

---

## Acceptance Criteria

- [ ] Parse `<appSettings>` section from web.config
- [ ] Extract key-value pairs with proper type inference
- [ ] Handle nested keys with colon (`:`) or dot (`.`) notation
- [ ] Support appSettings transformations (Web.Debug.config, Web.Release.config)
- [ ] Detect and warn about encrypted appSettings
- [ ] Support file attribute for external appSettings files
- [ ] Return structured AppSettingsSection model
- [ ] Unit tests cover flat, nested, and external appSettings

---

## Technical Notes

### XML Structure to Parse:

```xml
<!-- web.config -->
<configuration>
  <appSettings file="secrets.config">
    <add key="Environment" value="Production" />
    <add key="Logging:LogLevel:Default" value="Information" />
    <add key="Logging:LogLevel:Microsoft" value="Warning" />
    <add key="Azure:Storage:AccountName" value="mystorageaccount" />
    <add key="Azure:Storage:ContainerName" value="uploads" />
    <add key="Features:EnableNewUI" value="true" />
    <add key="Cache:ExpirationMinutes" value="30" />
    <add key="MaxRetryCount" value="3" />
  </appSettings>
</configuration>
```

### Model:

```csharp
namespace NetLift.Analysis.Config;

public sealed record AppSetting
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public SettingType InferredType { get; init; } = SettingType.String;
    public string[]? KeyPath { get; init; } // For nested keys: ["Azure", "Storage", "AccountName"]
}

public enum SettingType
{
    String,
    Boolean,
    Integer,
    Double,
    Json
}

public sealed record AppSettingsSection
{
    public IReadOnlyList<AppSetting> Settings { get; init; } = [];
    public string? ExternalFile { get; init; }
    public bool IsEncrypted { get; init; }
}
```

### Parser Implementation:

```csharp
namespace NetLift.Analysis.Config;

public sealed class WebConfigAppSettingsParser
{
    private static readonly char[] KeySeparators = [':', '.'];

    public AppSettingsSection Parse(XDocument webConfig)
    {
        var settings = new List<AppSetting>();

        var appSettingsElement = webConfig
            .Descendants("appSettings")
            .FirstOrDefault();

        if (appSettingsElement == null)
        {
            return new AppSettingsSection
            {
                Settings = Array.Empty<AppSetting>()
            };
        }

        var externalFile = appSettingsElement.Attribute("file")?.Value;
        var configProtectionProvider = appSettingsElement
            .Attribute("configProtectionProvider")?.Value;

        var isEncrypted = !string.IsNullOrEmpty(configProtectionProvider);

        foreach (var add in appSettingsElement.Elements("add"))
        {
            var key = add.Attribute("key")?.Value;
            var value = add.Attribute("value")?.Value;

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            settings.Add(new AppSetting
            {
                Key = key,
                Value = value ?? string.Empty,
                InferredType = InferType(value),
                KeyPath = ParseKeyPath(key)
            });
        }

        return new AppSettingsSection
        {
            Settings = settings,
            ExternalFile = externalFile,
            IsEncrypted = isEncrypted
        };
    }

    public AppSettingsSection ParseWithTransforms(
        XDocument webConfig,
        XDocument? transformConfig)
    {
        var baseSection = Parse(webConfig);

        if (transformConfig == null)
        {
            return baseSection;
        }

        return ApplyTransformations(baseSection, transformConfig);
    }

    private AppSettingsSection ApplyTransformations(
        AppSettingsSection baseSection,
        XDocument transformConfig)
    {
        var transformedSettings = baseSection.Settings.ToDictionary(
            s => s.Key,
            s => s
        );

        var transformElement = transformConfig
            .Descendants("appSettings")
            .FirstOrDefault();

        if (transformElement == null)
        {
            return baseSection;
        }

        foreach (var add in transformElement.Elements("add"))
        {
            var key = add.Attribute("key")?.Value;
            var value = add.Attribute("value")?.Value;
            var transform = add.Attribute(XName.Get("Transform",
                "http://schemas.microsoft.com/XML-Document-Transform"))?.Value;

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (transform == "SetAttributes" || transform == "Replace")
            {
                transformedSettings[key] = new AppSetting
                {
                    Key = key,
                    Value = value ?? string.Empty,
                    InferredType = InferType(value),
                    KeyPath = ParseKeyPath(key)
                };
            }
            else if (transform == "Insert")
            {
                transformedSettings.TryAdd(key, new AppSetting
                {
                    Key = key,
                    Value = value ?? string.Empty,
                    InferredType = InferType(value),
                    KeyPath = ParseKeyPath(key)
                });
            }
            else if (transform == "Remove")
            {
                transformedSettings.Remove(key);
            }
        }

        return new AppSettingsSection
        {
            Settings = transformedSettings.Values.ToList(),
            ExternalFile = baseSection.ExternalFile,
            IsEncrypted = baseSection.IsEncrypted
        };
    }

    private static string[]? ParseKeyPath(string key)
    {
        // Check if key contains hierarchy separators
        if (!key.Contains(':') && !key.Contains('.'))
        {
            return null;
        }

        // Prefer colon separator (appsettings.json standard)
        var separator = key.Contains(':') ? ':' : '.';
        return key.Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }

    private static SettingType InferType(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return SettingType.String;
        }

        // Boolean
        if (bool.TryParse(value, out _))
        {
            return SettingType.Boolean;
        }

        // Integer
        if (int.TryParse(value, out _))
        {
            return SettingType.Integer;
        }

        // Double
        if (double.TryParse(value, out _))
        {
            return SettingType.Double;
        }

        // JSON object or array
        if ((value.StartsWith('{') && value.EndsWith('}')) ||
            (value.StartsWith('[') && value.EndsWith(']')))
        {
            return SettingType.Json;
        }

        return SettingType.String;
    }

    public Dictionary<string, object> BuildHierarchy(AppSettingsSection section)
    {
        var hierarchy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in section.Settings)
        {
            if (setting.KeyPath == null || setting.KeyPath.Length == 0)
            {
                // Flat key-value pair
                hierarchy[setting.Key] = ConvertValue(setting.Value, setting.InferredType);
            }
            else
            {
                // Nested key - build hierarchy
                AddToHierarchy(hierarchy, setting.KeyPath, setting.Value, setting.InferredType);
            }
        }

        return hierarchy;
    }

    private void AddToHierarchy(
        Dictionary<string, object> hierarchy,
        string[] keyPath,
        string value,
        SettingType type)
    {
        var current = hierarchy;

        for (int i = 0; i < keyPath.Length - 1; i++)
        {
            var key = keyPath[i];

            if (!current.ContainsKey(key))
            {
                current[key] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            current = (Dictionary<string, object>)current[key];
        }

        var lastKey = keyPath[^1];
        current[lastKey] = ConvertValue(value, type);
    }

    private static object ConvertValue(string value, SettingType type)
    {
        return type switch
        {
            SettingType.Boolean => bool.Parse(value),
            SettingType.Integer => int.Parse(value),
            SettingType.Double => double.Parse(value),
            SettingType.Json => value, // Keep as string, will be parsed during JSON generation
            _ => value
        };
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class WebConfigAppSettingsParserTests
{
    private readonly WebConfigAppSettingsParser _parser = new();

    [Fact]
    public void Parse_ExtractsFlatKeyValuePairs()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <appSettings>
                <add key="Environment" value="Production" />
                <add key="MaxRetryCount" value="3" />
                <add key="EnableFeature" value="true" />
              </appSettings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Settings.Should().HaveCount(3);
        result.Settings[0].Key.Should().Be("Environment");
        result.Settings[1].InferredType.Should().Be(SettingType.Integer);
        result.Settings[2].InferredType.Should().Be(SettingType.Boolean);
    }

    [Fact]
    public void Parse_ParsesNestedKeysWithColon()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <appSettings>
                <add key="Logging:LogLevel:Default" value="Information" />
                <add key="Logging:LogLevel:Microsoft" value="Warning" />
              </appSettings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Settings[0].KeyPath.Should().Equal("Logging", "LogLevel", "Default");
        result.Settings[1].KeyPath.Should().Equal("Logging", "LogLevel", "Microsoft");
    }

    [Fact]
    public void BuildHierarchy_CreatesNestedStructure()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <appSettings>
                <add key="Azure:Storage:AccountName" value="mystorageaccount" />
                <add key="Azure:Storage:ContainerName" value="uploads" />
                <add key="Features:EnableNewUI" value="true" />
              </appSettings>
            </configuration>
            """);

        var section = _parser.Parse(xml);
        var hierarchy = _parser.BuildHierarchy(section);

        hierarchy.Should().ContainKey("Azure");
        var azure = (Dictionary<string, object>)hierarchy["Azure"];
        azure.Should().ContainKey("Storage");

        var storage = (Dictionary<string, object>)azure["Storage"];
        storage["AccountName"].Should().Be("mystorageaccount");
        storage["ContainerName"].Should().Be("uploads");
    }

    [Fact]
    public void Parse_DetectsExternalFile()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <appSettings file="secrets.config">
                <add key="PublicKey" value="value1" />
              </appSettings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.ExternalFile.Should().Be("secrets.config");
    }

    [Theory]
    [InlineData("true", SettingType.Boolean)]
    [InlineData("false", SettingType.Boolean)]
    [InlineData("123", SettingType.Integer)]
    [InlineData("45.67", SettingType.Double)]
    [InlineData("{\"key\":\"value\"}", SettingType.Json)]
    [InlineData("normal string", SettingType.String)]
    public void InferType_DetectsCorrectType(string value, SettingType expected)
    {
        var xml = XDocument.Parse($"""
            <configuration>
              <appSettings>
                <add key="TestKey" value="{value}" />
              </appSettings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Settings[0].InferredType.Should().Be(expected);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
