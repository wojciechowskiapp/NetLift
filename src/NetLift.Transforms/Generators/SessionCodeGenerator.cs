using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Generators;

/// <summary>
/// Generates ASP.NET Core session configuration code from Framework session state settings.
/// </summary>
public sealed class SessionCodeGenerator : ISessionCodeGenerator
{
    /// <inheritdoc />
    public string GenerateServicesCode(SessionStateSettings session)
    {
        if (session.Mode == SessionStateMode.Off)
        {
            return "// Session state disabled in original web.config";
        }

        var sb = new StringBuilder();

        // Generate cache configuration based on mode
        switch (session.Mode)
        {
            case SessionStateMode.InProc:
                GenerateInProcCode(sb, session);
                break;

            case SessionStateMode.StateServer:
                GenerateStateServerCode(sb, session);
                break;

            case SessionStateMode.SQLServer:
                GenerateSqlServerCode(sb, session);
                break;

            case SessionStateMode.Custom:
                GenerateCustomCode(sb, session);
                break;
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateMiddlewareCode()
    {
        return "app.UseSession(); // Must be called after UseRouting() and before UseEndpoints()";
    }

    private void GenerateInProcCode(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// In-memory session state (equivalent to InProc mode)");
        sb.AppendLine("builder.Services.AddDistributedMemoryCache();");
        AppendSessionOptions(sb, session);
    }

    private void GenerateStateServerCode(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// StateServer mode migrated to Redis distributed cache");
        sb.AppendLine("// Install package: dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(session.StateConnectionString))
        {
            sb.AppendLine($"// TODO: Convert StateServer connection '{session.StateConnectionString}' to Redis connection string");
        }
        else
        {
            sb.AppendLine("// TODO: Configure Redis connection string in appsettings.json");
        }

        sb.AppendLine("builder.Services.AddStackExchangeRedisCache(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.Configuration = builder.Configuration.GetConnectionString(\"RedisCache\");");
        sb.AppendLine("    options.InstanceName = \"Session_\";");
        sb.AppendLine("});");
        sb.AppendLine();
        AppendSessionOptions(sb, session);
    }

    private void GenerateSqlServerCode(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// SQL Server distributed cache (equivalent to SQLServer mode)");
        sb.AppendLine("// Install package: dotnet add package Microsoft.Extensions.Caching.SqlServer");
        sb.AppendLine();
        sb.AppendLine("// Setup SQL cache table:");
        sb.AppendLine("// dotnet sql-cache create \"<connection-string>\" dbo SessionCache");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(session.SqlConnectionString))
        {
            sb.AppendLine($"// Original connection string: {session.SqlConnectionString}");
        }
        else
        {
            sb.AppendLine("// TODO: Configure SQL Server connection string in appsettings.json");
        }

        sb.AppendLine("builder.Services.AddDistributedSqlServerCache(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.ConnectionString = builder.Configuration.GetConnectionString(\"SessionCache\");");
        sb.AppendLine("    options.SchemaName = \"dbo\";");
        sb.AppendLine("    options.TableName = \"SessionCache\";");
        sb.AppendLine("});");
        sb.AppendLine();
        AppendSessionOptions(sb, session);
    }

    private void GenerateCustomCode(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("// WARNING: Custom session state provider detected");

        if (!string.IsNullOrEmpty(session.CustomProvider))
        {
            sb.AppendLine($"// Original provider: {session.CustomProvider}");
        }

        sb.AppendLine("// TODO: Migrate custom provider to IDistributedCache implementation");
        sb.AppendLine("// Falling back to in-memory cache for now");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddDistributedMemoryCache();");
        AppendSessionOptions(sb, session);
    }

    private void AppendSessionOptions(StringBuilder sb, SessionStateSettings session)
    {
        sb.AppendLine("builder.Services.AddSession(options =>");
        sb.AppendLine("{");
        sb.AppendLine($"    options.IdleTimeout = TimeSpan.FromMinutes({session.TimeoutMinutes});");

        // Map ASP.NET cookie name to ASP.NET Core cookie name
        var coreCookieName = session.CookieName == "ASP.NET_SessionId"
            ? ".AspNetCore.Session"
            : session.CookieName;

        sb.AppendLine($"    options.Cookie.Name = \"{coreCookieName}\";");
        sb.AppendLine("    options.Cookie.HttpOnly = true; // Security best practice");
        sb.AppendLine("    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only");
        sb.AppendLine("    options.Cookie.SameSite = SameSiteMode.Lax; // CSRF protection");

        if (session.Cookieless)
        {
            sb.AppendLine("    // WARNING: Cookieless sessions not supported in ASP.NET Core");
            sb.AppendLine("    // Consider alternative authentication/state management");
        }

        sb.AppendLine("});");
    }
}
