namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms namespace references in Razor views from old .NET Framework namespaces to .NET Core equivalents.
/// </summary>
public interface IRazorNamespaceTransformer
{
    /// <summary>
    /// Transforms a Razor view to use modern namespace references.
    /// </summary>
    /// <param name="viewContent">The Razor view content.</param>
    /// <returns>The transformed view content.</returns>
    string TransformRazorView(string viewContent);
}
