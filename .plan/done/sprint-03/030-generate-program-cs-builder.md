# [TASK-030] Generate Program.cs with WebApplicationBuilder

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

- **Depends on:** TASK-028
- **Blocks:** TASK-031, TASK-032

---

## Description

Generate a Program.cs file using the minimal API style with WebApplicationBuilder pattern. Include configuration binding, dependency injection registration, and middleware pipeline setup based on parsed web.config settings.

---

## Acceptance Criteria

- [ ] Generate Program.cs using minimal API / top-level statements
- [ ] Include WebApplicationBuilder pattern for configuration
- [ ] Bind configuration sections to strongly-typed options
- [ ] Register required services in DI container
- [ ] Configure middleware pipeline (CORS, authentication, authorization, etc.)
- [ ] Add Kestrel configuration from httpRuntime settings
- [ ] Include exception handling middleware based on customErrors
- [ ] Generate proper using statements
- [ ] Output clean, formatted C# code
- [ ] Unit tests verify correct code generation

---

## Technical Notes

### Target Program.cs Structure:

```csharp
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Kestrel configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 33554432; // 32MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration binding
builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection("Azure:Storage"));

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Exception handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

### Generator Implementation:

```csharp
namespace NetLift.Generation.Config;

public sealed class ProgramCsGenerator
{
    public string Generate(
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options)
    {
        var sb = new StringBuilder();

        // Using statements
        GenerateUsingStatements(sb, options);

        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // Configuration
        GenerateConfiguration(sb);

        // Kestrel configuration
        GenerateKestrelConfig(sb, systemWeb);

        // Services
        GenerateServices(sb, appSettings, options);

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();

        // Middleware pipeline
        GenerateMiddleware(sb, systemWeb, options);

        // Endpoints
        GenerateEndpoints(sb, options);

        sb.AppendLine();
        sb.AppendLine("app.Run();");

        return sb.ToString();
    }

    private void GenerateUsingStatements(StringBuilder sb, ProgramGenerationOptions options)
    {
        var usings = new HashSet<string>
        {
            "Microsoft.AspNetCore.Diagnostics"
        };

        if (options.IncludeSwagger)
        {
            usings.Add("Microsoft.OpenApi.Models");
        }

        if (options.IncludeAuthentication)
        {
            usings.Add("Microsoft.AspNetCore.Authentication.Cookies");
        }

        foreach (var ns in usings.OrderBy(u => u))
        {
            sb.AppendLine($"using {ns};");
        }
    }

    private void GenerateConfiguration(StringBuilder sb)
    {
        sb.AppendLine("// Configuration");
        sb.AppendLine("builder.Configuration");
        sb.AppendLine("    .AddJsonFile(\"appsettings.json\", optional: false, reloadOnChange: true)");
        sb.AppendLine("    .AddJsonFile($\"appsettings.{builder.Environment.EnvironmentName}.json\", optional: true)");
        sb.AppendLine("    .AddEnvironmentVariables();");
        sb.AppendLine();
    }

    private void GenerateKestrelConfig(StringBuilder sb, SystemWebSection systemWeb)
    {
        var httpRuntime = systemWeb.HttpRuntime;
        if (httpRuntime == null)
        {
            return;
        }

        sb.AppendLine("// Kestrel configuration");
        sb.AppendLine("builder.WebHost.ConfigureKestrel(options =>");
        sb.AppendLine("{");

        if (httpRuntime.MaxRequestLengthKb.HasValue)
        {
            var bytes = httpRuntime.MaxRequestLengthKb.Value * 1024;
            sb.AppendLine($"    options.Limits.MaxRequestBodySize = {bytes}; // {httpRuntime.MaxRequestLengthKb}KB");
        }

        if (httpRuntime.ExecutionTimeoutSeconds.HasValue)
        {
            sb.AppendLine($"    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds({httpRuntime.ExecutionTimeoutSeconds});");
        }

        sb.AppendLine("});");
        sb.AppendLine();
    }

    private void GenerateServices(
        StringBuilder sb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options)
    {
        sb.AppendLine("// Services");
        sb.AppendLine("builder.Services.AddControllers();");

        if (options.IncludeSwagger)
        {
            sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
            sb.AppendLine("builder.Services.AddSwaggerGen();");
        }

        sb.AppendLine();

        // Generate options binding for hierarchical settings
        var hierarchicalKeys = appSettings.Settings
            .Where(s => s.KeyPath != null && s.KeyPath.Length >= 2)
            .Select(s => s.KeyPath![0])
            .Distinct()
            .ToList();

        if (hierarchicalKeys.Count > 0)
        {
            sb.AppendLine("// Configuration binding");
            foreach (var key in hierarchicalKeys)
            {
                var optionsName = $"{key}Options";
                sb.AppendLine($"builder.Services.Configure<{optionsName}>(");
                sb.AppendLine($"    builder.Configuration.GetSection(\"{key}\"));");
            }
            sb.AppendLine();
        }

        sb.AppendLine("// Health checks");
        sb.AppendLine("builder.Services.AddHealthChecks();");
    }

    private void GenerateMiddleware(
        StringBuilder sb,
        SystemWebSection systemWeb,
        ProgramGenerationOptions options)
    {
        // Exception handling
        sb.AppendLine("// Exception handling");
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.UseDeveloperExceptionPage();");
        sb.AppendLine("}");
        sb.AppendLine("else");
        sb.AppendLine("{");

        var defaultRedirect = systemWeb.CustomErrors?.DefaultRedirect ?? "/Error";
        sb.AppendLine($"    app.UseExceptionHandler(\"{defaultRedirect}\");");
        sb.AppendLine("    app.UseHsts();");
        sb.AppendLine("}");
        sb.AppendLine();

        if (options.IncludeSwagger)
        {
            sb.AppendLine("if (app.Environment.IsDevelopment())");
            sb.AppendLine("{");
            sb.AppendLine("    app.UseSwagger();");
            sb.AppendLine("    app.UseSwaggerUI();");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("app.UseHttpsRedirection();");
        sb.AppendLine("app.UseStaticFiles();");
        sb.AppendLine("app.UseRouting();");

        if (options.IncludeAuthentication)
        {
            sb.AppendLine("app.UseAuthentication();");
        }

        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine();
    }

    private void GenerateEndpoints(StringBuilder sb, ProgramGenerationOptions options)
    {
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine("app.MapHealthChecks(\"/health\");");
    }

    public async Task WriteToFileAsync(
        string outputPath,
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        var code = Generate(systemWeb, appSettings, options);
        await File.WriteAllTextAsync(outputPath, code, cancellationToken);
    }
}

public sealed class ProgramGenerationOptions
{
    public bool IncludeSwagger { get; init; } = true;
    public bool IncludeAuthentication { get; init; }
    public bool IncludeSession { get; init; }
    public bool IncludeHealthChecks { get; init; } = true;
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Generation.Config;

public sealed class ProgramCsGeneratorTests
{
    private readonly ProgramCsGenerator _generator = new();

    [Fact]
    public void Generate_IncludesWebApplicationBuilder()
    {
        var code = _generator.Generate(
            new SystemWebSection(),
            new AppSettingsSection(),
            new ProgramGenerationOptions());

        code.Should().Contain("WebApplication.CreateBuilder(args)");
    }

    [Fact]
    public void Generate_IncludesConfigurationSetup()
    {
        var code = _generator.Generate(
            new SystemWebSection(),
            new AppSettingsSection(),
            new ProgramGenerationOptions());

        code.Should().Contain("AddJsonFile(\"appsettings.json\"");
        code.Should().Contain("AddEnvironmentVariables()");
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

        var code = _generator.Generate(
            systemWeb,
            new AppSettingsSection(),
            new ProgramGenerationOptions());

        code.Should().Contain("ConfigureKestrel");
        code.Should().Contain("MaxRequestBodySize = 33554432");
        code.Should().Contain("TimeSpan.FromSeconds(300)");
    }

    [Fact]
    public void Generate_IncludesSwaggerWhenEnabled()
    {
        var options = new ProgramGenerationOptions { IncludeSwagger = true };

        var code = _generator.Generate(
            new SystemWebSection(),
            new AppSettingsSection(),
            options);

        code.Should().Contain("AddSwaggerGen()");
        code.Should().Contain("UseSwagger()");
    }

    [Fact]
    public void Generate_ConfiguresExceptionHandlerFromCustomErrors()
    {
        var systemWeb = new SystemWebSection
        {
            CustomErrors = new CustomErrorSettings
            {
                Mode = CustomErrorMode.On,
                DefaultRedirect = "~/CustomError"
            }
        };

        var code = _generator.Generate(
            systemWeb,
            new AppSettingsSection(),
            new ProgramGenerationOptions());

        code.Should().Contain("UseExceptionHandler(\"~/CustomError\")");
    }

    [Fact]
    public void Generate_GeneratesOptionsBinding()
    {
        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting
                {
                    Key = "Azure:Storage:AccountName",
                    Value = "test",
                    KeyPath = ["Azure", "Storage", "AccountName"]
                }
            ]
        };

        var code = _generator.Generate(
            new SystemWebSection(),
            appSettings,
            new ProgramGenerationOptions());

        code.Should().Contain("Configure<AzureOptions>");
        code.Should().Contain("GetSection(\"Azure\")");
    }

    [Fact]
    public void Generate_IncludesHealthChecks()
    {
        var options = new ProgramGenerationOptions { IncludeHealthChecks = true };

        var code = _generator.Generate(
            new SystemWebSection(),
            new AppSettingsSection(),
            options);

        code.Should().Contain("AddHealthChecks()");
        code.Should().Contain("MapHealthChecks(\"/health\")");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
