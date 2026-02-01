using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Rewrites ActionResult types and related method calls from ASP.NET MVC to ASP.NET Core equivalents.
/// Handles return types, Json() calls, HttpNotFound(), HttpStatusCodeResult, and RedirectToRoute().
/// </summary>
public sealed class ActionResultTypeRewriter : CSharpSyntaxRewriter, IActionResultRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public string Rewrite(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return sourceCode;
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _lowestConfidence = 100;

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

        // Normalize whitespace if usings were added
        if (_requiredUsings.Count > 0 && rewritten is CompilationUnitSyntax)
        {
            rewritten = rewritten.NormalizeWhitespace();
        }

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits method declarations to rewrite ActionResult return types.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // First visit children to handle nested content
        var visited = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if return type needs rewriting
        var returnType = visited.ReturnType;
        var newReturnType = RewriteReturnType(returnType);

        if (newReturnType != returnType)
        {
            visited = visited.WithReturnType(newReturnType);
        }

        return visited;
    }

    /// <summary>
    /// Rewrites return type syntax (handles ActionResult, Task&lt;ActionResult&gt;, etc.).
    /// </summary>
    private TypeSyntax RewriteReturnType(TypeSyntax returnType)
    {
        switch (returnType)
        {
            case IdentifierNameSyntax identifierName:
                return RewriteIdentifierType(identifierName);

            case GenericNameSyntax genericName:
                return RewriteGenericType(genericName);

            case QualifiedNameSyntax qualifiedName:
                return RewriteQualifiedType(qualifiedName);

            default:
                return returnType;
        }
    }

    /// <summary>
    /// Rewrites simple identifier types like "ActionResult".
    /// </summary>
    private TypeSyntax RewriteIdentifierType(IdentifierNameSyntax identifierName)
    {
        var typeName = identifierName.Identifier.Text;

        if (!ActionResultMappings.RequiresMapping(typeName))
        {
            return identifierName;
        }

        var mapped = ActionResultMappings.GetMapping(typeName);
        if (mapped == null)
        {
            return identifierName;
        }

        _requiredUsings.Add("Microsoft.AspNetCore.Mvc");
        _diagnostics.Add(new RewriterDiagnostic(
            $"Rewritten return type: '{typeName}' → '{mapped}'",
            RewriterDiagnosticSeverity.Info));

        return SyntaxFactory.IdentifierName(mapped)
            .WithTriviaFrom(identifierName);
    }

    /// <summary>
    /// Rewrites generic types like "Task&lt;ActionResult&gt;".
    /// </summary>
    private TypeSyntax RewriteGenericType(GenericNameSyntax genericName)
    {
        // Rewrite type arguments recursively
        var hasChanges = false;
        var newTypeArgs = new List<TypeSyntax>();

        foreach (var typeArg in genericName.TypeArgumentList.Arguments)
        {
            var rewritten = RewriteReturnType(typeArg);
            if (rewritten != typeArg)
            {
                hasChanges = true;
            }
            newTypeArgs.Add(rewritten);
        }

        if (!hasChanges)
        {
            return genericName;
        }

        var newTypeArgList = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(newTypeArgs));

        return genericName.WithTypeArgumentList(newTypeArgList);
    }

    /// <summary>
    /// Rewrites qualified types like "System.Web.Mvc.ActionResult".
    /// </summary>
    private TypeSyntax RewriteQualifiedType(QualifiedNameSyntax qualifiedName)
    {
        var rightName = qualifiedName.Right;

        if (rightName is IdentifierNameSyntax identifierName)
        {
            var typeName = identifierName.Identifier.Text;

            if (ActionResultMappings.RequiresMapping(typeName))
            {
                var mapped = ActionResultMappings.GetMapping(typeName);
                if (mapped != null)
                {
                    _requiredUsings.Add("Microsoft.AspNetCore.Mvc");
                    _diagnostics.Add(new RewriterDiagnostic(
                        $"Rewritten qualified return type: '{qualifiedName}' → '{mapped}'",
                        RewriterDiagnosticSeverity.Info));

                    return SyntaxFactory.IdentifierName(mapped)
                        .WithTriviaFrom(qualifiedName);
                }
            }
        }

        return qualifiedName;
    }

    /// <summary>
    /// Visits invocation expressions to rewrite method calls like Json(), HttpNotFound(), etc.
    /// </summary>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // First visit children
        var visited = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);
        if (visited == null)
        {
            return null;
        }

        // Extract method name
        var methodName = ExtractMethodName(visited.Expression);

        if (string.IsNullOrWhiteSpace(methodName))
        {
            return visited;
        }

        // Handle different method rewrites
        return methodName switch
        {
            "Json" => RewriteJsonMethod(visited),
            "HttpNotFound" => RewriteHttpNotFoundMethod(visited),
            "RedirectToRoute" => RewriteRedirectToRouteMethod(visited),
            _ => visited
        };
    }

    /// <summary>
    /// Rewrites Json() calls to remove JsonRequestBehavior parameter.
    /// </summary>
    private InvocationExpressionSyntax RewriteJsonMethod(InvocationExpressionSyntax node)
    {
        var argList = node.ArgumentList;

        // Check if there are 2+ arguments (data + JsonRequestBehavior)
        if (argList.Arguments.Count < 2)
        {
            return node;
        }

        // Check if last argument is JsonRequestBehavior
        var lastArg = argList.Arguments.Last();
        var lastArgText = lastArg.ToString();

        if (!lastArgText.Contains("JsonRequestBehavior"))
        {
            return node;
        }

        // Remove the last argument
        var newArgs = SyntaxFactory.SeparatedList(
            argList.Arguments.Take(argList.Arguments.Count - 1));

        var newArgList = argList.WithArguments(newArgs);

        _diagnostics.Add(new RewriterDiagnostic(
            "Removed JsonRequestBehavior parameter from Json() call",
            RewriterDiagnosticSeverity.Info));

        return node.WithArgumentList(newArgList);
    }

    /// <summary>
    /// Rewrites HttpNotFound() to NotFound().
    /// </summary>
    private InvocationExpressionSyntax RewriteHttpNotFoundMethod(InvocationExpressionSyntax node)
    {
        var newExpression = SyntaxFactory.IdentifierName("NotFound")
            .WithTriviaFrom(node.Expression);

        _diagnostics.Add(new RewriterDiagnostic(
            "Rewritten method call: HttpNotFound() → NotFound()",
            RewriterDiagnosticSeverity.Info));

        return node.WithExpression(newExpression);
    }

    /// <summary>
    /// Rewrites RedirectToRoute(new { controller = "X", action = "Y" }) to RedirectToAction("Y", "X").
    /// </summary>
    private InvocationExpressionSyntax RewriteRedirectToRouteMethod(InvocationExpressionSyntax node)
    {
        var argList = node.ArgumentList;

        if (argList.Arguments.Count == 0)
        {
            return node;
        }

        // Try to extract controller and action from anonymous object
        var firstArg = argList.Arguments[0];
        var (action, controller) = ExtractRouteValues(firstArg.Expression);

        if (action == null)
        {
            // Can't parse - lower confidence but still attempt conversion
            _lowestConfidence = Math.Min(_lowestConfidence, 70);
            _diagnostics.Add(new RewriterDiagnostic(
                "Could not parse RedirectToRoute anonymous object - manual review recommended",
                RewriterDiagnosticSeverity.Warning));
            return node;
        }

        // Build new argument list: RedirectToAction("action", "controller")
        var newArgs = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(action)))
        };

        if (controller != null)
        {
            newArgs.Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(controller))));
        }

        var newArgList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(newArgs));

        var newExpression = SyntaxFactory.IdentifierName("RedirectToAction")
            .WithTriviaFrom(node.Expression);

        _lowestConfidence = Math.Min(_lowestConfidence, 90);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Rewritten RedirectToRoute to RedirectToAction(\"{action}\"{(controller != null ? $", \"{controller}\"" : "")})",
            RewriterDiagnosticSeverity.Info));

        return node
            .WithExpression(newExpression)
            .WithArgumentList(newArgList);
    }

    /// <summary>
    /// Visits object creation expressions to rewrite "new HttpStatusCodeResult(404)".
    /// </summary>
    public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // First visit children
        var visited = (ObjectCreationExpressionSyntax?)base.VisitObjectCreationExpression(node);
        if (visited == null)
        {
            return null;
        }

        var typeName = ExtractTypeName(visited.Type);

        if (typeName == "HttpStatusCodeResult")
        {
            return RewriteHttpStatusCodeResult(visited);
        }

        return visited;
    }

    /// <summary>
    /// Rewrites "new HttpStatusCodeResult(404)" to "StatusCode(404)".
    /// </summary>
    private InvocationExpressionSyntax RewriteHttpStatusCodeResult(ObjectCreationExpressionSyntax node)
    {
        var argList = node.ArgumentList;

        if (argList == null || argList.Arguments.Count == 0)
        {
            // No arguments - can't convert properly
            _lowestConfidence = Math.Min(_lowestConfidence, 70);
            _diagnostics.Add(new RewriterDiagnostic(
                "HttpStatusCodeResult without status code - manual review recommended",
                RewriterDiagnosticSeverity.Warning));

            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName("StatusCode"));
        }

        var statusCodeArg = argList.Arguments[0];

        var newInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName("StatusCode"),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(statusCodeArg)))
            .WithTriviaFrom(node);

        _diagnostics.Add(new RewriterDiagnostic(
            $"Rewritten: new HttpStatusCodeResult(...) → StatusCode(...)",
            RewriterDiagnosticSeverity.Info));

        return newInvocation;
    }

    /// <summary>
    /// Extracts method name from invocation expression.
    /// </summary>
    private static string? ExtractMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Extracts type name from type syntax.
    /// </summary>
    private static string? ExtractTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Extracts controller and action from RedirectToRoute anonymous object.
    /// </summary>
    private static (string? action, string? controller) ExtractRouteValues(ExpressionSyntax expression)
    {
        if (expression is not AnonymousObjectCreationExpressionSyntax anonymousObject)
        {
            return (null, null);
        }

        string? action = null;
        string? controller = null;

        foreach (var initializer in anonymousObject.Initializers)
        {
            if (initializer.NameEquals == null)
            {
                continue;
            }

            var propertyName = initializer.NameEquals.Name.Identifier.Text;
            var value = ExtractStringValue(initializer.Expression);

            if (propertyName.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                action = value;
            }
            else if (propertyName.Equals("controller", StringComparison.OrdinalIgnoreCase))
            {
                controller = value;
            }
        }

        return (action, controller);
    }

    /// <summary>
    /// Extracts string value from expression (literal or identifier).
    /// </summary>
    private static string? ExtractStringValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => literal.Token.ValueText,
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

        // Find existing compilation unit
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
}
