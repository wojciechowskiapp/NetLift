using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Transforms.Converters;

namespace NetLift.Transforms;

/// <summary>
/// Converts old-format .csproj files to modern SDK-style format.
/// </summary>
public class SdkProjectConverter : ISdkProjectConverter
{
    private readonly IProjectReferenceConverter _projectReferenceConverter;

    private static readonly Dictionary<string, string> FrameworkVersionMap = new()
    {
        ["v4.5"] = "net45",
        ["v4.5.1"] = "net451",
        ["v4.5.2"] = "net452",
        ["v4.6"] = "net46",
        ["v4.6.1"] = "net461",
        ["v4.6.2"] = "net462",
        ["v4.7"] = "net47",
        ["v4.7.1"] = "net471",
        ["v4.7.2"] = "net472",
        ["v4.8"] = "net48",
        ["v4.8.1"] = "net481",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SdkProjectConverter"/> class.
    /// </summary>
    public SdkProjectConverter()
    {
        _projectReferenceConverter = new ProjectReferenceConverter();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SdkProjectConverter"/> class with dependencies.
    /// </summary>
    /// <param name="projectReferenceConverter">The project reference converter.</param>
    public SdkProjectConverter(IProjectReferenceConverter projectReferenceConverter)
    {
        _projectReferenceConverter = projectReferenceConverter ?? throw new ArgumentNullException(nameof(projectReferenceConverter));
    }

    /// <summary>
    /// Converts a ProjectInfo from old-format to SDK-style XML.
    /// </summary>
    /// <param name="projectInfo">The parsed old-format project information.</param>
    /// <returns>The generated SDK-style project XML document.</returns>
    public XDocument Convert(ProjectInfo projectInfo)
    {
        return Convert(projectInfo, null);
    }

    /// <summary>
    /// Converts a ProjectInfo from old-format to SDK-style XML with options.
    /// </summary>
    /// <param name="projectInfo">The parsed old-format project information.</param>
    /// <param name="targetFramework">Optional target framework override (e.g., "net8.0").</param>
    /// <returns>The generated SDK-style project XML document.</returns>
    public XDocument Convert(ProjectInfo projectInfo, string? targetFramework = null)
    {
        ArgumentNullException.ThrowIfNull(projectInfo);

        var sdkType = DetermineSdkType(projectInfo);
        var framework = targetFramework ?? ConvertTargetFramework(projectInfo);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project",
                new XAttribute("Sdk", GetSdkName(sdkType)))
        );

        var root = doc.Root!;

        // Add main PropertyGroup
        root.Add(CreatePropertyGroup(projectInfo, framework, sdkType));

        // Add ProjectReferences if any
        if (projectInfo.ProjectReferences.Count > 0)
        {
            var projectReferencesGroup = _projectReferenceConverter.ConvertProjectReferences(
                projectInfo.ProjectReferences,
                projectInfo.FilePath);

            if (projectReferencesGroup != null)
            {
                root.Add(projectReferencesGroup);
            }
        }

        // Placeholder for PackageReferences (TASK-017)
        // Placeholder for Content items (TASK-020)

        return doc;
    }

    /// <summary>
    /// Determines the appropriate SDK type based on project characteristics.
    /// </summary>
    private string DetermineSdkType(ProjectInfo projectInfo)
    {
        // Check for ASP.NET MVC/Web API
        if (HasWebReferences(projectInfo))
        {
            return "Web";
        }

        // Check for WPF/WinForms
        if (HasWindowsDesktopReferences(projectInfo))
        {
            return "WindowsDesktop";
        }

        // Default to standard SDK
        return "Default";
    }

    /// <summary>
    /// Checks if the project has web-related references.
    /// </summary>
    private bool HasWebReferences(ProjectInfo projectInfo)
    {
        // Check assembly references
        var hasWebAssembly = projectInfo.References.Any(r =>
            r.Name.StartsWith("System.Web", StringComparison.OrdinalIgnoreCase));

        // Check package references
        var hasWebPackage = projectInfo.PackageReferences.Any(p =>
            p.Id.StartsWith("Microsoft.AspNet", StringComparison.OrdinalIgnoreCase) ||
            p.Id.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));

        return hasWebAssembly || hasWebPackage;
    }

    /// <summary>
    /// Checks if the project has Windows Desktop references (WPF/WinForms).
    /// </summary>
    private bool HasWindowsDesktopReferences(ProjectInfo projectInfo)
    {
        return projectInfo.References.Any(r =>
            r.Name.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
            r.Name.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase) ||
            r.Name.StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the SDK name string for the Sdk attribute.
    /// </summary>
    private string GetSdkName(string sdkType)
    {
        return sdkType switch
        {
            "Web" => "Microsoft.NET.Sdk.Web",
            "WindowsDesktop" => "Microsoft.NET.Sdk.WindowsDesktop",
            "Worker" => "Microsoft.NET.Sdk.Worker",
            _ => "Microsoft.NET.Sdk"
        };
    }

    /// <summary>
    /// Converts old-style target framework version to new format.
    /// </summary>
    private string ConvertTargetFramework(ProjectInfo projectInfo)
    {
        if (projectInfo.TargetFramework?.OriginalVersion == null)
        {
            // Default to net8.0 if no framework specified
            return "net8.0";
        }

        var originalVersion = projectInfo.TargetFramework.OriginalVersion;

        // Try to map the version
        if (FrameworkVersionMap.TryGetValue(originalVersion, out var mappedVersion))
        {
            // For .NET Framework 4.8+, suggest upgrading to .NET 8.0
            if (originalVersion == "v4.8" || originalVersion == "v4.8.1")
            {
                return "net8.0";
            }

            return mappedVersion;
        }

        // Default to net8.0 for unknown versions
        return "net8.0";
    }

    /// <summary>
    /// Creates the main PropertyGroup element with essential properties.
    /// </summary>
    private XElement CreatePropertyGroup(ProjectInfo projectInfo, string targetFramework, string sdkType)
    {
        var propertyGroup = new XElement("PropertyGroup");

        // TargetFramework (required)
        propertyGroup.Add(new XElement("TargetFramework", targetFramework));

        // OutputType (only if not Library, which is default)
        if (!string.IsNullOrEmpty(projectInfo.OutputType) &&
            !projectInfo.OutputType.Equals("Library", StringComparison.OrdinalIgnoreCase))
        {
            propertyGroup.Add(new XElement("OutputType", projectInfo.OutputType));
        }

        // RootNamespace (only if different from project name)
        if (!string.IsNullOrEmpty(projectInfo.RootNamespace) &&
            projectInfo.RootNamespace != projectInfo.Name)
        {
            propertyGroup.Add(new XElement("RootNamespace", projectInfo.RootNamespace));
        }

        // AssemblyName (only if different from project name)
        if (!string.IsNullOrEmpty(projectInfo.AssemblyName) &&
            projectInfo.AssemblyName != projectInfo.Name)
        {
            propertyGroup.Add(new XElement("AssemblyName", projectInfo.AssemblyName));
        }

        // Add Windows Desktop specific properties
        if (sdkType == "WindowsDesktop")
        {
            AddWindowsDesktopProperties(propertyGroup, projectInfo);
        }

        return propertyGroup;
    }

    /// <summary>
    /// Adds Windows Desktop specific properties (WPF/WinForms).
    /// </summary>
    private void AddWindowsDesktopProperties(XElement propertyGroup, ProjectInfo projectInfo)
    {
        // Check for WPF
        var hasWpf = projectInfo.References.Any(r =>
            r.Name.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
            r.Name.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase));

        if (hasWpf)
        {
            propertyGroup.Add(new XElement("UseWPF", "true"));
        }

        // Check for WinForms
        var hasWinForms = projectInfo.References.Any(r =>
            r.Name.StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase));

        if (hasWinForms)
        {
            propertyGroup.Add(new XElement("UseWindowsForms", "true"));
        }
    }
}
