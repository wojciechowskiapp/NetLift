using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.SignalR;
using NetLift.Core.Models.SignalR;

namespace NetLift.Transforms.SignalR.Analyzers;

/// <summary>
/// Roslyn-based analyzer for GlobalHost usage.
/// </summary>
public class GlobalHostAnalyzer : IGlobalHostAnalyzer
{
    /// <inheritdoc />
    public GlobalHostUsageInfo? AnalyzeFile(string sourceCode, string filePath)
    {
        if (string.IsNullOrWhiteSpace(sourceCode) || !ContainsGlobalHost(sourceCode))
        {
            return null;
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var classDeclarations = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .ToList();

            var usages = new List<GlobalHostUsage>();
            var referencedHubTypes = new HashSet<string>();
            string? className = null;

            foreach (var classDecl in classDeclarations)
            {
                var classUsages = AnalyzeClassForGlobalHost(classDecl, referencedHubTypes);
                if (classUsages.Any())
                {
                    className ??= classDecl.Identifier.Text;
                    usages.AddRange(classUsages);
                }
            }

            if (!usages.Any())
            {
                return null;
            }

            var confidence = CalculateConfidence(usages, referencedHubTypes.Count);

            return new GlobalHostUsageInfo
            {
                FilePath = filePath,
                ClassName = className ?? "Unknown",
                Usages = usages,
                ReferencedHubTypes = referencedHubTypes.ToList(),
                Confidence = confidence
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlobalHostUsageInfo>> AnalyzeProjectAsync(string projectPath)
    {
        var results = new List<GlobalHostUsageInfo>();
        var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"));

        foreach (var file in csFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(file);
                var info = AnalyzeFile(sourceCode, file);
                if (info != null)
                {
                    results.Add(info);
                }
            }
            catch (IOException)
            {
                // Skip files that can't be read
            }
        }

        return results;
    }

    /// <inheritdoc />
    public bool ContainsGlobalHost(string sourceCode)
    {
        return sourceCode.Contains("GlobalHost") ||
               sourceCode.Contains("ConnectionManager.GetHubContext");
    }

    private static List<GlobalHostUsage> AnalyzeClassForGlobalHost(
        ClassDeclarationSyntax classDecl,
        HashSet<string> referencedHubTypes)
    {
        var usages = new List<GlobalHostUsage>();

        // Find GlobalHost.ConnectionManager.GetHubContext<T>() calls
        var invocations = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.ToString().Contains("GlobalHost"));

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();
            var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            // Extract hub type from GetHubContext<T>
            var hubType = ExtractHubType(invocation);
            if (!string.IsNullOrEmpty(hubType))
            {
                referencedHubTypes.Add(hubType);
            }

            var suggestedTransformation = GenerateSuggestedTransformation(hubType);

            usages.Add(new GlobalHostUsage
            {
                Pattern = GetPattern(invocationText),
                HubType = hubType ?? "unknown",
                LineNumber = lineNumber,
                OriginalCode = invocationText,
                SuggestedTransformation = suggestedTransformation
            });
        }

        return usages;
    }

    private static string? ExtractHubType(InvocationExpressionSyntax invocation)
    {
        // Look for GetHubContext<T> pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var name = memberAccess.Name;
            if (name is GenericNameSyntax genericName && genericName.Identifier.Text == "GetHubContext")
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                return typeArg?.ToString();
            }
        }

        // Try to extract from string
        var text = invocation.ToString();
        var startIndex = text.IndexOf("GetHubContext<", StringComparison.Ordinal);
        if (startIndex >= 0)
        {
            startIndex += "GetHubContext<".Length;
            var endIndex = text.IndexOf('>', startIndex);
            if (endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex);
            }
        }

        return null;
    }

    private static string GetPattern(string invocationText)
    {
        if (invocationText.Contains("GlobalHost.ConnectionManager.GetHubContext"))
        {
            return "GlobalHost.ConnectionManager.GetHubContext<T>";
        }

        if (invocationText.Contains("GlobalHost.DependencyResolver"))
        {
            return "GlobalHost.DependencyResolver";
        }

        if (invocationText.Contains("GlobalHost.HubPipeline"))
        {
            return "GlobalHost.HubPipeline";
        }

        return "GlobalHost";
    }

    private static string? GenerateSuggestedTransformation(string? hubType)
    {
        if (string.IsNullOrEmpty(hubType))
        {
            return null;
        }

        return $@"// Inject IHubContext<{hubType}> via constructor:
private readonly IHubContext<{hubType}> _hubContext;

public MyClass(IHubContext<{hubType}> hubContext)
{{
    _hubContext = hubContext;
}}

// Then use _hubContext instead of GlobalHost.ConnectionManager.GetHubContext<{hubType}>()";
    }

    private static int CalculateConfidence(List<GlobalHostUsage> usages, int hubTypeCount)
    {
        var confidence = 90;

        // Multiple different hub types reduce confidence
        if (hubTypeCount > 2)
        {
            confidence -= 10;
        }

        // HubPipeline or DependencyResolver patterns are harder to transform
        if (usages.Any(u => u.Pattern.Contains("HubPipeline") || u.Pattern.Contains("DependencyResolver")))
        {
            confidence -= 20;
        }

        return Math.Max(confidence, 50);
    }
}
