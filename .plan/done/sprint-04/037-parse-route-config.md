# Task 037: Parse RouteConfig.cs and Extract Route Definitions

## Meta
- **Priority**: P1
- **Estimate**: 5 points
- **Sprint**: 4
- **Dependencies**: 033, 034, 035, 036
- **Status**: Not Started

## Description
Implement a Roslyn-based parser for RouteConfig.cs files to extract MVC route definitions. This includes parsing MapRoute calls, extracting route names, URL templates, default values, and constraints. This foundational task enables subsequent attribute routing migration.

## Acceptance Criteria
- [ ] RouteConfigParser class implemented using Roslyn
- [ ] Parse routes.MapRoute() calls and extract parameters
- [ ] Extract route name from first argument
- [ ] Extract URL template/pattern from second argument
- [ ] Extract defaults object (controller, action, id, etc.)
- [ ] Extract constraints if present
- [ ] Handle multiple route registrations in single file
- [ ] Handle AreaRegistration routes
- [ ] Support both anonymous object and RouteValueDictionary defaults
- [ ] Generate RouteDefinition model objects
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### Route Definition Model
```csharp
namespace NetLift.Mvc.Models;

public record RouteDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
    public Dictionary<string, object?> Defaults { get; init; } = new();
    public Dictionary<string, string> Constraints { get; init; } = new();
    public string? AreaName { get; init; }
    public bool IsDefaultRoute { get; init; }
    public Location? SourceLocation { get; init; }
}

public record Location(string FilePath, int Line, int Column);
```

### RouteConfig Parser Implementation
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Parsers;

public class RouteConfigParser
{
    private readonly SemanticModel _semanticModel;

    public RouteConfigParser(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public IReadOnlyList<RouteDefinition> ParseRouteConfig(SyntaxNode root)
    {
        var routes = new List<RouteDefinition>();

        // Find all MapRoute invocations
        var mapRouteCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsMapRouteCall);

        foreach (var call in mapRouteCalls)
        {
            var route = ParseMapRouteCall(call);
            if (route != null)
            {
                routes.Add(route);
            }
        }

        return routes;
    }

    private bool IsMapRouteCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "MapRoute";
        }
        return false;
    }

    private RouteDefinition? ParseMapRouteCall(InvocationExpressionSyntax call)
    {
        var arguments = call.ArgumentList.Arguments;

        if (arguments.Count < 2)
            return null;

        var name = ExtractStringLiteral(arguments[0].Expression);
        var template = ExtractStringLiteral(arguments[1].Expression);

        var defaults = arguments.Count > 2
            ? ExtractDefaults(arguments[2].Expression)
            : new Dictionary<string, object?>();

        var constraints = arguments.Count > 3
            ? ExtractConstraints(arguments[3].Expression)
            : new Dictionary<string, string>();

        var location = call.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new RouteDefinition
        {
            Name = name ?? string.Empty,
            Template = template ?? string.Empty,
            Defaults = defaults,
            Constraints = constraints,
            IsDefaultRoute = name?.Equals("Default", StringComparison.OrdinalIgnoreCase) ?? false,
            SourceLocation = new Location(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1)
        };
    }

    private string? ExtractStringLiteral(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => interpolated.ToString(),
            _ => null
        };
    }

    private Dictionary<string, object?> ExtractDefaults(ExpressionSyntax expression)
    {
        var defaults = new Dictionary<string, object?>();

        if (expression is AnonymousObjectCreationExpressionSyntax anonymousObject)
        {
            foreach (var initializer in anonymousObject.Initializers)
            {
                var name = initializer.NameEquals?.Name.Identifier.Text;
                var value = ExtractDefaultValue(initializer.Expression);

                if (name != null)
                {
                    defaults[name] = value;
                }
            }
        }
        else if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            // Handle RouteValueDictionary initialization
            if (objectCreation.Initializer != null)
            {
                foreach (var expr in objectCreation.Initializer.Expressions)
                {
                    if (expr is AssignmentExpressionSyntax assignment)
                    {
                        var key = ExtractStringLiteral(assignment.Left);
                        var value = ExtractDefaultValue(assignment.Right);
                        if (key != null)
                        {
                            defaults[key] = value;
                        }
                    }
                }
            }
        }

        return defaults;
    }

    private object? ExtractDefaultValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.Value,
            MemberAccessExpressionSyntax member when member.ToString().Contains("UrlParameter.Optional")
                => RouteParameter.Optional,
            _ => expression.ToString()
        };
    }

    private Dictionary<string, string> ExtractConstraints(ExpressionSyntax expression)
    {
        var constraints = new Dictionary<string, string>();

        if (expression is AnonymousObjectCreationExpressionSyntax anonymousObject)
        {
            foreach (var initializer in anonymousObject.Initializers)
            {
                var name = initializer.NameEquals?.Name.Identifier.Text;
                var value = ExtractStringLiteral(initializer.Expression)
                    ?? initializer.Expression.ToString();

                if (name != null)
                {
                    constraints[name] = value;
                }
            }
        }

        return constraints;
    }
}

public static class RouteParameter
{
    public const string Optional = "?";
}
```

### Example Input (RouteConfig.cs)
```csharp
public class RouteConfig
{
    public static void RegisterRoutes(RouteCollection routes)
    {
        routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

        routes.MapRoute(
            name: "Product",
            url: "products/{category}/{id}",
            defaults: new { controller = "Products", action = "Details", id = UrlParameter.Optional },
            constraints: new { id = @"\d+" }
        );

        routes.MapRoute(
            name: "Default",
            url: "{controller}/{action}/{id}",
            defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
        );
    }
}
```

### Expected Output
```csharp
var routes = new List<RouteDefinition>
{
    new RouteDefinition
    {
        Name = "Product",
        Template = "products/{category}/{id}",
        Defaults = new() { ["controller"] = "Products", ["action"] = "Details", ["id"] = "?" },
        Constraints = new() { ["id"] = @"\d+" },
        IsDefaultRoute = false
    },
    new RouteDefinition
    {
        Name = "Default",
        Template = "{controller}/{action}/{id}",
        Defaults = new() { ["controller"] = "Home", ["action"] = "Index", ["id"] = "?" },
        Constraints = new(),
        IsDefaultRoute = true
    }
};
```

### Unit Tests
```csharp
public class RouteConfigParserTests
{
    [Fact]
    public async Task ParsesSimpleMapRoute()
    {
        var source = @"
routes.MapRoute(
    name: ""Default"",
    url: ""{controller}/{action}/{id}"",
    defaults: new { controller = ""Home"", action = ""Index"" }
);";

        var routes = await ParseRoutesAsync(source);

        Assert.Single(routes);
        Assert.Equal("Default", routes[0].Name);
        Assert.Equal("{controller}/{action}/{id}", routes[0].Template);
        Assert.Equal("Home", routes[0].Defaults["controller"]);
    }

    [Fact]
    public async Task ParsesRouteWithConstraints()
    {
        var source = @"
routes.MapRoute(
    name: ""ProductRoute"",
    url: ""products/{id}"",
    defaults: new { controller = ""Products"", action = ""Show"" },
    constraints: new { id = @""\d+"" }
);";

        var routes = await ParseRoutesAsync(source);

        Assert.Single(routes);
        Assert.Equal(@"\d+", routes[0].Constraints["id"]);
    }

    [Fact]
    public async Task ParsesOptionalParameter()
    {
        var source = @"
routes.MapRoute(
    name: ""Test"",
    url: ""{controller}/{id}"",
    defaults: new { id = UrlParameter.Optional }
);";

        var routes = await ParseRoutesAsync(source);

        Assert.Equal("?", routes[0].Defaults["id"]);
    }

    [Fact]
    public async Task ParsesMultipleRoutes()
    {
        var source = @"
routes.MapRoute(""Route1"", ""path1"", new { controller = ""A"" });
routes.MapRoute(""Route2"", ""path2"", new { controller = ""B"" });";

        var routes = await ParseRoutesAsync(source);

        Assert.Equal(2, routes.Count);
    }
}
```

## Progress Log
- [Created] - Task definition with Roslyn parser implementation details
