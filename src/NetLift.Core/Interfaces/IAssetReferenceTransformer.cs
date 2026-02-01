using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms asset references in Razor views from bundle syntax to modern asset pipeline references.
/// </summary>
public interface IAssetReferenceTransformer
{
    /// <summary>
    /// Transforms a Razor view to use modern asset references instead of bundle syntax.
    /// </summary>
    /// <param name="viewContent">The Razor view content.</param>
    /// <param name="bundles">The bundle definitions for mapping references.</param>
    /// <returns>The transformed view content.</returns>
    string TransformRazorView(string viewContent, IEnumerable<BundleDefinition> bundles);
}
