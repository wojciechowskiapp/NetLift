using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Transforms action filters from ASP.NET MVC to ASP.NET Core.
/// Handles filter base classes, attribute transformations, and policy generation.
/// </summary>
public sealed class ActionFilterTransformer : CSharpSyntaxRewriter, IActionFilterTransformer
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly List<PolicyDefinition> _generatedPolicies = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<PolicyDefinition> GeneratedPolicies => _generatedPolicies;

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
        _generatedPolicies.Clear();
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
    /// Visits class declarations to rewrite filter base classes.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // First visit children to handle nested content
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if base list needs rewriting
        if (visited.BaseList == null)
        {
            return visited;
        }

        var hasChanges = false;
        var newBaseTypes = new List<BaseTypeSyntax>();

        foreach (var baseType in visited.BaseList.Types)
        {
            var typeName = ExtractTypeName(baseType.Type);

            if (string.IsNullOrWhiteSpace(typeName))
            {
                newBaseTypes.Add(baseType);
                continue;
            }

            if (FilterMigrationMappings.TryGetBaseClassMapping(typeName, out var mappedInterface))
            {
                hasChanges = true;
                var newType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.IdentifierName(mappedInterface!)
                        .WithTriviaFrom(baseType.Type));

                newBaseTypes.Add(newType);

                _requiredUsings.Add("Microsoft.AspNetCore.Mvc.Filters");
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Rewritten filter base class: '{typeName}' → '{mappedInterface}'",
                    RewriterDiagnosticSeverity.Info));
            }
            else
            {
                newBaseTypes.Add(baseType);
            }
        }

        if (!hasChanges)
        {
            return visited;
        }

        var newBaseList = SyntaxFactory.BaseList(
            SyntaxFactory.SeparatedList(newBaseTypes))
            .WithTriviaFrom(visited.BaseList);

        return visited.WithBaseList(newBaseList);
    }

    /// <summary>
    /// Visits method declarations to remove 'override' keyword from filter methods.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // First visit children to handle nested content
        var visited = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this is a filter method with override keyword
        var methodName = visited.Identifier.Text;

        if (!FilterMigrationMappings.MethodMappings.ContainsKey(methodName))
        {
            return visited;
        }

        // Check if method has override modifier
        var overrideToken = visited.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.OverrideKeyword));

        if (overrideToken == default)
        {
            return visited;
        }

        // Remove override modifier
        var newModifiers = visited.Modifiers.Remove(overrideToken);

        _diagnostics.Add(new RewriterDiagnostic(
            $"Removed 'override' keyword from filter method '{methodName}'",
            RewriterDiagnosticSeverity.Info));

        return visited.WithModifiers(newModifiers);
    }

    /// <summary>
    /// Visits attribute lists to transform authorization and error handling attributes.
    /// </summary>
    public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
    {
        // First visit children
        var visited = (AttributeListSyntax?)base.VisitAttributeList(node);
        if (visited == null)
        {
            return null;
        }

        var hasChanges = false;
        var newAttributes = new List<AttributeSyntax>();

        foreach (var attribute in visited.Attributes)
        {
            var attributeName = ExtractAttributeName(attribute);

            if (string.IsNullOrWhiteSpace(attributeName))
            {
                newAttributes.Add(attribute);
                continue;
            }

            // Transform [HandleError] → [TypeFilter(typeof(GlobalExceptionFilter))]
            if (FilterMigrationMappings.IsTypeFilterCandidate(attributeName))
            {
                hasChanges = true;
                var transformed = TransformToTypeFilter(attribute, attributeName);
                newAttributes.Add(transformed);
                continue;
            }

            // Transform [Authorize(Roles = "...")] → [Authorize(Policy = "...")]
            if (attributeName == "Authorize")
            {
                var transformed = TransformAuthorizeAttribute(attribute);
                if (transformed != attribute)
                {
                    hasChanges = true;
                }
                newAttributes.Add(transformed);
                continue;
            }

            newAttributes.Add(attribute);
        }

        if (!hasChanges)
        {
            return visited;
        }

        return visited.WithAttributes(SyntaxFactory.SeparatedList(newAttributes));
    }

    /// <summary>
    /// Transforms [HandleError] to [TypeFilter(typeof(GlobalExceptionFilter))].
    /// </summary>
    private AttributeSyntax TransformToTypeFilter(AttributeSyntax attribute, string originalName)
    {
        // Build: TypeFilter(typeof(GlobalExceptionFilter))
        var typeofArgument = SyntaxFactory.TypeOfExpression(
            SyntaxFactory.IdentifierName("GlobalExceptionFilter"));

        var argument = SyntaxFactory.AttributeArgument(typeofArgument);

        var newAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("TypeFilter"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(argument)))
            .WithTriviaFrom(attribute);

        _requiredUsings.Add("Microsoft.AspNetCore.Mvc");
        _lowestConfidence = Math.Min(_lowestConfidence, 90);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed [{originalName}] → [TypeFilter(typeof(GlobalExceptionFilter))]",
            RewriterDiagnosticSeverity.Info));

        return newAttribute;
    }

    /// <summary>
    /// Transforms [Authorize(Roles = "Admin,Manager")] to [Authorize(Policy = "AdminManagerPolicy")].
    /// </summary>
    private AttributeSyntax TransformAuthorizeAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0)
        {
            // Simple [Authorize] - no transformation needed
            return attribute;
        }

        // Look for Roles argument
        AttributeArgumentSyntax? rolesArgument = null;
        var otherArguments = new List<AttributeArgumentSyntax>();

        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            if (arg.NameEquals?.Name.Identifier.Text == "Roles")
            {
                rolesArgument = arg;
            }
            else
            {
                otherArguments.Add(arg);
            }
        }

        if (rolesArgument == null)
        {
            // No Roles argument - no transformation needed
            return attribute;
        }

        // Extract roles from the argument
        var roles = ExtractRolesFromArgument(rolesArgument);

        if (roles == null || roles.Length == 0)
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 70);
            _diagnostics.Add(new RewriterDiagnostic(
                "Could not parse Roles argument in [Authorize] - manual review recommended",
                RewriterDiagnosticSeverity.Warning));
            return attribute;
        }

        // Generate policy name
        var policyName = GeneratePolicyName(roles);

        // Track the generated policy
        _generatedPolicies.Add(new PolicyDefinition(policyName, roles));

        // Build new Policy argument
        var policyArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Policy")),
            null,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(policyName)));

        // Combine with other arguments
        var allArguments = new List<AttributeArgumentSyntax> { policyArgument };
        allArguments.AddRange(otherArguments);

        var newArgumentList = SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SeparatedList(allArguments));

        var newAttribute = attribute.WithArgumentList(newArgumentList);

        _lowestConfidence = Math.Min(_lowestConfidence, 90);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed [Authorize(Roles = \"{string.Join(",", roles)}\")] → [Authorize(Policy = \"{policyName}\")]",
            RewriterDiagnosticSeverity.Info));

        return newAttribute;
    }

    /// <summary>
    /// Extracts roles from a Roles attribute argument.
    /// </summary>
    private static string[]? ExtractRolesFromArgument(AttributeArgumentSyntax argument)
    {
        if (argument.Expression is not LiteralExpressionSyntax literal)
        {
            return null;
        }

        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return null;
        }

        var rolesString = literal.Token.ValueText;

        if (string.IsNullOrWhiteSpace(rolesString))
        {
            return null;
        }

        // Split by comma and trim whitespace
        return rolesString
            .Split(',')
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToArray();
    }

    /// <summary>
    /// Generates a policy name from roles.
    /// </summary>
    private static string GeneratePolicyName(string[] roles)
    {
        if (roles.Length == 0)
        {
            return "DefaultPolicy";
        }

        if (roles.Length == 1)
        {
            return $"{roles[0]}Policy";
        }

        // Combine multiple roles: "Admin,Manager" → "AdminManagerPolicy"
        return $"{string.Join("", roles)}Policy";
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
    /// Extracts attribute name from attribute syntax.
    /// </summary>
    private static string? ExtractAttributeName(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            _ => null
        };

        if (name == null)
        {
            return null;
        }

        // Remove "Attribute" suffix if present
        return name.EndsWith("Attribute", StringComparison.Ordinal)
            ? name.Substring(0, name.Length - 9)
            : name;
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
