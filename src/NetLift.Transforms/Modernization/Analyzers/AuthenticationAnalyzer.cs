using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using ProjectInfo = NetLift.Core.Models.ProjectInfo;

namespace NetLift.Transforms.Modernization.Analyzers;

/// <summary>
/// Analyzes .NET Framework projects to detect authentication and authorization patterns using Roslyn.
/// Identifies [Authorize] attributes, role usage, Membership API calls, and custom identity implementations.
/// </summary>
public sealed class AuthenticationAnalyzer : IAuthenticationAnalyzer
{
    private static readonly HashSet<string> MembershipMethods = new(StringComparer.Ordinal)
    {
        "GetUser", "CreateUser", "DeleteUser", "UpdateUser", "ValidateUser",
        "GetAllUsers", "FindUsersByName", "FindUsersByEmail", "GetNumberOfUsersOnline"
    };

    private static readonly HashSet<string> FormsAuthMethods = new(StringComparer.Ordinal)
    {
        "SetAuthCookie", "SignOut", "RedirectFromLoginPage", "GetAuthCookie",
        "Decrypt", "Encrypt", "RenewTicketIfOld", "GetRedirectUrl"
    };

    /// <inheritdoc />
    public async Task<AuthenticationInfo?> AnalyzeFileAsync(
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return null;
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        var roles = new List<RoleUsage>();
        var customClaims = new List<CustomClaimUsage>();
        var membershipCalls = new List<MembershipUsage>();
        var formsAuthCalls = new List<FormsAuthUsage>();

        // Detect [Authorize] attributes
        DetectAuthorizeAttributes(root, filePath, roles);

        // Detect User.IsInRole() calls
        DetectIsInRoleCalls(root, filePath, roles);

        // Detect Roles.IsUserInRole() calls
        DetectRolesApiCalls(root, filePath, roles);

        // Detect Membership API calls
        DetectMembershipCalls(root, filePath, membershipCalls);

        // Detect FormsAuthentication calls
        DetectFormsAuthCalls(root, filePath, formsAuthCalls);

        // Detect custom claims from IPrincipal/IIdentity extensions
        DetectCustomClaims(root, filePath, customClaims);

        // Check for custom identity implementations
        var customIdentity = DetectCustomIdentity(root);
        var customPrincipal = DetectCustomPrincipal(root);

        // If nothing found, return null
        if (roles.Count == 0 && customClaims.Count == 0 && membershipCalls.Count == 0 &&
            formsAuthCalls.Count == 0 && !customIdentity.found && !customPrincipal.found)
        {
            return null;
        }

        return new AuthenticationInfo
        {
            ProjectPath = filePath,
            RolesDetected = roles.AsReadOnly(),
            CustomClaims = customClaims.AsReadOnly(),
            MembershipCalls = membershipCalls.AsReadOnly(),
            FormsAuthCalls = formsAuthCalls.AsReadOnly(),
            HasCustomIdentity = customIdentity.found || customPrincipal.found,
            CustomIdentityClassName = customIdentity.className,
            CustomPrincipalClassName = customPrincipal.className,
            RequiresJwt = false, // Will be determined at project level
            Confidence = CalculateConfidence(roles.Count, customClaims.Count, membershipCalls.Count, customIdentity.found)
        };
    }

    /// <inheritdoc />
    public async Task<AuthenticationInfo> AnalyzeProjectAsync(
        Core.Models.ProjectInfo projectInfo,
        CancellationToken cancellationToken = default)
    {
        var allRoles = new List<RoleUsage>();
        var allCustomClaims = new List<CustomClaimUsage>();
        var allMembershipCalls = new List<MembershipUsage>();
        var allFormsAuthCalls = new List<FormsAuthUsage>();
        var hasCustomIdentity = false;
        string? customIdentityClassName = null;
        string? customPrincipalClassName = null;
        var hasApiControllers = false;

        // Analyze all .cs files
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath) ?? string.Empty;
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();

        foreach (var file in csFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(file, cancellationToken);
                var fileInfo = await AnalyzeFileAsync(file, sourceCode, cancellationToken);

                if (fileInfo != null)
                {
                    allRoles.AddRange(fileInfo.RolesDetected);
                    allCustomClaims.AddRange(fileInfo.CustomClaims);
                    allMembershipCalls.AddRange(fileInfo.MembershipCalls);
                    allFormsAuthCalls.AddRange(fileInfo.FormsAuthCalls);

                    if (fileInfo.HasCustomIdentity)
                    {
                        hasCustomIdentity = true;
                        customIdentityClassName ??= fileInfo.CustomIdentityClassName;
                        customPrincipalClassName ??= fileInfo.CustomPrincipalClassName;
                    }
                }

                // Check for ApiController
                if (sourceCode.Contains(": ApiController") || sourceCode.Contains("[ApiController]"))
                {
                    hasApiControllers = true;
                }
            }
            catch (IOException)
            {
                // File access issue - skip file
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                // Permission denied - skip file
                continue;
            }
            catch (ArgumentException)
            {
                // Invalid syntax in source file - skip file
                continue;
            }
        }

        // Deduplicate roles
        var uniqueRoles = allRoles
            .GroupBy(r => r.Role)
            .Select(g => g.First())
            .ToList();

        return new AuthenticationInfo
        {
            ProjectPath = projectInfo.FilePath,
            RolesDetected = uniqueRoles.AsReadOnly(),
            CustomClaims = allCustomClaims.AsReadOnly(),
            MembershipCalls = allMembershipCalls.AsReadOnly(),
            FormsAuthCalls = allFormsAuthCalls.AsReadOnly(),
            HasCustomIdentity = hasCustomIdentity,
            CustomIdentityClassName = customIdentityClassName,
            CustomPrincipalClassName = customPrincipalClassName,
            RequiresJwt = hasApiControllers,
            Confidence = CalculateConfidence(uniqueRoles.Count, allCustomClaims.Count, allMembershipCalls.Count, hasCustomIdentity)
        };
    }

    private static void DetectAuthorizeAttributes(SyntaxNode root, string filePath, List<RoleUsage> roles)
    {
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(a => a.Name.ToString().Contains("Authorize"));

        foreach (var attr in attributes)
        {
            var rolesArg = attr.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.ToString() == "Roles");

            if (rolesArg?.Expression is LiteralExpressionSyntax literal)
            {
                var roleString = literal.Token.ValueText;
                var roleNames = roleString.Split(',').Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r));

                var lineNumber = attr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var location = $"{filePath}:{lineNumber}";

                var classDecl = attr.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                var methodDecl = attr.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();

                foreach (var role in roleNames)
                {
                    roles.Add(new RoleUsage
                    {
                        Role = role,
                        Location = location,
                        Type = RoleUsageType.AuthorizeAttribute,
                        ClassName = classDecl?.Identifier.Text,
                        MethodName = methodDecl?.Identifier.Text
                    });
                }
            }
        }
    }

    private static void DetectIsInRoleCalls(SyntaxNode root, string filePath, List<RoleUsage> roles)
    {
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Name.Identifier.Text == "IsInRole");

        foreach (var inv in invocations)
        {
            var arg = inv.ArgumentList.Arguments.FirstOrDefault();
            if (arg?.Expression is LiteralExpressionSyntax literal)
            {
                var role = literal.Token.ValueText;
                var lineNumber = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var location = $"{filePath}:{lineNumber}";

                var classDecl = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                var methodDecl = inv.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();

                roles.Add(new RoleUsage
                {
                    Role = role,
                    Location = location,
                    Type = RoleUsageType.IsInRoleCall,
                    ClassName = classDecl?.Identifier.Text,
                    MethodName = methodDecl?.Identifier.Text
                });
            }
        }
    }

    private static void DetectRolesApiCalls(SyntaxNode root, string filePath, List<RoleUsage> roles)
    {
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Expression is IdentifierNameSyntax id &&
                         id.Identifier.Text == "Roles" &&
                         member.Name.Identifier.Text == "IsUserInRole");

        foreach (var inv in invocations)
        {
            var roleArg = inv.ArgumentList.Arguments.Skip(1).FirstOrDefault(); // Second argument is role
            if (roleArg?.Expression is LiteralExpressionSyntax literal)
            {
                var role = literal.Token.ValueText;
                var lineNumber = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var location = $"{filePath}:{lineNumber}";

                var classDecl = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                var methodDecl = inv.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();

                roles.Add(new RoleUsage
                {
                    Role = role,
                    Location = location,
                    Type = RoleUsageType.RolesApiCall,
                    ClassName = classDecl?.Identifier.Text,
                    MethodName = methodDecl?.Identifier.Text
                });
            }
        }
    }

    private static void DetectMembershipCalls(SyntaxNode root, string filePath, List<MembershipUsage> membershipCalls)
    {
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Expression is IdentifierNameSyntax id &&
                         id.Identifier.Text == "Membership" &&
                         MembershipMethods.Contains(member.Name.Identifier.Text));

        foreach (var inv in invocations)
        {
            var member = (MemberAccessExpressionSyntax)inv.Expression;
            var method = member.Name.Identifier.Text;
            var lineNumber = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var location = $"{filePath}:{lineNumber}";

            var classDecl = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            membershipCalls.Add(new MembershipUsage
            {
                Method = method,
                Location = location,
                ClassName = classDecl?.Identifier.Text
            });
        }
    }

    private static void DetectFormsAuthCalls(SyntaxNode root, string filePath, List<FormsAuthUsage> formsAuthCalls)
    {
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Expression is IdentifierNameSyntax id &&
                         id.Identifier.Text == "FormsAuthentication" &&
                         FormsAuthMethods.Contains(member.Name.Identifier.Text));

        foreach (var inv in invocations)
        {
            var member = (MemberAccessExpressionSyntax)inv.Expression;
            var method = member.Name.Identifier.Text;
            var lineNumber = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var location = $"{filePath}:{lineNumber}";

            var classDecl = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            formsAuthCalls.Add(new FormsAuthUsage
            {
                Method = method,
                Location = location,
                ClassName = classDecl?.Identifier.Text
            });
        }
    }

    private static void DetectCustomClaims(SyntaxNode root, string filePath, List<CustomClaimUsage> customClaims)
    {
        // Look for properties on custom IPrincipal/IIdentity extensions
        var properties = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p =>
            {
                var classDecl = p.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl?.BaseList == null) return false;

                var baseTypes = classDecl.BaseList.Types.Select(t => t.ToString());
                return baseTypes.Any(bt => bt.Contains("IIdentity") || bt.Contains("IPrincipal"));
            });

        foreach (var prop in properties)
        {
            // Skip standard properties
            if (prop.Identifier.Text is "Name" or "IsAuthenticated" or "AuthenticationType" or "Identity")
                continue;

            var lineNumber = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var location = $"{filePath}:{lineNumber}";

            customClaims.Add(new CustomClaimUsage
            {
                ClaimName = prop.Identifier.Text,
                Location = location,
                DataType = prop.Type.ToString()
            });
        }
    }

    private static (bool found, string? className) DetectCustomIdentity(SyntaxNode root)
    {
        var customIdentity = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.BaseList?.Types.Any(t => t.ToString().Contains("IIdentity")) == true);

        return (customIdentity != null, customIdentity?.Identifier.Text);
    }

    private static (bool found, string? className) DetectCustomPrincipal(SyntaxNode root)
    {
        var customPrincipal = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.BaseList?.Types.Any(t => t.ToString().Contains("IPrincipal")) == true);

        return (customPrincipal != null, customPrincipal?.Identifier.Text);
    }

    private static int CalculateConfidence(int roleCount, int claimCount, int membershipCount, bool hasCustomIdentity)
    {
        // High confidence if patterns are clear and standard
        if (roleCount > 0 && !hasCustomIdentity && membershipCount == 0)
        {
            return 95; // Simple role-based auth, easy to migrate
        }

        if (roleCount > 0 && membershipCount > 0 && !hasCustomIdentity)
        {
            return 85; // Membership with roles, standard pattern
        }

        if (roleCount > 0 && hasCustomIdentity && claimCount <= 3)
        {
            return 75; // Custom identity with few claims, manageable
        }

        if (hasCustomIdentity && claimCount > 3)
        {
            return 60; // Complex custom identity, needs review
        }

        if (roleCount == 0 && membershipCount > 0)
        {
            return 70; // Membership without explicit roles
        }

        return 50; // Unknown or complex patterns
    }
}
