using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Configuration;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites Entity Framework 6 Fluent API relationship configuration to EF Core equivalents.
/// Transforms HasRequired/HasOptional to HasOne and adds appropriate IsRequired() calls.
/// </summary>
public sealed class FluentApiRelationshipRewriter : CSharpSyntaxRewriter, IFluentApiRelationshipRewriter
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
    /// Visits invocation expressions to detect and transform EF6 relationship configurations.
    /// </summary>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // First visit children
        var visited = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this invocation is the outermost in a relationship chain
        if (!IsOutermostInRelationshipChain(node))
        {
            return visited;
        }

        // Check if this chain contains a relationship starter
        if (!ContainsRelationshipStarter(node))
        {
            return visited;
        }

        // Transform the entire chain
        var transformed = TransformRelationshipChain(visited);

        return transformed;
    }

    /// <summary>
    /// Checks if this invocation is the outermost in a fluent chain.
    /// </summary>
    private bool IsOutermostInRelationshipChain(InvocationExpressionSyntax node)
    {
        // Check if parent is an invocation with member access
        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Parent is InvocationExpressionSyntax)
            {
                return false; // We're part of a larger chain
            }

            if (parent is ArgumentSyntax || parent is ExpressionStatementSyntax)
            {
                break;
            }

            parent = parent.Parent;
        }

        return true;
    }

    /// <summary>
    /// Checks if the invocation chain contains a relationship starter (HasRequired/HasOptional).
    /// </summary>
    private bool ContainsRelationshipStarter(InvocationExpressionSyntax node)
    {
        var current = node;

        while (current != null)
        {
            var methodName = ExtractMethodName(current.Expression);
            if (methodName != null && FluentApiMappings.IsRelationshipStarter(methodName))
            {
                return true;
            }

            // Move to the next invocation in the chain (the expression part)
            if (current.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is InvocationExpressionSyntax innerInvocation)
            {
                current = innerInvocation;
            }
            else
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Transforms an entire relationship configuration chain.
    /// </summary>
    private InvocationExpressionSyntax TransformRelationshipChain(InvocationExpressionSyntax node)
    {
        // Track if we found required or optional
        bool? isRequired = null;

        // Recursively transform all invocations in the chain
        var transformed = TransformInvocationsRecursively(node, ref isRequired);

        // Append IsRequired at the end if we found a relationship starter
        if (isRequired.HasValue)
        {
            transformed = AppendIsRequiredCall(transformed, isRequired.Value);
        }

        return transformed;
    }

    /// <summary>
    /// Recursively transforms invocations in the chain.
    /// </summary>
    private InvocationExpressionSyntax TransformInvocationsRecursively(
        InvocationExpressionSyntax node,
        ref bool? isRequired)
    {
        var methodName = ExtractMethodName(node.Expression);

        // Transform the expression part (which might be another invocation)
        ExpressionSyntax newExpression = node.Expression;

        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var innerExpression = memberAccess.Expression;

            // Recursively transform inner invocations
            if (innerExpression is InvocationExpressionSyntax innerInvocation)
            {
                var transformedInner = TransformInvocationsRecursively(innerInvocation, ref isRequired);
                innerExpression = transformedInner;
            }

            newExpression = memberAccess.WithExpression(innerExpression);
        }

        // Transform the current method name if it needs mapping
        if (methodName != null && FluentApiMappings.RequiresMapping(methodName))
        {
            // Determine isRequired based on current method
            if (FluentApiMappings.IsRelationshipStarter(methodName))
            {
                isRequired = FluentApiMappings.IsRequiredMethod(methodName);
            }
            else if (FluentApiMappings.IsRequiredMethod(methodName))
            {
                isRequired = true;
            }
            else if (FluentApiMappings.IsOptionalMethod(methodName))
            {
                isRequired = false;
            }

            var mapped = FluentApiMappings.GetMapping(methodName);
            if (mapped != null)
            {
                // Determine confidence based on complexity
                var confidence = methodName switch
                {
                    "HasRequired" or "HasOptional" => 95,
                    "WithRequired" or "WithOptional" => 90,
                    "WithOptionalPrincipal" or "WithOptionalDependent" => 75,
                    _ => 85
                };

                _lowestConfidence = Math.Min(_lowestConfidence, confidence);

                newExpression = ReplaceMethodName(newExpression, methodName, mapped);

                _requiredUsings.Add("Microsoft.EntityFrameworkCore");
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Rewritten relationship method: {methodName}() → {mapped}()",
                    RewriterDiagnosticSeverity.Info));
            }
        }

        return node.WithExpression(newExpression);
    }

    /// <summary>
    /// Replaces the method name in an expression.
    /// </summary>
    private ExpressionSyntax ReplaceMethodName(ExpressionSyntax expression, string oldName, string newName)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier when identifier.Identifier.Text == oldName =>
                SyntaxFactory.IdentifierName(newName).WithTriviaFrom(identifier),

            MemberAccessExpressionSyntax memberAccess when memberAccess.Name.Identifier.Text == oldName =>
                memberAccess.WithName(SyntaxFactory.IdentifierName(newName).WithTriviaFrom(memberAccess.Name)),

            _ => expression
        };
    }

    /// <summary>
    /// Appends .IsRequired(true/false) at the end of the chain.
    /// </summary>
    private InvocationExpressionSyntax AppendIsRequiredCall(InvocationExpressionSyntax node, bool isRequired)
    {
        // Create .IsRequired(true/false)
        var isRequiredArgument = SyntaxFactory.Argument(
            SyntaxFactory.LiteralExpression(
                isRequired ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression));

        var isRequiredCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                node,
                SyntaxFactory.IdentifierName("IsRequired")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(isRequiredArgument)));

        _diagnostics.Add(new RewriterDiagnostic(
            $"Added IsRequired({(isRequired ? "true" : "false")}) to relationship chain",
            RewriterDiagnosticSeverity.Info));

        return isRequiredCall;
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
}
