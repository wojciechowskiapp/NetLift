using System.Text.RegularExpressions;
using NetLift.Core.Interfaces.StaticFiles;
using NetLift.Core.Models.StaticFiles;

namespace NetLift.Transforms.StaticFiles.Analyzers;

/// <summary>
/// Analyzes static file folders and references in a project.
/// </summary>
public partial class StaticFilesAnalyzer : IStaticFilesAnalyzer
{
    private static readonly Dictionary<string, (string Target, StaticFolderType Type)> FolderMappings = new()
    {
        { "Content", ("wwwroot/css", StaticFolderType.Css) },
        { "Scripts", ("wwwroot/js", StaticFolderType.JavaScript) },
        { "Images", ("wwwroot/images", StaticFolderType.Images) },
        { "Content/images", ("wwwroot/images", StaticFolderType.Images) },
        { "fonts", ("wwwroot/fonts", StaticFolderType.Fonts) },
        { "Content/fonts", ("wwwroot/fonts", StaticFolderType.Fonts) }
    };

    // Reference patterns
    [GeneratedRegex(@"<link[^>]+href\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LinkHrefRegex();

    [GeneratedRegex(@"<script[^>]+src\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptSrcRegex();

    [GeneratedRegex(@"<img[^>]+src\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcRegex();

    [GeneratedRegex(@"@Url\.Content\s*\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled)]
    private static partial Regex UrlContentRegex();

    [GeneratedRegex(@"url\s*\(\s*[""']?([^""')]+)[""']?\s*\)", RegexOptions.Compiled)]
    private static partial Regex CssUrlRegex();

    /// <inheritdoc />
    public async Task<StaticFilesInfo> AnalyzeAsync(string projectPath)
    {
        var folders = DetectStaticFolders(projectPath);
        var references = await DetectReferencesAsync(projectPath);

        var totalFiles = folders.Sum(f => f.Files.Count);
        var totalSize = folders.Sum(f => f.SizeBytes);

        return new StaticFilesInfo
        {
            ProjectPath = projectPath,
            Folders = folders,
            References = references,
            HasContentFolder = folders.Any(f => f.SourcePath.Equals("Content", StringComparison.OrdinalIgnoreCase)),
            HasScriptsFolder = folders.Any(f => f.SourcePath.Equals("Scripts", StringComparison.OrdinalIgnoreCase)),
            HasImagesFolder = folders.Any(f => f.SourcePath.Contains("Images", StringComparison.OrdinalIgnoreCase)),
            HasFontsFolder = folders.Any(f => f.SourcePath.Contains("fonts", StringComparison.OrdinalIgnoreCase)),
            HasBundleConfig = File.Exists(Path.Combine(projectPath, "App_Start", "BundleConfig.cs")),
            HasWwwroot = Directory.Exists(Path.Combine(projectPath, "wwwroot")),
            TotalFiles = totalFiles,
            TotalSizeBytes = totalSize
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<StaticFolder> DetectStaticFolders(string projectPath)
    {
        var folders = new List<StaticFolder>();

        foreach (var (sourceName, (targetPath, folderType)) in FolderMappings)
        {
            var sourcePath = Path.Combine(projectPath, sourceName);
            if (Directory.Exists(sourcePath))
            {
                var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(sourcePath, f))
                    .ToList();

                var size = files.Sum(f =>
                {
                    var fullPath = Path.Combine(sourcePath, f);
                    return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                });

                folders.Add(new StaticFolder
                {
                    SourcePath = sourceName,
                    TargetPath = targetPath,
                    FolderType = folderType,
                    Files = files,
                    SizeBytes = size,
                    Exists = true
                });
            }
        }

        return folders;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticFileReference>> DetectReferencesAsync(string projectPath)
    {
        var references = new List<StaticFileReference>();

        // Scan Razor views
        var viewsPath = Path.Combine(projectPath, "Views");
        if (Directory.Exists(viewsPath))
        {
            var cshtmlFiles = Directory.GetFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories);
            foreach (var file in cshtmlFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                references.AddRange(DetectReferencesInContent(content, file));
            }
        }

        // Scan CSS files for url() references
        var contentPath = Path.Combine(projectPath, "Content");
        if (Directory.Exists(contentPath))
        {
            var cssFiles = Directory.GetFiles(contentPath, "*.css", SearchOption.AllDirectories);
            foreach (var file in cssFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                references.AddRange(DetectCssUrlReferences(content, file));
            }
        }

        return references;
    }

    /// <inheritdoc />
    public string MapToWwwroot(string originalPath)
    {
        if (string.IsNullOrEmpty(originalPath))
        {
            return originalPath;
        }

        var path = originalPath;

        // Common mappings
        if (path.StartsWith("~/Content/images/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("~/Content/images/", "~/images/", StringComparison.OrdinalIgnoreCase);
        }
        if (path.StartsWith("~/Content/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("~/Content/", "~/css/", StringComparison.OrdinalIgnoreCase);
        }
        if (path.StartsWith("~/Scripts/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("~/Scripts/", "~/js/", StringComparison.OrdinalIgnoreCase);
        }
        if (path.StartsWith("~/Images/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("~/Images/", "~/images/", StringComparison.OrdinalIgnoreCase);
        }
        if (path.StartsWith("~/fonts/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("~/fonts/", "~/fonts/", StringComparison.OrdinalIgnoreCase);
        }

        return path;
    }

    private IReadOnlyList<StaticFileReference> DetectReferencesInContent(string content, string filePath)
    {
        var references = new List<StaticFileReference>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Link href
            foreach (Match match in LinkHrefRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                if (IsStaticPath(path))
                {
                    references.Add(CreateReference(filePath, path, lineNumber, StaticReferenceType.LinkHref, match.Value));
                }
            }

            // Script src
            foreach (Match match in ScriptSrcRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                if (IsStaticPath(path))
                {
                    references.Add(CreateReference(filePath, path, lineNumber, StaticReferenceType.ScriptSrc, match.Value));
                }
            }

            // Img src
            foreach (Match match in ImgSrcRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                if (IsStaticPath(path))
                {
                    references.Add(CreateReference(filePath, path, lineNumber, StaticReferenceType.ImageSrc, match.Value));
                }
            }

            // Url.Content
            foreach (Match match in UrlContentRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                if (IsStaticPath(path))
                {
                    references.Add(CreateReference(filePath, path, lineNumber, StaticReferenceType.UrlContent, match.Value));
                }
            }
        }

        return references;
    }

    private IReadOnlyList<StaticFileReference> DetectCssUrlReferences(string content, string filePath)
    {
        var references = new List<StaticFileReference>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            foreach (Match match in CssUrlRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                if (!path.StartsWith("data:") && !path.StartsWith("http"))
                {
                    references.Add(CreateReference(filePath, path, lineNumber, StaticReferenceType.CssUrl, match.Value));
                }
            }
        }

        return references;
    }

    private StaticFileReference CreateReference(string sourceFile, string originalPath, int lineNumber, StaticReferenceType type, string originalCode)
    {
        return new StaticFileReference
        {
            SourceFile = sourceFile,
            OriginalPath = originalPath,
            NewPath = MapToWwwroot(originalPath),
            LineNumber = lineNumber,
            ReferenceType = type,
            OriginalCode = originalCode
        };
    }

    private static bool IsStaticPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.StartsWith("~/Content", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("~/Scripts", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("~/Images", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("~/fonts", StringComparison.OrdinalIgnoreCase);
    }
}
