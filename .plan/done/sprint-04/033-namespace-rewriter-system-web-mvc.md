# Task 033: Roslyn Rewriter for System.Web.Mvc Namespaces

## Meta
- **Priority**: P0
- **Estimate**: 5 points
- **Sprint**: 4
- **Dependencies**: 031
- **Status**: Not Started

## Description
Implement Roslyn SyntaxRewriter to transform System.Web.Mvc namespace references to Microsoft.AspNetCore.Mvc equivalents. This is the foundational namespace migration for MVC controllers.

## Acceptance Criteria
- [ ] SystemWebMvcNamespaceRewriter class implemented
- [ ] Using directives rewritten (System.Web.Mvc → Microsoft.AspNetCore.Mvc)
- [ ] Fully qualified type references updated
- [ ] System.Web.Routing → Microsoft.AspNetCore.Routing
- [ ] System.Web.Optimization → WebOptimizer (or remove if unused)
- [ ] System.Web.Helpers → Microsoft.AspNetCore.Mvc.Rendering
- [ ] Unit tests with 95%+ coverage
- [ ] Handles edge cases (aliases, static usings, global usings)

## Technical Notes

### Namespace Mapping
```csharp
public static class MvcNamespaceMappings
{
    public static readonly Dictionary<string, string> NamespaceMap = new()
    {
        // Core MVC
        ["System.Web.Mvc"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Ajax"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Async"] = "Microsoft.AspNetCore.Mvc",
        ["System.Web.Mvc.Html"] = "Microsoft.AspNetCore.Mvc.Rendering",

        // Routing
        ["System.Web.Routing"] = "Microsoft.AspNetCore.Routing",

        // Filters
        ["System.Web.Mvc.Filters"] = "Microsoft.AspNetCore.Mvc.Filters",

        // Model Binding
        ["System.Web.Mvc.ModelBinding"] = "Microsoft.AspNetCore.Mvc.ModelBinding",

        // Razor
        ["System.Web.WebPages"] = "Microsoft.AspNetCore.Mvc.Razor",
        ["System.Web.Helpers"] = "Microsoft.AspNetCore.Mvc.Rendering",

        // Optimization (requires decision - remove or migrate to WebOptimizer)
        ["System.Web.Optimization"] = "WebOptimizer"
    };
}
```

### Roslyn SyntaxRewriter Implementation
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Rewriters;

/// <summary>
/// Rewrites System.Web.Mvc namespace references to ASP.NET Core equivalents
/// </summary>
public class SystemWebMvcNamespaceRewriter : CSharpSyntaxRewriter
{
    private readonly Dictionary<string, string> _namespaceMap;
    private readonly SemanticModel _semanticModel;
    private readonly HashSet<string> _processedAliases = new();

    public SystemWebMvcNamespaceRewriter(
        SemanticModel semanticModel,
        Dictionary<string, string>? customMappings = null)
    {
        _semanticModel = semanticModel;
        _namespaceMap = customMappings ?? MvcNamespaceMappings.NamespaceMap;
    }

    public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
    {
        // Handle using System.Web.Mvc;
        if (node.Name is QualifiedNameSyntax qualifiedName)
        {
            var namespaceName = qualifiedName.ToString();

            if (_namespaceMap.TryGetValue(namespaceName, out var newNamespace))
            {
                var newName = SyntaxFactory.ParseName(newNamespace)
                    .WithTriviaFrom(node.Name);

                var newUsing = node.WithName(newName);

                // Track alias if present
                if (node.Alias != null)
                {
                    _processedAliases.Add(node.Alias.Name.Identifier.Text);
                }

                return newUsing.WithLeadingTrivia(
                    SyntaxFactory.Comment($"// Migrated from {namespaceName}"),
                    SyntaxFactory.EndOfLine("\r\n"),
                    node.GetLeadingTrivia()
                );
            }
        }

        // Handle using static System.Web.Mvc.Controller;
        if (node.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
        {
            return VisitStaticUsingDirective(node);
        }

        return base.VisitUsingDirective(node);
    }

    private SyntaxNode? VisitStaticUsingDirective(UsingDirectiveSyntax node)
    {
        var typeName = node.Name.ToString();

        // Extract namespace from fully qualified type
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot > 0)
        {
            var namespacePart = typeName.Substring(0, lastDot);
            var typePart = typeName.Substring(lastDot + 1);

            if (_namespaceMap.TryGetValue(namespacePart, out var newNamespace))
            {
                var newFullName = $"{newNamespace}.{typePart}";
                var newName = SyntaxFactory.ParseName(newFullName)
                    .WithTriviaFrom(node.Name);

                return node.WithName(newName);
            }
        }

        return base.VisitUsingDirective(node);
    }

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        // Handle fully qualified names: System.Web.Mvc.Controller
        var fullName = node.ToString();

        // Try exact match first
        if (_namespaceMap.TryGetValue(fullName, out var exactMapping))
        {
            return SyntaxFactory.ParseName(exactMapping)
                .WithTriviaFrom(node);
        }

        // Try prefix match for nested types
        foreach (var (oldNs, newNs) in _namespaceMap)
        {
            if (fullName.StartsWith(oldNs + "."))
            {
                var suffix = fullName.Substring(oldNs.Length);
                var newName = newNs + suffix;

                return SyntaxFactory.ParseName(newName)
                    .WithTriviaFrom(node);
            }
        }

        return base.VisitQualifiedName(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Use semantic model to resolve actual type
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
        {
            var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();

            if (containingNamespace != null &&
                _namespaceMap.TryGetValue(containingNamespace, out var newNamespace))
            {
                // Check if we need to add qualification or if using directive exists
                if (!HasUsingDirective(node.SyntaxTree, newNamespace))
                {
                    // Qualify the name
                    var typeName = typeSymbol.Name;
                    var qualifiedName = $"{newNamespace}.{typeName}";

                    return SyntaxFactory.ParseName(qualifiedName)
                        .WithTriviaFrom(node);
                }
            }
        }

        return base.VisitIdentifierName(node);
    }

    private bool HasUsingDirective(SyntaxTree tree, string namespaceName)
    {
        var root = tree.GetRoot();
        var compilationUnit = root as CompilationUnitSyntax;

        return compilationUnit?.Usings.Any(u =>
            u.Name.ToString() == namespaceName) ?? false;
    }
}
```

### Usage Example
```csharp
public class MvcNamespaceMigrator
{
    private readonly SemanticModel _semanticModel;

    public async Task<Document> MigrateNamespacesAsync(Document document)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        if (root == null || semanticModel == null)
            return document;

        var rewriter = new SystemWebMvcNamespaceRewriter(semanticModel);
        var newRoot = rewriter.Visit(root);

        if (newRoot == null)
            return document;

        // Clean up duplicate using directives
        newRoot = RemoveDuplicateUsings(newRoot);

        // Sort using directives
        newRoot = SortUsings(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private SyntaxNode RemoveDuplicateUsings(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        var uniqueUsings = compilationUnit.Usings
            .GroupBy(u => u.Name?.ToString())
            .Select(g => g.First())
            .ToArray();

        return compilationUnit.WithUsings(
            SyntaxFactory.List(uniqueUsings));
    }

    private SyntaxNode SortUsings(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        var sortedUsings = compilationUnit.Usings
            .OrderBy(u => u.StaticKeyword.IsKind(SyntaxKind.None) ? 0 : 1)
            .ThenBy(u => u.Alias != null ? 1 : 0)
            .ThenBy(u => u.Name?.ToString())
            .ToArray();

        return compilationUnit.WithUsings(
            SyntaxFactory.List(sortedUsings));
    }
}
```

### Edge Cases to Handle
```csharp
// 1. Aliased using directives
using Mvc = System.Web.Mvc;
// Should become:
using Mvc = Microsoft.AspNetCore.Mvc;

// 2. Static using directives
using static System.Web.Mvc.Html.InputExtensions;
// Should become:
using static Microsoft.AspNetCore.Mvc.Rendering.InputExtensions;

// 3. Nested namespaces
using System.Web.Mvc.Ajax;
// Should become:
using Microsoft.AspNetCore.Mvc; // Ajax merged into core

// 4. Fully qualified names in code
var controller = new System.Web.Mvc.Controller();
// Should become:
var controller = new Microsoft.AspNetCore.Mvc.Controller();

// 5. Global using (C# 10+)
global using System.Web.Mvc;
// Should become:
global using Microsoft.AspNetCore.Mvc;
```

### Unit Tests
```csharp
public class SystemWebMvcNamespaceRewriterTests
{
    [Fact]
    public async Task RewritesSimpleUsingDirective()
    {
        var source = @"
using System.Web.Mvc;

namespace MyApp.Controllers
{
    public class HomeController { }
}";

        var expected = @"
// Migrated from System.Web.Mvc
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    public class HomeController { }
}";

        var result = await RewriteAsync(source);
        Assert.Equal(expected, result, ignoreWhiteSpace: true);
    }

    [Fact]
    public async Task RewritesAliasedUsing()
    {
        var source = @"
using Mvc = System.Web.Mvc;

namespace MyApp
{
    public class Test : Mvc.Controller { }
}";

        var expected = @"
// Migrated from System.Web.Mvc
using Mvc = Microsoft.AspNetCore.Mvc;

namespace MyApp
{
    public class Test : Mvc.Controller { }
}";

        var result = await RewriteAsync(source);
        Assert.Equal(expected, result, ignoreWhiteSpace: true);
    }

    [Fact]
    public async Task RewritesFullyQualifiedNames()
    {
        var source = @"
namespace MyApp
{
    public class Test : System.Web.Mvc.Controller { }
}";

        var expected = @"
namespace MyApp
{
    public class Test : Microsoft.AspNetCore.Mvc.Controller { }
}";

        var result = await RewriteAsync(source);
        Assert.Equal(expected, result, ignoreWhiteSpace: true);
    }

    [Fact]
    public async Task RemovesDuplicateUsingsAfterRewrite()
    {
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace MyApp { }";

        var result = await RewriteAsync(source);

        var usings = GetUsings(result);
        Assert.Single(usings.Where(u => u.Contains("Microsoft.AspNetCore.Mvc")));
    }

    [Fact]
    public async Task HandlesStaticUsings()
    {
        var source = @"
using static System.Web.Mvc.Html.InputExtensions;

namespace MyApp { }";

        var expected = @"
using static Microsoft.AspNetCore.Mvc.Rendering.InputExtensions;

namespace MyApp { }";

        var result = await RewriteAsync(source);
        Assert.Contains("Microsoft.AspNetCore.Mvc.Rendering", result);
    }

    [Fact]
    public async Task HandlesGlobalUsings()
    {
        var source = @"
global using System.Web.Mvc;

namespace MyApp { }";

        var expected = @"
// Migrated from System.Web.Mvc
global using Microsoft.AspNetCore.Mvc;

namespace MyApp { }";

        var result = await RewriteAsync(source);
        Assert.Contains("global using Microsoft.AspNetCore.Mvc", result);
    }

    private async Task<string> RewriteAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location));

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        var rewriter = new SystemWebMvcNamespaceRewriter(semanticModel);
        var newRoot = rewriter.Visit(root);

        return newRoot?.ToFullString() ?? string.Empty;
    }

    private List<string> GetUsings(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        return root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString() ?? string.Empty)
            .ToList();
    }
}
```

## Progress Log
- [Created] - Task definition with Roslyn implementation details
