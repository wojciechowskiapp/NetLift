using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Transforms convention-based routing to attribute routing.
/// Adds [Route] to controllers and HTTP method attributes to actions.
/// </summary>
public sealed class AttributeRoutingTransformer : CSharpSyntaxRewriter, IAttributeRoutingTransformer
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private int _lowestConfidence = 100;
    private IReadOnlyList<RouteDefinition>? _routes;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public string Rewrite(string sourceCode, IReadOnlyList<RouteDefinition>? routes = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return sourceCode;
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _lowestConfidence = 100;
        _routes = routes;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Add new using directives
        rewritten = AddRequiredUsings(rewritten);

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits class declarations to add [Route] attribute to controllers.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // First visit children to handle nested classes and methods
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this is a controller class
        if (!IsControllerClass(visited))
        {
            return visited;
        }

        // Check if [Route] attribute already exists
        if (HasRouteAttribute(visited))
        {
            return visited;
        }

        // Add [Route("[controller]")] attribute
        var result = AddRouteAttribute(visited);

        _requiredUsings.Add("Microsoft.AspNetCore.Mvc");
        _diagnostics.Add(new RewriterDiagnostic(
            $"Added [Route(\"[controller]\")] to {visited.Identifier.Text}",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Visits method declarations to add HTTP method attributes to action methods.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // First visit children
        var visited = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this is an action method (public method in a controller)
        if (!IsActionMethod(visited))
        {
            return visited;
        }

        // Check if HTTP method attribute already exists
        if (HasHttpMethodAttribute(visited))
        {
            return visited;
        }

        // Infer HTTP method from method name
        var httpMethod = InferHttpMethod(visited.Identifier.Text);
        var attributeName = GetHttpMethodAttributeName(httpMethod);

        // Build route template for the attribute (if needed)
        var routeTemplate = BuildRouteTemplate(visited);

        // Add HTTP method attribute
        var result = AddHttpMethodAttribute(visited, attributeName, routeTemplate);

        _requiredUsings.Add("Microsoft.AspNetCore.Mvc");

        var message = string.IsNullOrEmpty(routeTemplate)
            ? $"Added [{attributeName}] to {visited.Identifier.Text}"
            : $"Added [{attributeName}(\"{routeTemplate}\")] to {visited.Identifier.Text}";

        _diagnostics.Add(new RewriterDiagnostic(message, RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Checks if a class is a controller class.
    /// </summary>
    private static bool IsControllerClass(ClassDeclarationSyntax node)
    {
        if (node.BaseList == null)
        {
            return false;
        }

        foreach (var baseType in node.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            if (typeName.Contains("Controller", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a method is a public action method.
    /// </summary>
    private static bool IsActionMethod(MethodDeclarationSyntax node)
    {
        // Must be public
        if (!node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            return false;
        }

        // Must not be static
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        // Return type should be ActionResult-like (but we don't strictly enforce this)
        return true;
    }

    /// <summary>
    /// Checks if a class has a [Route] attribute.
    /// </summary>
    private static bool HasRouteAttribute(ClassDeclarationSyntax node)
    {
        return HasAttribute(node.AttributeLists, "Route");
    }

    /// <summary>
    /// Checks if a method has an HTTP method attribute.
    /// </summary>
    private static bool HasHttpMethodAttribute(MethodDeclarationSyntax node)
    {
        return HasAttribute(node.AttributeLists, "HttpGet") ||
               HasAttribute(node.AttributeLists, "HttpPost") ||
               HasAttribute(node.AttributeLists, "HttpPut") ||
               HasAttribute(node.AttributeLists, "HttpDelete") ||
               HasAttribute(node.AttributeLists, "HttpPatch") ||
               HasAttribute(node.AttributeLists, "HttpHead") ||
               HasAttribute(node.AttributeLists, "HttpOptions");
    }

    /// <summary>
    /// Checks if an attribute list contains a specific attribute.
    /// </summary>
    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
    {
        return attributeLists
            .SelectMany(al => al.Attributes)
            .Any(a =>
            {
                var name = a.Name.ToString();
                return name.Equals(attributeName, StringComparison.Ordinal) ||
                       name.Equals(attributeName + "Attribute", StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Adds [Route("[controller]")] attribute to a class.
    /// </summary>
    private static ClassDeclarationSyntax AddRouteAttribute(ClassDeclarationSyntax node)
    {
        var routeArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal("[controller]")));

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Route"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(routeArgument)));

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return node.WithAttributeLists(
            node.AttributeLists.Add(attributeList));
    }

    /// <summary>
    /// Adds HTTP method attribute to a method.
    /// </summary>
    private static MethodDeclarationSyntax AddHttpMethodAttribute(
        MethodDeclarationSyntax node,
        string attributeName,
        string? routeTemplate)
    {
        AttributeSyntax attribute;

        if (string.IsNullOrEmpty(routeTemplate))
        {
            // [HttpGet]
            attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName(attributeName));
        }
        else
        {
            // [HttpGet("{id}")]
            var routeArgument = SyntaxFactory.AttributeArgument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(routeTemplate)));

            attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName(attributeName),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(routeArgument)));
        }

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return node.WithAttributeLists(
            node.AttributeLists.Add(attributeList));
    }

    /// <summary>
    /// Infers HTTP method from method name.
    /// </summary>
    private HttpMethodType InferHttpMethod(string methodName)
    {
        var lowerName = methodName.ToLowerInvariant();

        // GET patterns
        if (lowerName.StartsWith("get", StringComparison.Ordinal) ||
            lowerName.Equals("index", StringComparison.Ordinal) ||
            lowerName.Equals("details", StringComparison.Ordinal) ||
            lowerName.Equals("list", StringComparison.Ordinal) ||
            lowerName.Equals("show", StringComparison.Ordinal))
        {
            return HttpMethodType.Get;
        }

        // POST patterns
        if (lowerName.StartsWith("post", StringComparison.Ordinal) ||
            lowerName.StartsWith("create", StringComparison.Ordinal) ||
            lowerName.Equals("add", StringComparison.Ordinal) ||
            lowerName.Equals("insert", StringComparison.Ordinal))
        {
            return HttpMethodType.Post;
        }

        // PUT patterns
        if (lowerName.StartsWith("put", StringComparison.Ordinal) ||
            lowerName.StartsWith("update", StringComparison.Ordinal) ||
            lowerName.StartsWith("edit", StringComparison.Ordinal) ||
            lowerName.Equals("modify", StringComparison.Ordinal))
        {
            return HttpMethodType.Put;
        }

        // DELETE patterns
        if (lowerName.StartsWith("delete", StringComparison.Ordinal) ||
            lowerName.StartsWith("remove", StringComparison.Ordinal) ||
            lowerName.Equals("destroy", StringComparison.Ordinal))
        {
            return HttpMethodType.Delete;
        }

        // Default to GET
        _lowestConfidence = Math.Min(_lowestConfidence, 85);
        return HttpMethodType.Get;
    }

    /// <summary>
    /// Gets the attribute name for an HTTP method type.
    /// </summary>
    private static string GetHttpMethodAttributeName(HttpMethodType httpMethod)
    {
        return httpMethod switch
        {
            HttpMethodType.Get => "HttpGet",
            HttpMethodType.Post => "HttpPost",
            HttpMethodType.Put => "HttpPut",
            HttpMethodType.Delete => "HttpDelete",
            _ => "HttpGet"
        };
    }

    /// <summary>
    /// Builds route template for a method based on its parameters.
    /// </summary>
    private string? BuildRouteTemplate(MethodDeclarationSyntax method)
    {
        if (method.ParameterList.Parameters.Count == 0)
        {
            return null;
        }

        // Check for simple ID parameter pattern
        var idParam = method.ParameterList.Parameters
            .FirstOrDefault(p => p.Identifier.Text.Equals("id", StringComparison.OrdinalIgnoreCase));

        if (idParam != null)
        {
            // Convert type to route constraint
            var constraint = ConvertTypeToConstraint(idParam.Type?.ToString());

            if (!string.IsNullOrEmpty(constraint))
            {
                _lowestConfidence = Math.Min(_lowestConfidence, 90);
                return $"{{id:{constraint}}}";
            }

            return "{id}";
        }

        // For other parameters, don't auto-generate complex routes
        return null;
    }

    /// <summary>
    /// Converts a C# type to a route constraint.
    /// </summary>
    private static string? ConvertTypeToConstraint(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        return typeName.ToLowerInvariant() switch
        {
            "int" => "int",
            "long" => "long",
            "guid" => "guid",
            "bool" => "bool",
            "decimal" => "decimal",
            "double" => "double",
            "float" => "float",
            "datetime" => "datetime",
            _ => null
        };
    }

    /// <summary>
    /// Adds required using directives that were identified during rewriting.
    /// </summary>
    private SyntaxNode AddRequiredUsings(SyntaxNode root)
    {
        if (_requiredUsings.Count == 0)
        {
            return root;
        }

        if (root is CompilationUnitSyntax compilationUnit)
        {
            var existingUsings = compilationUnit.Usings
                .Select(u => u.Name?.ToString())
                .Where(n => n != null)
                .ToHashSet(StringComparer.Ordinal);

            var newUsings = _requiredUsings
                .Where(ns => !existingUsings.Contains(ns) && !string.IsNullOrWhiteSpace(ns))
                .Select(ns => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                    .NormalizeWhitespace())
                .ToList();

            if (newUsings.Count > 0)
            {
                return compilationUnit.AddUsings(newUsings.ToArray());
            }
        }

        return root;
    }

    /// <summary>
    /// HTTP method types.
    /// </summary>
    private enum HttpMethodType
    {
        Get,
        Post,
        Put,
        Delete
    }
}
