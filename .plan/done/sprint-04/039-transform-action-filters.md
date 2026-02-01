# Task 039: Transform ActionFilterAttribute and AuthorizeAttribute Usage

## Meta
- **Priority**: P1
- **Estimate**: 8 points
- **Sprint**: 4
- **Dependencies**: 037
- **Status**: Not Started

## Description
Implement a Roslyn-based transformer to migrate ASP.NET MVC action filters and authorization attributes to their ASP.NET Core equivalents. This includes converting ActionFilterAttribute implementations to IActionFilter interface and migrating role-based AuthorizeAttribute to policy-based authorization.

## Acceptance Criteria
- [ ] ActionFilterTransformer class implemented
- [ ] Convert ActionFilterAttribute to IActionFilter/IAsyncActionFilter interface
- [ ] Transform OnActionExecuting/OnActionExecuted methods
- [ ] Convert IAuthorizationFilter implementations
- [ ] Migrate AuthorizeAttribute (Roles="Admin") to policy-based [Authorize(Policy="AdminPolicy")]
- [ ] Generate authorization policy registrations for Program.cs
- [ ] Handle IExceptionFilter migration
- [ ] Handle IResultFilter migration
- [ ] Support filter ordering with Order property
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### Filter Interface Migration Map
```csharp
public static class FilterMigrationMap
{
    public static readonly Dictionary<string, string> InterfaceMap = new()
    {
        // Action Filters
        ["ActionFilterAttribute"] = "IActionFilter, IAsyncActionFilter",
        ["IActionFilter"] = "Microsoft.AspNetCore.Mvc.Filters.IActionFilter",

        // Authorization Filters
        ["AuthorizeAttribute"] = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute",
        ["IAuthorizationFilter"] = "Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter",

        // Exception Filters
        ["HandleErrorAttribute"] = "IExceptionFilter",
        ["IExceptionFilter"] = "Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter",

        // Result Filters
        ["IResultFilter"] = "Microsoft.AspNetCore.Mvc.Filters.IResultFilter"
    };

    public static readonly Dictionary<string, string> MethodMap = new()
    {
        // ActionFilterAttribute methods
        ["OnActionExecuting"] = "OnActionExecuting",
        ["OnActionExecuted"] = "OnActionExecuted",
        ["OnActionExecutionAsync"] = "OnActionExecutionAsync",

        // Context types
        ["ActionExecutingContext"] = "ActionExecutingContext",
        ["ActionExecutedContext"] = "ActionExecutedContext",

        // Exception filter
        ["OnException"] = "OnException",
        ["ExceptionContext"] = "ExceptionContext",

        // Result filter
        ["OnResultExecuting"] = "OnResultExecuting",
        ["OnResultExecuted"] = "OnResultExecuted"
    };
}
```

### Action Filter Transformer
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Transformers;

public class ActionFilterTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly List<PolicyDefinition> _generatedPolicies = new();

    public IReadOnlyList<PolicyDefinition> GeneratedPolicies => _generatedPolicies;

    public ActionFilterTransformer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Check if this class extends ActionFilterAttribute
        if (InheritsFromActionFilterAttribute(node))
        {
            return TransformActionFilterClass(node);
        }

        return base.VisitClassDeclaration(node);
    }

    public override SyntaxNode? VisitAttribute(AttributeSyntax node)
    {
        var attributeName = node.Name.ToString();

        // Transform [Authorize(Roles = "Admin")] to [Authorize(Policy = "AdminPolicy")]
        if (attributeName == "Authorize" || attributeName == "AuthorizeAttribute")
        {
            return TransformAuthorizeAttribute(node);
        }

        // Transform [HandleError] to custom exception filter
        if (attributeName == "HandleError" || attributeName == "HandleErrorAttribute")
        {
            return TransformHandleErrorAttribute(node);
        }

        return base.VisitAttribute(node);
    }

    private bool InheritsFromActionFilterAttribute(ClassDeclarationSyntax node)
    {
        if (node.BaseList == null) return false;

        return node.BaseList.Types.Any(t =>
            t.Type.ToString().Contains("ActionFilterAttribute") ||
            t.Type.ToString().Contains("FilterAttribute"));
    }

    private ClassDeclarationSyntax TransformActionFilterClass(
        ClassDeclarationSyntax node)
    {
        // Change base class from ActionFilterAttribute to implementing interfaces
        var newBaseList = CreateFilterInterfaceBaseList(node);

        // Transform method signatures
        var newMembers = node.Members.Select(m =>
            m is MethodDeclarationSyntax method
                ? TransformFilterMethod(method)
                : m);

        return node
            .WithBaseList(newBaseList)
            .WithMembers(SyntaxFactory.List(newMembers));
    }

    private BaseListSyntax CreateFilterInterfaceBaseList(ClassDeclarationSyntax node)
    {
        var interfaces = new List<BaseTypeSyntax>();

        // Determine which interfaces to implement based on overridden methods
        var methods = node.Members.OfType<MethodDeclarationSyntax>().ToList();

        if (methods.Any(m => m.Identifier.Text.StartsWith("OnAction")))
        {
            interfaces.Add(SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName("IActionFilter")));
        }

        if (methods.Any(m => m.Identifier.Text == "OnException"))
        {
            interfaces.Add(SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName("IExceptionFilter")));
        }

        if (methods.Any(m => m.Identifier.Text.StartsWith("OnResult")))
        {
            interfaces.Add(SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName("IResultFilter")));
        }

        // Add Attribute base if this should still be usable as attribute
        if (node.AttributeLists.Any(al => al.Attributes.Any(a =>
            a.Name.ToString() == "AttributeUsage")))
        {
            interfaces.Insert(0, SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName("Attribute")));
        }

        return SyntaxFactory.BaseList(
            SyntaxFactory.SeparatedList(interfaces));
    }

    private MemberDeclarationSyntax TransformFilterMethod(
        MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;

        // Update parameter types
        var newParameters = method.ParameterList.Parameters.Select(p =>
        {
            var typeName = p.Type?.ToString() ?? "";

            // Update context parameter types
            if (typeName.Contains("ActionExecutingContext") ||
                typeName.Contains("ActionExecutedContext") ||
                typeName.Contains("ExceptionContext"))
            {
                // Types are the same in ASP.NET Core, just different namespace
                return p;
            }

            return p;
        });

        // Remove override modifier if present (interface implementation)
        var newModifiers = method.Modifiers
            .Where(m => !m.IsKind(SyntaxKind.OverrideKeyword))
            .ToList();

        if (!newModifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            newModifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.PublicKeyword));
        }

        return method
            .WithModifiers(SyntaxFactory.TokenList(newModifiers))
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(newParameters)));
    }

    private AttributeSyntax TransformAuthorizeAttribute(AttributeSyntax node)
    {
        var arguments = node.ArgumentList?.Arguments ?? default;

        // Look for Roles argument
        var rolesArg = arguments.FirstOrDefault(a =>
            a.NameEquals?.Name.Identifier.Text == "Roles");

        if (rolesArg != null)
        {
            // Extract roles value
            var rolesValue = (rolesArg.Expression as LiteralExpressionSyntax)
                ?.Token.ValueText ?? "";

            // Generate policy name
            var policyName = GeneratePolicyName(rolesValue);

            // Store policy definition for later generation
            _generatedPolicies.Add(new PolicyDefinition
            {
                Name = policyName,
                Roles = rolesValue.Split(',').Select(r => r.Trim()).ToArray()
            });

            // Create new [Authorize(Policy = "PolicyName")] attribute
            return SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("Authorize"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(policyName)))
                        .WithNameEquals(SyntaxFactory.NameEquals("Policy")))));
        }

        // Keep simple [Authorize] as-is
        return node;
    }

    private AttributeSyntax TransformHandleErrorAttribute(AttributeSyntax node)
    {
        // [HandleError] becomes [ServiceFilter(typeof(CustomExceptionFilter))]
        // User needs to implement IExceptionFilter

        return SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("TypeFilter"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.TypeOfExpression(
                            SyntaxFactory.ParseTypeName("GlobalExceptionFilter"))))));
    }

    private string GeneratePolicyName(string roles)
    {
        // Convert "Admin,Manager" to "AdminManagerPolicy"
        var cleanRoles = string.Join("",
            roles.Split(',')
                .Select(r => char.ToUpper(r.Trim()[0]) + r.Trim().Substring(1)));
        return $"{cleanRoles}Policy";
    }
}

public record PolicyDefinition
{
    public string Name { get; init; } = "";
    public string[] Roles { get; init; } = Array.Empty<string>();
}
```

### Policy Registration Generator
```csharp
public class PolicyRegistrationGenerator
{
    public string GeneratePolicyRegistrations(IEnumerable<PolicyDefinition> policies)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Add to Program.cs or Startup.cs");
        sb.AppendLine("builder.Services.AddAuthorization(options =>");
        sb.AppendLine("{");

        foreach (var policy in policies)
        {
            sb.AppendLine($"    options.AddPolicy(\"{policy.Name}\", policy =>");
            sb.AppendLine($"        policy.RequireRole({string.Join(", ", policy.Roles.Select(r => $"\"{r}\""))}));");
        }

        sb.AppendLine("});");

        return sb.ToString();
    }
}
```

### Example Transformation

**Before:**
```csharp
public class LogActionFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        Debug.WriteLine($"Executing: {filterContext.ActionDescriptor.ActionName}");
        base.OnActionExecuting(filterContext);
    }

    public override void OnActionExecuted(ActionExecutedContext filterContext)
    {
        Debug.WriteLine($"Executed: {filterContext.ActionDescriptor.ActionName}");
        base.OnActionExecuted(filterContext);
    }
}

[Authorize(Roles = "Admin,Manager")]
public class AdminController : Controller { }
```

**After:**
```csharp
public class LogActionFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        Debug.WriteLine($"Executing: {context.ActionDescriptor.DisplayName}");
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        Debug.WriteLine($"Executed: {context.ActionDescriptor.DisplayName}");
    }
}

[Authorize(Policy = "AdminManagerPolicy")]
public class AdminController : Controller { }

// Generated policy registration:
// builder.Services.AddAuthorization(options =>
// {
//     options.AddPolicy("AdminManagerPolicy", policy =>
//         policy.RequireRole("Admin", "Manager"));
// });
```

### Unit Tests
```csharp
public class ActionFilterTransformerTests
{
    [Fact]
    public async Task TransformsActionFilterAttributeToInterface()
    {
        var source = @"
public class MyFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context) { }
}";

        var result = await TransformAsync(source);

        Assert.Contains("IActionFilter", result);
        Assert.DoesNotContain("ActionFilterAttribute", result);
        Assert.DoesNotContain("override", result);
    }

    [Fact]
    public async Task TransformsAuthorizeRolesToPolicy()
    {
        var source = @"
[Authorize(Roles = ""Admin"")]
public class SecureController : Controller { }";

        var (result, policies) = await TransformWithPoliciesAsync(source);

        Assert.Contains("Policy = \"AdminPolicy\"", result);
        Assert.Single(policies);
        Assert.Equal("AdminPolicy", policies[0].Name);
    }

    [Fact]
    public async Task TransformsMultipleRolesToSinglePolicy()
    {
        var source = @"
[Authorize(Roles = ""Admin,Manager,Editor"")]
public class ContentController : Controller { }";

        var (result, policies) = await TransformWithPoliciesAsync(source);

        Assert.Contains("AdminManagerEditorPolicy", result);
        Assert.Equal(3, policies[0].Roles.Length);
    }

    [Fact]
    public async Task TransformsHandleErrorToExceptionFilter()
    {
        var source = @"
[HandleError]
public class HomeController : Controller { }";

        var result = await TransformAsync(source);

        Assert.Contains("TypeFilter", result);
        Assert.Contains("GlobalExceptionFilter", result);
    }

    [Fact]
    public async Task PreservesSimpleAuthorizeAttribute()
    {
        var source = @"
[Authorize]
public class SecureController : Controller { }";

        var result = await TransformAsync(source);

        Assert.Contains("[Authorize]", result);
        Assert.DoesNotContain("Policy", result);
    }
}
```

## Progress Log
- [Created] - Task definition with filter transformation implementation details
