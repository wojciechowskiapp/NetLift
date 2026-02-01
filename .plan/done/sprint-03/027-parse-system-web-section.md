# [TASK-027] Parse system.web Section

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

- **Depends on:** TASK-005
- **Blocks:** TASK-028

---

## Description

Implement parsing of the `<system.web>` section from web.config files. Extract compilation settings, httpRuntime configuration, and customErrors mode for conversion to ASP.NET Core configuration.

---

## Acceptance Criteria

- [ ] Parse `<compilation>` element (debug, targetFramework, optimizeCompilations)
- [ ] Parse `<httpRuntime>` element (targetFramework, maxRequestLength, executionTimeout)
- [ ] Parse `<customErrors>` element (mode, defaultRedirect, error pages)
- [ ] Extract debug flag for environment-specific configuration
- [ ] Extract targetFramework for migration compatibility checking
- [ ] Handle missing or empty system.web section gracefully
- [ ] Return structured SystemWebSection model
- [ ] Unit tests cover various system.web configurations

---

## Technical Notes

### XML Structure to Parse:

```xml
<!-- web.config -->
<configuration>
  <system.web>
    <compilation debug="true" targetFramework="4.8" optimizeCompilations="true">
      <assemblies>
        <add assembly="System.Web.Abstractions, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
      </assemblies>
    </compilation>
    <httpRuntime targetFramework="4.8" maxRequestLength="32768" executionTimeout="300" enableVersionHeader="false" />
    <customErrors mode="RemoteOnly" defaultRedirect="~/Error">
      <error statusCode="404" redirect="~/NotFound" />
      <error statusCode="500" redirect="~/ServerError" />
    </customErrors>
  </system.web>
</configuration>
```

### Model:

```csharp
namespace NetLift.Analysis.Config;

public sealed record CompilationSettings
{
    public bool Debug { get; init; }
    public string? TargetFramework { get; init; }
    public bool OptimizeCompilations { get; init; }
    public IReadOnlyList<string> Assemblies { get; init; } = [];
}

public sealed record HttpRuntimeSettings
{
    public string? TargetFramework { get; init; }
    public int? MaxRequestLengthKb { get; init; }
    public int? ExecutionTimeoutSeconds { get; init; }
    public bool EnableVersionHeader { get; init; } = true;
}

public sealed record CustomErrorSettings
{
    public CustomErrorMode Mode { get; init; } = CustomErrorMode.RemoteOnly;
    public string? DefaultRedirect { get; init; }
    public IReadOnlyList<CustomErrorPage> ErrorPages { get; init; } = [];
}

public enum CustomErrorMode
{
    Off,
    On,
    RemoteOnly
}

public sealed record CustomErrorPage
{
    public int StatusCode { get; init; }
    public required string Redirect { get; init; }
}

public sealed record SystemWebSection
{
    public CompilationSettings? Compilation { get; init; }
    public HttpRuntimeSettings? HttpRuntime { get; init; }
    public CustomErrorSettings? CustomErrors { get; init; }
}
```

### Parser Implementation:

```csharp
namespace NetLift.Analysis.Config;

public sealed class SystemWebParser
{
    public SystemWebSection Parse(XDocument webConfig)
    {
        var systemWeb = webConfig
            .Descendants("system.web")
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

    private CompilationSettings? ParseCompilation(XElement systemWeb)
    {
        var compilation = systemWeb.Element("compilation");
        if (compilation == null)
        {
            return null;
        }

        var assemblies = compilation
            .Element("assemblies")?
            .Elements("add")
            .Select(e => e.Attribute("assembly")?.Value)
            .Where(a => !string.IsNullOrEmpty(a))
            .Cast<string>()
            .ToList() ?? [];

        return new CompilationSettings
        {
            Debug = bool.TryParse(compilation.Attribute("debug")?.Value, out var debug) && debug,
            TargetFramework = compilation.Attribute("targetFramework")?.Value,
            OptimizeCompilations = bool.TryParse(
                compilation.Attribute("optimizeCompilations")?.Value, out var opt) && opt,
            Assemblies = assemblies
        };
    }

    private HttpRuntimeSettings? ParseHttpRuntime(XElement systemWeb)
    {
        var httpRuntime = systemWeb.Element("httpRuntime");
        if (httpRuntime == null)
        {
            return null;
        }

        return new HttpRuntimeSettings
        {
            TargetFramework = httpRuntime.Attribute("targetFramework")?.Value,
            MaxRequestLengthKb = int.TryParse(
                httpRuntime.Attribute("maxRequestLength")?.Value, out var maxLen) ? maxLen : null,
            ExecutionTimeoutSeconds = int.TryParse(
                httpRuntime.Attribute("executionTimeout")?.Value, out var timeout) ? timeout : null,
            EnableVersionHeader = !bool.TryParse(
                httpRuntime.Attribute("enableVersionHeader")?.Value, out var header) || header
        };
    }

    private CustomErrorSettings? ParseCustomErrors(XElement systemWeb)
    {
        var customErrors = systemWeb.Element("customErrors");
        if (customErrors == null)
        {
            return null;
        }

        var modeStr = customErrors.Attribute("mode")?.Value ?? "RemoteOnly";
        var mode = modeStr switch
        {
            "Off" => CustomErrorMode.Off,
            "On" => CustomErrorMode.On,
            _ => CustomErrorMode.RemoteOnly
        };

        var errorPages = customErrors
            .Elements("error")
            .Select(e => new CustomErrorPage
            {
                StatusCode = int.TryParse(e.Attribute("statusCode")?.Value, out var code) ? code : 0,
                Redirect = e.Attribute("redirect")?.Value ?? string.Empty
            })
            .Where(e => e.StatusCode > 0)
            .ToList();

        return new CustomErrorSettings
        {
            Mode = mode,
            DefaultRedirect = customErrors.Attribute("defaultRedirect")?.Value,
            ErrorPages = errorPages
        };
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class SystemWebParserTests
{
    private readonly SystemWebParser _parser = new();

    [Fact]
    public void Parse_ExtractsCompilationSettings()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <compilation debug="true" targetFramework="4.8" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Compilation.Should().NotBeNull();
        result.Compilation!.Debug.Should().BeTrue();
        result.Compilation.TargetFramework.Should().Be("4.8");
    }

    [Fact]
    public void Parse_ExtractsHttpRuntimeSettings()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <httpRuntime targetFramework="4.8" maxRequestLength="32768" executionTimeout="300" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.HttpRuntime.Should().NotBeNull();
        result.HttpRuntime!.MaxRequestLengthKb.Should().Be(32768);
        result.HttpRuntime.ExecutionTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void Parse_ExtractsCustomErrorsWithPages()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <customErrors mode="On" defaultRedirect="~/Error">
                  <error statusCode="404" redirect="~/NotFound" />
                  <error statusCode="500" redirect="~/ServerError" />
                </customErrors>
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.CustomErrors.Should().NotBeNull();
        result.CustomErrors!.Mode.Should().Be(CustomErrorMode.On);
        result.CustomErrors.DefaultRedirect.Should().Be("~/Error");
        result.CustomErrors.ErrorPages.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ReturnsEmptySection_WhenSystemWebMissing()
    {
        var xml = XDocument.Parse("""
            <configuration>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Compilation.Should().BeNull();
        result.HttpRuntime.Should().BeNull();
        result.CustomErrors.Should().BeNull();
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
