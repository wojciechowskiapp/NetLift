using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Generators;

/// <summary>
/// Generates authentication and authorization code for ASP.NET Core.
/// </summary>
public class AuthenticationCodeGenerator : IAuthenticationCodeGenerator
{
    /// <inheritdoc />
    public string GenerateServicesCode(AuthenticationSection auth)
    {
        if (auth.Mode == AuthenticationMode.None)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        switch (auth.Mode)
        {
            case AuthenticationMode.Forms:
                GenerateFormsAuthCode(sb, auth.FormsSettings);
                break;

            case AuthenticationMode.Windows:
                GenerateWindowsAuthCode(sb);
                break;

            case AuthenticationMode.Passport:
                GeneratePassportWarning(sb);
                break;
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateAuthorizationPolicies(AuthenticationSection auth)
    {
        if (auth.AuthorizationRules.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("builder.Services.AddAuthorization(options =>");
        sb.AppendLine("{");

        // Check if we need a global fallback policy (e.g., deny users="?")
        var requiresAuthenticatedUser = auth.AuthorizationRules.Any(r =>
            !r.IsAllow && r.Users != null && r.Users.Contains("?"));

        if (requiresAuthenticatedUser)
        {
            sb.AppendLine("    // Global policy requiring authenticated users (from deny users=\"?\")");
            sb.AppendLine("    options.FallbackPolicy = new AuthorizationPolicyBuilder()");
            sb.AppendLine("        .RequireAuthenticatedUser()");
            sb.AppendLine("        .Build();");
            sb.AppendLine();
        }

        // Generate role-based policies
        var rolePolicies = GenerateRolePolicies(auth.AuthorizationRules);
        if (!string.IsNullOrEmpty(rolePolicies))
        {
            sb.AppendLine(rolePolicies);
        }

        sb.AppendLine("});");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateJwtAlternative()
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Alternative: JWT Bearer Authentication");
        sb.AppendLine("// Uncomment and configure if migrating to token-based auth:");
        sb.AppendLine("/*");
        sb.AppendLine("builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddJwtBearer(options =>");
        sb.AppendLine("    {");
        sb.AppendLine("        options.TokenValidationParameters = new TokenValidationParameters");
        sb.AppendLine("        {");
        sb.AppendLine("            ValidateIssuer = true,");
        sb.AppendLine("            ValidateAudience = true,");
        sb.AppendLine("            ValidateLifetime = true,");
        sb.AppendLine("            ValidateIssuerSigningKey = true,");
        sb.AppendLine("            ValidIssuer = builder.Configuration[\"Jwt:Issuer\"],");
        sb.AppendLine("            ValidAudience = builder.Configuration[\"Jwt:Audience\"],");
        sb.AppendLine("            IssuerSigningKey = new SymmetricSecurityKey(");
        sb.AppendLine("                Encoding.UTF8.GetBytes(builder.Configuration[\"Jwt:Key\"] ?? throw new InvalidOperationException(\"JWT Key not configured\")))");
        sb.AppendLine("        };");
        sb.AppendLine("    });");
        sb.AppendLine("*/");

        return sb.ToString();
    }

    private static void GenerateFormsAuthCode(StringBuilder sb, FormsAuthSettings? settings)
    {
        settings ??= new FormsAuthSettings();

        sb.AppendLine("// Cookie Authentication (migrated from Forms Authentication)");
        sb.AppendLine("builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddCookie(options =>");
        sb.AppendLine("    {");

        if (!string.IsNullOrWhiteSpace(settings.LoginUrl))
        {
            sb.AppendLine($"        options.LoginPath = \"{settings.LoginUrl}\";");
        }

        sb.AppendLine($"        options.ExpireTimeSpan = TimeSpan.FromMinutes({settings.TimeoutMinutes});");
        sb.AppendLine($"        options.SlidingExpiration = {settings.SlidingExpiration.ToString().ToLowerInvariant()};");
        sb.AppendLine($"        options.Cookie.Name = \"{settings.CookieName}\";");

        if (settings.RequireSsl)
        {
            sb.AppendLine("        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;");
        }

        if (!string.IsNullOrWhiteSpace(settings.Domain))
        {
            sb.AppendLine($"        options.Cookie.Domain = \"{settings.Domain}\";");
        }

        if (!string.IsNullOrWhiteSpace(settings.CookiePath) && settings.CookiePath != "/")
        {
            sb.AppendLine($"        options.Cookie.Path = \"{settings.CookiePath}\";");
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultUrl))
        {
            sb.AppendLine($"        // Note: DefaultUrl from Forms auth - consider using ReturnUrlParameter");
            sb.AppendLine($"        // Original defaultUrl: \"{settings.DefaultUrl}\"");
        }

        if (settings.EnableCrossAppRedirects)
        {
            sb.AppendLine("        // TODO: Review cross-app redirects - may require custom logic");
            sb.AppendLine("        // enableCrossAppRedirects was true in web.config");
        }

        sb.AppendLine("    });");
    }

    private static void GenerateWindowsAuthCode(StringBuilder sb)
    {
        sb.AppendLine("// Windows Authentication (requires IIS or HTTP.sys)");
        sb.AppendLine("builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddNegotiate();");
        sb.AppendLine();
        sb.AppendLine("// Note: Windows Authentication requires:");
        sb.AppendLine("// 1. Microsoft.AspNetCore.Authentication.Negotiate package");
        sb.AppendLine("// 2. IIS with Windows Auth enabled, or HTTP.sys configuration");
        sb.AppendLine("// 3. Review launchSettings.json for IIS Express settings");
    }

    private static void GeneratePassportWarning(StringBuilder sb)
    {
        sb.AppendLine("// WARNING: Passport authentication is deprecated and not supported in .NET Core");
        sb.AppendLine("// TODO: Migrate to a modern authentication provider:");
        sb.AppendLine("//   - Azure AD / Microsoft Identity Platform");
        sb.AppendLine("//   - IdentityServer / Duende IdentityServer");
        sb.AppendLine("//   - Auth0, Okta, or other identity providers");
        sb.AppendLine("// See: https://learn.microsoft.com/aspnet/core/security/authentication/");
    }

    private static string GenerateRolePolicies(IReadOnlyList<AuthorizationRule> rules)
    {
        var sb = new StringBuilder();
        var policyIndex = 1;

        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.Roles))
            {
                var roles = rule.Roles.Split(',')
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                if (roles.Count > 0)
                {
                    var policyName = roles.Count == 1
                        ? $"{roles[0]}Policy"
                        : $"CustomPolicy{policyIndex++}";

                    sb.AppendLine($"    // Policy for roles: {string.Join(", ", roles)}");
                    sb.AppendLine($"    options.AddPolicy(\"{policyName}\", policy =>");

                    if (rule.IsAllow)
                    {
                        if (roles.Count == 1)
                        {
                            sb.AppendLine($"        policy.RequireRole(\"{roles[0]}\"));");
                        }
                        else
                        {
                            sb.AppendLine($"        policy.RequireRole({string.Join(", ", roles.Select(r => $"\"{r}\""))}));");
                        }
                    }
                    else
                    {
                        sb.AppendLine("        policy.RequireAssertion(context =>");
                        sb.AppendLine($"            !context.User.IsInRole(\"{roles[0]}\")));");
                        sb.AppendLine($"        // Note: Complex deny rules may need custom authorization handlers");
                    }

                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.Verbs))
            {
                sb.AppendLine($"    // TODO: Verb-based authorization ({rule.Verbs}) requires custom authorization handlers");
                sb.AppendLine($"    // Consider using endpoint-level authorization with HTTP method attributes");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
