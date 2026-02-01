using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Parsers;

/// <summary>
/// Parses AreaRegistration classes to extract area definitions using Roslyn.
/// Supports both AreaName property override and class name convention (e.g., AdminAreaRegistration -> Admin).
/// </summary>
public sealed class AreaRegistrationParser : IAreaRegistrationParser
{
    /// <inheritdoc />
    public IReadOnlyList<AreaDefinition> Parse(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<AreaDefinition>();
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var areas = new List<AreaDefinition>();

        // Find all classes that inherit from AreaRegistration
        var areaRegistrationClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(IsAreaRegistrationClass);

        foreach (var classDecl in areaRegistrationClasses)
        {
            var area = ParseAreaRegistrationClass(classDecl);
            if (area != null)
            {
                areas.Add(area);
            }
        }

        return areas;
    }

    /// <summary>
    /// Determines if a class declaration inherits from AreaRegistration.
    /// </summary>
    private static bool IsAreaRegistrationClass(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
        {
            return false;
        }

        return classDecl.BaseList.Types.Any(baseType =>
        {
            var typeName = ExtractTypeName(baseType.Type);
            return typeName != null && typeName.Equals("AreaRegistration", StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Parses an AreaRegistration class to extract the area definition.
    /// </summary>
    private static AreaDefinition? ParseAreaRegistrationClass(ClassDeclarationSyntax classDecl)
    {
        var areaName = ExtractAreaName(classDecl);
        if (string.IsNullOrEmpty(areaName))
        {
            return null;
        }

        var routes = ExtractRoutes(classDecl);

        // Use the area name as the route prefix (typically lowercase)
        var routePrefix = areaName;

        return new AreaDefinition
        {
            Name = areaName,
            RoutePrefix = routePrefix,
            Routes = routes,
            SourceFilePath = string.Empty // Will be set by the caller
        };
    }

    /// <summary>
    /// Extracts the area name from the AreaRegistration class.
    /// First tries to find an AreaName property override, then falls back to class name convention.
    /// </summary>
    private static string? ExtractAreaName(ClassDeclarationSyntax classDecl)
    {
        // Look for AreaName property override
        var areaNameProperty = classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text.Equals("AreaName", StringComparison.Ordinal));

        if (areaNameProperty != null)
        {
            // Try to extract the string literal from the getter
            var returnValue = ExtractPropertyReturnValue(areaNameProperty);
            if (!string.IsNullOrEmpty(returnValue))
            {
                return returnValue;
            }
        }

        // Fall back to class name convention: AdminAreaRegistration -> Admin
        var className = classDecl.Identifier.Text;
        if (className.EndsWith("AreaRegistration", StringComparison.Ordinal))
        {
            return className.Substring(0, className.Length - "AreaRegistration".Length);
        }

        return null;
    }

    /// <summary>
    /// Extracts the return value from a property getter (if it's a string literal).
    /// </summary>
    private static string? ExtractPropertyReturnValue(PropertyDeclarationSyntax property)
    {
        // Check for expression-bodied property: public override string AreaName => "Admin";
        if (property.ExpressionBody != null)
        {
            return ExtractStringLiteral(property.ExpressionBody.Expression);
        }

        // Check for getter with return statement
        var getter = property.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

        if (getter?.Body != null)
        {
            var returnStatement = getter.Body.Statements
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();

            if (returnStatement?.Expression != null)
            {
                return ExtractStringLiteral(returnStatement.Expression);
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts routes from the RegisterArea method.
    /// </summary>
    private static List<RouteDefinition> ExtractRoutes(ClassDeclarationSyntax classDecl)
    {
        var routes = new List<RouteDefinition>();

        // Find the RegisterArea method
        var registerAreaMethod = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text.Equals("RegisterArea", StringComparison.Ordinal));

        if (registerAreaMethod?.Body == null)
        {
            return routes;
        }

        // Find all MapRoute invocations
        var mapRouteInvocations = registerAreaMethod.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsMapRouteInvocation);

        foreach (var invocation in mapRouteInvocations)
        {
            var route = ParseMapRouteInvocation(invocation);
            if (route != null)
            {
                routes.Add(route);
            }
        }

        return routes;
    }

    /// <summary>
    /// Determines if an invocation is a MapRoute call.
    /// </summary>
    private static bool IsMapRouteInvocation(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        return methodName == "MapRoute";
    }

    /// <summary>
    /// Parses a MapRoute invocation into a RouteDefinition.
    /// Reuses the same logic as RouteConfigParser.
    /// </summary>
    private static RouteDefinition? ParseMapRouteInvocation(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        // Extract arguments - support both positional and named
        var name = ExtractArgumentValue(arguments, 0, "name");
        var template = ExtractArgumentValue(arguments, 1, "url");
        var defaultsArg = FindArgument(arguments, 2, "defaults");
        var constraintsArg = FindArgument(arguments, 3, "constraints");

        if (name == null || template == null)
        {
            return null;
        }

        var defaults = ParseDefaults(defaultsArg);
        var constraints = ParseConstraints(constraintsArg);

        return new RouteDefinition
        {
            Name = name,
            Template = template,
            Defaults = defaults,
            Constraints = constraints,
            IsDefaultRoute = name.Equals("Default", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Extracts a string argument value by position or name.
    /// </summary>
    private static string? ExtractArgumentValue(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int position,
        string name)
    {
        var argument = FindArgument(arguments, position, name);
        if (argument == null)
        {
            return null;
        }

        return ExtractStringLiteral(argument.Expression);
    }

    /// <summary>
    /// Finds an argument by position or name.
    /// </summary>
    private static ArgumentSyntax? FindArgument(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int position,
        string name)
    {
        // First try named argument
        var namedArg = arguments.FirstOrDefault(a =>
            a.NameColon?.Name.Identifier.Text.Equals(name, StringComparison.Ordinal) == true);

        if (namedArg != null)
        {
            return namedArg;
        }

        // Fall back to positional argument
        if (position < arguments.Count && arguments[position].NameColon == null)
        {
            return arguments[position];
        }

        return null;
    }

    /// <summary>
    /// Extracts a string literal value from an expression.
    /// </summary>
    private static string? ExtractStringLiteral(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Parses the defaults argument (anonymous object or null).
    /// </summary>
    private static Dictionary<string, object?> ParseDefaults(ArgumentSyntax? argument)
    {
        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (argument?.Expression is not AnonymousObjectCreationExpressionSyntax anonymousObject)
        {
            return defaults;
        }

        foreach (var initializer in anonymousObject.Initializers)
        {
            if (initializer.NameEquals == null)
            {
                continue;
            }

            var propertyName = initializer.NameEquals.Name.Identifier.Text;
            var value = ParseDefaultValue(initializer.Expression);

            defaults[propertyName] = value;
        }

        return defaults;
    }

    /// <summary>
    /// Parses a default value expression.
    /// </summary>
    private static object? ParseDefaultValue(ExpressionSyntax expression)
    {
        // Check for UrlParameter.Optional
        if (IsUrlParameterOptional(expression))
        {
            return RouteDefinition.OptionalParameter;
        }

        // String literal
        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }

            if (literal.IsKind(SyntaxKind.NumericLiteralExpression))
            {
                return literal.Token.Value;
            }

            if (literal.IsKind(SyntaxKind.TrueLiteralExpression) ||
                literal.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return literal.Token.Value;
            }

            if (literal.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return null;
            }
        }

        // For anything else, store the source text
        return expression.ToString();
    }

    /// <summary>
    /// Checks if an expression is UrlParameter.Optional.
    /// </summary>
    private static bool IsUrlParameterOptional(ExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var memberName = memberAccess.Name.Identifier.Text;
            var objectPart = memberAccess.Expression.ToString();

            return memberName == "Optional" && objectPart.Contains("UrlParameter");
        }

        return false;
    }

    /// <summary>
    /// Parses the constraints argument (anonymous object or null).
    /// </summary>
    private static Dictionary<string, string> ParseConstraints(ArgumentSyntax? argument)
    {
        var constraints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (argument?.Expression is not AnonymousObjectCreationExpressionSyntax anonymousObject)
        {
            return constraints;
        }

        foreach (var initializer in anonymousObject.Initializers)
        {
            if (initializer.NameEquals == null)
            {
                continue;
            }

            var propertyName = initializer.NameEquals.Name.Identifier.Text;
            var value = ParseConstraintValue(initializer.Expression);

            if (value != null)
            {
                constraints[propertyName] = value;
            }
        }

        return constraints;
    }

    /// <summary>
    /// Parses a constraint value expression.
    /// </summary>
    private static string? ParseConstraintValue(ExpressionSyntax expression)
    {
        // String literal (regex pattern)
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        // For object instantiations or anything else, use the source text
        return expression.ToString();
    }

    /// <summary>
    /// Extracts the type name from a type syntax node.
    /// </summary>
    private static string? ExtractTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null
        };
    }
}
