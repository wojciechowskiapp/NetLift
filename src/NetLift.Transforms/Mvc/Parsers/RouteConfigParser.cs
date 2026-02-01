using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Parsers;

/// <summary>
/// Parses RouteConfig.cs files to extract route definitions from MapRoute() calls using Roslyn.
/// Supports both positional and named arguments, anonymous objects, and UrlParameter.Optional.
/// </summary>
public sealed class RouteConfigParser : IRouteConfigParser
{
    /// <inheritdoc />
    public IReadOnlyList<RouteDefinition> Parse(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<RouteDefinition>();
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var routes = new List<RouteDefinition>();

        // Find all MapRoute invocations
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsMapRouteInvocation);

        foreach (var invocation in invocations)
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
    /// Parses a single MapRoute invocation into a RouteDefinition.
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
}
