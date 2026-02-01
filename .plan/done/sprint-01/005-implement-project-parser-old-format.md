# [TASK-005] Implement Project Parser (Old Format)

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001, TASK-002
- **Blocks:** TASK-007, TASK-008, TASK-015

---

## Description

Implement parser for old-style (non-SDK) .csproj files. Extract all relevant information needed for migration analysis.

---

## Acceptance Criteria

- [ ] Can parse old-style .csproj XML format
- [ ] Extracts target framework version
- [ ] Extracts assembly references
- [ ] Extracts project references
- [ ] Extracts compile items
- [ ] Extracts content/resource items
- [ ] Extracts NuGet packages (from packages.config or HintPath)
- [ ] Handles conditional PropertyGroups
- [ ] Returns ProjectInfo model
- [ ] Unit tests with sample .csproj files

---

## Technical Notes

### ProjectInfo model:

```csharp
public class ProjectInfo
{
    public string FilePath { get; set; }
    public string Name { get; set; }
    public string AssemblyName { get; set; }
    public string RootNamespace { get; set; }
    public TargetFramework TargetFramework { get; set; }
    public ProjectFormat Format { get; set; }  // OldStyle or SdkStyle

    public List<AssemblyReference> References { get; set; }
    public List<ProjectReference> ProjectReferences { get; set; }
    public List<PackageReference> PackageReferences { get; set; }
    public List<CompileItem> CompileItems { get; set; }
    public List<ContentItem> ContentItems { get; set; }
    public List<EmbeddedResource> EmbeddedResources { get; set; }

    public Dictionary<string, string> Properties { get; set; }
}

public class TargetFramework
{
    public string Moniker { get; set; }  // e.g., "net48"
    public FrameworkType Type { get; set; }  // Framework, Core, Standard
    public Version Version { get; set; }
}
```

### Old-style .csproj structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\...\Microsoft.Common.props" />

  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <OutputType>Library</OutputType>
    <AssemblyName>MyProject</AssemblyName>
    <RootNamespace>MyProject</RootNamespace>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <DebugSymbols>true</DebugSymbols>
    <!-- ... -->
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Web.Mvc, Version=5.2.7.0, ...">
      <HintPath>..\packages\Microsoft.AspNet.Mvc.5.2.7\lib\net45\System.Web.Mvc.dll</HintPath>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Compile Include="Controllers\HomeController.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Views\Home\Index.cshtml" />
    <Content Include="Web.config" />
  </ItemGroup>

  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

### Key elements to extract:

1. **PropertyGroup**: TargetFrameworkVersion, OutputType, AssemblyName, RootNamespace
2. **Reference**: System assemblies, NuGet packages (via HintPath)
3. **ProjectReference**: Other projects in solution
4. **Compile**: C# source files
5. **Content**: Web files, configs
6. **EmbeddedResource**: .resx files

### Implementation:

Use System.Xml.Linq for parsing:

```csharp
public class ProjectAnalyzer : IProjectAnalyzer
{
    public async Task<ProjectInfo> AnalyzeAsync(string projectPath)
    {
        var doc = XDocument.Load(projectPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        // Extract properties
        var targetFramework = doc.Descendants(ns + "TargetFrameworkVersion")
            .FirstOrDefault()?.Value;

        // Extract references
        var references = doc.Descendants(ns + "Reference")
            .Select(r => ParseReference(r, ns))
            .ToList();

        // ... etc
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
