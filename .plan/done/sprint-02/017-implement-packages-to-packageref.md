# [TASK-017] Implement packages.config to PackageReference Conversion

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | L |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-006, TASK-018
- **Blocks:** TASK-015

---

## Description

Convert legacy packages.config NuGet package management to modern PackageReference format. This includes mapping old package IDs to new equivalents, handling version conflicts, and removing packages that are now part of the framework.

---

## Acceptance Criteria

- [ ] Parse packages.config XML file
- [ ] Map legacy package IDs to modern equivalents
- [ ] Remove framework packages (now implicit in SDK)
- [ ] Detect and resolve version conflicts
- [ ] Handle transitive dependency changes
- [ ] Generate PackageReference ItemGroup
- [ ] Apply mapping rules from YAML (TASK-018)
- [ ] Warn about deprecated/obsolete packages
- [ ] Unit tests with various package scenarios
- [ ] Integration test with real packages.config

---

## Technical Notes

### Legacy packages.config Format:

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="Microsoft.AspNet.Mvc" version="5.2.7" targetFramework="net48" />
  <package id="Microsoft.AspNet.WebApi" version="5.2.7" targetFramework="net48" />
  <package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
  <package id="EntityFramework" version="6.4.4" targetFramework="net48" />
  <package id="Microsoft.CodeDom.Providers.DotNetCompilerPlatform" version="2.0.1" targetFramework="net48" />
</packages>
```

### Modern PackageReference Format:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.2.0" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  <PackageReference Include="EntityFramework" Version="6.4.4" />
</ItemGroup>
```

### Package Converter Implementation:

```csharp
public class PackageReferenceConverter
{
    private readonly PackageMappingRules _mappingRules;
    private readonly ILogger<PackageReferenceConverter> _logger;

    public async Task<PackageConversionResult> ConvertAsync(
        List<PackageReference> packages,
        string targetFramework)
    {
        var result = new PackageConversionResult();

        foreach (var package in packages)
        {
            var conversion = ConvertPackage(package, targetFramework);

            switch (conversion.Action)
            {
                case PackageAction.Keep:
                    result.Packages.Add(conversion.NewPackage!);
                    break;

                case PackageAction.Replace:
                    result.Packages.Add(conversion.NewPackage!);
                    result.Replacements.Add(new PackageReplacement
                    {
                        OldPackage = package,
                        NewPackage = conversion.NewPackage!,
                        Reason = conversion.Reason
                    });
                    break;

                case PackageAction.Remove:
                    result.RemovedPackages.Add(package);
                    result.Warnings.Add(new ConversionWarning
                    {
                        Severity = WarningSeverity.Info,
                        Message = $"Package '{package.Id}' removed: {conversion.Reason}"
                    });
                    break;

                case PackageAction.Manual:
                    result.ManualReviewRequired.Add(package);
                    result.Warnings.Add(new ConversionWarning
                    {
                        Severity = WarningSeverity.Warning,
                        Message = $"Package '{package.Id}' requires manual review: {conversion.Reason}"
                    });
                    break;
            }
        }

        // Remove duplicates (keeping highest version)
        result.Packages = DeduplicatePackages(result.Packages);

        return result;
    }

    private PackageConversion ConvertPackage(
        PackageReference package,
        string targetFramework)
    {
        // Check mapping rules
        if (_mappingRules.TryGetMapping(package.Id, out var mapping))
        {
            return mapping.Action switch
            {
                MappingAction.Remove => new PackageConversion
                {
                    Action = PackageAction.Remove,
                    Reason = mapping.Reason ?? "Package is now part of framework"
                },

                MappingAction.Replace => new PackageConversion
                {
                    Action = PackageAction.Replace,
                    NewPackage = new PackageReference
                    {
                        Id = mapping.NewPackageId!,
                        Version = mapping.NewVersion ?? package.Version
                    },
                    Reason = mapping.Reason ?? $"Replaced with {mapping.NewPackageId}"
                },

                MappingAction.Upgrade => new PackageConversion
                {
                    Action = PackageAction.Replace,
                    NewPackage = new PackageReference
                    {
                        Id = package.Id,
                        Version = mapping.NewVersion ?? SuggestVersion(package.Id, targetFramework)
                    },
                    Reason = mapping.Reason ?? "Version upgraded for compatibility"
                },

                _ => new PackageConversion
                {
                    Action = PackageAction.Keep,
                    NewPackage = package
                }
            };
        }

        // Check if package is obsolete
        if (IsObsoletePackage(package.Id, out var obsoleteReason))
        {
            return new PackageConversion
            {
                Action = PackageAction.Manual,
                Reason = obsoleteReason
            };
        }

        // Default: keep the package
        return new PackageConversion
        {
            Action = PackageAction.Keep,
            NewPackage = package
        };
    }

    private bool IsObsoletePackage(string packageId, out string reason)
    {
        var obsoletePackages = new Dictionary<string, string>
        {
            ["Microsoft.Bcl"] = "Functionality is now built into .NET",
            ["Microsoft.Bcl.Async"] = "Async/await is now built into .NET",
            ["Microsoft.Bcl.Build"] = "No longer needed in modern .NET",
            ["Microsoft.Net.Compilers"] = "Roslyn compilers are built into SDK",
            ["Microsoft.CodeDom.Providers.DotNetCompilerPlatform"] = "Not needed with Roslyn SDK"
        };

        if (obsoletePackages.TryGetValue(packageId, out var msg))
        {
            reason = msg;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private List<PackageReference> DeduplicatePackages(List<PackageReference> packages)
    {
        return packages
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => Version.Parse(p.Version)).First())
            .OrderBy(p => p.Id)
            .ToList();
    }

    private string SuggestVersion(string packageId, string targetFramework)
    {
        // For .NET 8, suggest latest compatible versions
        if (targetFramework.StartsWith("net8"))
        {
            return packageId switch
            {
                "Newtonsoft.Json" => "13.0.3",
                "EntityFramework" => "6.4.4",
                "AutoMapper" => "13.0.1",
                "Serilog" => "3.1.1",
                _ => "latest"
            };
        }

        return "latest"; // Let NuGet resolve
    }
}

public class PackageConversionResult
{
    public List<PackageReference> Packages { get; set; } = new();
    public List<PackageReference> RemovedPackages { get; set; } = new();
    public List<PackageReference> ManualReviewRequired { get; set; } = new();
    public List<PackageReplacement> Replacements { get; set; } = new();
    public List<ConversionWarning> Warnings { get; set; } = new();
}

public class PackageReplacement
{
    public PackageReference OldPackage { get; set; } = null!;
    public PackageReference NewPackage { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
}

public class PackageConversion
{
    public PackageAction Action { get; set; }
    public PackageReference? NewPackage { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public enum PackageAction
{
    Keep,       // Keep package as-is
    Replace,    // Replace with different package or version
    Remove,     // Remove package (now in framework)
    Manual      // Requires manual review
}
```

### Handling ASP.NET MVC to ASP.NET Core Migration:

```csharp
public class AspNetMvcPackageConverter
{
    public List<PackageConversionRule> GetMvcConversionRules()
    {
        return new List<PackageConversionRule>
        {
            new()
            {
                OldPackageId = "Microsoft.AspNet.Mvc",
                Action = MappingAction.Replace,
                NewPackageId = "Microsoft.AspNetCore.Mvc",
                NewVersion = "2.2.0",
                Reason = "ASP.NET MVC 5 → ASP.NET Core MVC",
                RequiresCodeChanges = true,
                Notes = "Requires namespace changes: System.Web.Mvc → Microsoft.AspNetCore.Mvc"
            },

            new()
            {
                OldPackageId = "Microsoft.AspNet.WebApi",
                Action = MappingAction.Replace,
                NewPackageId = "Microsoft.AspNetCore.Mvc",
                NewVersion = "2.2.0",
                Reason = "WebAPI is unified with MVC in ASP.NET Core",
                RequiresCodeChanges = true
            },

            new()
            {
                OldPackageId = "Microsoft.AspNet.WebPages",
                Action = MappingAction.Replace,
                NewPackageId = "Microsoft.AspNetCore.Mvc.Razor",
                NewVersion = "2.2.0",
                Reason = "Razor pages are part of ASP.NET Core MVC"
            },

            new()
            {
                OldPackageId = "Microsoft.AspNet.Razor",
                Action = MappingAction.Remove,
                Reason = "Razor engine is built into ASP.NET Core"
            },

            new()
            {
                OldPackageId = "Microsoft.Web.Infrastructure",
                Action = MappingAction.Remove,
                Reason = "Not needed in ASP.NET Core"
            }
        };
    }
}

public class PackageConversionRule
{
    public string OldPackageId { get; set; } = string.Empty;
    public MappingAction Action { get; set; }
    public string? NewPackageId { get; set; }
    public string? NewVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RequiresCodeChanges { get; set; }
    public string? Notes { get; set; }
}

public enum MappingAction
{
    Keep,
    Replace,
    Remove,
    Upgrade,
    Manual
}
```

### XML Generation:

```csharp
public class PackageReferenceXmlGenerator
{
    public XElement GenerateItemGroup(List<PackageReference> packages)
    {
        var itemGroup = new XElement("ItemGroup");

        foreach (var package in packages.OrderBy(p => p.Id))
        {
            var packageRef = new XElement("PackageReference",
                new XAttribute("Include", package.Id),
                new XAttribute("Version", package.Version));

            // Add condition if package is framework-specific
            if (!string.IsNullOrEmpty(package.Condition))
            {
                packageRef.Add(new XAttribute("Condition", package.Condition));
            }

            // Add PrivateAssets if needed (e.g., analyzers)
            if (IsAnalyzerPackage(package.Id))
            {
                packageRef.Add(new XElement("PrivateAssets", "all"));
                packageRef.Add(new XElement("IncludeAssets",
                    "runtime; build; native; contentfiles; analyzers; buildtransitive"));
            }

            itemGroup.Add(packageRef);
        }

        return itemGroup;
    }

    private bool IsAnalyzerPackage(string packageId)
    {
        return packageId.Contains("Analyzer", StringComparison.OrdinalIgnoreCase) ||
               packageId.Contains("CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
               packageId.EndsWith(".Analyzers", StringComparison.OrdinalIgnoreCase);
    }
}
```

### Version Conflict Resolution:

```csharp
public class PackageVersionResolver
{
    public List<PackageReference> ResolveConflicts(
        List<PackageReference> packages,
        string targetFramework)
    {
        var conflicts = packages
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (!conflicts.Any())
            return packages;

        var resolved = new List<PackageReference>();

        foreach (var group in conflicts)
        {
            // Strategy: take highest version
            var highest = group
                .OrderByDescending(p => Version.Parse(p.Version))
                .First();

            resolved.Add(highest);

            _logger.LogWarning(
                "Version conflict for {PackageId}: {Versions}. Using {ResolvedVersion}",
                group.Key,
                string.Join(", ", group.Select(p => p.Version)),
                highest.Version);
        }

        // Add non-conflicting packages
        var nonConflicting = packages
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .Select(g => g.First());

        resolved.AddRange(nonConflicting);

        return resolved;
    }
}
```

### Files to create/modify:

- `src/NetLift.Migration/Converters/PackageReferenceConverter.cs` - Main converter
- `src/NetLift.Migration/Converters/AspNetMvcPackageConverter.cs` - MVC-specific logic
- `src/NetLift.Migration/Converters/PackageVersionResolver.cs` - Conflict resolution
- `src/NetLift.Migration/Generators/PackageReferenceXmlGenerator.cs` - XML generation
- `src/NetLift.Migration/Models/PackageConversionResult.cs` - Result model
- `tests/NetLift.Tests/Converters/PackageReferenceConverterTests.cs` - Unit tests

### Key Decisions:

- Use mapping rules from YAML for flexibility (TASK-018)
- Preserve package versions unless explicitly upgraded
- Highest version wins in conflict resolution
- Warn about deprecated packages rather than fail
- Support both .NET Framework and .NET Core/5+ targets

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
