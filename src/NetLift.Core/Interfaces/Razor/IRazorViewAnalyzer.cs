using NetLift.Core.Models.Razor;

namespace NetLift.Core.Interfaces.Razor;

/// <summary>
/// Analyzes Razor view files to identify HTML helpers and other patterns that need transformation.
/// </summary>
public interface IRazorViewAnalyzer
{
    /// <summary>
    /// Analyzes a single Razor view file.
    /// </summary>
    /// <param name="filePath">The path to the .cshtml file.</param>
    /// <returns>Information about the analyzed view.</returns>
    Task<RazorViewInfo> AnalyzeViewAsync(string filePath);

    /// <summary>
    /// Analyzes a Razor view from its content.
    /// </summary>
    /// <param name="content">The view content.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>Information about the analyzed view.</returns>
    RazorViewInfo AnalyzeView(string content, string filePath);

    /// <summary>
    /// Analyzes all Razor views in a project.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <returns>List of analyzed views.</returns>
    Task<IReadOnlyList<RazorViewInfo>> AnalyzeProjectViewsAsync(string projectPath);

    /// <summary>
    /// Detects HTML helper usages in view content.
    /// </summary>
    /// <param name="content">The view content.</param>
    /// <returns>List of HTML helper usages.</returns>
    IReadOnlyList<HtmlHelperUsage> DetectHtmlHelpers(string content);

    /// <summary>
    /// Detects bundle references in view content.
    /// </summary>
    /// <param name="content">The view content.</param>
    /// <returns>List of bundle references.</returns>
    IReadOnlyList<BundleReference> DetectBundleReferences(string content);
}
