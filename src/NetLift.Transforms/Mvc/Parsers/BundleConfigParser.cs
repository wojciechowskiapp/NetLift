using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Parsers;

/// <summary>
/// Parses BundleConfig.cs files to extract bundle definitions using Roslyn.
/// Supports ScriptBundle, StyleBundle with .Include() chains and CDN configuration.
/// </summary>
public sealed class BundleConfigParser : IBundleConfigParser
{
    /// <inheritdoc />
    public IReadOnlyList<BundleDefinition> Parse(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<BundleDefinition>();
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var bundles = new List<BundleDefinition>();

        // Find bundles.Add() calls in RegisterBundles method
        var addInvocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsBundlesAddInvocation);

        foreach (var invocation in addInvocations)
        {
            var bundle = ParseBundleAddInvocation(invocation);
            if (bundle != null)
            {
                bundles.Add(bundle);
            }
        }

        return bundles;
    }

    /// <summary>
    /// Determines if an invocation is a bundles.Add() call.
    /// </summary>
    private static bool IsBundlesAddInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            var objectName = memberAccess.Expression.ToString();
            return methodName == "Add" && objectName.Contains("bundles");
        }

        return false;
    }

    /// <summary>
    /// Parses a single bundles.Add() invocation into a BundleDefinition.
    /// </summary>
    private static BundleDefinition? ParseBundleAddInvocation(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        // The first argument should be a bundle instantiation (ScriptBundle or StyleBundle)
        var bundleExpression = arguments[0].Expression;
        return ParseBundleExpression(bundleExpression);
    }

    /// <summary>
    /// Parses a bundle creation expression (new ScriptBundle(...).Include(...)).
    /// </summary>
    private static BundleDefinition? ParseBundleExpression(ExpressionSyntax expression)
    {
        // Handle chained method calls (e.g., new ScriptBundle("path").Include(...).IncludeDirectory(...))
        var bundleType = BundleType.Script;
        string? virtualPath = null;
        string? cdnPath = null;
        string? cdnFallbackExpression = null;
        var includedFiles = new List<string>();
        var includedDirectories = new List<string>();

        // Start from the end of the chain and work backwards
        // Use InsertRange at index 0 to maintain correct order
        var currentExpression = expression;

        while (currentExpression != null)
        {
            if (currentExpression is InvocationExpressionSyntax invocation)
            {
                // Check for .Include() calls
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;

                    if (methodName == "Include")
                    {
                        var files = ExtractIncludePaths(invocation);
                        includedFiles.InsertRange(0, files);
                        currentExpression = memberAccess.Expression;
                    }
                    else if (methodName == "IncludeDirectory")
                    {
                        var directories = ExtractIncludePaths(invocation);
                        includedDirectories.InsertRange(0, directories);
                        currentExpression = memberAccess.Expression;
                    }
                    else
                    {
                        currentExpression = memberAccess.Expression;
                    }
                }
                else
                {
                    break;
                }
            }
            else if (currentExpression is ObjectCreationExpressionSyntax objectCreation)
            {
                // Extract bundle type and virtual path
                var typeName = objectCreation.Type.ToString();
                bundleType = typeName.Contains("ScriptBundle") ? BundleType.Script : BundleType.Style;

                // Extract virtual path from constructor
                if (objectCreation.ArgumentList?.Arguments.Count > 0)
                {
                    virtualPath = ExtractStringLiteral(objectCreation.ArgumentList.Arguments[0].Expression);
                }

                // Check for CDN path (second parameter)
                if (objectCreation.ArgumentList?.Arguments.Count > 1)
                {
                    cdnPath = ExtractStringLiteral(objectCreation.ArgumentList.Arguments[1].Expression);
                }

                // Check for named arguments
                foreach (var arg in objectCreation.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
                {
                    if (arg.NameColon?.Name.Identifier.Text == "cdnPath")
                    {
                        cdnPath = ExtractStringLiteral(arg.Expression);
                    }
                    else if (arg.NameColon?.Name.Identifier.Text == "cdnFallbackExpression")
                    {
                        cdnFallbackExpression = ExtractStringLiteral(arg.Expression);
                    }
                }

                break;
            }
            else
            {
                break;
            }
        }

        if (string.IsNullOrEmpty(virtualPath))
        {
            return null;
        }

        return new BundleDefinition
        {
            VirtualPath = virtualPath,
            Type = bundleType,
            IncludedFiles = includedFiles,
            IncludedDirectories = includedDirectories,
            IsMinified = true, // ASP.NET bundling minifies by default in release mode
            CdnPath = cdnPath,
            CdnFallbackExpression = cdnFallbackExpression
        };
    }

    /// <summary>
    /// Extracts file paths from .Include() or .IncludeDirectory() arguments.
    /// </summary>
    private static List<string> ExtractIncludePaths(InvocationExpressionSyntax invocation)
    {
        var paths = new List<string>();

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var path = ExtractStringLiteral(arg.Expression);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        return paths;
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

        // Handle concatenated strings or expressions
        if (expression is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.AddExpression))
        {
            var left = ExtractStringLiteral(binary.Left);
            var right = ExtractStringLiteral(binary.Right);
            return left + right;
        }

        return null;
    }
}
