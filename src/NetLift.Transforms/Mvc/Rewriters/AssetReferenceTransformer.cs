using System.Text;
using System.Text.RegularExpressions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Transforms asset references in Razor views from bundle syntax to modern asset pipeline references.
/// </summary>
public sealed class AssetReferenceTransformer : IAssetReferenceTransformer
{
    private static readonly Regex StylesRenderRegex = new(
        @"@Styles\.Render\([""']([^""']+)[""']\)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ScriptsRenderRegex = new(
        @"@Scripts\.Render\([""']([^""']+)[""']\)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <inheritdoc />
    public string TransformRazorView(string viewContent, IEnumerable<BundleDefinition> bundles)
    {
        if (string.IsNullOrWhiteSpace(viewContent))
        {
            return viewContent;
        }

        if (bundles == null)
            throw new ArgumentNullException(nameof(bundles));

        var bundleList = bundles.ToList();
        var result = viewContent;

        // Build bundle lookup dictionary
        var bundleLookup = bundleList.ToDictionary(
            b => b.VirtualPath,
            b => b,
            StringComparer.OrdinalIgnoreCase);

        // Transform @Styles.Render() calls
        result = StylesRenderRegex.Replace(result, match =>
        {
            var virtualPath = match.Groups[1].Value;
            if (bundleLookup.TryGetValue(virtualPath, out var bundle))
            {
                return GenerateStyleReference(bundle);
            }

            // If bundle not found, add TODO comment
            return GenerateTodoComment("Style", virtualPath, match.Value);
        });

        // Transform @Scripts.Render() calls
        result = ScriptsRenderRegex.Replace(result, match =>
        {
            var virtualPath = match.Groups[1].Value;
            if (bundleLookup.TryGetValue(virtualPath, out var bundle))
            {
                return GenerateScriptReference(bundle);
            }

            // If bundle not found, add TODO comment
            return GenerateTodoComment("Script", virtualPath, match.Value);
        });

        return result;
    }

    /// <summary>
    /// Generates a modern style reference from a bundle definition.
    /// </summary>
    private static string GenerateStyleReference(BundleDefinition bundle)
    {
        var bundleName = GetBundleName(bundle.VirtualPath);
        var href = $"/dist/css/{bundleName}.css";

        var sb = new StringBuilder();
        sb.Append($"<link rel=\"stylesheet\" href=\"{href}\" asp-append-version=\"true\" />");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a modern script reference from a bundle definition.
    /// </summary>
    private static string GenerateScriptReference(BundleDefinition bundle)
    {
        var bundleName = GetBundleName(bundle.VirtualPath);
        var src = $"/dist/js/{bundleName}.js";

        var sb = new StringBuilder();
        sb.Append($"<script src=\"{src}\" asp-append-version=\"true\"></script>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a TODO comment for unmapped bundles.
    /// </summary>
    private static string GenerateTodoComment(string type, string virtualPath, string originalCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"@* TODO: Map {type} bundle '{virtualPath}' to modern asset pipeline *@");
        sb.Append($"@* Original: {originalCode} *@");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts a clean bundle name from the virtual path.
    /// </summary>
    private static string GetBundleName(string virtualPath)
    {
        // Extract name from virtual path (e.g., "~/bundles/jquery" -> "jquery", "~/Content/css" -> "css")
        var name = virtualPath
            .Replace("~/bundles/", "")
            .Replace("~/Bundle/", "")
            .Replace("~/Content/", "")
            .Replace("~/Scripts/", "")
            .Replace("~/", "");

        // Replace slashes with hyphens for nested paths
        name = name.Replace("/", "-");

        return name;
    }
}
