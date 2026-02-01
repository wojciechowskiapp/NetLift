using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Rewrites System.Web.Mvc namespace references to ASP.NET Core equivalents.
/// Handles using directives, qualified names, and fully qualified type references.
/// </summary>
public sealed class SystemWebMvcNamespaceRewriter : CSharpSyntaxRewriter, IMvcNamespaceRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _processedNamespaces = new(StringComparer.Ordinal);
    private readonly List<(string source, string target)> _mappings = new();
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
        _processedNamespaces.Clear();
        _mappings.Clear();
        _lowestConfidence = 100;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Phase 1: Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Phase 2: Add new using directives and cleanup
        rewritten = AddRequiredUsings(rewritten);
        rewritten = RemoveDuplicateUsings(rewritten);
        rewritten = SortUsings(rewritten);

        // Calculate overall confidence considering merges
        // Check if multiple source namespaces mapped to the same target
        var targetCounts = _mappings.GroupBy(m => m.target).Where(g => g.Count() > 1).ToList();
        if (targetCounts.Any())
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 90);
        }

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Internal method for direct syntax node rewriting (used by tests).
    /// </summary>
    internal SyntaxNode? RewriteNode(SyntaxNode root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _processedNamespaces.Clear();
        _mappings.Clear();
        _lowestConfidence = 100;

        // Phase 1: Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return null;
        }

        // Phase 2: Add new using directives and cleanup
        rewritten = AddRequiredUsings(rewritten);
        rewritten = RemoveDuplicateUsings(rewritten);
        rewritten = SortUsings(rewritten);

        // Calculate overall confidence considering merges
        // Check if multiple source namespaces mapped to the same target
        var targetCounts = _mappings.GroupBy(m => m.target).Where(g => g.Count() > 1).ToList();
        if (targetCounts.Any())
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 90);
        }

        return rewritten;
    }

    /// <summary>
    /// Visits using directives to rewrite namespace imports.
    /// Handles simple, aliased, static, and global using directives.
    /// </summary>
    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name == null)
        {
            return base.VisitUsingDirective(node);
        }

        var namespaceName = node.Name.ToString();

        if (!MvcNamespaceMappings.RequiresMapping(namespaceName))
        {
            return base.VisitUsingDirective(node);
        }

        var mappedNamespace = MvcNamespaceMappings.GetMapping(namespaceName);

        if (mappedNamespace == null)
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"No mapping found for namespace '{namespaceName}'",
                RewriterDiagnosticSeverity.Warning));
            return base.VisitUsingDirective(node);
        }

        // Track confidence
        var confidence = MvcNamespaceMappings.CalculateConfidenceScore(namespaceName, mappedNamespace);
        _lowestConfidence = Math.Min(_lowestConfidence, confidence);

        // Track required using - for child namespaces, track the base namespace too
        if (mappedNamespace.Contains('.'))
        {
            // Extract base namespace (e.g., Microsoft.AspNetCore.Mvc.Rendering from Microsoft.AspNetCore.Mvc.Rendering.InputExtensions)
            var parts = mappedNamespace.Split('.');
            if (parts.Length > 2)
            {
                // Track up to 3 levels deep as potential base
                var baseNamespace = string.Join(".", parts.Take(Math.Min(4, parts.Length)));
                _requiredUsings.Add(baseNamespace);
            }
        }
        _requiredUsings.Add(mappedNamespace);
        _processedNamespaces.Add(namespaceName);
        _mappings.Add((namespaceName, mappedNamespace));

        // Create new using directive with mapped namespace
        var newName = SyntaxFactory.ParseName(mappedNamespace)
            .WithTriviaFrom(node.Name);

        var newUsing = node.WithName(newName);

        _diagnostics.Add(new RewriterDiagnostic(
            $"Rewritten using directive: '{namespaceName}' → '{mappedNamespace}' (confidence: {confidence}%)",
            RewriterDiagnosticSeverity.Info));

        return newUsing;
    }

    /// <summary>
    /// Visits qualified names to rewrite fully qualified type references.
    /// Example: System.Web.Mvc.Controller → Microsoft.AspNetCore.Mvc.Controller
    /// </summary>
    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        var qualifiedName = node.ToString();

        // Check if this qualified name starts with a namespace that needs mapping
        var matchingNamespace = MvcNamespaceMappings.Mappings.Keys
            .Where(key => qualifiedName.StartsWith(key + ".", StringComparison.Ordinal) || qualifiedName == key)
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();

        if (matchingNamespace == null)
        {
            return base.VisitQualifiedName(node);
        }

        var mappedNamespace = MvcNamespaceMappings.GetMapping(matchingNamespace);

        if (mappedNamespace == null)
        {
            return base.VisitQualifiedName(node);
        }

        // Track confidence
        var confidence = MvcNamespaceMappings.CalculateConfidenceScore(matchingNamespace, mappedNamespace);
        _lowestConfidence = Math.Min(_lowestConfidence, confidence);
        _requiredUsings.Add(mappedNamespace);
        _mappings.Add((matchingNamespace, mappedNamespace));

        // Replace the namespace part
        var remainder = qualifiedName.Substring(matchingNamespace.Length);
        var newQualifiedName = mappedNamespace + remainder;

        var newNode = SyntaxFactory.ParseName(newQualifiedName)
            .WithTriviaFrom(node);

        _diagnostics.Add(new RewriterDiagnostic(
            $"Rewritten qualified name: '{qualifiedName}' → '{newQualifiedName}' (confidence: {confidence}%)",
            RewriterDiagnosticSeverity.Info));

        return newNode;
    }

    /// <summary>
    /// Visits member access expressions to handle cases like System.Web.Mvc.Controller.
    /// </summary>
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // First visit children
        var visited = base.VisitMemberAccessExpression(node);

        // If the expression contains a qualified name that was rewritten, it's already handled
        return visited;
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

        // Find existing compilation unit or namespace declaration
        if (root is CompilationUnitSyntax compilationUnit)
        {
            var existingUsings = compilationUnit.Usings
                .Select(u => u.Name?.ToString())
                .Where(n => n != null)
                .ToHashSet(StringComparer.Ordinal);

            var newUsings = _requiredUsings
                .Where(ns => !existingUsings.Contains(ns) && !string.IsNullOrWhiteSpace(ns))
                .Select(ns => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns)))
                .ToList();

            if (newUsings.Count > 0)
            {
                return compilationUnit.AddUsings(newUsings.ToArray());
            }
        }

        return root;
    }

    /// <summary>
    /// Removes duplicate using directives after rewriting.
    /// </summary>
    private SyntaxNode RemoveDuplicateUsings(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var seenUsings = new HashSet<string>(StringComparer.Ordinal);
        var uniqueUsings = new List<UsingDirectiveSyntax>();

        foreach (var usingDirective in compilationUnit.Usings)
        {
            var namespaceName = usingDirective.Name?.ToString();

            if (namespaceName == null)
            {
                uniqueUsings.Add(usingDirective);
                continue;
            }

            // Build a key that includes alias and static modifiers
            var key = BuildUsingKey(usingDirective);

            if (!seenUsings.Contains(key))
            {
                seenUsings.Add(key);
                uniqueUsings.Add(usingDirective);
            }
        }

        return compilationUnit.WithUsings(SyntaxFactory.List(uniqueUsings));
    }

    /// <summary>
    /// Sorts using directives with System namespaces first.
    /// </summary>
    private SyntaxNode SortUsings(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var sortedUsings = compilationUnit.Usings
            .OrderBy(u => u.StaticKeyword.IsKind(SyntaxKind.None) ? 0 : 1) // Non-static first
            .ThenBy(u => u.Alias != null ? 1 : 0) // Non-aliased first
            .ThenBy(u =>
            {
                var name = u.Name?.ToString() ?? string.Empty;
                // System namespaces first, then Microsoft, then others
                if (name.StartsWith("System.", StringComparison.Ordinal) || name == "System")
                {
                    return 0;
                }
                if (name.StartsWith("Microsoft.", StringComparison.Ordinal))
                {
                    return 1;
                }
                return 2;
            })
            .ThenBy(u => u.Name?.ToString(), StringComparer.Ordinal)
            .ToList();

        return compilationUnit.WithUsings(SyntaxFactory.List(sortedUsings));
    }

    /// <summary>
    /// Builds a unique key for a using directive to detect duplicates.
    /// </summary>
    private static string BuildUsingKey(UsingDirectiveSyntax usingDirective)
    {
        var parts = new List<string>();

        if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
        {
            parts.Add("global");
        }

        if (usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
        {
            parts.Add("static");
        }

        if (usingDirective.Alias != null)
        {
            parts.Add($"alias:{usingDirective.Alias.Name}");
        }

        parts.Add(usingDirective.Name?.ToString() ?? string.Empty);

        return string.Join("|", parts);
    }
}
