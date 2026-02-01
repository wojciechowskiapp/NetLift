# Task 038: Convert Convention Routing to Attribute Routing

## Meta
- **Priority**: P1
- **Estimate**: 8 points
- **Sprint**: 4
- **Dependencies**: 037
- **Status**: Not Started

## Description
Implement a Roslyn-based transformer to convert ASP.NET MVC convention-based routing (from RouteConfig.cs) to ASP.NET Core attribute routing using [Route], [HttpGet], [HttpPost], and other HTTP method attributes on controllers and actions.

## Acceptance Criteria
- [ ] AttributeRoutingTransformer class implemented
- [ ] Add [Route] attributes to controllers based on parsed routes
- [ ] Add [HttpGet], [HttpPost], [HttpPut], [HttpDelete] attributes to actions
- [ ] Handle route parameters with constraints ({id:int})
- [ ] Handle optional parameters ({id?})
- [ ] Preserve route names using Name property
- [ ] Generate [ApiController] attribute where appropriate
- [ ] Handle area routing with [Area] attribute
- [ ] Support route prefixes on controllers
- [ ] Unit tests with 95%+ coverage
- [ ] Integration with TASK-037 route definitions

## Technical Notes

### Attribute Routing Transformer
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Transformers;

public class AttributeRoutingTransformer : CSharpSyntaxRewriter
{
    private readonly IReadOnlyList<RouteDefinition> _routes;
    private readonly SemanticModel _semanticModel;

    public AttributeRoutingTransformer(
        IReadOnlyList<RouteDefinition> routes,
        SemanticModel semanticModel)
    {
        _routes = routes;
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Check if this is a controller
        if (!IsController(node))
            return base.VisitClassDeclaration(node);

        var controllerName = GetControllerName(node);
        var matchingRoutes = _routes
            .Where(r => r.Defaults.TryGetValue("controller", out var c) &&
                       c?.ToString()?.Equals(controllerName, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        // Add [Route] attribute to controller
        var routeAttribute = CreateControllerRouteAttribute(matchingRoutes);
        var newNode = AddAttributeToClass(node, routeAttribute);

        // Visit child methods to add action attributes
        return base.VisitClassDeclaration(newNode);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Check if this is an action method
        if (!IsActionMethod(node))
            return base.VisitMethodDeclaration(node);

        var actionName = node.Identifier.Text;
        var httpMethod = InferHttpMethod(node);

        // Create HTTP method attribute
        var httpAttribute = CreateHttpMethodAttribute(httpMethod, actionName);

        // Add route template if custom route exists
        var customRoute = FindCustomRouteForAction(node);
        if (customRoute != null)
        {
            httpAttribute = AddRouteTemplateToAttribute(httpAttribute, customRoute);
        }

        return AddAttributeToMethod(node, httpAttribute);
    }

    private bool IsController(ClassDeclarationSyntax node)
    {
        // Check if inherits from Controller or has Controller suffix
        if (node.Identifier.Text.EndsWith("Controller"))
            return true;

        if (node.BaseList?.Types.Any(t =>
            t.Type.ToString().Contains("Controller")) == true)
            return true;

        return false;
    }

    private string GetControllerName(ClassDeclarationSyntax node)
    {
        var name = node.Identifier.Text;
        return name.EndsWith("Controller")
            ? name.Substring(0, name.Length - "Controller".Length)
            : name;
    }

    private AttributeSyntax CreateControllerRouteAttribute(
        IReadOnlyList<RouteDefinition> routes)
    {
        // Create [Route("[controller]")] or specific route template
        var template = routes.FirstOrDefault()?.Template ?? "[controller]";
        template = ConvertRouteTemplate(template);

        return SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Route"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(template))))));
    }

    private string ConvertRouteTemplate(string template)
    {
        // Convert {controller} to [controller]
        template = template.Replace("{controller}", "[controller]");
        template = template.Replace("{action}", "[action]");

        // Convert optional parameters: {id} with UrlParameter.Optional to {id?}
        // This is handled based on the RouteDefinition defaults

        return template;
    }

    private string InferHttpMethod(MethodDeclarationSyntax node)
    {
        var methodName = node.Identifier.Text.ToLowerInvariant();

        // Infer from method name prefix
        if (methodName.StartsWith("get") || methodName == "index" ||
            methodName == "details" || methodName == "list")
            return "HttpGet";

        if (methodName.StartsWith("post") || methodName.StartsWith("create") ||
            methodName.StartsWith("add"))
            return "HttpPost";

        if (methodName.StartsWith("put") || methodName.StartsWith("update") ||
            methodName.StartsWith("edit"))
            return "HttpPut";

        if (methodName.StartsWith("delete") || methodName.StartsWith("remove"))
            return "HttpDelete";

        // Check for [HttpPost] attribute in existing attributes
        if (node.AttributeLists.SelectMany(a => a.Attributes)
            .Any(a => a.Name.ToString().Contains("HttpPost")))
            return "HttpPost";

        // Default to HttpGet for actions
        return "HttpGet";
    }

    private AttributeSyntax CreateHttpMethodAttribute(
        string httpMethod, string actionName)
    {
        var attributeName = httpMethod; // HttpGet, HttpPost, etc.

        return SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName(attributeName));
    }

    private ClassDeclarationSyntax AddAttributeToClass(
        ClassDeclarationSyntax node, AttributeSyntax attribute)
    {
        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute));

        return node.AddAttributeLists(attributeList);
    }

    private MethodDeclarationSyntax AddAttributeToMethod(
        MethodDeclarationSyntax node, AttributeSyntax attribute)
    {
        // Don't add duplicate attributes
        if (HasAttribute(node, attribute.Name.ToString()))
            return node;

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute));

        return node.AddAttributeLists(attributeList);
    }

    private bool HasAttribute(MethodDeclarationSyntax node, string attributeName)
    {
        return node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains(attributeName));
    }

    private bool IsActionMethod(MethodDeclarationSyntax node)
    {
        // Public, non-static, returns ActionResult or IActionResult
        return node.Modifiers.Any(SyntaxKind.PublicKeyword) &&
               !node.Modifiers.Any(SyntaxKind.StaticKeyword) &&
               IsActionReturnType(node.ReturnType);
    }

    private bool IsActionReturnType(TypeSyntax returnType)
    {
        var typeName = returnType.ToString();
        return typeName.Contains("ActionResult") ||
               typeName.Contains("IActionResult") ||
               typeName.Contains("ViewResult") ||
               typeName.Contains("JsonResult") ||
               typeName.Contains("Task<");
    }

    private string? FindCustomRouteForAction(MethodDeclarationSyntax node)
    {
        // Check for existing [Route] attribute
        var routeAttr = node.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() == "Route");

        if (routeAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression
            is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private AttributeSyntax AddRouteTemplateToAttribute(
        AttributeSyntax attribute, string template)
    {
        var argument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(template)));

        var argumentList = SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SingletonSeparatedList(argument));

        return attribute.WithArgumentList(argumentList);
    }
}
```

### Route Constraint Conversion
```csharp
public static class RouteConstraintConverter
{
    public static string ConvertToInlineConstraint(string paramName, string constraint)
    {
        // Convert legacy constraint patterns to inline constraints
        return constraint switch
        {
            @"\d+" => $"{{{paramName}:int}}",
            @"\d{{4}}" => $"{{{paramName}:regex(\\d{{4}})}}",
            @"[a-zA-Z]+" => $"{{{paramName}:alpha}}",
            @"[0-9a-fA-F-]+" => $"{{{paramName}:guid}}",
            _ => $"{{{paramName}:regex({constraint})}}"
        };
    }

    public static string AddOptionalModifier(string param, bool isOptional)
    {
        if (!isOptional) return param;

        // {id} becomes {id?}
        if (param.EndsWith("}"))
        {
            return param.Insert(param.Length - 1, "?");
        }

        return param;
    }
}
```

### Example Transformation

**Before (Convention Routing):**
```csharp
// RouteConfig.cs
routes.MapRoute(
    name: "ProductDetails",
    url: "products/{category}/{id}",
    defaults: new { controller = "Products", action = "Details", id = UrlParameter.Optional },
    constraints: new { id = @"\d+" }
);

// ProductsController.cs
public class ProductsController : Controller
{
    public ActionResult Details(string category, int? id)
    {
        return View();
    }

    [HttpPost]
    public ActionResult Create(Product product)
    {
        return RedirectToAction("Index");
    }
}
```

**After (Attribute Routing):**
```csharp
[Route("products")]
public class ProductsController : Controller
{
    [HttpGet("{category}/{id:int?}")]
    public IActionResult Details(string category, int? id)
    {
        return View();
    }

    [HttpPost]
    public IActionResult Create(Product product)
    {
        return RedirectToAction("Index");
    }
}
```

### Unit Tests
```csharp
public class AttributeRoutingTransformerTests
{
    [Fact]
    public async Task AddsRouteAttributeToController()
    {
        var source = @"
public class HomeController : Controller
{
    public ActionResult Index() => View();
}";

        var routes = new[] { CreateRoute("Default", "{controller}/{action}") };
        var result = await TransformAsync(source, routes);

        Assert.Contains("[Route(\"[controller]\")]", result);
    }

    [Fact]
    public async Task AddsHttpGetAttributeToGetActions()
    {
        var source = @"
public class ProductsController : Controller
{
    public ActionResult Details(int id) => View();
}";

        var result = await TransformAsync(source, Array.Empty<RouteDefinition>());

        Assert.Contains("[HttpGet]", result);
    }

    [Fact]
    public async Task AddsHttpPostAttributeToPostActions()
    {
        var source = @"
public class ProductsController : Controller
{
    public ActionResult Create(Product p) => View();
}";

        var result = await TransformAsync(source, Array.Empty<RouteDefinition>());

        Assert.Contains("[HttpPost]", result);
    }

    [Fact]
    public async Task ConvertsRouteConstraintsToInlineFormat()
    {
        var routes = new[]
        {
            new RouteDefinition
            {
                Name = "Product",
                Template = "products/{id}",
                Constraints = new() { ["id"] = @"\d+" }
            }
        };

        var source = @"
public class ProductsController : Controller
{
    public ActionResult Show(int id) => View();
}";

        var result = await TransformAsync(source, routes);

        Assert.Contains("{id:int}", result);
    }

    [Fact]
    public async Task HandlesOptionalParameters()
    {
        var routes = new[]
        {
            new RouteDefinition
            {
                Template = "{controller}/{action}/{id}",
                Defaults = new() { ["id"] = RouteParameter.Optional }
            }
        };

        var result = await TransformAsync(GetSampleController(), routes);

        Assert.Contains("{id?}", result);
    }
}
```

## Progress Log
- [Created] - Task definition with attribute routing transformer implementation
