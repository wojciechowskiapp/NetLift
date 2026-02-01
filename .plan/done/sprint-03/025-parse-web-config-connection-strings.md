# [TASK-025] Parse web.config Connection Strings

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

Implement parsing of the `<connectionStrings>` section from web.config files. Extract connection string names, values, and provider names for conversion to appsettings.json format.

---

## Acceptance Criteria

- [ ] Parse `<connectionStrings>` section from web.config
- [ ] Extract connection string name, connectionString, and providerName
- [ ] Handle encrypted connection strings (detect and warn)
- [ ] Support connection string transformations (Web.Debug.config, Web.Release.config)
- [ ] Return structured ConnectionStringInfo model
- [ ] Unit tests cover various connection string formats (SQL Server, LocalDB, Oracle, MySQL)
- [ ] Handle missing or empty connectionStrings section gracefully

---

## Technical Notes

### XML Structure to Parse:

```xml
<!-- web.config -->
<configuration>
  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="Server=localhost;Database=MyDb;Integrated Security=true;"
         providerName="System.Data.SqlClient" />
    <add name="OracleConnection"
         connectionString="Data Source=OracleDB;User Id=myUser;Password=myPass;"
         providerName="Oracle.ManagedDataAccess.Client" />
    <add name="LocalDb"
         connectionString="Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\MyDb.mdf;Integrated Security=True"
         providerName="System.Data.SqlClient" />
  </connectionStrings>
</configuration>
```

### Model:

```csharp
namespace NetLift.Analysis.Config;

public sealed record ConnectionStringInfo
{
    public required string Name { get; init; }
    public required string ConnectionString { get; init; }
    public required string ProviderName { get; init; }
    public bool IsEncrypted { get; init; }
}

public sealed record ConnectionStringsSection
{
    public IReadOnlyList<ConnectionStringInfo> ConnectionStrings { get; init; } = [];
    public bool HasEncryptedStrings { get; init; }
}
```

### Parser Implementation:

```csharp
namespace NetLift.Analysis.Config;

public sealed class WebConfigConnectionStringParser
{
    public ConnectionStringsSection Parse(XDocument webConfig)
    {
        var connectionStrings = new List<ConnectionStringInfo>();
        var hasEncrypted = false;

        var connectionStringsElement = webConfig
            .Descendants("connectionStrings")
            .FirstOrDefault();

        if (connectionStringsElement == null)
        {
            return new ConnectionStringsSection
            {
                ConnectionStrings = Array.Empty<ConnectionStringInfo>()
            };
        }

        // Check if section is encrypted
        var configProtectionProvider = connectionStringsElement
            .Attribute("configProtectionProvider")?.Value;

        if (!string.IsNullOrEmpty(configProtectionProvider))
        {
            hasEncrypted = true;
            // Log warning: encrypted connection strings detected
        }

        foreach (var add in connectionStringsElement.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var connStr = add.Attribute("connectionString")?.Value;
            var provider = add.Attribute("providerName")?.Value
                ?? "System.Data.SqlClient"; // Default for .NET Framework

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(connStr))
            {
                continue;
            }

            connectionStrings.Add(new ConnectionStringInfo
            {
                Name = name,
                ConnectionString = connStr,
                ProviderName = provider,
                IsEncrypted = hasEncrypted
            });
        }

        return new ConnectionStringsSection
        {
            ConnectionStrings = connectionStrings,
            HasEncryptedStrings = hasEncrypted
        };
    }

    public ConnectionStringsSection ParseWithTransforms(
        XDocument webConfig,
        XDocument? transformConfig)
    {
        // First parse base config
        var baseSection = Parse(webConfig);

        if (transformConfig == null)
        {
            return baseSection;
        }

        // Apply XDT transformations
        return ApplyTransformations(baseSection, transformConfig);
    }

    private ConnectionStringsSection ApplyTransformations(
        ConnectionStringsSection baseSection,
        XDocument transformConfig)
    {
        var transformedStrings = baseSection.ConnectionStrings.ToList();

        var transformElement = transformConfig
            .Descendants("connectionStrings")
            .FirstOrDefault();

        if (transformElement == null)
        {
            return baseSection;
        }

        foreach (var add in transformElement.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var transform = add.Attribute(XName.Get("Transform",
                "http://schemas.microsoft.com/XML-Document-Transform"))?.Value;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (transform == "SetAttributes")
            {
                // Replace existing connection string
                var index = transformedStrings.FindIndex(x => x.Name == name);
                if (index >= 0)
                {
                    var connStr = add.Attribute("connectionString")?.Value;
                    var provider = add.Attribute("providerName")?.Value;

                    transformedStrings[index] = transformedStrings[index] with
                    {
                        ConnectionString = connStr ?? transformedStrings[index].ConnectionString,
                        ProviderName = provider ?? transformedStrings[index].ProviderName
                    };
                }
            }
            else if (transform == "Insert")
            {
                var connStr = add.Attribute("connectionString")?.Value;
                var provider = add.Attribute("providerName")?.Value ?? "System.Data.SqlClient";

                if (!string.IsNullOrEmpty(connStr))
                {
                    transformedStrings.Add(new ConnectionStringInfo
                    {
                        Name = name,
                        ConnectionString = connStr,
                        ProviderName = provider
                    });
                }
            }
        }

        return new ConnectionStringsSection
        {
            ConnectionStrings = transformedStrings,
            HasEncryptedStrings = baseSection.HasEncryptedStrings
        };
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class WebConfigConnectionStringParserTests
{
    private readonly WebConfigConnectionStringParser _parser = new();

    [Fact]
    public void Parse_ExtractsMultipleConnectionStrings()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb;"
                     providerName="System.Data.SqlClient" />
                <add name="RedisCache"
                     connectionString="localhost:6379"
                     providerName="StackExchange.Redis" />
              </connectionStrings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.ConnectionStrings.Should().HaveCount(2);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
        result.ConnectionStrings[0].ProviderName.Should().Be("System.Data.SqlClient");
        result.ConnectionStrings[1].Name.Should().Be("RedisCache");
    }

    [Fact]
    public void Parse_DetectsEncryptedConnectionStrings()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings configProtectionProvider="RsaProtectedConfigurationProvider">
                <EncryptedData>
                  <!-- encrypted content -->
                </EncryptedData>
              </connectionStrings>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.HasEncryptedStrings.Should().BeTrue();
    }

    [Fact]
    public void ParseWithTransforms_AppliesSetAttributes()
    {
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=prod-server;Database=Prod;"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ConnectionString
            .Should().Contain("prod-server");
    }
}
```

### Provider Name Mapping:

```csharp
// Map .NET Framework providers to .NET Core/EF Core equivalents
public static class ProviderMapper
{
    public static string MapToModernProvider(string frameworkProvider)
    {
        return frameworkProvider switch
        {
            "System.Data.SqlClient" => "Microsoft.Data.SqlClient",
            "System.Data.OracleClient" => "Oracle.ManagedDataAccess.Client",
            "MySql.Data.MySqlClient" => "MySql.Data.MySqlClient",
            "System.Data.SQLite" => "Microsoft.Data.Sqlite",
            _ => frameworkProvider
        };
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
