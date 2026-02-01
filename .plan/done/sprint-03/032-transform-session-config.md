# [TASK-032] Transform Session State Configuration

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | M |
| **Sprint** | 3 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-030
- **Blocks:** (none)

---

## Description

Transform ASP.NET Framework session state configuration to ASP.NET Core distributed session patterns. Generate AddDistributedMemoryCache(), AddSession(), and UseSession() middleware based on the source sessionState configuration.

---

## Acceptance Criteria

- [ ] Parse `<sessionState>` element from system.web (mode, timeout, cookieless, etc.)
- [ ] Transform InProc mode to AddDistributedMemoryCache() + AddSession()
- [ ] Transform StateServer mode to AddStackExchangeRedisCache() scaffolding
- [ ] Transform SQLServer mode to AddDistributedSqlServerCache() scaffolding
- [ ] Generate session cookie configuration (name, httpOnly, secure, sameSite)
- [ ] Generate UseSession() middleware call in correct pipeline position
- [ ] Handle custom session providers (detect and warn)
- [ ] Generate session timeout configuration
- [ ] Unit tests cover InProc, StateServer, and SQLServer modes

---

## Technical Notes

### Source XML Structure:

```xml
<configuration>
  <system.web>
    <sessionState mode="InProc"
                  timeout="20"
                  cookieless="UseCookies"
                  cookieName="ASP.NET_SessionId"
                  regenerateExpiredSessionId="true">
    </sessionState>

    <!-- StateServer example -->
    <sessionState mode="StateServer"
                  stateConnectionString="tcpip=127.0.0.1:42424"
                  timeout="20" />

    <!-- SQLServer example -->
    <sessionState mode="SQLServer"
                  sqlConnectionString="data source=localhost;Integrated Security=SSPI"
                  timeout="20" />
  </system.web>
</configuration>
```

### Model:

```csharp
namespace NetLift.Analysis.Config;

public enum SessionStateMode
{
    Off,
    InProc,
    StateServer,
    SQLServer,
    Custom
}

public sealed record SessionStateSettings
{
    public SessionStateMode Mode { get; init; } = SessionStateMode.InProc;
    public int TimeoutMinutes { get; init; } = 20;
    public string CookieName { get; init; } = "ASP.NET_SessionId";
    public bool Cookieless { get; init; }
    public bool RegenerateExpiredSessionId { get; init; }

    // StateServer settings
    public string? StateConnectionString { get; init; }

    // SQLServer settings
    public string? SqlConnectionString { get; init; }

    // Custom provider
    public string? CustomProvider { get; init; }
}
```

### Parser Implementation:

```csharp
namespace NetLift.Analysis.Config;

public sealed class SessionStateParser
{
    public SessionStateSettings Parse(XDocument webConfig)
    {
        var sessionState = webConfig
            .Descendants("sessionState")
            .FirstOrDefault();

        if (sessionState == null)
        {
            return new SessionStateSettings();
        }

        var modeStr = sessionState.Attribute("mode")?.Value ?? "InProc";
        var mode = modeStr switch
        {
            "Off" => SessionStateMode.Off,
            "InProc" => SessionStateMode.InProc,
            "StateServer" => SessionStateMode.StateServer,
            "SQLServer" => SessionStateMode.SQLServer,
            "Custom" => SessionStateMode.Custom,
            _ => SessionStateMode.InProc
        };

        return new SessionStateSettings
        {
            Mode = mode,
            TimeoutMinutes = int.TryParse(
                sessionState.Attribute("timeout")?.Value, out var t) ? t : 20,
            CookieName = sessionState.Attribute("cookieName")?.Value ?? "ASP.NET_SessionId",
            Cookieless = sessionState.Attribute("cookieless")?.Value == "true",
            RegenerateExpiredSessionId = bool.TryParse(
                sessionState.Attribute("regenerateExpiredSessionId")?.Value, out var r) && r,
            StateConnectionString = sessionState.Attribute("stateConnectionString")?.Value,
            SqlConnectionString = sessionState.Attribute("sqlConnectionString")?.Value,
            CustomProvider = sessionState.Attribute("customProvider")?.Value
        };
    }
}
```

### Code Generator:

```csharp
namespace NetLift.Generation.Config;

public sealed class SessionCodeGenerator
{
    public string GenerateServicesCode(SessionStateSettings session)
    {
        var sb = new StringBuilder();

        if (session.Mode == SessionStateMode.Off)
        {
            sb.AppendLine("// Session state was disabled in web.config");
            return sb.ToString();
        }

        sb.AppendLine("// Session (migrated from sessionState)");

        switch (session.Mode)
        {
            case SessionStateMode.InProc:
                GenerateInProcSession(sb, session);
                break;
            case SessionStateMode.StateServer:
                GenerateRedisSession(sb, session);
                break;
            case SessionStateMode.SQLServer:
                GenerateSqlServerSession(sb, session);
                break;
            case SessionStateMode.Custom:
                GenerateCustomSessionWarning(sb, session);
                break;
        }

        return sb.ToString();
    }

    private void GenerateInProcSession(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("builder.Services.AddDistributedMemoryCache();");
        sb.AppendLine("builder.Services.AddSession(options =>");
        sb.AppendLine("{");
        sb.AppendLine($"    options.IdleTimeout = TimeSpan.FromMinutes({session.TimeoutMinutes});");
        sb.AppendLine($"    options.Cookie.Name = \"{MapCookieName(session.CookieName)}\";");
        sb.AppendLine("    options.Cookie.HttpOnly = true;");
        sb.AppendLine("    options.Cookie.IsEssential = true;");
        sb.AppendLine("    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;");
        sb.AppendLine("    options.Cookie.SameSite = SameSiteMode.Strict;");
        sb.AppendLine("});");
    }

    private void GenerateRedisSession(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// StateServer mode -> Migrated to Redis distributed cache");
        sb.AppendLine("// Install: dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis");
        sb.AppendLine("builder.Services.AddStackExchangeRedisCache(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.Configuration = builder.Configuration.GetConnectionString(\"Redis\");");
        sb.AppendLine("    options.InstanceName = \"Session:\";");
        sb.AppendLine("});");
        sb.AppendLine();
        GenerateSessionOptions(sb, session);
    }

    private void GenerateSqlServerSession(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// SQLServer mode -> Migrated to SQL Server distributed cache");
        sb.AppendLine("// Install: dotnet add package Microsoft.Extensions.Caching.SqlServer");
        sb.AppendLine("// Run: dotnet sql-cache create <connection-string> dbo SessionCache");
        sb.AppendLine("builder.Services.AddDistributedSqlServerCache(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.ConnectionString = builder.Configuration.GetConnectionString(\"SessionDb\");");
        sb.AppendLine("    options.SchemaName = \"dbo\";");
        sb.AppendLine("    options.TableName = \"SessionCache\";");
        sb.AppendLine("});");
        sb.AppendLine();
        GenerateSessionOptions(sb, session);
    }

    private void GenerateSessionOptions(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("builder.Services.AddSession(options =>");
        sb.AppendLine("{");
        sb.AppendLine($"    options.IdleTimeout = TimeSpan.FromMinutes({session.TimeoutMinutes});");
        sb.AppendLine($"    options.Cookie.Name = \"{MapCookieName(session.CookieName)}\";");
        sb.AppendLine("    options.Cookie.HttpOnly = true;");
        sb.AppendLine("    options.Cookie.IsEssential = true;");
        sb.AppendLine("    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;");
        sb.AppendLine("    options.Cookie.SameSite = SameSiteMode.Strict;");
        sb.AppendLine("});");
    }

    private void GenerateCustomSessionWarning(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine($"// WARNING: Custom session provider '{session.CustomProvider}' detected");
        sb.AppendLine("// Manual migration required - implement IDistributedCache or use built-in providers");
        sb.AppendLine("builder.Services.AddDistributedMemoryCache(); // Placeholder");
        GenerateSessionOptions(sb, session);
    }

    public string GenerateMiddlewareCode()
    {
        return "app.UseSession();";
    }

    private string MapCookieName(string aspNetCookieName)
    {
        // Map ASP.NET Framework cookie name to ASP.NET Core style
        return aspNetCookieName switch
        {
            "ASP.NET_SessionId" => ".AspNetCore.Session",
            _ => aspNetCookieName.StartsWith(".") ? aspNetCookieName : $".{aspNetCookieName}"
        };
    }
}
```

### Middleware Pipeline Position:

```csharp
// Session middleware must be placed after routing and before endpoints
// Correct order in Program.cs:
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession(); // <-- Session goes here
app.MapControllers();
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class SessionStateParserTests
{
    private readonly SessionStateParser _parser = new();

    [Fact]
    public void Parse_ExtractsInProcSession()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <sessionState mode="InProc" timeout="30" cookieName="MySession" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(SessionStateMode.InProc);
        result.TimeoutMinutes.Should().Be(30);
        result.CookieName.Should().Be("MySession");
    }

    [Fact]
    public void Parse_ExtractsStateServerSession()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <sessionState mode="StateServer"
                              stateConnectionString="tcpip=127.0.0.1:42424"
                              timeout="20" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(SessionStateMode.StateServer);
        result.StateConnectionString.Should().Be("tcpip=127.0.0.1:42424");
    }

    [Fact]
    public void Parse_ExtractsSqlServerSession()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <sessionState mode="SQLServer"
                              sqlConnectionString="Server=.;Database=Session;Integrated Security=true"
                              timeout="20" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(SessionStateMode.SQLServer);
        result.SqlConnectionString.Should().Contain("Session");
    }

    [Fact]
    public void Parse_ReturnsDefaults_WhenSectionMissing()
    {
        var xml = XDocument.Parse("<configuration></configuration>");

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(SessionStateMode.InProc);
        result.TimeoutMinutes.Should().Be(20);
    }
}

public sealed class SessionCodeGeneratorTests
{
    private readonly SessionCodeGenerator _generator = new();

    [Fact]
    public void GenerateServicesCode_GeneratesDistributedMemoryCache()
    {
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc,
            TimeoutMinutes = 30
        };

        var code = _generator.GenerateServicesCode(session);

        code.Should().Contain("AddDistributedMemoryCache()");
        code.Should().Contain("AddSession");
        code.Should().Contain("TimeSpan.FromMinutes(30)");
    }

    [Fact]
    public void GenerateServicesCode_GeneratesRedisCache_ForStateServer()
    {
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.StateServer
        };

        var code = _generator.GenerateServicesCode(session);

        code.Should().Contain("AddStackExchangeRedisCache");
        code.Should().Contain("GetConnectionString(\"Redis\")");
    }

    [Fact]
    public void GenerateServicesCode_GeneratesSqlServerCache()
    {
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.SQLServer
        };

        var code = _generator.GenerateServicesCode(session);

        code.Should().Contain("AddDistributedSqlServerCache");
        code.Should().Contain("SessionCache");
    }

    [Fact]
    public void GenerateServicesCode_GeneratesWarning_ForCustomProvider()
    {
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.Custom,
            CustomProvider = "MyCustomProvider"
        };

        var code = _generator.GenerateServicesCode(session);

        code.Should().Contain("WARNING");
        code.Should().Contain("MyCustomProvider");
    }

    [Fact]
    public void GenerateMiddlewareCode_ReturnsUseSession()
    {
        var code = _generator.GenerateMiddlewareCode();

        code.Should().Be("app.UseSession();");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
