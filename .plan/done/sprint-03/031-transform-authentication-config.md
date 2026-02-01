# [TASK-031] Transform Authentication Configuration

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | L |
| **Sprint** | 3 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-030
- **Blocks:** (none)

---

## Description

Transform ASP.NET Framework Forms Authentication and Windows Authentication configurations to ASP.NET Core authentication patterns. Generate appropriate .AddAuthentication() and .AddCookie() or .AddJwtBearer() scaffolding based on the source authentication mode.

---

## Acceptance Criteria

- [ ] Parse `<authentication>` element from system.web (mode: Forms, Windows, None)
- [ ] Parse `<forms>` element (loginUrl, timeout, cookieless, requireSSL, etc.)
- [ ] Transform Forms auth to .AddAuthentication().AddCookie() pattern
- [ ] Transform Windows auth to .AddAuthentication().AddNegotiate() pattern
- [ ] Generate cookie configuration from forms element attributes
- [ ] Include sliding expiration and persistent cookie options
- [ ] Generate scaffolding for JWT Bearer as alternative
- [ ] Handle authorization rules from `<authorization>` element
- [ ] Unit tests cover Forms, Windows, and None authentication modes

---

## Technical Notes

### Source XML Structure:

```xml
<configuration>
  <system.web>
    <authentication mode="Forms">
      <forms loginUrl="~/Account/Login"
             timeout="30"
             slidingExpiration="true"
             requireSSL="true"
             cookieless="UseCookies"
             name=".ASPXAUTH"
             protection="All" />
    </authentication>
    <authorization>
      <deny users="?" />
      <allow users="*" />
    </authorization>
  </system.web>
</configuration>
```

### Model:

```csharp
namespace NetLift.Analysis.Config;

public enum AuthenticationMode
{
    None,
    Forms,
    Windows,
    Passport // Legacy, rarely used
}

public sealed record FormsAuthSettings
{
    public string? LoginUrl { get; init; }
    public int TimeoutMinutes { get; init; } = 30;
    public bool SlidingExpiration { get; init; } = true;
    public bool RequireSsl { get; init; }
    public string CookieName { get; init; } = ".ASPXAUTH";
    public string? DefaultUrl { get; init; }
    public string? Domain { get; init; }
    public bool EnableCrossAppRedirects { get; init; }
}

public sealed record AuthorizationRule
{
    public bool IsAllow { get; init; }
    public string? Users { get; init; }
    public string? Roles { get; init; }
    public string? Verbs { get; init; }
}

public sealed record AuthenticationSection
{
    public AuthenticationMode Mode { get; init; } = AuthenticationMode.None;
    public FormsAuthSettings? FormsSettings { get; init; }
    public IReadOnlyList<AuthorizationRule> AuthorizationRules { get; init; } = [];
}
```

### Parser Implementation:

```csharp
namespace NetLift.Analysis.Config;

public sealed class AuthenticationParser
{
    public AuthenticationSection Parse(XDocument webConfig)
    {
        var systemWeb = webConfig.Descendants("system.web").FirstOrDefault();
        if (systemWeb == null)
        {
            return new AuthenticationSection();
        }

        var authentication = systemWeb.Element("authentication");
        var authorization = systemWeb.Element("authorization");

        var modeStr = authentication?.Attribute("mode")?.Value ?? "None";
        var mode = modeStr switch
        {
            "Forms" => AuthenticationMode.Forms,
            "Windows" => AuthenticationMode.Windows,
            "Passport" => AuthenticationMode.Passport,
            _ => AuthenticationMode.None
        };

        return new AuthenticationSection
        {
            Mode = mode,
            FormsSettings = mode == AuthenticationMode.Forms
                ? ParseFormsSettings(authentication!)
                : null,
            AuthorizationRules = ParseAuthorizationRules(authorization)
        };
    }

    private FormsAuthSettings ParseFormsSettings(XElement authentication)
    {
        var forms = authentication.Element("forms");
        if (forms == null)
        {
            return new FormsAuthSettings();
        }

        return new FormsAuthSettings
        {
            LoginUrl = forms.Attribute("loginUrl")?.Value,
            TimeoutMinutes = int.TryParse(forms.Attribute("timeout")?.Value, out var t) ? t : 30,
            SlidingExpiration = bool.TryParse(forms.Attribute("slidingExpiration")?.Value, out var s) && s,
            RequireSsl = bool.TryParse(forms.Attribute("requireSSL")?.Value, out var ssl) && ssl,
            CookieName = forms.Attribute("name")?.Value ?? ".ASPXAUTH",
            DefaultUrl = forms.Attribute("defaultUrl")?.Value,
            Domain = forms.Attribute("domain")?.Value,
            EnableCrossAppRedirects = bool.TryParse(forms.Attribute("enableCrossAppRedirects")?.Value, out var cross) && cross
        };
    }

    private List<AuthorizationRule> ParseAuthorizationRules(XElement? authorization)
    {
        if (authorization == null)
        {
            return [];
        }

        var rules = new List<AuthorizationRule>();

        foreach (var element in authorization.Elements())
        {
            var isAllow = element.Name.LocalName == "allow";
            rules.Add(new AuthorizationRule
            {
                IsAllow = isAllow,
                Users = element.Attribute("users")?.Value,
                Roles = element.Attribute("roles")?.Value,
                Verbs = element.Attribute("verbs")?.Value
            });
        }

        return rules;
    }
}
```

### Code Generator:

```csharp
namespace NetLift.Generation.Config;

public sealed class AuthenticationCodeGenerator
{
    public string GenerateServicesCode(AuthenticationSection auth)
    {
        var sb = new StringBuilder();

        switch (auth.Mode)
        {
            case AuthenticationMode.Forms:
                GenerateFormsAuth(sb, auth.FormsSettings!);
                break;
            case AuthenticationMode.Windows:
                GenerateWindowsAuth(sb);
                break;
            case AuthenticationMode.None:
            default:
                sb.AppendLine("// No authentication configured in source web.config");
                break;
        }

        return sb.ToString();
    }

    private void GenerateFormsAuth(StringBuilder sb, FormsAuthSettings forms)
    {
        sb.AppendLine("// Authentication (migrated from Forms auth)");
        sb.AppendLine("builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddCookie(options =>");
        sb.AppendLine("    {");

        if (!string.IsNullOrEmpty(forms.LoginUrl))
        {
            sb.AppendLine($"        options.LoginPath = \"{forms.LoginUrl}\";");
        }

        sb.AppendLine($"        options.ExpireTimeSpan = TimeSpan.FromMinutes({forms.TimeoutMinutes});");
        sb.AppendLine($"        options.SlidingExpiration = {forms.SlidingExpiration.ToString().ToLower()};");
        sb.AppendLine($"        options.Cookie.Name = \"{forms.CookieName}\";");

        if (forms.RequireSsl)
        {
            sb.AppendLine("        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;");
        }

        sb.AppendLine("        options.Cookie.HttpOnly = true;");
        sb.AppendLine("        options.Cookie.SameSite = SameSiteMode.Strict;");
        sb.AppendLine("    });");
    }

    private void GenerateWindowsAuth(StringBuilder sb)
    {
        sb.AppendLine("// Authentication (migrated from Windows auth)");
        sb.AppendLine("builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddNegotiate();");
        sb.AppendLine();
        sb.AppendLine("// Note: Windows Authentication requires IIS or HTTP.sys hosting");
        sb.AppendLine("// For Kestrel, consider using NTLM via Negotiate or switching to JWT");
    }

    public string GenerateJwtAlternative()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Alternative: JWT Bearer Authentication");
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
        sb.AppendLine("                Encoding.UTF8.GetBytes(builder.Configuration[\"Jwt:Key\"]!))");
        sb.AppendLine("        };");
        sb.AppendLine("    });");
        return sb.ToString();
    }

    public string GenerateAuthorizationPolicies(AuthenticationSection auth)
    {
        var sb = new StringBuilder();

        var denyAnonymous = auth.AuthorizationRules
            .Any(r => !r.IsAllow && r.Users == "?");

        if (denyAnonymous)
        {
            sb.AppendLine("// Authorization (migrated from web.config rules)");
            sb.AppendLine("builder.Services.AddAuthorization(options =>");
            sb.AppendLine("{");
            sb.AppendLine("    options.FallbackPolicy = new AuthorizationPolicyBuilder()");
            sb.AppendLine("        .RequireAuthenticatedUser()");
            sb.AppendLine("        .Build();");
            sb.AppendLine("});");
        }

        return sb.ToString();
    }
}
```

### Unit Tests:

```csharp
namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class AuthenticationParserTests
{
    private readonly AuthenticationParser _parser = new();

    [Fact]
    public void Parse_ExtractsFormsAuthSettings()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <authentication mode="Forms">
                  <forms loginUrl="~/Login" timeout="60" requireSSL="true" />
                </authentication>
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(AuthenticationMode.Forms);
        result.FormsSettings.Should().NotBeNull();
        result.FormsSettings!.LoginUrl.Should().Be("~/Login");
        result.FormsSettings.TimeoutMinutes.Should().Be(60);
        result.FormsSettings.RequireSsl.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExtractsWindowsAuth()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <authentication mode="Windows" />
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.Mode.Should().Be(AuthenticationMode.Windows);
    }

    [Fact]
    public void Parse_ExtractsAuthorizationRules()
    {
        var xml = XDocument.Parse("""
            <configuration>
              <system.web>
                <authentication mode="Forms" />
                <authorization>
                  <deny users="?" />
                  <allow roles="Admin" />
                </authorization>
              </system.web>
            </configuration>
            """);

        var result = _parser.Parse(xml);

        result.AuthorizationRules.Should().HaveCount(2);
        result.AuthorizationRules[0].IsAllow.Should().BeFalse();
        result.AuthorizationRules[0].Users.Should().Be("?");
        result.AuthorizationRules[1].IsAllow.Should().BeTrue();
        result.AuthorizationRules[1].Roles.Should().Be("Admin");
    }
}

public sealed class AuthenticationCodeGeneratorTests
{
    private readonly AuthenticationCodeGenerator _generator = new();

    [Fact]
    public void GenerateServicesCode_GeneratesCookieAuth()
    {
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                LoginUrl = "/Account/Login",
                TimeoutMinutes = 30,
                RequireSsl = true
            }
        };

        var code = _generator.GenerateServicesCode(auth);

        code.Should().Contain("AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)");
        code.Should().Contain("AddCookie");
        code.Should().Contain("LoginPath = \"/Account/Login\"");
        code.Should().Contain("CookieSecurePolicy.Always");
    }

    [Fact]
    public void GenerateServicesCode_GeneratesNegotiateAuth()
    {
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Windows
        };

        var code = _generator.GenerateServicesCode(auth);

        code.Should().Contain("AddAuthentication(NegotiateDefaults.AuthenticationScheme)");
        code.Should().Contain("AddNegotiate()");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
