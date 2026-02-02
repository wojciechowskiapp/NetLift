using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis.Parsers;

/// <summary>
/// Parser for old-style (non-SDK) .csproj files.
/// </summary>
public class OldFormatProjectParser : IProjectParser
{
    private static readonly XNamespace MsBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

    // Files to exclude from migration (cause conflicts or are obsolete in .NET Core)
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssemblyInfo.cs",     // Conflicts with SDK auto-generated assembly info
        "Global.asax.cs",      // Replaced by Program.cs in ASP.NET Core
    };

    // Directories to exclude from migration
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Migrations",      // EF6 migrations are incompatible with EF Core
        "App_Start",       // ASP.NET MVC specific, obsolete in Core
        "Properties"       // AssemblyInfo.cs is in Properties folder
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

            // Old-style projects have the MSBuild namespace and don't have Sdk attribute
            return root?.Name.Namespace == MsBuildNamespace &&
                   root.Attribute("Sdk") == null;
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
        var ns = doc.Root?.Name.Namespace ?? MsBuildNamespace;

        var projectInfo = new ProjectInfo
        {
            FilePath = Path.GetFullPath(projectPath),
            Name = Path.GetFileNameWithoutExtension(projectPath),
            Format = ProjectFormat.OldStyle
        };

        // Extract properties from all PropertyGroup elements
        ExtractProperties(doc, ns, projectInfo);

        // Extract references
        ExtractReferences(doc, ns, projectInfo);

        // Extract project references
        ExtractProjectReferences(doc, ns, projectInfo);

        // Extract compile items
        ExtractCompileItems(doc, ns, projectInfo);

        // Extract content items
        ExtractContentItems(doc, ns, projectInfo);

        // Extract embedded resources
        ExtractEmbeddedResources(doc, ns, projectInfo);

        return projectInfo;
    }

    private void ExtractProperties(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        // Get all PropertyGroup elements (unconditional and conditional)
        var propertyGroups = doc.Descendants(ns + "PropertyGroup");

        foreach (var propertyGroup in propertyGroups)
        {
            // TargetFrameworkVersion
            var targetFrameworkVersion = propertyGroup.Element(ns + "TargetFrameworkVersion")?.Value;
            if (!string.IsNullOrEmpty(targetFrameworkVersion))
            {
                projectInfo.TargetFramework = ParseTargetFramework(targetFrameworkVersion);
            }

            // AssemblyName
            var assemblyName = propertyGroup.Element(ns + "AssemblyName")?.Value;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                projectInfo.AssemblyName = assemblyName;
            }

            // RootNamespace
            var rootNamespace = propertyGroup.Element(ns + "RootNamespace")?.Value;
            if (!string.IsNullOrEmpty(rootNamespace))
            {
                projectInfo.RootNamespace = rootNamespace;
            }

            // OutputType
            var outputType = propertyGroup.Element(ns + "OutputType")?.Value;
            if (!string.IsNullOrEmpty(outputType))
            {
                projectInfo.OutputType = outputType;
            }

            // ProjectGuid
            var projectGuid = propertyGroup.Element(ns + "ProjectGuid")?.Value;
            if (!string.IsNullOrEmpty(projectGuid))
            {
                projectInfo.ProjectGuid = projectGuid;
            }

            // ProjectTypeGuids
            var projectTypeGuids = propertyGroup.Element(ns + "ProjectTypeGuids")?.Value;
            if (!string.IsNullOrEmpty(projectTypeGuids))
            {
                projectInfo.ProjectTypeGuids = projectTypeGuids
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .ToList();
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
    }

    private TargetFramework ParseTargetFramework(string frameworkVersion)
    {
        var targetFramework = new TargetFramework
        {
            OriginalVersion = frameworkVersion
        };

        // Parse version (e.g., "v4.8" -> "4.8")
        var versionString = frameworkVersion.TrimStart('v', 'V');

        if (Version.TryParse(versionString, out var version))
        {
            targetFramework.Version = version;
        }

        // Determine framework type and moniker
        if (frameworkVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            // .NET Framework
            targetFramework.Type = FrameworkType.Framework;
            targetFramework.Moniker = $"net{versionString.Replace(".", "")}";
        }
        else
        {
            targetFramework.Type = FrameworkType.Unknown;
            targetFramework.Moniker = frameworkVersion;
        }

        return targetFramework;
    }

    private void ExtractReferences(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        var references = doc.Descendants(ns + "Reference");

        foreach (var reference in references)
        {
            var includeAttr = reference.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var assemblyRef = new AssemblyReference();

            // Parse the Include attribute which may contain version, culture, etc.
            // Format: "System.Web.Mvc, Version=5.2.7.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
            var parts = includeAttr.Split(',').Select(p => p.Trim()).ToArray();
            assemblyRef.Name = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var keyValue = parts[i].Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "version":
                            assemblyRef.Version = value;
                            break;
                        case "culture":
                            assemblyRef.Culture = value;
                            break;
                        case "publickeytoken":
                            assemblyRef.PublicKeyToken = value;
                            break;
                        case "processorarchitecture":
                            assemblyRef.ProcessorArchitecture = value;
                            break;
                    }
                }
            }

            // Extract HintPath (indicates NuGet package)
            var hintPath = reference.Element(ns + "HintPath")?.Value;
            if (!string.IsNullOrEmpty(hintPath))
            {
                // Normalize path separators for cross-platform compatibility
                assemblyRef.HintPath = NormalizePath(hintPath);
            }

            // Extract Private
            var privateElement = reference.Element(ns + "Private")?.Value;
            if (!string.IsNullOrEmpty(privateElement) && bool.TryParse(privateElement, out var isPrivate))
            {
                assemblyRef.IsPrivate = isPrivate;
            }

            projectInfo.References.Add(assemblyRef);
        }
    }

    private void ExtractProjectReferences(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        var projectReferences = doc.Descendants(ns + "ProjectReference");

        foreach (var reference in projectReferences)
        {
            var includeAttr = reference.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            // Normalize path separators for cross-platform compatibility
            var normalizedPath = NormalizePath(includeAttr);
            var projectRef = new ProjectReference
            {
                Path = normalizedPath,
                Name = Path.GetFileNameWithoutExtension(normalizedPath)
            };

            // Extract Project GUID
            var projectGuid = reference.Element(ns + "Project")?.Value;
            if (!string.IsNullOrEmpty(projectGuid))
            {
                projectRef.Guid = projectGuid;
            }

            // Extract Name (if explicitly specified)
            var name = reference.Element(ns + "Name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                projectRef.Name = name;
            }

            projectInfo.ProjectReferences.Add(projectRef);
        }
    }

    /// <summary>
    /// Normalizes path separators for cross-platform compatibility.
    /// Converts backslashes to the current platform's directory separator.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private void ExtractCompileItems(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        var compileItems = doc.Descendants(ns + "Compile");

        foreach (var item in compileItems)
        {
            var includeAttr = item.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            // Skip excluded files (AssemblyInfo.cs, Global.asax.cs, etc.)
            var fileName = Path.GetFileName(includeAttr);
            if (ExcludedFiles.Contains(fileName))
            {
                continue;
            }

            // Skip files in excluded directories (Migrations, App_Start, Properties)
            var pathParts = includeAttr.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length > 1 && ExcludedDirectories.Contains(pathParts[0]))
            {
                continue;
            }

            var compileItem = new CompileItem
            {
                // Normalize path separators for cross-platform compatibility
                Include = NormalizePath(includeAttr)
            };

            // Extract DependentUpon
            var dependentUpon = item.Element(ns + "DependentUpon")?.Value;
            if (!string.IsNullOrEmpty(dependentUpon))
            {
                compileItem.DependentUpon = NormalizePath(dependentUpon);
            }

            // Extract SubType
            var subType = item.Element(ns + "SubType")?.Value;
            if (!string.IsNullOrEmpty(subType))
            {
                compileItem.SubType = subType;
            }

            projectInfo.CompileItems.Add(compileItem);
        }
    }

    private void ExtractContentItems(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        var contentItems = doc.Descendants(ns + "Content")
            .Concat(doc.Descendants(ns + "None"));

        foreach (var item in contentItems)
        {
            var includeAttr = item.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var contentItem = new ContentItem
            {
                // Normalize path separators for cross-platform compatibility
                Include = NormalizePath(includeAttr)
            };

            // Extract CopyToOutputDirectory
            var copyToOutputDirectory = item.Element(ns + "CopyToOutputDirectory")?.Value;
            if (!string.IsNullOrEmpty(copyToOutputDirectory))
            {
                contentItem.CopyToOutputDirectory = copyToOutputDirectory;
            }

            projectInfo.ContentItems.Add(contentItem);
        }
    }

    private void ExtractEmbeddedResources(XDocument doc, XNamespace ns, ProjectInfo projectInfo)
    {
        var embeddedResources = doc.Descendants(ns + "EmbeddedResource");

        foreach (var item in embeddedResources)
        {
            var includeAttr = item.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(includeAttr))
            {
                continue;
            }

            var embeddedResource = new EmbeddedResource
            {
                // Normalize path separators for cross-platform compatibility
                Include = NormalizePath(includeAttr)
            };

            // Extract DependentUpon
            var dependentUpon = item.Element(ns + "DependentUpon")?.Value;
            if (!string.IsNullOrEmpty(dependentUpon))
            {
                embeddedResource.DependentUpon = NormalizePath(dependentUpon);
            }

            // Extract Generator
            var generator = item.Element(ns + "Generator")?.Value;
            if (!string.IsNullOrEmpty(generator))
            {
                embeddedResource.Generator = generator;
            }

            // Extract LastGenOutput
            var lastGenOutput = item.Element(ns + "LastGenOutput")?.Value;
            if (!string.IsNullOrEmpty(lastGenOutput))
            {
                embeddedResource.LastGenOutput = NormalizePath(lastGenOutput);
            }

            projectInfo.EmbeddedResources.Add(embeddedResource);
        }
    }
}
