using System.Text;
using System.Text.Json;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates modern observability code for ASP.NET Core applications.
/// Creates ILogger injection, structured logging, health checks, and OpenTelemetry setup.
/// </summary>
public sealed class ObservabilityGenerator : IObservabilityGenerator
{
    /// <inheritdoc />
    public ObservabilityResult Generate(
        LoggingInfo loggingInfo,
        ProjectInfo projectInfo,
        bool includeOpenTelemetry = false)
    {
        var generatedFiles = new Dictionary<string, string>();
        var packagesToAdd = new List<PackageReference>();
        var configChanges = new List<ConfigurationChange>();
        var warnings = new List<string>();
        var manualSteps = new List<string>();
        var modifiedFiles = new List<string>();
        var healthCheckEndpoints = new List<string>();

        // Add Microsoft.Extensions.Logging package
        packagesToAdd.Add(new PackageReference
        {
            Id = "Microsoft.Extensions.Logging",
            Version = "8.0.0"
        });

        packagesToAdd.Add(new PackageReference
        {
            Id = "Microsoft.Extensions.Logging.Console",
            Version = "8.0.0"
        });

        // Add health checks packages
        packagesToAdd.Add(new PackageReference
        {
            Id = "Microsoft.Extensions.Diagnostics.HealthChecks",
            Version = "8.0.0"
        });

        packagesToAdd.Add(new PackageReference
        {
            Id = "AspNetCore.HealthChecks.UI.Client",
            Version = "8.0.1"
        });

        // Generate logging configuration
        var loggingConfig = GenerateLoggingConfiguration();
        configChanges.Add(new ConfigurationChange
        {
            Path = "Logging",
            Value = JsonSerializer.Deserialize<object>(loggingConfig)!,
            Description = "ASP.NET Core logging configuration",
            IsRequired = true
        });

        // Generate Program.cs changes
        var programCsChanges = GenerateProgramCsLoggingSetup(includeOpenTelemetry);

        // Generate health check endpoint
        var healthCheckCode = GenerateHealthCheckEndpoint(true, false);
        healthCheckEndpoints.Add("/health");
        healthCheckEndpoints.Add("/health/ready");
        healthCheckEndpoints.Add("/health/live");

        // Add OpenTelemetry if requested
        string? openTelemetryConfig = null;
        if (includeOpenTelemetry)
        {
            var serviceName = Path.GetFileNameWithoutExtension(projectInfo.FilePath);
            openTelemetryConfig = GenerateOpenTelemetrySetup(serviceName, true, true);

            packagesToAdd.Add(new PackageReference
            {
                Id = "OpenTelemetry.Extensions.Hosting",
                Version = "1.7.0"
            });

            packagesToAdd.Add(new PackageReference
            {
                Id = "OpenTelemetry.Instrumentation.AspNetCore",
                Version = "1.7.0"
            });

            packagesToAdd.Add(new PackageReference
            {
                Id = "OpenTelemetry.Instrumentation.Http",
                Version = "1.7.0"
            });

            packagesToAdd.Add(new PackageReference
            {
                Id = "OpenTelemetry.Exporter.Console",
                Version = "1.7.0"
            });
        }

        // Add warnings based on detected framework
        warnings.AddRange(GenerateFrameworkWarnings(loggingInfo));

        // Add manual steps
        manualSteps.AddRange(GenerateManualSteps(loggingInfo, includeOpenTelemetry));

        // Calculate confidence
        var confidence = CalculateConfidence(loggingInfo, includeOpenTelemetry);

        return new ObservabilityResult
        {
            GeneratedFiles = generatedFiles,
            PackagesToAdd = packagesToAdd,
            ConfigurationChanges = configChanges,
            ProgramCsChanges = programCsChanges,
            HealthCheckEndpoints = healthCheckEndpoints,
            OpenTelemetryConfig = openTelemetryConfig,
            Confidence = confidence,
            Warnings = warnings,
            ManualSteps = manualSteps,
            ModifiedSourceFiles = modifiedFiles,
            IncludesOpenTelemetry = includeOpenTelemetry
        };
    }

    /// <inheritdoc />
    public string GenerateLoggerField(string className, string fieldName = "_logger")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    private readonly ILogger<{className}> {fieldName};");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(ILogger<{className}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {fieldName} = logger ?? throw new ArgumentNullException(nameof(logger));");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateStructuredLoggingCall(
        string level,
        string message,
        params string[] properties)
    {
        var sb = new StringBuilder();
        sb.Append($"_logger.Log{level}(\"");
        sb.Append(message);

        if (properties.Length > 0)
        {
            sb.Append(" {");
            sb.Append(string.Join("}, {", properties));
            sb.Append("}");
        }

        sb.Append('"');

        if (properties.Length > 0)
        {
            sb.Append(", ");
            sb.Append(string.Join(", ", properties.Select(p => p.ToLowerInvariant())));
        }

        sb.Append(");");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateHealthCheckEndpoint(
        bool includeDatabase = false,
        bool includeCustomChecks = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Health check configuration");
        sb.AppendLine("builder.Services.AddHealthChecks()");

        if (includeDatabase)
        {
            sb.AppendLine("    .AddSqlServer(");
            sb.AppendLine("        connectionString: builder.Configuration.GetConnectionString(\"DefaultConnection\")!,");
            sb.AppendLine("        name: \"database\",");
            sb.AppendLine("        tags: new[] { \"db\", \"sql\" })");
        }

        if (includeCustomChecks)
        {
            sb.AppendLine("    .AddCheck<CustomHealthCheck>(\"custom\", tags: new[] { \"custom\" })");
        }

        sb.AppendLine("    .AddCheck(\"self\", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { \"api\" });");
        sb.AppendLine();
        sb.AppendLine("// Map health check endpoints");
        sb.AppendLine("app.MapHealthChecks(\"/health\", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions");
        sb.AppendLine("{");
        sb.AppendLine("    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("app.MapHealthChecks(\"/health/ready\", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions");
        sb.AppendLine("{");
        sb.AppendLine("    Predicate = check => check.Tags.Contains(\"ready\")");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("app.MapHealthChecks(\"/health/live\", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions");
        sb.AppendLine("{");
        sb.AppendLine("    Predicate = _ => false");
        sb.AppendLine("});");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateOpenTelemetrySetup(
        string serviceName,
        bool includeTracing = true,
        bool includeMetrics = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// OpenTelemetry configuration");
        sb.AppendLine("builder.Services.AddOpenTelemetry()");

        if (includeTracing)
        {
            sb.AppendLine("    .WithTracing(tracing => tracing");
            sb.AppendLine($"        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(\"{serviceName}\"))");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddHttpClientInstrumentation()");
            sb.AppendLine("        .AddConsoleExporter())");
        }

        if (includeMetrics)
        {
            sb.AppendLine("    .WithMetrics(metrics => metrics");
            sb.AppendLine($"        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(\"{serviceName}\"))");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddHttpClientInstrumentation()");
            sb.AppendLine("        .AddConsoleExporter());");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateLoggingConfiguration(string minimumLevel = "Information")
    {
        var config = new
        {
            Logging = new
            {
                LogLevel = new Dictionary<string, string>
                {
                    { "Default", minimumLevel },
                    { "Microsoft.AspNetCore", "Warning" },
                    { "System", "Warning" }
                },
                Console = new
                {
                    FormatterName = "simple",
                    FormatterOptions = new
                    {
                        SingleLine = true,
                        IncludeScopes = true,
                        TimestampFormat = "yyyy-MM-dd HH:mm:ss ",
                        UseUtcTimestamp = true
                    }
                }
            }
        };

        return JsonSerializer.Serialize(config.Logging, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GenerateProgramCsLoggingSetup(bool includeOpenTelemetry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Configure logging");
        sb.AppendLine("builder.Logging.ClearProviders();");
        sb.AppendLine("builder.Logging.AddConsole();");
        sb.AppendLine("builder.Logging.AddDebug();");
        sb.AppendLine("builder.Logging.AddEventSourceLogger();");
        sb.AppendLine();

        if (includeOpenTelemetry)
        {
            sb.AppendLine("// Configure OpenTelemetry");
            sb.AppendLine("builder.Services.AddOpenTelemetry()");
            sb.AppendLine("    .WithTracing(tracing => tracing");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddHttpClientInstrumentation()");
            sb.AppendLine("        .AddConsoleExporter())");
            sb.AppendLine("    .WithMetrics(metrics => metrics");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddHttpClientInstrumentation()");
            sb.AppendLine("        .AddConsoleExporter());");
            sb.AppendLine();
        }

        sb.AppendLine("// Configure health checks");
        sb.AppendLine("builder.Services.AddHealthChecks()");
        sb.AppendLine("    .AddCheck(\"self\", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());");

        return sb.ToString();
    }

    private static List<string> GenerateFrameworkWarnings(LoggingInfo loggingInfo)
    {
        var warnings = new List<string>();

        switch (loggingInfo.Framework)
        {
            case LoggingFramework.Log4Net:
                warnings.Add("log4net configuration needs manual review. Complex appenders may require custom implementation.");
                warnings.Add("File-based logging should be configured using Serilog or NLog sinks in ASP.NET Core.");
                break;

            case LoggingFramework.NLog:
                warnings.Add("NLog can be used with ASP.NET Core via NLog.Web.AspNetCore package.");
                warnings.Add("Review NLog configuration for ASP.NET Core compatibility.");
                break;

            case LoggingFramework.EnterpriseLibrary:
                warnings.Add("Enterprise Library is not supported in .NET Core. Migration to Microsoft.Extensions.Logging required.");
                warnings.Add("Custom logging blocks need manual reimplementation.");
                break;

            case LoggingFramework.Console:
                warnings.Add("Console.WriteLine calls should be replaced with structured logging for better observability.");
                break;

            case LoggingFramework.Custom:
                warnings.Add("Custom logger detected. Manual review and migration required.");
                warnings.Add("Consider implementing a custom ILogger provider if wrapper functionality is needed.");
                break;

            case LoggingFramework.Mixed:
                warnings.Add("Multiple logging frameworks detected. Consolidate to Microsoft.Extensions.Logging.");
                break;
        }

        return warnings;
    }

    private static List<string> GenerateManualSteps(LoggingInfo loggingInfo, bool includeOpenTelemetry)
    {
        var steps = new List<string>();

        steps.Add("1. Review all logger usages and update to use ILogger<T> dependency injection");
        steps.Add("2. Replace logger initialization calls with constructor injection");
        steps.Add("3. Convert log messages to structured logging format with message templates");
        steps.Add("4. Remove legacy logging package references after migration is complete");

        if (loggingInfo.ConfigurationFilePath != null)
        {
            steps.Add($"5. Review logging configuration in {loggingInfo.ConfigurationFilePath} and migrate settings to appsettings.json");
        }

        if (loggingInfo.Framework == LoggingFramework.Log4Net ||
            loggingInfo.Framework == LoggingFramework.NLog)
        {
            steps.Add("6. If file-based logging is required, add Serilog.Sinks.File or NLog.Web.AspNetCore");
        }

        if (includeOpenTelemetry)
        {
            steps.Add("7. Configure OpenTelemetry exporters (OTLP, Jaeger, Zipkin) for production environments");
            steps.Add("8. Set up distributed tracing context propagation across service boundaries");
        }

        steps.Add($"9. Test health check endpoints: /health, /health/ready, /health/live");

        return steps;
    }

    private static int CalculateConfidence(LoggingInfo loggingInfo, bool includeOpenTelemetry)
    {
        var confidence = loggingInfo.Confidence;

        // Boost confidence for well-known frameworks
        if (loggingInfo.Framework == LoggingFramework.Log4Net ||
            loggingInfo.Framework == LoggingFramework.NLog)
        {
            confidence = Math.Min(confidence + 5, 100);
        }

        // Lower confidence for custom loggers
        if (loggingInfo.Framework == LoggingFramework.Custom)
        {
            confidence = Math.Min(confidence, 70);
        }

        // Lower confidence if OpenTelemetry is included (more complex setup)
        if (includeOpenTelemetry)
        {
            confidence = Math.Min(confidence, 90);
        }

        return confidence;
    }
}
