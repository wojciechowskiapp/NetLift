# [TASK-015] Implement .csproj SDK-Style Converter

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | XL |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-005, TASK-016, TASK-017
- **Blocks:** TASK-021

---

## Description

Implement the core converter that transforms old-style .csproj files to modern SDK-style format. This is the heart of the migration engine, performing the actual XML transformation while preserving all necessary configuration.

---

## Acceptance Criteria

- [ ] Converts old-style .csproj to SDK-style format
- [ ] Removes redundant ItemGroup elements (auto-included files)
- [ ] Converts TargetFrameworkVersion to TargetFramework
- [ ] Removes Import statements for standard targets
- [ ] Preserves custom PropertyGroups and build configurations
- [ ] Handles multi-targeting when needed
- [ ] Generates clean, minimal XML output
- [ ] Validates output is valid MSBuild project
- [ ] Unit tests with various project types
- [ ] Integration tests with real .csproj files

---

## Technical Notes

### SDK-Style Project Structure:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Only explicit package references -->
    <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="5.2.7" />
  </ItemGroup>

  <!-- Only non-standard files need to be listed -->
  <ItemGroup>
    <Content Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

### Conversion Strategy:

1. **Determine SDK Type**:
```csharp
public enum SdkType
{
    Default,            // Microsoft.NET.Sdk
    Web,                // Microsoft.NET.Sdk.Web
    Worker,             // Microsoft.NET.Sdk.Worker
    WindowsDesktop,     // Microsoft.NET.Sdk.WindowsDesktop
    Razor,              // Microsoft.NET.Sdk.Razor
}

public class SdkTypeDetector
{
    public SdkType DetectSdkType(ProjectInfo project)
    {
        // Check for ASP.NET MVC/Web API
        if (project.PackageReferences.Any(p =>
            p.Id.StartsWith("Microsoft.AspNet.Mvc") ||
            p.Id.StartsWith("Microsoft.AspNet.WebApi")))
            return SdkType.Web;

        // Check for WPF/WinForms
        if (project.References.Any(r =>
            r.Name.StartsWith("PresentationFramework") ||
            r.Name.StartsWith("System.Windows.Forms")))
            return SdkType.WindowsDesktop;

        // Check for Worker Service
        if (project.PackageReferences.Any(p =>
            p.Id == "Microsoft.Extensions.Hosting"))
            return SdkType.Worker;

        return SdkType.Default;
    }
}
```

2. **Convert Target Framework**:
```csharp
public class TargetFrameworkConverter
{
    private static readonly Dictionary<string, string> FrameworkMap = new()
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

    public string ConvertFrameworkMoniker(string oldMoniker)
    {
        if (FrameworkMap.TryGetValue(oldMoniker, out var newMoniker))
            return newMoniker;

        throw new NotSupportedException($"Framework version '{oldMoniker}' not supported");
    }

    public string GetModernEquivalent(string oldMoniker, bool preferModern = true)
    {
        // For .NET Framework 4.8, we can suggest net8.0
        if (preferModern && (oldMoniker == "v4.8" || oldMoniker == "v4.8.1"))
        {
            return "net8.0"; // Latest LTS
        }

        return ConvertFrameworkMoniker(oldMoniker);
    }
}
```

3. **Main Converter Implementation**:
```csharp
public class CsprojConverter
{
    private readonly SdkTypeDetector _sdkDetector;
    private readonly TargetFrameworkConverter _frameworkConverter;
    private readonly ILogger<CsprojConverter> _logger;

    public async Task<ConversionResult> ConvertAsync(
        ProjectInfo oldProject,
        ConversionOptions options)
    {
        var sdkType = _sdkDetector.DetectSdkType(oldProject);
        var targetFramework = _frameworkConverter.ConvertFrameworkMoniker(
            oldProject.TargetFramework.Moniker);

        // Create new SDK-style project
        var newProject = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project",
                new XAttribute("Sdk", GetSdkName(sdkType))));

        var root = newProject.Root!;

        // Add PropertyGroup
        var propertyGroup = CreatePropertyGroup(oldProject, targetFramework, sdkType);
        root.Add(propertyGroup);

        // Add PackageReferences (from TASK-017)
        var packageGroup = CreatePackageReferenceGroup(oldProject);
        if (packageGroup.HasElements)
            root.Add(packageGroup);

        // Add ProjectReferences (from TASK-019)
        var projectRefGroup = CreateProjectReferenceGroup(oldProject);
        if (projectRefGroup.HasElements)
            root.Add(projectRefGroup);

        // Add explicit content files (from TASK-020)
        var contentGroup = CreateContentGroup(oldProject);
        if (contentGroup.HasElements)
            root.Add(contentGroup);

        // Preserve custom build configurations
        AddCustomBuildProperties(root, oldProject);

        return new ConversionResult
        {
            Success = true,
            NewProjectXml = newProject,
            SdkType = sdkType,
            TargetFramework = targetFramework,
            Warnings = CollectWarnings(oldProject)
        };
    }

    private XElement CreatePropertyGroup(
        ProjectInfo oldProject,
        string targetFramework,
        SdkType sdkType)
    {
        var pg = new XElement("PropertyGroup");

        // Essential properties
        pg.Add(new XElement("TargetFramework", targetFramework));

        // Output type (if not Library, which is default)
        if (oldProject.Properties.TryGetValue("OutputType", out var outputType)
            && outputType != "Library")
        {
            pg.Add(new XElement("OutputType", outputType));
        }

        // AssemblyName (only if different from project name)
        if (oldProject.AssemblyName != oldProject.Name)
        {
            pg.Add(new XElement("AssemblyName", oldProject.AssemblyName));
        }

        // RootNamespace (only if different from project name)
        if (oldProject.RootNamespace != oldProject.Name)
        {
            pg.Add(new XElement("RootNamespace", oldProject.RootNamespace));
        }

        // Modern C# features
        pg.Add(new XElement("Nullable", "enable"));
        pg.Add(new XElement("ImplicitUsings", "enable"));
        pg.Add(new XElement("LangVersion", "latest"));

        // Web-specific properties
        if (sdkType == SdkType.Web)
        {
            pg.Add(new XElement("AspNetCoreHostingModel", "InProcess"));
        }

        // WindowsDesktop-specific
        if (sdkType == SdkType.WindowsDesktop)
        {
            if (oldProject.Properties.ContainsKey("UseWPF"))
                pg.Add(new XElement("UseWPF", "true"));

            if (oldProject.Properties.ContainsKey("UseWindowsForms"))
                pg.Add(new XElement("UseWindowsForms", "true"));
        }

        return pg;
    }

    private XElement CreatePackageReferenceGroup(ProjectInfo oldProject)
    {
        var itemGroup = new XElement("ItemGroup");

        foreach (var package in oldProject.PackageReferences.OrderBy(p => p.Id))
        {
            var packageRef = new XElement("PackageReference",
                new XAttribute("Include", package.Id),
                new XAttribute("Version", package.Version));

            itemGroup.Add(packageRef);
        }

        return itemGroup;
    }

    private string GetSdkName(SdkType sdkType) => sdkType switch
    {
        SdkType.Web => "Microsoft.NET.Sdk.Web",
        SdkType.Worker => "Microsoft.NET.Sdk.Worker",
        SdkType.WindowsDesktop => "Microsoft.NET.Sdk.WindowsDesktop",
        SdkType.Razor => "Microsoft.NET.Sdk.Razor",
        _ => "Microsoft.NET.Sdk"
    };

    private void AddCustomBuildProperties(XElement root, ProjectInfo oldProject)
    {
        // Preserve important custom properties like:
        // - GeneratePackageOnBuild
        // - PackageId, Authors, Description (for NuGet packages)
        // - Custom MSBuild properties

        var customProps = oldProject.Properties
            .Where(p => IsCustomProperty(p.Key))
            .ToList();

        if (customProps.Any())
        {
            var pg = new XElement("PropertyGroup");
            foreach (var prop in customProps)
            {
                pg.Add(new XElement(prop.Key, prop.Value));
            }
            root.Add(pg);
        }
    }

    private bool IsCustomProperty(string propertyName)
    {
        // Properties that should be preserved
        var preserveList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GeneratePackageOnBuild",
            "PackageId",
            "Authors",
            "Description",
            "Copyright",
            "PackageLicenseExpression",
            "PackageProjectUrl",
            "RepositoryUrl",
            "Version",
            "AssemblyVersion",
            "FileVersion",
            "InformationalVersion"
        };

        return preserveList.Contains(propertyName);
    }
}
```

4. **File Inclusion Strategy**:
```csharp
public class FileInclusionAnalyzer
{
    // SDK-style projects auto-include these patterns
    private static readonly string[] AutoIncludedPatterns =
    {
        "**/*.cs",           // All C# files
        "**/*.cshtml",       // Razor views
        "**/*.razor",        // Blazor components
        "**/*.resx",         // Resource files
    };

    private static readonly string[] AutoExcludedPatterns =
    {
        "**/bin/**",
        "**/obj/**",
        "**/.vs/**"
    };

    public List<string> GetExplicitInclusions(ProjectInfo oldProject)
    {
        var explicitFiles = new List<string>();

        // Files that DON'T match auto-include patterns need to be explicit
        foreach (var compile in oldProject.CompileItems)
        {
            if (!IsAutoIncluded(compile.FilePath))
            {
                explicitFiles.Add(compile.FilePath);
            }
        }

        return explicitFiles;
    }

    private bool IsAutoIncluded(string filePath)
    {
        // Check if file matches any auto-include pattern
        return AutoIncludedPatterns.Any(pattern =>
            FileMatchesPattern(filePath, pattern));
    }
}
```

5. **Conversion Result Model**:
```csharp
public class ConversionResult
{
    public bool Success { get; set; }
    public XDocument? NewProjectXml { get; set; }
    public SdkType SdkType { get; set; }
    public string TargetFramework { get; set; } = string.Empty;
    public List<ConversionWarning> Warnings { get; set; } = new();
    public List<string> ManualSteps { get; set; } = new();
}

public class ConversionWarning
{
    public WarningSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Suggestion { get; set; }
}

public enum WarningSeverity
{
    Info,
    Warning,
    Error
}
```

### Files to create/modify:

- `src/NetLift.Migration/Converters/CsprojConverter.cs` - Main converter
- `src/NetLift.Migration/Converters/SdkTypeDetector.cs` - SDK type detection
- `src/NetLift.Migration/Converters/TargetFrameworkConverter.cs` - Framework conversion
- `src/NetLift.Migration/Converters/FileInclusionAnalyzer.cs` - File analysis
- `src/NetLift.Migration/Models/ConversionResult.cs` - Result model
- `tests/NetLift.Tests/Converters/CsprojConverterTests.cs` - Unit tests

### Key Decisions:

- Use XDocument/XElement for XML manipulation (cleaner than string manipulation)
- Auto-enable modern C# features (Nullable, ImplicitUsings) for better DX
- Preserve custom MSBuild properties that have business value
- Generate warnings for manual steps (e.g., web.config migration)
- Support both in-place and side-by-side conversion modes

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
