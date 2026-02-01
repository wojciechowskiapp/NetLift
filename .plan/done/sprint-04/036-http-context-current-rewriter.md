# Task 036: HttpContext.Current Rewriter

## Meta
- **Priority**: P0
- **Estimate**: 7 points
- **Sprint**: 4
- **Dependencies**: 034
- **Status**: Not Started

## Description
Implement Roslyn SyntaxRewriter to replace HttpContext.Current static access with ASP.NET Core dependency injection patterns. This includes injecting IHttpContextAccessor and updating all HttpContext.Current references.

## Acceptance Criteria
- [ ] HttpContextCurrentRewriter implemented
- [ ] Detect all HttpContext.Current usages
- [ ] Add IHttpContextAccessor DI to class constructors
- [ ] Replace HttpContext.Current with _httpContextAccessor.HttpContext
- [ ] Handle HttpContext.Current.User → User (from ControllerBase)
- [ ] Handle HttpContext.Current.Request → Request
- [ ] Handle HttpContext.Current.Response → Response
- [ ] Handle HttpContext.Current.Session → HttpContext.Session
- [ ] Unit tests with 95%+ coverage
- [ ] Preserve null-safety with nullable reference types

## Technical Notes

### HttpContext.Current Access Patterns
```csharp
public static class HttpContextPatterns
{
    // Common patterns to detect and replace
    public static readonly Dictionary<string, string> PropertyMappings = new()
    {
        // Controller base properties (available directly)
        ["HttpContext.Current.User"] = "User",
        ["HttpContext.Current.Request"] = "Request",
        ["HttpContext.Current.Response"] = "Response",

        // Requires IHttpContextAccessor
        ["HttpContext.Current.Session"] = "_httpContextAccessor.HttpContext?.Session",
        ["HttpContext.Current.Items"] = "_httpContextAccessor.HttpContext?.Items",
        ["HttpContext.Current.Server"] = "_httpContextAccessor.HttpContext?.RequestServices",

        // Methods
        ["HttpContext.Current.GetOwinContext()"] = "_httpContextAccessor.HttpContext",
    };

    public static readonly string[] ControllerBaseProperties = new[]
    {
        "User",
        "Request",
        "Response",
        "HttpContext",
        "RouteData",
        "Url"
    };
}
```

### Roslyn SyntaxRewriter Implementation
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Rewriters;

/// <summary>
/// Rewrites HttpContext.Current to ASP.NET Core patterns with DI
/// </summary>
public class HttpContextCurrentRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly HashSet<ClassDeclarationSyntax> _classesNeedingAccessor = new();
    private ClassDeclarationSyntax? _currentClass;

    public HttpContextCurrentRewriter(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public IEnumerable<ClassDeclarationSyntax> ClassesNeedingAccessor => _classesNeedingAccessor;

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previousClass = _currentClass;
        _currentClass = node;

        var result = base.VisitClassDeclaration(node);

        _currentClass = previousClass;
        return result;
    }

    public override SyntaxNode? VisitMemberAccessExpression(
        MemberAccessExpressionSyntax node)
    {
        // Check for HttpContext.Current pattern
        if (!IsHttpContextCurrent(node, out var fullChain))
            return base.VisitMemberAccessExpression(node);

        // Determine replacement strategy
        if (IsInControllerClass())
        {
            // Use controller base properties when possible
            var replacement = GetControllerBaseReplacement(fullChain);
            if (replacement != null)
                return SyntaxFactory.ParseExpression(replacement)
                    .WithTriviaFrom(node);
        }

        // Use IHttpContextAccessor for non-controller classes
        if (_currentClass != null)
        {
            _classesNeedingAccessor.Add(_currentClass);
        }

        var accessorReplacement = GetHttpContextAccessorReplacement(fullChain);
        return SyntaxFactory.ParseExpression(accessorReplacement)
            .WithTriviaFrom(node);
    }

    private bool IsHttpContextCurrent(
        MemberAccessExpressionSyntax node,
        out string fullChain)
    {
        fullChain = node.ToString();

        // Check if this is HttpContext.Current or longer chain
        if (fullChain.StartsWith("HttpContext.Current"))
            return true;

        if (fullChain.StartsWith("System.Web.HttpContext.Current"))
            return true;

        // Use semantic model for accuracy
        var symbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
        if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
        {
            if (propertySymbol.Name == "Current" &&
                propertySymbol.ContainingType?.Name == "HttpContext" &&
                propertySymbol.ContainingNamespace?.ToDisplayString() == "System.Web")
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInControllerClass()
    {
        if (_currentClass == null)
            return false;

        // Check if class inherits from Controller or ControllerBase
        var classSymbol = _semanticModel.GetDeclaredSymbol(_currentClass);
        if (classSymbol == null)
            return false;

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "Controller" || baseType.Name == "ControllerBase")
                return true;

            baseType = baseType.BaseType;
        }

        return false;
    }

    private string? GetControllerBaseReplacement(string fullChain)
    {
        // HttpContext.Current.User → User
        if (fullChain.EndsWith(".User"))
            return "User";

        // HttpContext.Current.Request → Request
        if (fullChain.EndsWith(".Request"))
            return ExtractRemainingChain(fullChain, ".Request", "Request");

        // HttpContext.Current.Response → Response
        if (fullChain.EndsWith(".Response"))
            return ExtractRemainingChain(fullChain, ".Response", "Response");

        // HttpContext.Current → HttpContext
        if (fullChain == "HttpContext.Current" ||
            fullChain == "System.Web.HttpContext.Current")
            return "HttpContext";

        return null;
    }

    private string GetHttpContextAccessorReplacement(string fullChain)
    {
        // HttpContext.Current.User → _httpContextAccessor.HttpContext?.User
        if (fullChain.Contains(".User"))
            return "_httpContextAccessor.HttpContext?.User";

        // HttpContext.Current.Request → _httpContextAccessor.HttpContext?.Request
        if (fullChain.Contains(".Request"))
        {
            var remaining = ExtractRemainingAfter(fullChain, ".Request");
            return $"_httpContextAccessor.HttpContext?.Request{remaining}";
        }

        // HttpContext.Current.Response → _httpContextAccessor.HttpContext?.Response
        if (fullChain.Contains(".Response"))
        {
            var remaining = ExtractRemainingAfter(fullChain, ".Response");
            return $"_httpContextAccessor.HttpContext?.Response{remaining}";
        }

        // HttpContext.Current.Session → _httpContextAccessor.HttpContext?.Session
        if (fullChain.Contains(".Session"))
        {
            var remaining = ExtractRemainingAfter(fullChain, ".Session");
            return $"_httpContextAccessor.HttpContext?.Session{remaining}";
        }

        // HttpContext.Current.Items → _httpContextAccessor.HttpContext?.Items
        if (fullChain.Contains(".Items"))
        {
            var remaining = ExtractRemainingAfter(fullChain, ".Items");
            return $"_httpContextAccessor.HttpContext?.Items{remaining}";
        }

        // Default: HttpContext.Current → _httpContextAccessor.HttpContext
        return "_httpContextAccessor.HttpContext";
    }

    private string ExtractRemainingChain(string fullChain, string marker, string replacement)
    {
        var index = fullChain.IndexOf(marker);
        if (index < 0)
            return replacement;

        var remaining = fullChain.Substring(index + marker.Length);
        return replacement + remaining;
    }

    private string ExtractRemainingAfter(string fullChain, string marker)
    {
        var index = fullChain.IndexOf(marker);
        if (index < 0)
            return string.Empty;

        return fullChain.Substring(index + marker.Length);
    }

    /// <summary>
    /// Post-process classes that need IHttpContextAccessor
    /// </summary>
    public static ClassDeclarationSyntax AddHttpContextAccessor(
        ClassDeclarationSyntax classDeclaration)
    {
        // Add field
        var field = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.ParseTypeName("IHttpContextAccessor"))
                .AddVariables(
                    SyntaxFactory.VariableDeclarator("_httpContextAccessor")))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Find or create constructor
        var existingConstructor = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        ClassDeclarationSyntax newClass;

        if (existingConstructor == null)
        {
            // Create new constructor
            var constructor = CreateConstructorWithAccessor(classDeclaration.Identifier);
            newClass = classDeclaration.AddMembers(field, constructor);
        }
        else
        {
            // Update existing constructor
            var updatedConstructor = AddAccessorToConstructor(existingConstructor);
            newClass = classDeclaration
                .ReplaceNode(existingConstructor, updatedConstructor)
                .WithMembers(classDeclaration.Members.Insert(0, field));
        }

        return newClass;
    }

    private static ConstructorDeclarationSyntax CreateConstructorWithAccessor(
        SyntaxToken className)
    {
        var parameter = SyntaxFactory.Parameter(
            SyntaxFactory.Identifier("httpContextAccessor"))
            .WithType(SyntaxFactory.ParseTypeName("IHttpContextAccessor"));

        var assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName("_httpContextAccessor"),
                SyntaxFactory.IdentifierName("httpContextAccessor")));

        return SyntaxFactory.ConstructorDeclaration(className)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(parameter)
            .WithBody(SyntaxFactory.Block(assignment))
            .WithLeadingTrivia(
                SyntaxFactory.Trivia(
                    SyntaxFactory.DocumentationCommentTrivia(
                        SyntaxKind.SingleLineDocumentationCommentTrivia)));
    }

    private static ConstructorDeclarationSyntax AddAccessorToConstructor(
        ConstructorDeclarationSyntax constructor)
    {
        // Add parameter
        var parameter = SyntaxFactory.Parameter(
            SyntaxFactory.Identifier("httpContextAccessor"))
            .WithType(SyntaxFactory.ParseTypeName("IHttpContextAccessor"));

        var newParameters = constructor.ParameterList.AddParameters(parameter);

        // Add assignment
        var assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName("_httpContextAccessor"),
                SyntaxFactory.IdentifierName("httpContextAccessor")));

        var newBody = constructor.Body?.AddStatements(assignment)
            ?? SyntaxFactory.Block(assignment);

        return constructor
            .WithParameterList(newParameters)
            .WithBody(newBody);
    }
}
```

### Post-Processing for DI Integration
```csharp
public class HttpContextMigrator
{
    public async Task<Document> MigrateHttpContextAsync(Document document)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        if (root == null || semanticModel == null)
            return document;

        // First pass: rewrite HttpContext.Current references
        var rewriter = new HttpContextCurrentRewriter(semanticModel);
        var newRoot = rewriter.Visit(root);

        if (newRoot == null)
            return document;

        // Second pass: add IHttpContextAccessor to classes that need it
        foreach (var classNode in rewriter.ClassesNeedingAccessor)
        {
            var updatedClass = HttpContextCurrentRewriter.AddHttpContextAccessor(classNode);
            newRoot = newRoot.ReplaceNode(classNode, updatedClass);
        }

        // Add required using directives
        newRoot = AddRequiredUsings(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private SyntaxNode AddRequiredUsings(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        var requiredUsings = new[]
        {
            "Microsoft.AspNetCore.Http"
        };

        var existingUsings = compilationUnit.Usings
            .Select(u => u.Name?.ToString())
            .ToHashSet();

        var newUsings = requiredUsings
            .Where(u => !existingUsings.Contains(u))
            .Select(u => SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(u)))
            .ToArray();

        if (!newUsings.Any())
            return root;

        return compilationUnit.AddUsings(newUsings);
    }
}
```

### Migration Examples

#### Example 1: Controller Class
```csharp
// BEFORE
using System.Web;
using System.Web.Mvc;

public class HomeController : Controller
{
    public ActionResult Index()
    {
        var userName = HttpContext.Current.User.Identity.Name;
        var userAgent = HttpContext.Current.Request.UserAgent;

        return View();
    }
}

// AFTER
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        var userName = User.Identity?.Name;
        var userAgent = Request.Headers["User-Agent"].ToString();

        return View();
    }
}
```

#### Example 2: Non-Controller Class
```csharp
// BEFORE
using System.Web;

public class UserService
{
    public string GetCurrentUserName()
    {
        return HttpContext.Current.User.Identity.Name;
    }

    public void SetSessionValue(string key, string value)
    {
        HttpContext.Current.Session[key] = value;
    }
}

// AFTER
using Microsoft.AspNetCore.Http;

public class UserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of UserService
    /// </summary>
    public UserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User.Identity?.Name;
    }

    public void SetSessionValue(string key, string value)
    {
        if (_httpContextAccessor.HttpContext?.Session != null)
        {
            _httpContextAccessor.HttpContext.Session.SetString(key, value);
        }
    }
}
```

#### Example 3: Complex HttpContext Access
```csharp
// BEFORE
public class RequestLogger
{
    public void LogRequest()
    {
        var context = HttpContext.Current;
        var url = context.Request.Url.ToString();
        var method = context.Request.HttpMethod;
        var ip = context.Request.UserHostAddress;

        context.Items["RequestId"] = Guid.NewGuid();
    }
}

// AFTER
public class RequestLogger
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestLogger(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void LogRequest()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        var url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        var method = context.Request.Method;
        var ip = context.Connection.RemoteIpAddress?.ToString();

        context.Items["RequestId"] = Guid.NewGuid();
    }
}
```

### Unit Tests
```csharp
public class HttpContextCurrentRewriterTests
{
    [Fact]
    public async Task RewritesHttpContextCurrentInController()
    {
        var source = @"
public class HomeController : Controller
{
    public ActionResult Index()
    {
        var user = HttpContext.Current.User;
        return View();
    }
}";

        var result = await RewriteAsync(source);
        Assert.Contains("var user = User", result);
        Assert.DoesNotContain("HttpContext.Current", result);
    }

    [Fact]
    public async Task AddsHttpContextAccessorToNonController()
    {
        var source = @"
public class UserService
{
    public string GetUser()
    {
        return HttpContext.Current.User.Identity.Name;
    }
}";

        var result = await RewriteAsync(source);
        Assert.Contains("IHttpContextAccessor _httpContextAccessor", result);
        Assert.Contains("_httpContextAccessor.HttpContext?.User", result);
    }

    [Fact]
    public async Task HandlesRequestProperty()
    {
        var source = @"
public ActionResult Index()
{
    var userAgent = HttpContext.Current.Request.UserAgent;
    return View();
}";

        var result = await RewriteAsync(source);
        Assert.Contains("Request.Headers", result);
    }

    [Fact]
    public async Task HandlesSessionAccess()
    {
        var source = @"
public class SessionService
{
    public void SetValue()
    {
        HttpContext.Current.Session[""key""] = ""value"";
    }
}";

        var result = await RewriteAsync(source);
        Assert.Contains("_httpContextAccessor.HttpContext?.Session", result);
    }

    [Fact]
    public async Task PreservesNullSafety()
    {
        var source = @"
public void Test()
{
    var value = HttpContext.Current.Items[""key""];
}";

        var result = await RewriteAsync(source);
        Assert.Contains("?.", result); // Null-conditional operator
    }
}
```

## Progress Log
- [Created] - Task definition with HttpContext.Current migration implementation
