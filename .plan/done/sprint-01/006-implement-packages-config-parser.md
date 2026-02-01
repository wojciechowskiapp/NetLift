# [TASK-006] Implement packages.config Parser

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | S |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001
- **Blocks:** TASK-007, TASK-017

---

## Description

Implement parser for packages.config files to extract NuGet package dependencies.

---

## Acceptance Criteria

- [ ] Can parse packages.config XML format
- [ ] Extracts package ID, version, target framework
- [ ] Handles multiple packages
- [ ] Returns list of PackageReference models
- [ ] Handles missing/empty packages.config gracefully
- [ ] Unit tests

---

## Technical Notes

### packages.config format:

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="Microsoft.AspNet.Mvc" version="5.2.7" targetFramework="net48" />
  <package id="Microsoft.AspNet.Razor" version="3.2.7" targetFramework="net48" />
  <package id="EntityFramework" version="6.4.4" targetFramework="net48" />
  <package id="Newtonsoft.Json" version="13.0.1" targetFramework="net48" />
</packages>
```

### PackageReference model:

```csharp
public class PackageReference
{
    public string Id { get; set; }
    public string Version { get; set; }
    public string TargetFramework { get; set; }
    public bool IsDevelopmentDependency { get; set; }

    // For migration planning
    public PackageCompatibility Compatibility { get; set; }
    public string? ReplacementPackageId { get; set; }
    public string? ReplacementVersion { get; set; }
}

public enum PackageCompatibility
{
    Unknown,
    Compatible,           // Works on .NET Core as-is
    HasReplacement,       // Different package for .NET Core
    Incompatible,         // No .NET Core support
    Deprecated            // Package is deprecated
}
```

### Implementation:

```csharp
public class PackagesConfigParser
{
    public List<PackageReference> Parse(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<PackageReference>();

        var doc = XDocument.Load(filePath);

        return doc.Descendants("package")
            .Select(p => new PackageReference
            {
                Id = p.Attribute("id")?.Value ?? "",
                Version = p.Attribute("version")?.Value ?? "",
                TargetFramework = p.Attribute("targetFramework")?.Value ?? "",
                IsDevelopmentDependency =
                    p.Attribute("developmentDependency")?.Value == "true"
            })
            .ToList();
    }
}
```

### Location:

packages.config is typically in the same directory as the .csproj file.

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
