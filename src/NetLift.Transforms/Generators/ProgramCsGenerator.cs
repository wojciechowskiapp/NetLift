using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Generators;

/// <summary>
/// Generates Program.cs files with WebApplicationBuilder pattern for ASP.NET Core.
/// </summary>
public sealed class ProgramCsGenerator : IProgramCsGenerator
{
    /// <inheritdoc />
    public string Generate(
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options)
    {
        var sb = new StringBuilder();

        // Generate using statements
        GenerateUsings(sb, options);
        sb.AppendLine();

        // Create WebApplicationBuilder
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // Configure configuration sources
        GenerateConfigurationSetup(sb);
        sb.AppendLine();

        // Configure Kestrel if httpRuntime settings exist
        if (systemWeb.HttpRuntime != null)
        {
            GenerateKestrelConfiguration(sb, systemWeb.HttpRuntime);
            sb.AppendLine();
        }

        // Register services
        GenerateServicesConfiguration(sb, appSettings, options);
        sb.AppendLine();

        // Build the app
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();

        // Configure middleware pipeline
        GenerateMiddlewarePipeline(sb, systemWeb, options);
        sb.AppendLine();

        // Run the app
        sb.AppendLine("app.Run();");

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task WriteToFileAsync(
        string outputPath,
        SystemWebSection systemWeb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        var content = Generate(systemWeb, appSettings, options);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
    }

    private static void GenerateUsings(StringBuilder sb, ProgramGenerationOptions options)
    {
        // Only include necessary usings based on what features are enabled
        if (options.IncludeHealthChecks)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Diagnostics.HealthChecks;");
        }
    }

    private static void GenerateConfigurationSetup(StringBuilder sb)
    {
        sb.AppendLine("// Configure configuration sources");
        sb.AppendLine("builder.Configuration");
        sb.AppendLine("    .AddJsonFile(\"appsettings.json\", optional: false, reloadOnChange: true)");
        sb.AppendLine("    .AddJsonFile($\"appsettings.{builder.Environment.EnvironmentName}.json\", optional: true, reloadOnChange: true)");
        sb.AppendLine("    .AddEnvironmentVariables();");
    }

    private static void GenerateKestrelConfiguration(StringBuilder sb, HttpRuntimeSettings httpRuntime)
    {
        sb.AppendLine("// Configure Kestrel server options");
        sb.AppendLine("builder.WebHost.ConfigureKestrel(options =>");
        sb.AppendLine("{");

        if (httpRuntime.MaxRequestLengthKb.HasValue)
        {
            // Convert KB to bytes
            var maxRequestBodySize = httpRuntime.MaxRequestLengthKb.Value * 1024L;
            sb.AppendLine($"    options.Limits.MaxRequestBodySize = {maxRequestBodySize}L; // {httpRuntime.MaxRequestLengthKb}KB from web.config");
        }

        if (httpRuntime.ExecutionTimeoutSeconds.HasValue)
        {
            sb.AppendLine($"    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds({httpRuntime.ExecutionTimeoutSeconds});");
        }

        sb.AppendLine("});");
    }

    private static void GenerateServicesConfiguration(
        StringBuilder sb,
        AppSettingsSection appSettings,
        ProgramGenerationOptions options)
    {
        sb.AppendLine("// Configure services");
        sb.AppendLine("builder.Services.AddControllers();");

        if (options.IncludeSwagger)
        {
            sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
            sb.AppendLine("builder.Services.AddSwaggerGen();");
        }

        if (options.IncludeHealthChecks)
        {
            sb.AppendLine("builder.Services.AddHealthChecks();");
        }

        if (options.IncludeAuthentication)
        {
            sb.AppendLine("builder.Services.AddAuthentication();");
            sb.AppendLine("builder.Services.AddAuthorization();");
        }

        if (options.IncludeSession)
        {
            sb.AppendLine("builder.Services.AddDistributedMemoryCache();");
            sb.AppendLine("builder.Services.AddSession(options =>");
            sb.AppendLine("{");
            sb.AppendLine("    options.IdleTimeout = TimeSpan.FromMinutes(20);");
            sb.AppendLine("    options.Cookie.HttpOnly = true;");
            sb.AppendLine("    options.Cookie.IsEssential = true;");
            sb.AppendLine("});");
        }

        // Generate options binding for hierarchical settings
        GenerateOptionsBinding(sb, appSettings);
    }

    private static void GenerateOptionsBinding(StringBuilder sb, AppSettingsSection appSettings)
    {
        // Build a set of top-level sections from hierarchical keys
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in appSettings.Settings)
        {
            if (setting.KeyPath != null && setting.KeyPath.Length > 1)
            {
                sections.Add(setting.KeyPath[0]);
            }
        }

        if (sections.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// Configure options binding for hierarchical settings");

            foreach (var section in sections.OrderBy(s => s))
            {
                sb.AppendLine($"// builder.Services.Configure<{section}Options>(builder.Configuration.GetSection(\"{section}\"));");
            }
        }
    }

    private static void GenerateMiddlewarePipeline(
        StringBuilder sb,
        SystemWebSection systemWeb,
        ProgramGenerationOptions options)
    {
        sb.AppendLine("// Configure middleware pipeline");

        // Exception handling based on customErrors mode
        GenerateExceptionHandling(sb, systemWeb.CustomErrors);
        sb.AppendLine();

        // Swagger in development
        if (options.IncludeSwagger)
        {
            sb.AppendLine("if (app.Environment.IsDevelopment())");
            sb.AppendLine("{");
            sb.AppendLine("    app.UseSwagger();");
            sb.AppendLine("    app.UseSwaggerUI();");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Standard middleware
        sb.AppendLine("app.UseHttpsRedirection();");
        sb.AppendLine("app.UseStaticFiles();");
        sb.AppendLine("app.UseRouting();");
        sb.AppendLine();

        // Session if enabled
        if (options.IncludeSession)
        {
            sb.AppendLine("app.UseSession();");
        }

        // Authentication/Authorization
        if (options.IncludeAuthentication)
        {
            sb.AppendLine("app.UseAuthentication();");
        }
        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine();

        // Map endpoints
        sb.AppendLine("app.MapControllers();");

        if (options.IncludeHealthChecks)
        {
            sb.AppendLine("app.MapHealthChecks(\"/health\");");
        }
    }

    private static void GenerateExceptionHandling(StringBuilder sb, CustomErrorSettings? customErrors)
    {
        if (customErrors == null || customErrors.Mode == CustomErrorMode.Off)
        {
            // Always show detailed errors
            sb.AppendLine("app.UseDeveloperExceptionPage();");
        }
        else if (customErrors.Mode == CustomErrorMode.On)
        {
            // Always use custom error page
            var errorPath = customErrors.DefaultRedirect ?? "/Error";
            sb.AppendLine($"app.UseExceptionHandler(\"{errorPath}\");");
        }
        else // RemoteOnly
        {
            // Use developer page in development, custom error page in production
            sb.AppendLine("if (app.Environment.IsDevelopment())");
            sb.AppendLine("{");
            sb.AppendLine("    app.UseDeveloperExceptionPage();");
            sb.AppendLine("}");
            sb.AppendLine("else");
            sb.AppendLine("{");
            var errorPath = customErrors.DefaultRedirect ?? "/Error";
            sb.AppendLine($"    app.UseExceptionHandler(\"{errorPath}\");");
            sb.AppendLine("    app.UseHsts();");
            sb.AppendLine("}");
        }
    }
}
