using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis.Parsers;

/// <summary>
/// Parser for SDK-style .csproj files (.NET Core / .NET 5+).
/// SDK-style projects use implicit file globbing, so this parser enumerates
/// .cs files from the filesystem instead of relying on explicit Compile items.
/// </summary>
public class SdkProjectParser : IProjectParser
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj",
        "bin",
        ".git",
        ".vs",
        "node_modules",
        "packages",
        "Migrations",      // EF6 migrations are incompatible with EF Core
        "App_Start",       // ASP.NET MVC specific, obsolete in Core
        "App_Data"         // Often contains local DB files
    };

    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssemblyInfo.cs",     // Conflicts with SDK auto-generated assembly info
        "Global.asax.cs",      // Replaced by Program.cs in ASP.NET Core
        "Startup.cs"           // May need manual review, skip auto-transformation
    };

    /// <inheritdoc/>
    public bool CanParse(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return false;
        }

        try
        {
            var doc = XDocument.Load(projectPath);
            var root = doc.Root;

            // SDK-style projects have the Sdk attribute on the Project element
            return root?.Attribute("Sdk") != null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectInfo> AnalyzeAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        var doc = await Task.Run(() => XDocument.Load(projectPath), cancellationToken);
        var root = doc.Root!;

        var projectInfo = new ProjectInfo
        {
            FilePath = Path.GetFullPath(projectPath),
            Name = Path.GetFileNameWithoutExtension(projectPath),
            Format = ProjectFormat.SdkStyle
        };

        // Extract properties
        ExtractProperties(doc, projectInfo);

        // Extract project references
        ExtractProjectReferences(doc, projectInfo);

        // Extract package references
        ExtractPackageReferences(doc, projectInfo);

        // SDK-style projects use implicit file globbing - enumerate from filesystem
        await EnumerateCompileItemsFromFilesystemAsync(projectPath, doc, projectInfo, cancellationToken);

        // Extract content items
        ExtractContentItems(doc, projectInfo);

        return projectInfo;
    }

    private void ExtractProperties(XDocument doc, ProjectInfo projectInfo)
    {
        var propertyGroups = doc.Descendants("PropertyGroup");

        foreach (var propertyGroup in propertyGroups)
        {
            // TargetFramework (SDK-style)
            var targetFramework = propertyGroup.Element("TargetFramework")?.Value;
            if (!string.IsNullOrEmpty(targetFramework))
            {
                projectInfo.TargetFramework = ParseTargetFramework(targetFramework);
            }

            // TargetFrameworks (multi-targeting)
            var targetFrameworks = propertyGroup.Element("TargetFrameworks")?.Value;
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                // Take the first framework for primary analysis
                var firstFramework = targetFrameworks.Split(';').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstFramework))
                {
                    projectInfo.TargetFramework = ParseTargetFramework(firstFramework);
                }
            }

            // AssemblyName
            var assemblyName = propertyGroup.Element("AssemblyName")?.Value;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                projectInfo.AssemblyName = assemblyName;
            }

            // RootNamespace
            var rootNamespace = propertyGroup.Element("RootNamespace")?.Value;
            if (!string.IsNullOrEmpty(rootNamespace))
            {
                projectInfo.RootNamespace = rootNamespace;
            }

            // OutputType
            var outputType = propertyGroup.Element("OutputType")?.Value;
            if (!string.IsNullOrEmpty(outputType))
            {
                projectInfo.OutputType = outputType;
            }

            // Store all other properties
            foreach (var element in propertyGroup.Elements())
            {
                var key = element.Name.LocalName;
                var value = element.Value;

                if (!string.IsNullOrEmpty(value) && !projectInfo.Properties.ContainsKey(key))
                {
                    projectInfo.Properties[key] = value;
                }
            }
        }

        // Default RootNamespace to project name if not specified
        if (string.IsNullOrEmpty(projectInfo.RootNamespace))
        {
            projectInfo.RootNamespace = projectInfo.Name;
        }
    }

    private TargetFramework ParseTargetFramework(string frameworkMoniker)
    {
        var targetFramework = new TargetFramework
        {
            OriginalVersion = frameworkMoniker,
            Moniker = frameworkMoniker
        };

        // Parse net8.0, net6.0, netcoreapp3.1, netstandard2.0, net472, etc.
        if (frameworkMoniker.StartsWith("net") && !frameworkMoniker.StartsWith("netcoreapp") && !frameworkMoniker.StartsWith("netstandard"))
        {
            // Modern .NET (net5.0+) or .NET Framework (net472)
            var versionPart = frameworkMoniker.Substring(3);

            if (versionPart.Contains('.'))
            {
                // Modern .NET (net8.0, net6.0, etc.)
                targetFramework.Type = FrameworkType.Net;
                if (Version.TryParse(versionPart, out var version))
                {
                    targetFramework.Version = version;
                }
            }
            else
            {
                // .NET Framework (net472, net48)
                targetFramework.Type = FrameworkType.Framework;
                // Convert net472 to 4.7.2
                if (versionPart.Length >= 2)
                {
                    var major = versionPart[0].ToString();
                    var minor = versionPart.Length > 1 ? versionPart[1].ToString() : "0";
                    var patch = versionPart.Length > 2 ? versionPart[2].ToString() : "0";
                    if (Version.TryParse($"{major}.{minor}.{patch}", out var version))
                    {
                        targetFramework.Version = version;
                    }
                }
            }
        }
        else if (frameworkMoniker.StartsWith("netcoreapp"))
        {
            targetFramework.Type = FrameworkType.Core;
            var versionPart = frameworkMoniker.Substring("netcoreapp".Length);
            if (Version.TryParse(versionPart, out var version))
            {
                targetFramework.Version = version;
            }
        }
        else if (frameworkMoniker.StartsWith("netstandard"))
        {
            targetFramework.Type = FrameworkType.Standard;
            var versionPart = frameworkMoniker.Substring("netstandard".Length);
            if (Version.TryParse(versionPart, out var version))
            {
                targetFramework.Version = version;
            }
        }

        return targetFramework;
    }

    private void ExtractProjectReferences(XDocument doc, ProjectInfo projectInfo)
    {
        var projectReferences = doc.Descendants("ProjectReference");

        foreach (var reference in projectReferences)
        {
            var includeAttr = reference.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var projectRef = new ProjectReference
            {
                Path = includeAttr,
                Name = Path.GetFileNameWithoutExtension(includeAttr)
            };

            projectInfo.ProjectReferences.Add(projectRef);
        }
    }

    private void ExtractPackageReferences(XDocument doc, ProjectInfo projectInfo)
    {
        var packageReferences = doc.Descendants("PackageReference");

        foreach (var reference in packageReferences)
        {
            var includeAttr = reference.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var versionAttr = reference.Attribute("Version")?.Value ??
                              reference.Element("Version")?.Value ??
                              "*";

            var packageRef = new PackageReference
            {
                Id = includeAttr,
                Version = versionAttr
            };

            projectInfo.PackageReferences.Add(packageRef);
        }
    }

    private async Task EnumerateCompileItemsFromFilesystemAsync(
        string projectPath,
        XDocument doc,
        ProjectInfo projectInfo,
        CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDir))
        {
            throw new ArgumentException($"Invalid path - cannot determine directory: {projectPath}", nameof(projectPath));
        }

        // Get explicit excludes from the project file
        var excludePatterns = GetExcludePatterns(doc);

        // Enumerate all .cs files
        var csFiles = await Task.Run(() =>
        {
            return EnumerateCsFiles(projectDir, excludePatterns);
        }, cancellationToken);

        foreach (var csFile in csFiles)
        {
            // Make path relative to project directory
            var relativePath = Path.GetRelativePath(projectDir, csFile);

            projectInfo.CompileItems.Add(new CompileItem
            {
                Include = relativePath
            });
        }
    }

    private HashSet<string> GetExcludePatterns(XDocument doc)
    {
        var excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check for <Compile Remove="..."/> elements
        var removeElements = doc.Descendants("Compile")
            .Where(e => e.Attribute("Remove") != null);

        foreach (var element in removeElements)
        {
            var removePattern = element.Attribute("Remove")?.Value;
            if (!string.IsNullOrEmpty(removePattern))
            {
                excludes.Add(removePattern);
            }
        }

        return excludes;
    }

    private IEnumerable<string> EnumerateCsFiles(string projectDir, HashSet<string> excludePatterns)
    {
        var allCsFiles = new List<string>();

        try
        {
            EnumerateCsFilesRecursive(projectDir, projectDir, excludePatterns, allCsFiles);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }

        return allCsFiles;
    }

    private void EnumerateCsFilesRecursive(
        string rootDir,
        string currentDir,
        HashSet<string> excludePatterns,
        List<string> results)
    {
        // Check if this directory should be excluded
        var dirName = Path.GetFileName(currentDir);
        if (ExcludedDirectories.Contains(dirName))
        {
            return;
        }

        try
        {
            // Add .cs files from current directory
            foreach (var file in Directory.EnumerateFiles(currentDir, "*.cs"))
            {
                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(rootDir, file);

                // Skip excluded files (AssemblyInfo.cs, Global.asax.cs, etc.)
                if (ExcludedFiles.Contains(fileName))
                {
                    continue;
                }

                // Check if file matches any exclude pattern
                if (!IsExcluded(relativePath, excludePatterns))
                {
                    results.Add(file);
                }
            }

            // Recurse into subdirectories
            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
                EnumerateCsFilesRecursive(rootDir, subDir, excludePatterns, results);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Skip directories that no longer exist
        }
    }

    private bool IsExcluded(string relativePath, HashSet<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            // Simple wildcard matching
            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*\\*", ".*")
                    .Replace("\\*", "[^/\\\\]*") + "$";

                if (System.Text.RegularExpressions.Regex.IsMatch(relativePath, regexPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else if (relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ExtractContentItems(XDocument doc, ProjectInfo projectInfo)
    {
        var contentItems = doc.Descendants("Content")
            .Concat(doc.Descendants("None"));

        foreach (var item in contentItems)
        {
            var includeAttr = item.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var contentItem = new ContentItem
            {
                Include = includeAttr
            };

            // Extract CopyToOutputDirectory
            var copyToOutputDirectory = item.Attribute("CopyToOutputDirectory")?.Value ??
                                         item.Element("CopyToOutputDirectory")?.Value;
            if (!string.IsNullOrEmpty(copyToOutputDirectory))
            {
                contentItem.CopyToOutputDirectory = copyToOutputDirectory;
            }

            projectInfo.ContentItems.Add(contentItem);
        }
    }
}
