using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace NetLift.Transforms.Modernization.Utilities;

/// <summary>
/// Roslyn rewriter that transforms controller-specific patterns to handler patterns.
/// Handles: HttpContext, Session, User, db/storeDB references.
/// </summary>
internal sealed class PrivateMethodTransformRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.Text;

        // Handle standalone identifiers (db, storeDB)
        if (name.Equals("db", StringComparison.OrdinalIgnoreCase) || name.Equals("storeDB", StringComparison.Ordinal))
        {
            // Check if parent is a member access (e.g., db.Students)
            if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                return SyntaxFactory.IdentifierName("_context")
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var expressionText = node.Expression.ToString().Trim();
        var memberName = node.Name.ToString();

        // Handle: this.HttpContext -> _httpContextAccessor.HttpContext
        if (expressionText == "this" && memberName == "HttpContext")
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("_httpContextAccessor"),
                SyntaxFactory.IdentifierName("HttpContext"))
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        // Handle: HttpContext (standalone) -> _httpContextAccessor.HttpContext
        // But avoid transforming _httpContextAccessor.HttpContext or HttpContextAccessor
        if (node.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == "HttpContext")
        {
            // Check parent to see if we're already accessing _httpContextAccessor
            if (node.Parent is MemberAccessExpressionSyntax parentMemberAccess)
            {
                var grandParent = parentMemberAccess.Expression.ToString();
                if (!grandParent.Contains("_httpContextAccessor") && !grandParent.Contains("HttpContextAccessor"))
                {
                    // Transform HttpContext.Something -> _httpContextAccessor.HttpContext.Something
                    return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("_httpContextAccessor"),
                            SyntaxFactory.IdentifierName("HttpContext")),
                        node.Name)
                        .WithLeadingTrivia(node.GetLeadingTrivia())
                        .WithTrailingTrivia(node.GetTrailingTrivia());
                }
            }
        }

        // Handle: Session[...] -> add TODO comment
        if (memberName == "Session" && node.Parent is ElementAccessExpressionSyntax)
        {
            // Add TODO comment before the statement
            var todoComment = SyntaxFactory.Comment("// TODO: Replace Session with IDistributedCache or appropriate state management\n        ");

            var replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("_httpContextAccessor"),
                    SyntaxFactory.IdentifierName("HttpContext")),
                SyntaxFactory.IdentifierName("Session"))
                .WithLeadingTrivia(node.GetLeadingTrivia().Add(todoComment));

            return replacement;
        }

        // Handle: User.Identity -> _httpContextAccessor.HttpContext?.User.Identity
        if (node.Expression is IdentifierNameSyntax userIdentifier && userIdentifier.Identifier.Text == "User")
        {
            // Ensure we're not already inside _httpContextAccessor or another context
            var parent = node.Parent;
            var parentText = parent?.ToString() ?? string.Empty;

            if (!parentText.Contains("_httpContextAccessor") && !parentText.Contains("_currentUser"))
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("_httpContextAccessor"),
                            SyntaxFactory.IdentifierName("HttpContext")),
                        SyntaxFactory.Token(SyntaxKind.QuestionToken),
                        SyntaxFactory.IdentifierName("User")),
                    node.Name)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        return base.VisitMemberAccessExpression(node);
    }
}
