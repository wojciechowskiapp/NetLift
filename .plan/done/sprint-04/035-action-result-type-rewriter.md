# Task 035: ActionResult Type Rewriter

## Meta
- **Priority**: P0
- **Estimate**: 6 points
- **Sprint**: 4
- **Dependencies**: 034
- **Status**: Not Started

## Description
Implement Roslyn SyntaxRewriter to convert MVC5 ActionResult types to ASP.NET Core IActionResult and ActionResult<T> patterns, including return type inference and generic action results.

## Acceptance Criteria
- [ ] ActionResultTypeRewriter implemented
- [ ] ActionResult → IActionResult conversion
- [ ] Infer ActionResult<T> for typed returns
- [ ] Update return statements (View(), Json(), etc.)
- [ ] Convert HttpStatusCodeResult → StatusCodeResult
- [ ] Convert HttpNotFoundResult → NotFoundResult
- [ ] Convert RedirectToRouteResult → RedirectToActionResult
- [ ] Handle JsonResult with JsonRequestBehavior
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### ActionResult Type Mappings
```csharp
public static class ActionResultMappings
{
    public static readonly Dictionary<string, string> TypeMap = new()
    {
        // Base types
        ["ActionResult"] = "IActionResult",

        // Specific result types (most are compatible)
        ["ViewResult"] = "ViewResult",
        ["PartialViewResult"] = "PartialViewResult",
        ["JsonResult"] = "JsonResult",
        ["RedirectResult"] = "RedirectResult",
        ["RedirectToRouteResult"] = "RedirectToActionResult",
        ["ContentResult"] = "ContentResult",
        ["FileResult"] = "FileResult",
        ["EmptyResult"] = "EmptyResult",

        // HTTP status results
        ["HttpStatusCodeResult"] = "StatusCodeResult",
        ["HttpNotFoundResult"] = "NotFoundResult",
        ["HttpUnauthorizedResult"] = "UnauthorizedResult",

        // Web API results
        ["IHttpActionResult"] = "IActionResult"
    };

    public static readonly HashSet<string> HelperMethodsRequiringUpdate = new()
    {
        "Json",           // JsonRequestBehavior parameter removed
        "RedirectToRoute", // Changed to RedirectToAction
        "HttpNotFound",   // Changed to NotFound
        "HttpStatusCode"  // Changed to StatusCode
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
/// Rewrites MVC5 ActionResult types to ASP.NET Core equivalents
/// </summary>
public class ActionResultTypeRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, string> _typeMap;

    public ActionResultTypeRewriter(
        SemanticModel semanticModel,
        Dictionary<string, string>? customMappings = null)
    {
        _semanticModel = semanticModel;
        _typeMap = customMappings ?? ActionResultMappings.TypeMap;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Check if return type is an ActionResult
        var returnType = node.ReturnType;

        if (returnType is IdentifierNameSyntax identifier)
        {
            var typeName = identifier.Identifier.Text;

            if (_typeMap.TryGetValue(typeName, out var newTypeName))
            {
                // Check if we can infer generic ActionResult<T>
                var inferredType = InferGenericActionResult(node);

                if (inferredType != null)
                {
                    // Use ActionResult<T>
                    var genericType = SyntaxFactory.GenericName(
                        SyntaxFactory.Identifier("ActionResult"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.ParseTypeName(inferredType))));

                    node = node.WithReturnType(genericType.WithTriviaFrom(returnType));
                }
                else
                {
                    // Use IActionResult
                    var newReturnType = SyntaxFactory.IdentifierName(newTypeName)
                        .WithTriviaFrom(returnType);

                    node = node.WithReturnType(newReturnType);
                }
            }
        }
        else if (returnType is GenericNameSyntax generic)
        {
            // Handle Task<ActionResult> → Task<IActionResult>
            if (generic.Identifier.Text == "Task")
            {
                var typeArg = generic.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArg is IdentifierNameSyntax typeArgId &&
                    _typeMap.TryGetValue(typeArgId.Identifier.Text, out var newTypeName))
                {
                    // Check for generic inference
                    var inferredType = InferGenericActionResult(node);

                    TypeSyntax newTypeArg;
                    if (inferredType != null)
                    {
                        // Task<ActionResult<T>>
                        newTypeArg = SyntaxFactory.GenericName(
                            SyntaxFactory.Identifier("ActionResult"))
                            .WithTypeArgumentList(
                                SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.ParseTypeName(inferredType))));
                    }
                    else
                    {
                        // Task<IActionResult>
                        newTypeArg = SyntaxFactory.IdentifierName(newTypeName);
                    }

                    var newGeneric = generic.WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(newTypeArg)));

                    node = node.WithReturnType(newGeneric.WithTriviaFrom(returnType));
                }
            }
        }

        return base.VisitMethodDeclaration(node);
    }

    private string? InferGenericActionResult(MethodDeclarationSyntax method)
    {
        if (method.Body == null)
            return null;

        // Analyze return statements to infer type
        var returnStatements = method.Body.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .ToList();

        if (!returnStatements.Any())
            return null;

        // Look for typed returns (e.g., return Ok(model))
        var typedReturns = returnStatements
            .Select(r => InferTypeFromReturnStatement(r))
            .Where(t => t != null)
            .Distinct()
            .ToList();

        // Only use generic if all returns are consistent
        if (typedReturns.Count == 1)
            return typedReturns[0];

        return null;
    }

    private string? InferTypeFromReturnStatement(ReturnStatementSyntax returnStatement)
    {
        if (returnStatement.Expression is InvocationExpressionSyntax invocation)
        {
            var methodName = GetMethodName(invocation);

            if (methodName == "Ok" || methodName == "Created" || methodName == "Accepted")
            {
                // Get the argument type
                if (invocation.ArgumentList.Arguments.Count > 0)
                {
                    var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                    var typeInfo = _semanticModel.GetTypeInfo(firstArg);

                    if (typeInfo.Type != null)
                        return typeInfo.Type.ToDisplayString();
                }
            }
        }

        return null;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var methodName = GetMethodName(node);

        return methodName switch
        {
            "Json" => RewriteJsonCall(node),
            "RedirectToRoute" => RewriteRedirectToRoute(node),
            "HttpNotFound" => RewriteHttpNotFound(node),
            "HttpStatusCode" => RewriteHttpStatusCode(node),
            _ => base.VisitInvocationExpression(node)
        };
    }

    private InvocationExpressionSyntax? RewriteJsonCall(InvocationExpressionSyntax node)
    {
        // Remove JsonRequestBehavior parameter
        // Before: Json(data, JsonRequestBehavior.AllowGet)
        // After: Json(data)

        if (node.ArgumentList.Arguments.Count <= 1)
            return node; // Already correct

        var args = node.ArgumentList.Arguments;

        // Check if second parameter is JsonRequestBehavior
        if (args.Count >= 2)
        {
            var secondArg = args[1].Expression;

            if (secondArg is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax id &&
                id.Identifier.Text == "JsonRequestBehavior")
            {
                // Remove the JsonRequestBehavior argument
                var newArgs = SyntaxFactory.SeparatedList(
                    args.Take(1).Concat(args.Skip(2)));

                return node.WithArgumentList(
                    node.ArgumentList.WithArguments(newArgs));
            }
        }

        return node;
    }

    private InvocationExpressionSyntax RewriteRedirectToRoute(InvocationExpressionSyntax node)
    {
        // RedirectToRoute → RedirectToAction
        // Before: RedirectToRoute(new { controller = "Home", action = "Index" })
        // After: RedirectToAction("Index", "Home")

        if (node.Expression is not IdentifierNameSyntax identifier)
            return node;

        // Change method name
        var newExpression = SyntaxFactory.IdentifierName("RedirectToAction")
            .WithTriviaFrom(identifier);

        // Try to parse route values
        var (action, controller) = ExtractRouteValues(node);

        if (action != null && controller != null)
        {
            // Create new argument list
            var newArgs = SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(action))),
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(controller)))
                }));

            return node
                .WithExpression(newExpression)
                .WithArgumentList(newArgs);
        }

        // Fallback: just change the method name
        return node.WithExpression(newExpression);
    }

    private InvocationExpressionSyntax RewriteHttpNotFound(InvocationExpressionSyntax node)
    {
        // HttpNotFound() → NotFound()
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            var newExpression = SyntaxFactory.IdentifierName("NotFound")
                .WithTriviaFrom(identifier);

            return node.WithExpression(newExpression);
        }

        return node;
    }

    private InvocationExpressionSyntax RewriteHttpStatusCode(InvocationExpressionSyntax node)
    {
        // new HttpStatusCodeResult(404) → StatusCode(404)
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            var newExpression = SyntaxFactory.IdentifierName("StatusCode")
                .WithTriviaFrom(identifier);

            return node.WithExpression(newExpression);
        }

        return node;
    }

    private (string? action, string? controller) ExtractRouteValues(
        InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return (null, null);

        var arg = invocation.ArgumentList.Arguments[0].Expression;

        if (arg is AnonymousObjectCreationExpressionSyntax anonObj)
        {
            string? action = null;
            string? controller = null;

            foreach (var initializer in anonObj.Initializers)
            {
                if (initializer is AssignmentExpressionSyntax assignment &&
                    assignment.Left is IdentifierNameSyntax property)
                {
                    var propertyName = property.Identifier.Text;
                    var value = assignment.Right;

                    if (propertyName == "action" &&
                        value is LiteralExpressionSyntax actionLiteral)
                    {
                        action = actionLiteral.Token.ValueText;
                    }
                    else if (propertyName == "controller" &&
                             value is LiteralExpressionSyntax controllerLiteral)
                    {
                        controller = controllerLiteral.Token.ValueText;
                    }
                }
            }

            return (action, controller);
        }

        return (null, null);
    }

    private string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            _ => null
        };
    }

    public override SyntaxNode? VisitObjectCreationExpression(
        ObjectCreationExpressionSyntax node)
    {
        // Handle: new HttpStatusCodeResult(404) → StatusCode(404)
        if (node.Type is IdentifierNameSyntax typeName &&
            _typeMap.TryGetValue(typeName.Identifier.Text, out var newTypeName))
        {
            // Convert to method call instead of object creation
            if (typeName.Identifier.Text == "HttpStatusCodeResult")
            {
                var invocation = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("StatusCode"),
                    node.ArgumentList ?? SyntaxFactory.ArgumentList());

                return invocation.WithTriviaFrom(node);
            }
            else if (typeName.Identifier.Text == "HttpNotFoundResult")
            {
                var invocation = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("NotFound"),
                    SyntaxFactory.ArgumentList());

                return invocation.WithTriviaFrom(node);
            }
        }

        return base.VisitObjectCreationExpression(node);
    }
}
```

### Migration Examples

#### Example 1: Basic ActionResult
```csharp
// BEFORE
public ActionResult Index()
{
    return View();
}

// AFTER
public IActionResult Index()
{
    return View();
}
```

#### Example 2: Typed ActionResult
```csharp
// BEFORE
public ActionResult GetProduct(int id)
{
    var product = _service.GetById(id);
    if (product == null)
        return HttpNotFound();

    return Json(product, JsonRequestBehavior.AllowGet);
}

// AFTER
public ActionResult<Product> GetProduct(int id)
{
    var product = _service.GetById(id);
    if (product == null)
        return NotFound();

    return Json(product);
}
```

#### Example 3: Async ActionResult
```csharp
// BEFORE
public async Task<ActionResult> CreateAsync(ProductViewModel model)
{
    var product = await _service.CreateAsync(model);
    return RedirectToRoute(new { controller = "Products", action = "Details", id = product.Id });
}

// AFTER
public async Task<IActionResult> CreateAsync(ProductViewModel model)
{
    var product = await _service.CreateAsync(model);
    return RedirectToAction("Details", "Products", new { id = product.Id });
}
```

#### Example 4: HTTP Status Results
```csharp
// BEFORE
public ActionResult Delete(int id)
{
    if (!_service.Exists(id))
        return new HttpStatusCodeResult(404);

    _service.Delete(id);
    return new HttpStatusCodeResult(204);
}

// AFTER
public IActionResult Delete(int id)
{
    if (!_service.Exists(id))
        return StatusCode(404);

    _service.Delete(id);
    return StatusCode(204);
}
```

### Unit Tests
```csharp
public class ActionResultTypeRewriterTests
{
    [Fact]
    public async Task RewritesActionResultToIActionResult()
    {
        var source = @"
public class TestController : Controller
{
    public ActionResult Index()
    {
        return View();
    }
}";

        var expected = @"
public class TestController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}";

        var result = await RewriteAsync(source);
        Assert.Contains("IActionResult Index()", result);
    }

    [Fact]
    public async Task InfersGenericActionResult()
    {
        var source = @"
public class TestController : Controller
{
    public ActionResult GetProduct()
    {
        var product = new Product();
        return Ok(product);
    }
}";

        var result = await RewriteAsync(source);
        Assert.Contains("ActionResult<Product> GetProduct()", result);
    }

    [Fact]
    public async Task RemovesJsonRequestBehavior()
    {
        var source = @"
public ActionResult GetData()
{
    return Json(data, JsonRequestBehavior.AllowGet);
}";

        var result = await RewriteAsync(source);
        Assert.Contains("Json(data)", result);
        Assert.DoesNotContain("JsonRequestBehavior", result);
    }

    [Fact]
    public async Task RewritesHttpNotFound()
    {
        var source = @"
public ActionResult Get(int id)
{
    if (id < 0)
        return HttpNotFound();
    return Ok();
}";

        var result = await RewriteAsync(source);
        Assert.Contains("NotFound()", result);
        Assert.DoesNotContain("HttpNotFound", result);
    }

    [Fact]
    public async Task RewritesRedirectToRoute()
    {
        var source = @"
public ActionResult Save()
{
    return RedirectToRoute(new { controller = ""Home"", action = ""Index"" });
}";

        var result = await RewriteAsync(source);
        Assert.Contains("RedirectToAction(\"Index\", \"Home\")", result);
    }

    [Fact]
    public async Task RewritesHttpStatusCodeResult()
    {
        var source = @"
public ActionResult Delete()
{
    return new HttpStatusCodeResult(204);
}";

        var result = await RewriteAsync(source);
        Assert.Contains("StatusCode(204)", result);
    }

    [Fact]
    public async Task HandlesAsyncMethods()
    {
        var source = @"
public async Task<ActionResult> GetAsync()
{
    var data = await _service.GetAsync();
    return Ok(data);
}";

        var result = await RewriteAsync(source);
        Assert.Contains("Task<ActionResult<", result);
    }
}
```

## Progress Log
- [Created] - Task definition with Roslyn implementation for ActionResult migration
