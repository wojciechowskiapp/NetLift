using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Transforms.Common;

/// <summary>
/// Extension methods for Roslyn syntax rewriters to share common functionality.
/// </summary>
internal static class RoslynRewriterExtensions
{
    /// <summary>
    /// Adds required using directives to a compilation unit if they don't already exist.
    /// Parses each using directive properly to ensure correct formatting.
    /// </summary>
    /// <param name="root">The syntax node (must be CompilationUnitSyntax).</param>
    /// <param name="usings">Collection of namespace strings to add as using directives.</param>
    /// <returns>The updated syntax node with new using directives added.</returns>
    public static CompilationUnitSyntax AddRequiredUsings(
        this CompilationUnitSyntax root,
        IEnumerable<string> usings)
    {
        var usingsList = usings.ToList();

        if (usingsList.Count == 0)
        {
            return root;
        }

        var existingUsings = root.Usings
            .Select(u => u.Name?.ToString())
            .Where(n => n != null)
            .ToHashSet(StringComparer.Ordinal);

        var newUsings = usingsList
            .Where(ns => !existingUsings.Contains(ns) && !string.IsNullOrWhiteSpace(ns))
            .Select(ns =>
            {
                // Parse complete using directive to ensure proper spacing
                var usingCode = $"using {ns};";
                var usingTree = CSharpSyntaxTree.ParseText(usingCode);
                var parsedUsing = usingTree.GetRoot()
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .FirstOrDefault();

                if (parsedUsing is null)
                {
                    throw new InvalidOperationException($"Failed to parse using directive: {ns}");
                }

                return parsedUsing.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
            })
            .ToList();

        if (newUsings.Count > 0)
        {
            return root.AddUsings(newUsings.ToArray());
        }

        return root;
    }
}
