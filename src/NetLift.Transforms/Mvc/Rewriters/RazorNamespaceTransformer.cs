using System.Text.RegularExpressions;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Transforms namespace references in Razor views from old .NET Framework namespaces to .NET Core equivalents.
/// Handles PagedList → X.PagedList transformations and other common namespace migrations.
/// </summary>
public sealed class RazorNamespaceTransformer : IRazorNamespaceTransformer
{
    // Pattern to match @using PagedList.Mvc (with optional whitespace and semicolon)
    private static readonly Regex UsingPagedListMvcRegex = new(
        @"@using\s+PagedList\.Mvc\s*;?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Pattern to match @using PagedList (but not PagedList.Mvc)
    private static readonly Regex UsingPagedListRegex = new(
        @"@using\s+PagedList(?!\.Mvc)\s*;?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Pattern to match PagedList.IPagedList< (in @model declarations or code)
    private static readonly Regex PagedListTypeRegex = new(
        @"\bPagedList\.(IPagedList|PagedList)\s*<",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <inheritdoc />
    public string TransformRazorView(string viewContent)
    {
        if (string.IsNullOrWhiteSpace(viewContent))
        {
            return viewContent;
        }

        var result = viewContent;

        // Transform @using directives
        // Important: Transform PagedList.Mvc BEFORE PagedList to avoid double transformation
        result = UsingPagedListMvcRegex.Replace(result, "@using X.PagedList.Mvc.Core");
        result = UsingPagedListRegex.Replace(result, "@using X.PagedList");

        // Transform type references (IPagedList<T>, PagedList<T>)
        result = PagedListTypeRegex.Replace(result, match =>
        {
            var typeName = match.Groups[1].Value; // IPagedList or PagedList
            return $"X.PagedList.{typeName}<";
        });

        return result;
    }
}
