using NetLift.Core.Models.Razor;

namespace NetLift.Core.Interfaces.Razor;

/// <summary>
/// Transforms MVC5 Razor views to ASP.NET Core Tag Helper syntax.
/// </summary>
public interface IRazorViewTransformer
{
    /// <summary>
    /// Transforms a Razor view to use Tag Helpers.
    /// </summary>
    /// <param name="viewInfo">The analyzed view information.</param>
    /// <returns>The transformation result.</returns>
    Task<RazorViewTransformResult> TransformViewAsync(RazorViewInfo viewInfo);

    /// <summary>
    /// Transforms view content directly.
    /// </summary>
    /// <param name="content">The view content.</param>
    /// <param name="filePath">The file path for context.</param>
    /// <returns>The transformed content.</returns>
    string TransformContent(string content, string filePath);

    /// <summary>
    /// Transforms an HTML helper usage to a Tag Helper.
    /// </summary>
    /// <param name="helper">The HTML helper usage.</param>
    /// <returns>The transformed Tag Helper code.</returns>
    string TransformHtmlHelper(HtmlHelperUsage helper);

    /// <summary>
    /// Transforms a bundle reference to direct script/link tags.
    /// </summary>
    /// <param name="bundle">The bundle reference.</param>
    /// <returns>The transformed code.</returns>
    string TransformBundleReference(BundleReference bundle);

    /// <summary>
    /// Transforms all views in a project.
    /// </summary>
    /// <param name="projectPath">The project directory path.</param>
    /// <param name="dryRun">If true, don't write changes to disk.</param>
    /// <returns>List of transformation results.</returns>
    Task<IReadOnlyList<RazorViewTransformResult>> TransformProjectViewsAsync(string projectPath, bool dryRun = false);
}
