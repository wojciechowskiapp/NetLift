# [TASK-004] Implement Solution Parser

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | Claude Code |
| **Started** | 2026-01-31 |
| **Completed** | 2026-01-31 |

## Dependencies

- **Depends on:** TASK-001, TASK-002
- **Blocks:** TASK-007, TASK-010

---

## Description

Implement parser for Visual Studio solution (.sln) files. Extract project references, solution folders, and build configurations.

---

## Acceptance Criteria

- [x] Can parse standard .sln file format
- [x] Extracts all project references with paths
- [x] Identifies project types by GUID
- [x] Handles solution folders
- [x] Extracts build configurations (Debug/Release)
- [x] Returns SolutionInfo model
- [x] Unit tests for parser

---

## Technical Notes

### SolutionInfo model:

```csharp
public class SolutionInfo
{
    public string FilePath { get; set; }
    public string Name { get; set; }
    public List<ProjectReference> Projects { get; set; }
    public List<SolutionFolder> Folders { get; set; }
    public List<BuildConfiguration> Configurations { get; set; }
}

public class ProjectReference
{
    public Guid ProjectGuid { get; set; }
    public Guid TypeGuid { get; set; }  // Identifies project type
    public string Name { get; set; }
    public string RelativePath { get; set; }
    public string AbsolutePath { get; set; }
    public ProjectType DetectedType { get; set; }
}

public enum ProjectType
{
    Unknown,
    CSharpClassLibrary,
    CSharpConsole,
    CSharpWeb,      // ASP.NET
    CSharpWcf,
    CSharpWpf,
    SolutionFolder,
    // ... etc
}
```

### Project Type GUIDs:

```csharp
// Common project type GUIDs
public static class ProjectTypeGuids
{
    public static readonly Guid CSharp = new("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");
    public static readonly Guid SolutionFolder = new("2150E333-8FDC-42A3-9474-1A3956D46DE8");
    public static readonly Guid Web = new("349C5851-65DF-11DA-9384-00065B846F21");
    public static readonly Guid Wcf = new("3D9AD99F-2412-4246-B90B-4EAA41C64699");
    // ... etc
}
```

### .sln format example:

```
Project("{FAE04EC0-...}") = "MyProject", "src\MyProject\MyProject.csproj", "{GUID}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
    EndGlobalSection
EndGlobal
```

### Implementation approach:

Use regex or simple string parsing - .sln format is simple enough that Roslyn isn't needed.

```csharp
public class SolutionAnalyzer : ISolutionAnalyzer
{
    private static readonly Regex ProjectLineRegex = new(
        @"Project\(""(?<typeGuid>{[^}]+})""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)"",\s*""(?<guid>{[^}]+})""",
        RegexOptions.Compiled);

    public async Task<SolutionInfo> AnalyzeAsync(string solutionPath)
    {
        var content = await File.ReadAllTextAsync(solutionPath);
        // Parse projects, configurations, etc.
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
| 2026-01-31 | Claude Code | Implemented all models, ISolutionParser interface, and SolutionParser |
| 2026-01-31 | Claude Code | Created comprehensive unit tests - 22 tests passing |
| 2026-01-31 | Claude Code | Completed - all acceptance criteria met |
