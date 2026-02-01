using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites Entity Framework 6 Include chaining patterns to EF Core ThenInclude equivalents.
/// Transforms nested Select patterns like Include(x => x.Items.Select(i => i.Product))
/// to Include(x => x.Items).ThenInclude(i => i.Product).
/// Also handles string-based includes like Include("Items.Product.Category").
/// </summary>
public sealed class IncludeThenIncludeRewriter : CSharpSyntaxRewriter, IIncludeThenIncludeRewriter
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
    /// Visits invocation expressions to detect and transform Include patterns.
    /// </summary>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // First visit children
        var visited = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this is an Include call
        var methodName = ExtractMethodName(visited.Expression);
        if (methodName != "Include")
        {
            return visited;
        }

        // Check if we have arguments
        if (visited.ArgumentList.Arguments.Count == 0)
        {
            return visited;
        }

        var argument = visited.ArgumentList.Arguments[0].Expression;

        // Try lambda-based transformation
        if (argument is SimpleLambdaExpressionSyntax lambdaExpression)
        {
            return TransformLambdaBasedInclude(visited, lambdaExpression);
        }

        // Try string-based transformation
        if (argument is LiteralExpressionSyntax literalExpression &&
            literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return TransformStringBasedInclude(visited, literalExpression);
        }

        return visited;
    }

    /// <summary>
    /// Transforms lambda-based Include with nested Select to ThenInclude chain.
    /// </summary>
    private InvocationExpressionSyntax TransformLambdaBasedInclude(
        InvocationExpressionSyntax includeCall,
        SimpleLambdaExpressionSyntax lambdaExpression)
    {
        // Extract navigation path from lambda body
        if (lambdaExpression.Body is not ExpressionSyntax bodyExpression)
        {
            return includeCall;
        }

        var navigationPath = ExtractNavigationPath(bodyExpression);

        if (navigationPath.Count <= 1)
        {
            // Simple Include, no transformation needed
            return includeCall;
        }

        // Extract the base expression (e.g., Orders from Orders.Include(...))
        var baseExpression = (includeCall.Expression as MemberAccessExpressionSyntax)?.Expression
            ?? includeCall.Expression;

        // Build Include().ThenInclude().ThenInclude() chain
        var result = BuildThenIncludeChain(
            baseExpression,
            navigationPath,
            lambdaExpression.Parameter.Identifier.Text);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");
        _lowestConfidence = Math.Min(_lowestConfidence, 90);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed nested Include with Select to ThenInclude chain ({navigationPath.Count} levels)",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Transforms string-based Include to ThenInclude chain.
    /// </summary>
    private InvocationExpressionSyntax TransformStringBasedInclude(
        InvocationExpressionSyntax includeCall,
        LiteralExpressionSyntax literalExpression)
    {
        // Extract the path from the string literal
        var path = literalExpression.Token.ValueText;
        var parts = path.Split('.').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

        if (parts.Count <= 1)
        {
            // Simple Include, no transformation needed
            return includeCall;
        }

        // Extract the base expression (e.g., Orders from Orders.Include(...))
        var baseExpression = (includeCall.Expression as MemberAccessExpressionSyntax)?.Expression
            ?? includeCall.Expression;

        // Build Include().ThenInclude().ThenInclude() chain
        var result = BuildThenIncludeChainFromStringPath(
            baseExpression,
            parts);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");
        _lowestConfidence = Math.Min(_lowestConfidence, 75);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed string-based Include(\"{path}\") to ThenInclude chain ({parts.Count} levels) - TODO: Verify property names and types",
            RewriterDiagnosticSeverity.Warning));

        return result;
    }

    /// <summary>
    /// Extracts navigation path from lambda body, handling nested Select calls.
    /// </summary>
    private List<NavigationSegment> ExtractNavigationPath(ExpressionSyntax expression)
    {
        var path = new List<NavigationSegment>();

        // Start with the first property access
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            ExtractFromMemberAccess(memberAccess, path);
        }
        else if (expression is InvocationExpressionSyntax invocation)
        {
            ExtractFromInvocation(invocation, path);
        }

        return path;
    }

    /// <summary>
    /// Extracts navigation segments from member access expression.
    /// </summary>
    private void ExtractFromMemberAccess(MemberAccessExpressionSyntax memberAccess, List<NavigationSegment> path)
    {
        // Recursively process the left side
        if (memberAccess.Expression is MemberAccessExpressionSyntax innerMemberAccess)
        {
            ExtractFromMemberAccess(innerMemberAccess, path);
        }
        else if (memberAccess.Expression is InvocationExpressionSyntax invocation)
        {
            ExtractFromInvocation(invocation, path);
        }

        // Add current property
        path.Add(new NavigationSegment(
            memberAccess.Name.Identifier.Text,
            null)); // No parameter for simple property access
    }

    /// <summary>
    /// Extracts navigation segments from invocation (typically Select calls).
    /// </summary>
    private void ExtractFromInvocation(InvocationExpressionSyntax invocation, List<NavigationSegment> path)
    {
        var methodName = ExtractMethodName(invocation.Expression);

        if (methodName == "Select" && invocation.ArgumentList.Arguments.Count > 0)
        {
            // This is a Select call - extract the collection and the nested path
            if (invocation.Expression is MemberAccessExpressionSyntax selectMemberAccess)
            {
                // Extract the collection property
                if (selectMemberAccess.Expression is MemberAccessExpressionSyntax collectionAccess)
                {
                    ExtractFromMemberAccess(collectionAccess, path);
                }

                // Extract parameter and nested path from Select lambda
                var argument = invocation.ArgumentList.Arguments[0].Expression;
                if (argument is SimpleLambdaExpressionSyntax selectLambda &&
                    selectLambda.Body is ExpressionSyntax selectBodyExpression)
                {
                    // Extract the nested navigation from lambda body
                    var nestedPath = ExtractNavigationPath(selectBodyExpression);
                    foreach (var segment in nestedPath)
                    {
                        // Don't use parameter names from source - generate new ones
                        path.Add(new NavigationSegment(
                            segment.PropertyName,
                            null)); // null means we'll generate a name later
                    }
                }
            }
        }
        else
        {
            // Not a Select call, try to extract from the expression
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                ExtractFromMemberAccess(memberAccess, path);
            }
        }
    }

    /// <summary>
    /// Builds Include().ThenInclude() chain from navigation path.
    /// </summary>
    private InvocationExpressionSyntax BuildThenIncludeChain(
        ExpressionSyntax baseExpression,
        List<NavigationSegment> navigationPath,
        string initialParameterName)
    {
        if (navigationPath.Count == 0)
        {
            throw new InvalidOperationException("Navigation path cannot be empty");
        }

        // Build first Include call
        var currentParameter = initialParameterName;
        var firstProperty = navigationPath[0].PropertyName;

        var includeLambda = SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(currentParameter)),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(currentParameter),
                SyntaxFactory.IdentifierName(firstProperty)));

        var includeCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                baseExpression,
                SyntaxFactory.IdentifierName("Include")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(includeLambda))));

        // Build ThenInclude chain
        InvocationExpressionSyntax currentCall = includeCall;
        for (int i = 1; i < navigationPath.Count; i++)
        {
            var segment = navigationPath[i];
            var newParameter = segment.ParameterName ?? GenerateParameterName(i);

            var thenIncludeLambda = SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(newParameter)),
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(newParameter),
                    SyntaxFactory.IdentifierName(segment.PropertyName)));

            currentCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    currentCall,
                    SyntaxFactory.IdentifierName("ThenInclude")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(thenIncludeLambda))));
        }

        return currentCall;
    }

    /// <summary>
    /// Builds Include().ThenInclude() chain from string-based path parts.
    /// </summary>
    private InvocationExpressionSyntax BuildThenIncludeChainFromStringPath(
        ExpressionSyntax baseExpression,
        List<string> pathParts)
    {
        if (pathParts.Count == 0)
        {
            throw new InvalidOperationException("Path parts cannot be empty");
        }

        // Build first Include call with lambda
        var firstParameter = GenerateParameterName(0);
        var includeLambda = SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(firstParameter)),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(firstParameter),
                SyntaxFactory.IdentifierName(pathParts[0])));

        var includeCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                baseExpression,
                SyntaxFactory.IdentifierName("Include")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(includeLambda))));

        // Build ThenInclude chain
        InvocationExpressionSyntax currentCall = includeCall;
        for (int i = 1; i < pathParts.Count; i++)
        {
            var newParameter = GenerateParameterName(i);

            var thenIncludeLambda = SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(newParameter)),
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(newParameter),
                    SyntaxFactory.IdentifierName(pathParts[i])));

            currentCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    currentCall,
                    SyntaxFactory.IdentifierName("ThenInclude")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(thenIncludeLambda))));
        }

        return currentCall;
    }

    /// <summary>
    /// Generates parameter names (x, y, z, a, b, c, etc.).
    /// </summary>
    private static string GenerateParameterName(int index)
    {
        // Use x, y, z for first three, then i, j, k, then a, b, c
        return index switch
        {
            0 => "x",
            1 => "y",
            2 => "z",
            3 => "i",
            4 => "j",
            5 => "k",
            _ => $"p{index}"
        };
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

    /// <summary>
    /// Represents a segment in the navigation path.
    /// </summary>
    private sealed record NavigationSegment(string PropertyName, string? ParameterName);
}
