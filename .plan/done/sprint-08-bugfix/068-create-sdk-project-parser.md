# [TASK-068] Create SdkProjectParser for SDK-style projects

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 8 (Bugfix) |
| **Agent** | Claude |
| **Started** | 2026-02-01 |
| **Completed** | 2026-02-01 |

## Dependencies

- **Depends on:** TASK-067
- **Blocks:** (none)

---

## Description

Create an `SdkProjectParser` that can parse SDK-style (.NET Core / .NET 5+) project files. Currently only `OldFormatProjectParser` exists, which fails on SDK-style projects.

SDK-style projects:
- Have `<Project Sdk="Microsoft.NET.Sdk">` attribute
- Use implicit file globbing (no explicit `<Compile>` items)
- Have simplified format without XML namespace

This parser is needed for:
1. Migrating projects that are already partially SDK-style
2. Re-running migration on already-migrated projects
3. Robustness when project format is unknown

---

## Acceptance Criteria

- [ ] `SdkProjectParser` implements `IProjectParser`
- [ ] `CanParse()` correctly identifies SDK-style projects (has `Sdk` attribute)
- [ ] `AnalyzeAsync()` enumerates `.cs` files from filesystem (not from explicit Compile items)
- [ ] Excludes `obj/` and `bin/` directories from enumeration
- [ ] Extracts properties: TargetFramework, RootNamespace, AssemblyName, etc.
- [ ] Registered in DI container alongside `OldFormatProjectParser`
- [ ] Composite parser pattern to auto-select correct parser
- [ ] Tests for SDK-style project parsing

---

## Technical Notes

### Files to create:

1. **`src/NetLift.Analysis/Parsers/SdkProjectParser.cs`**

```csharp
public class SdkProjectParser : IProjectParser
{
    public bool CanParse(string projectPath)
    {
        var doc = XDocument.Load(projectPath);
        return doc.Root?.Attribute("Sdk") != null;
    }

    public async Task<ProjectInfo> AnalyzeAsync(string projectPath, CancellationToken ct)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

        // Convert to CompileItems...
    }
}
```

2. **`src/NetLift.Analysis/Parsers/CompositeProjectParser.cs`**

```csharp
public class CompositeProjectParser : IProjectParser
{
    private readonly IEnumerable<IProjectParser> _parsers;

    public bool CanParse(string path) => _parsers.Any(p => p.CanParse(path));

    public Task<ProjectInfo> AnalyzeAsync(string path, CancellationToken ct)
    {
        var parser = _parsers.First(p => p.CanParse(path));
        return parser.AnalyzeAsync(path, ct);
    }
}
```

3. **`src/NetLift.Cli/Program.cs`** - Update DI registration

### Key decisions:
- Use filesystem enumeration for SDK-style projects (they use implicit globbing)
- Exclude `obj/` and `bin/` directories
- Support explicit `<Compile Remove="..." />` excludes if present

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-02-01 | Claude | Created |

---
