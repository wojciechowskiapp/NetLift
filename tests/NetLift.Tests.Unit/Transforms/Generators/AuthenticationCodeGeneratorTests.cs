using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;

namespace NetLift.Tests.Unit.Transforms.Generators;

/// <summary>
/// Tests for the authentication code generator.
/// </summary>
public class AuthenticationCodeGeneratorTests
{
    private readonly IAuthenticationCodeGenerator _generator;

    public AuthenticationCodeGeneratorTests()
    {
        _generator = new AuthenticationCodeGenerator();
    }

    [Fact]
    public void GenerateServicesCode_NoneMode_ReturnsEmpty()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.None
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateServicesCode_FormsWithDefaults_GeneratesCookieAuth()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings()
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)");
        result.Should().Contain(".AddCookie(options =>");
        result.Should().Contain("options.ExpireTimeSpan = TimeSpan.FromMinutes(30)");
        result.Should().Contain("options.SlidingExpiration = true");
        result.Should().Contain("options.Cookie.Name = \".ASPXAUTH\"");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithLoginUrl_IncludesLoginPath()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                LoginUrl = "~/Account/Login"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.LoginPath = \"~/Account/Login\"");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithCustomTimeout_IncludesTimeout()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                TimeoutMinutes = 120
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.ExpireTimeSpan = TimeSpan.FromMinutes(120)");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithRequireSsl_IncludesSecurePolicy()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                RequireSsl = true
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.Cookie.SecurePolicy = CookieSecurePolicy.Always");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithCustomCookieName_IncludesCookieName()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                CookieName = ".MyCustomAuth"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.Cookie.Name = \".MyCustomAuth\"");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithDomain_IncludesDomain()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                Domain = ".example.com"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.Cookie.Domain = \".example.com\"");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithCustomPath_IncludesPath()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                CookiePath = "/myapp"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.Cookie.Path = \"/myapp\"");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithDefaultPath_DoesNotIncludePath()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                CookiePath = "/"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().NotContain("options.Cookie.Path");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithDefaultUrl_IncludesComment()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                DefaultUrl = "~/Home/Index"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("DefaultUrl from Forms auth");
        result.Should().Contain("~/Home/Index");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithCrossAppRedirects_IncludesTodo()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                EnableCrossAppRedirects = true
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("TODO: Review cross-app redirects");
        result.Should().Contain("enableCrossAppRedirects was true");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithSlidingExpirationFalse_SetsFalse()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                SlidingExpiration = false
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.SlidingExpiration = false");
    }

    [Fact]
    public void GenerateServicesCode_WindowsMode_GeneratesNegotiateAuth()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Windows
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("AddAuthentication(NegotiateDefaults.AuthenticationScheme)");
        result.Should().Contain(".AddNegotiate()");
        result.Should().Contain("Windows Authentication requires:");
        result.Should().Contain("Microsoft.AspNetCore.Authentication.Negotiate package");
        result.Should().Contain("IIS with Windows Auth enabled");
    }

    [Fact]
    public void GenerateServicesCode_PassportMode_GeneratesWarning()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Passport
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("WARNING: Passport authentication is deprecated");
        result.Should().Contain("TODO: Migrate to a modern authentication provider");
        result.Should().Contain("Azure AD");
        result.Should().Contain("IdentityServer");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_NoRules_ReturnsEmpty()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules = []
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateAuthorizationPolicies_DenyAnonymous_CreatesFallbackPolicy()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = false,
                    Users = "?"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("builder.Services.AddAuthorization(options =>");
        result.Should().Contain("options.FallbackPolicy");
        result.Should().Contain("RequireAuthenticatedUser()");
        result.Should().Contain("deny users=\"?\"");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_AllowSingleRole_CreatesNamedPolicy()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = true,
                    Roles = "Admin"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("options.AddPolicy(\"AdminPolicy\"");
        result.Should().Contain("policy.RequireRole(\"Admin\")");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_AllowMultipleRoles_CreatesPolicy()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = true,
                    Roles = "Admin,Manager,Editor"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("policy.RequireRole(\"Admin\", \"Manager\", \"Editor\")");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_DenyRole_CreatesAssertionPolicy()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = false,
                    Roles = "Guest"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("policy.RequireAssertion(context =>");
        result.Should().Contain("!context.User.IsInRole(\"Guest\")");
        result.Should().Contain("Complex deny rules may need custom authorization handlers");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_VerbBasedRule_GeneratesTodo()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = true,
                    Verbs = "GET,POST"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("TODO: Verb-based authorization (GET,POST)");
        result.Should().Contain("requires custom authorization handlers");
        result.Should().Contain("endpoint-level authorization with HTTP method attributes");
    }

    [Fact]
    public void GenerateAuthorizationPolicies_ComplexRules_GeneratesMultiplePolicies()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            AuthorizationRules =
            [
                new AuthorizationRule
                {
                    IsAllow = false,
                    Users = "?"
                },
                new AuthorizationRule
                {
                    IsAllow = true,
                    Roles = "Admin"
                },
                new AuthorizationRule
                {
                    IsAllow = true,
                    Roles = "Manager,Editor"
                }
            ]
        };

        // Act
        var result = _generator.GenerateAuthorizationPolicies(auth);

        // Assert
        result.Should().Contain("options.FallbackPolicy");
        result.Should().Contain("AdminPolicy");
        result.Should().Contain("CustomPolicy1");
    }

    [Fact]
    public void GenerateJwtAlternative_GeneratesCommentedTemplate()
    {
        // Act
        var result = _generator.GenerateJwtAlternative();

        // Assert
        result.Should().Contain("// Alternative: JWT Bearer Authentication");
        result.Should().Contain("/*");
        result.Should().Contain("*/");
        result.Should().Contain("AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
        result.Should().Contain(".AddJwtBearer(options =>");
        result.Should().Contain("TokenValidationParameters");
        result.Should().Contain("ValidateIssuer = true");
        result.Should().Contain("ValidateAudience = true");
        result.Should().Contain("ValidateLifetime = true");
        result.Should().Contain("ValidateIssuerSigningKey = true");
        result.Should().Contain("builder.Configuration[\"Jwt:Issuer\"]");
        result.Should().Contain("builder.Configuration[\"Jwt:Audience\"]");
        result.Should().Contain("builder.Configuration[\"Jwt:Key\"]");
    }

    [Fact]
    public void GenerateServicesCode_FormsWithAllSettings_GeneratesCompleteCode()
    {
        // Arrange
        var auth = new AuthenticationSection
        {
            Mode = AuthenticationMode.Forms,
            FormsSettings = new FormsAuthSettings
            {
                LoginUrl = "~/Account/Login",
                TimeoutMinutes = 60,
                SlidingExpiration = false,
                RequireSsl = true,
                CookieName = ".MyAuth",
                DefaultUrl = "~/Home",
                Domain = ".example.com",
                EnableCrossAppRedirects = true,
                CookiePath = "/app"
            }
        };

        // Act
        var result = _generator.GenerateServicesCode(auth);

        // Assert
        result.Should().Contain("options.LoginPath = \"~/Account/Login\"");
        result.Should().Contain("options.ExpireTimeSpan = TimeSpan.FromMinutes(60)");
        result.Should().Contain("options.SlidingExpiration = false");
        result.Should().Contain("options.Cookie.SecurePolicy = CookieSecurePolicy.Always");
        result.Should().Contain("options.Cookie.Name = \".MyAuth\"");
        result.Should().Contain("options.Cookie.Domain = \".example.com\"");
        result.Should().Contain("options.Cookie.Path = \"/app\"");
        result.Should().Contain("~/Home");
        result.Should().Contain("TODO: Review cross-app redirects");
    }
}
