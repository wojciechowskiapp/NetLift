using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Transforms.Converters;

/// <summary>
/// Converts old-style project references to SDK-style format.
/// </summary>
public class ProjectReferenceConverter : IProjectReferenceConverter
{
    private readonly ILogger<ProjectReferenceConverter>? _logger;

    // Metadata that should be preserved in SDK-style project references
    private static readonly HashSet<string> PreserveMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReferenceOutputAssembly",  // Build-time only references
        "PrivateAssets",            // Analyzer/tools references
        "IncludeAssets",            // Asset filtering
        "ExcludeAssets",            // Asset filtering
        "Aliases",                  // Extern aliases
        "EmbedInteropTypes"         // COM interop
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectReferenceConverter"/> class.
    /// </summary>
    public ProjectReferenceConverter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectReferenceConverter"/> class with logging.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ProjectReferenceConverter(ILogger<ProjectReferenceConverter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public XElement? ConvertProjectReferences(List<ProjectReference> references, string sourceProjectPath)
    {
        ArgumentNullException.ThrowIfNull(references);

        if (string.IsNullOrWhiteSpace(sourceProjectPath))
        {
            throw new ArgumentException("Source project path cannot be null or empty.", nameof(sourceProjectPath));
        }

        if (references.Count == 0)
        {
            return null;
        }

        var itemGroup = new XElement("ItemGroup");

        // Order references alphabetically for consistency
        foreach (var reference in references.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase))
        {
            var converted = ConvertReference(reference, sourceProjectPath);
            if (converted != null)
            {
                itemGroup.Add(converted);
            }
        }

        return itemGroup.HasElements ? itemGroup : null;
    }

    /// <summary>
    /// Converts a single project reference to SDK-style format.
    /// </summary>
    /// <param name="reference">The project reference to convert.</param>
    /// <param name="sourceProjectPath">The path to the source project file.</param>
    /// <returns>An XElement representing the converted project reference, or null if it should be skipped.</returns>
    private XElement? ConvertReference(ProjectReference reference, string sourceProjectPath)
    {
        // Validate reference path exists
        var absolutePath = ResolveRelativePath(sourceProjectPath, reference.Path);

        if (!File.Exists(absolutePath))
        {
            _logger?.LogWarning(
                "Project reference not found: {Path} (referenced from {Source})",
                reference.Path,
                sourceProjectPath);

            return CreateCommentedReference(reference, "Path not found");
        }

        // Normalize the path for cross-platform compatibility
        var normalizedPath = NormalizePath(reference.Path);

        // Create simple ProjectReference (no GUID, no Name)
        var projectRef = new XElement("ProjectReference",
            new XAttribute("Include", normalizedPath));

        // Preserve important metadata
        PreserveImportantMetadata(reference, projectRef);

        return projectRef;
    }

    /// <summary>
    /// Resolves a relative path to an absolute path.
    /// </summary>
    /// <param name="basePath">The base path (project file path).</param>
    /// <param name="relativePath">The relative path to resolve.</param>
    /// <returns>The absolute path.</returns>
    private string ResolveRelativePath(string basePath, string relativePath)
    {
        var baseDir = System.IO.Path.GetDirectoryName(basePath) ?? string.Empty;
        var combined = System.IO.Path.Combine(baseDir, relativePath);
        return System.IO.Path.GetFullPath(combined);
    }

    /// <summary>
    /// Normalizes a path for cross-platform compatibility.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    private string NormalizePath(string path)
    {
        // Convert backslashes to forward slashes for consistency
        var normalized = path.Replace('\\', '/');

        // Simplify path (remove redundant ../ segments where possible)
        normalized = SimplifyPath(normalized);

        return normalized;
    }

    /// <summary>
    /// Simplifies a path by removing redundant directory traversals.
    /// </summary>
    /// <param name="path">The path to simplify.</param>
    /// <returns>The simplified path.</returns>
    private string SimplifyPath(string path)
    {
        var segments = path.Split('/', '\\');
        var stack = new Stack<string>();

        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                if (stack.Count > 0 && stack.Peek() != "..")
                {
                    stack.Pop();
                }
                else
                {
                    stack.Push(segment);
                }
            }
            else if (segment != "." && !string.IsNullOrEmpty(segment))
            {
                stack.Push(segment);
            }
        }

        return string.Join("/", stack.Reverse());
    }

    /// <summary>
    /// Preserves important metadata from the old-style reference.
    /// </summary>
    /// <param name="reference">The source project reference.</param>
    /// <param name="projectRefElement">The target XElement to add metadata to.</param>
    private void PreserveImportantMetadata(ProjectReference reference, XElement projectRefElement)
    {
        foreach (var metadata in reference.Metadata)
        {
            if (PreserveMetadata.Contains(metadata.Key))
            {
                projectRefElement.Add(new XElement(metadata.Key, metadata.Value));
            }
        }
    }

    /// <summary>
    /// Creates a commented reference element for references that couldn't be converted.
    /// </summary>
    /// <param name="reference">The project reference.</param>
    /// <param name="reason">The reason why it couldn't be converted.</param>
    /// <returns>An XElement with a comment explaining the issue.</returns>
    private XElement CreateCommentedReference(ProjectReference reference, string reason)
    {
        return new XElement("ProjectReference",
            new XComment($" MIGRATION WARNING: {reason} - {reference.Path} "),
            new XAttribute("Include", reference.Path));
    }
}
