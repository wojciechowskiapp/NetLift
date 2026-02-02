using System.Text;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates modern ASP.NET Core Identity authentication code from detected legacy patterns.
/// </summary>
public sealed class AuthenticationGenerator : IAuthenticationGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <inheritdoc />
    public AuthModernizationResult Generate(AuthenticationInfo authInfo)
    {
        ArgumentNullException.ThrowIfNull(authInfo);

        var identityUserCode = GenerateIdentityUser(authInfo);
        var identityDbContextCode = GenerateIdentityDbContext(authInfo, "Data");
        var programCsAuthCode = GenerateProgramCsAuth(authInfo);
        var programCsAuthorizationCode = GenerateAuthorizationPolicies(authInfo);
        var jwtConfigCode = GenerateJwtConfiguration(authInfo);
        var policies = GeneratePolicyDefinitions(authInfo);
        var packages = DetermineRequiredPackages(authInfo);
        var warnings = GenerateWarnings(authInfo);
        var migrationGuide = GenerateMigrationGuide(authInfo);

        return new AuthModernizationResult
        {
            SourceInfo = authInfo,
            IdentityUserCode = identityUserCode,
            IdentityDbContextCode = identityDbContextCode,
            ProgramCsAuthCode = programCsAuthCode,
            ProgramCsAuthorizationCode = programCsAuthorizationCode,
            JwtConfigurationCode = jwtConfigCode,
            GeneratedPolicies = policies,
            RequiredPackages = packages,
            MigrationGuide = migrationGuide,
            Warnings = warnings,
            Confidence = authInfo.Confidence
        };
    }

    /// <inheritdoc />
    public string? GenerateIdentityUser(AuthenticationInfo authInfo)
    {
        if (authInfo.CustomClaims.Count == 0 && !authInfo.HasCustomIdentity)
        {
            return null; // Default IdentityUser is sufficient
        }

        var sb = new StringBuilder();

        sb.AppendLine("using Microsoft.AspNetCore.Identity;");
        sb.AppendLine();
        sb.AppendLine("namespace YourApp.Models;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Application user with custom claims and properties.");

        if (authInfo.CustomIdentityClassName != null)
        {
            sb.AppendLine($"/// Migrated from {authInfo.CustomIdentityClassName}.");
        }

        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ApplicationUser : IdentityUser");
        sb.AppendLine("{");

        // Add custom properties from detected claims
        foreach (var claim in authInfo.CustomClaims.DistinctBy(c => c.ClaimName))
        {
            sb.AppendLine($"{Indent}/// <summary>");
            sb.AppendLine($"{Indent}/// Custom claim: {claim.ClaimName}.");
            sb.AppendLine($"{Indent}/// </summary>");

            var propertyType = claim.DataType ?? "string";
            sb.AppendLine($"{Indent}public {propertyType}? {claim.ClaimName} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateIdentityDbContext(AuthenticationInfo authInfo, string dbContextNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Microsoft.AspNetCore.Identity.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

        if (authInfo.CustomClaims.Count > 0 || authInfo.HasCustomIdentity)
        {
            sb.AppendLine("using YourApp.Models;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace YourApp.{dbContextNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Database context for ASP.NET Core Identity.");
        sb.AppendLine("/// </summary>");

        if (authInfo.CustomClaims.Count > 0 || authInfo.HasCustomIdentity)
        {
            sb.AppendLine("public class ApplicationDbContext : IdentityDbContext<ApplicationUser>");
        }
        else
        {
            sb.AppendLine("public class ApplicationDbContext : IdentityDbContext");
        }

        sb.AppendLine("{");
        sb.AppendLine($"{Indent}public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)");
        sb.AppendLine($"{DoubleIndent}: base(options)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{Indent}protected override void OnModelCreating(ModelBuilder builder)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}base.OnModelCreating(builder);");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}// Customize Identity schema if needed");
        sb.AppendLine($"{DoubleIndent}// Example: builder.Entity<ApplicationUser>().ToTable(\"Users\");");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateProgramCsAuth(AuthenticationInfo authInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Add Identity services");

        if (authInfo.CustomClaims.Count > 0 || authInfo.HasCustomIdentity)
        {
            sb.AppendLine("builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>");
        }
        else
        {
            sb.AppendLine("builder.Services.AddDefaultIdentity<IdentityUser>(options =>");
        }

        sb.AppendLine("{");
        sb.AppendLine($"{Indent}// Password settings");
        sb.AppendLine($"{Indent}options.Password.RequireDigit = true;");
        sb.AppendLine($"{Indent}options.Password.RequireLowercase = true;");
        sb.AppendLine($"{Indent}options.Password.RequireUppercase = true;");
        sb.AppendLine($"{Indent}options.Password.RequireNonAlphanumeric = false;");
        sb.AppendLine($"{Indent}options.Password.RequiredLength = 8;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}// Lockout settings");
        sb.AppendLine($"{Indent}options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);");
        sb.AppendLine($"{Indent}options.Lockout.MaxFailedAccessAttempts = 5;");
        sb.AppendLine($"{Indent}options.Lockout.AllowedForNewUsers = true;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}// User settings");
        sb.AppendLine($"{Indent}options.User.RequireUniqueEmail = true;");
        sb.AppendLine("})");
        sb.AppendLine($"{Indent}.AddEntityFrameworkStores<ApplicationDbContext>()");
        sb.AppendLine($"{Indent}.AddDefaultTokenProviders();");
        sb.AppendLine();

        if (!authInfo.RequiresJwt)
        {
            // Cookie authentication for MVC apps
            sb.AppendLine("// Configure cookie authentication");
            sb.AppendLine("builder.Services.ConfigureApplicationCookie(options =>");
            sb.AppendLine("{");
            sb.AppendLine($"{Indent}options.LoginPath = \"/Account/Login\";");
            sb.AppendLine($"{Indent}options.LogoutPath = \"/Account/Logout\";");
            sb.AppendLine($"{Indent}options.AccessDeniedPath = \"/Account/AccessDenied\";");
            sb.AppendLine($"{Indent}options.ExpireTimeSpan = TimeSpan.FromMinutes(30);");
            sb.AppendLine($"{Indent}options.SlidingExpiration = true;");
            sb.AppendLine("});");
        }
        else
        {
            // JWT for APIs
            sb.AppendLine();
            sb.AppendLine("// Add JWT Bearer authentication for API");
            sb.AppendLine("builder.Services.AddAuthentication(options =>");
            sb.AppendLine("{");
            sb.AppendLine($"{Indent}options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;");
            sb.AppendLine($"{Indent}options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;");
            sb.AppendLine("})");
            sb.AppendLine(".AddJwtBearer(options =>");
            sb.AppendLine("{");
            sb.AppendLine($"{Indent}options.TokenValidationParameters = new TokenValidationParameters");
            sb.AppendLine($"{Indent}{{");
            sb.AppendLine($"{DoubleIndent}ValidateIssuer = true,");
            sb.AppendLine($"{DoubleIndent}ValidateAudience = true,");
            sb.AppendLine($"{DoubleIndent}ValidateLifetime = true,");
            sb.AppendLine($"{DoubleIndent}ValidateIssuerSigningKey = true,");
            sb.AppendLine($"{DoubleIndent}ValidIssuer = builder.Configuration[\"Jwt:Issuer\"],");
            sb.AppendLine($"{DoubleIndent}ValidAudience = builder.Configuration[\"Jwt:Audience\"],");
            sb.AppendLine($"{DoubleIndent}IssuerSigningKey = new SymmetricSecurityKey(");
            sb.AppendLine($"{TripleIndent}Encoding.UTF8.GetBytes(builder.Configuration[\"Jwt:Key\"] ?? throw new InvalidOperationException(\"JWT Key not configured\")))");
            sb.AppendLine($"{Indent}}};");
            sb.AppendLine("});");
        }

        if (authInfo.MembershipCalls.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// TODO: Migrate Membership API calls to UserManager<ApplicationUser>");
            sb.AppendLine($"// Found {authInfo.MembershipCalls.Count} Membership API call(s) in the codebase");
        }

        if (authInfo.FormsAuthCalls.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// TODO: Migrate FormsAuthentication calls to SignInManager<ApplicationUser>");
            sb.AppendLine($"// Found {authInfo.FormsAuthCalls.Count} FormsAuthentication call(s) in the codebase");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateAuthorizationPolicies(AuthenticationInfo authInfo)
    {
        if (authInfo.RolesDetected.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        sb.AppendLine("// Add authorization policies");
        sb.AppendLine("builder.Services.AddAuthorization(options =>");
        sb.AppendLine("{");

        var uniqueRoles = authInfo.RolesDetected
            .Select(r => r.Role)
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        foreach (var role in uniqueRoles)
        {
            var policyName = $"{role}Policy";
            sb.AppendLine($"{Indent}options.AddPolicy(\"{policyName}\", policy =>");
            sb.AppendLine($"{DoubleIndent}policy.RequireRole(\"{role}\"));");
        }

        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("// Update [Authorize] attributes to use policies:");

        foreach (var role in uniqueRoles)
        {
            var policyName = $"{role}Policy";
            sb.AppendLine($"// [Authorize(Roles = \"{role}\")] → [Authorize(Policy = \"{policyName}\")]");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string? GenerateJwtConfiguration(AuthenticationInfo authInfo)
    {
        if (!authInfo.RequiresJwt)
        {
            return null;
        }

        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.AppendLine($"{Indent}\"Jwt\": {{");
        sb.AppendLine($"{DoubleIndent}\"Key\": \"your-secret-key-min-16-chars\",");
        sb.AppendLine($"{DoubleIndent}\"Issuer\": \"https://localhost:5001\",");
        sb.AppendLine($"{DoubleIndent}\"Audience\": \"https://localhost:5001\",");
        sb.AppendLine($"{DoubleIndent}\"ExpiryMinutes\": 60");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static List<PolicyDefinition> GeneratePolicyDefinitions(AuthenticationInfo authInfo)
    {
        var policies = new List<PolicyDefinition>();

        var uniqueRoles = authInfo.RolesDetected
            .Select(r => r.Role)
            .Distinct()
            .OrderBy(r => r);

        foreach (var role in uniqueRoles)
        {
            var policyName = $"{role}Policy";
            policies.Add(new PolicyDefinition
            {
                Name = policyName,
                Roles = role,
                Description = $"Requires {role} role",
                OriginalAttribute = $"[Authorize(Roles = \"{role}\")]",
                RecommendedAttribute = $"[Authorize(Policy = \"{policyName}\")]"
            });
        }

        return policies;
    }

    private static List<string> DetermineRequiredPackages(AuthenticationInfo authInfo)
    {
        var packages = new List<string>
        {
            "Microsoft.AspNetCore.Identity.EntityFrameworkCore"
        };

        if (authInfo.RequiresJwt)
        {
            packages.Add("Microsoft.AspNetCore.Authentication.JwtBearer");
            packages.Add("System.IdentityModel.Tokens.Jwt");
        }

        return packages;
    }

    private static List<string> GenerateWarnings(AuthenticationInfo authInfo)
    {
        var warnings = new List<string>();

        if (authInfo.HasCustomIdentity)
        {
            warnings.Add($"Custom identity implementation detected: {authInfo.CustomIdentityClassName}. Review and migrate custom claims carefully.");
        }

        if (authInfo.MembershipCalls.Count > 0)
        {
            warnings.Add($"Found {authInfo.MembershipCalls.Count} Membership API calls. These must be migrated to UserManager<TUser>.");
        }

        if (authInfo.FormsAuthCalls.Count > 0)
        {
            warnings.Add($"Found {authInfo.FormsAuthCalls.Count} FormsAuthentication calls. These must be migrated to SignInManager<TUser>.");
        }

        if (authInfo.CustomClaims.Count > 5)
        {
            warnings.Add($"Found {authInfo.CustomClaims.Count} custom claims. Consider using a separate profile table to avoid bloating the user table.");
        }

        return warnings;
    }

    private static string GenerateMigrationGuide(AuthenticationInfo authInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Authentication Modernization Guide");
        sb.AppendLine();
        sb.AppendLine("## Steps to Complete Migration:");
        sb.AppendLine();
        sb.AppendLine("1. Install required NuGet packages:");

        foreach (var package in DetermineRequiredPackages(authInfo))
        {
            sb.AppendLine($"   - {package}");
        }

        sb.AppendLine();
        sb.AppendLine("2. Add generated ApplicationDbContext to your project");
        sb.AppendLine("3. Run database migration: `dotnet ef migrations add AddIdentity`");
        sb.AppendLine("4. Update Program.cs with generated authentication code");
        sb.AppendLine("5. Replace [Authorize(Roles = \"...\")] with policy-based attributes");
        sb.AppendLine();

        if (authInfo.MembershipCalls.Count > 0)
        {
            sb.AppendLine("## Membership API Migration:");
            sb.AppendLine();

            var membershipMethods = authInfo.MembershipCalls
                .GroupBy(m => m.Method)
                .Select(g => g.Key)
                .Distinct();

            foreach (var method in membershipMethods)
            {
                var replacement = method switch
                {
                    "CreateUser" => "UserManager.CreateAsync(user, password)",
                    "ValidateUser" => "SignInManager.PasswordSignInAsync(username, password, isPersistent, lockoutOnFailure)",
                    "GetUser" => "UserManager.FindByNameAsync(username)",
                    "DeleteUser" => "UserManager.DeleteAsync(user)",
                    "UpdateUser" => "UserManager.UpdateAsync(user)",
                    _ => "See UserManager<TUser> documentation"
                };

                sb.AppendLine($"- Membership.{method}() → {replacement}");
            }

            sb.AppendLine();
        }

        if (authInfo.FormsAuthCalls.Count > 0)
        {
            sb.AppendLine("## FormsAuthentication Migration:");
            sb.AppendLine();

            var formsAuthMethods = authInfo.FormsAuthCalls
                .GroupBy(f => f.Method)
                .Select(g => g.Key)
                .Distinct();

            foreach (var method in formsAuthMethods)
            {
                var replacement = method switch
                {
                    "SetAuthCookie" => "SignInManager.SignInAsync(user, isPersistent)",
                    "SignOut" => "SignInManager.SignOutAsync()",
                    "RedirectFromLoginPage" => "SignInManager.SignInAsync(user, isPersistent) + Redirect",
                    _ => "See SignInManager<TUser> documentation"
                };

                sb.AppendLine($"- FormsAuthentication.{method}() → {replacement}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Testing:");
        sb.AppendLine();
        sb.AppendLine("1. Test user registration and login");
        sb.AppendLine("2. Verify role-based authorization works");
        sb.AppendLine("3. Test password reset and account lockout");
        sb.AppendLine("4. Validate custom claims are preserved");

        return sb.ToString();
    }
}
