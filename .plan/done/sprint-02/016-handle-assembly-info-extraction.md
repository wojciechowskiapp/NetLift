# [TASK-016] Handle AssemblyInfo.cs Extraction

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-002, TASK-005
- **Blocks:** TASK-015

---

## Description

Extract assembly attributes from AssemblyInfo.cs and migrate them to .csproj properties. In SDK-style projects, assembly metadata is specified in the project file rather than in AssemblyInfo.cs using Roslyn source generators.

---

## Acceptance Criteria

- [ ] Parse AssemblyInfo.cs using Roslyn
- [ ] Extract all assembly-level attributes
- [ ] Map attributes to .csproj properties
- [ ] Generate PropertyGroup with assembly metadata
- [ ] Handle multiple AssemblyInfo files (shared, project-specific)
- [ ] Preserve custom attributes that can't be mapped
- [ ] Generate warning for unmapped attributes
- [ ] Unit tests with various AssemblyInfo patterns
- [ ] Integration test with real projects

---

## Technical Notes

### Typical AssemblyInfo.cs Structure:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("MyProject")]
[assembly: AssemblyDescription("Sample application")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Acme Corp")]
[assembly: AssemblyProduct("MyProject")]
[assembly: AssemblyCopyright("Copyright © 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("12345678-1234-1234-1234-123456789012")]

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

[assembly: InternalsVisibleTo("MyProject.Tests")]
```

### Attribute to Property Mapping:

| AssemblyInfo Attribute | .csproj Property |
|------------------------|------------------|
| AssemblyTitle | AssemblyTitle |
| AssemblyDescription | Description |
| AssemblyCompany | Company / Authors |
| AssemblyProduct | Product |
| AssemblyCopyright | Copyright |
| AssemblyVersion | AssemblyVersion |
| AssemblyFileVersion | FileVersion |
| AssemblyInformationalVersion | InformationalVersion / Version |
| AssemblyConfiguration | Configuration |
| NeutralResourcesLanguage | NeutralLanguage |

### Implementation:

```csharp
public class AssemblyInfoExtractor
{
    private readonly ILogger<AssemblyInfoExtractor> _logger;

    public async Task<AssemblyInfoData> ExtractAsync(string assemblyInfoPath)
    {
        var sourceText = await File.ReadAllTextAsync(assemblyInfoPath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await syntaxTree.GetRootAsync();

        var result = new AssemblyInfoData
        {
            FilePath = assemblyInfoPath
        };

        // Find all assembly attributes
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => IsAssemblyAttribute(attr))
            .ToList();

        foreach (var attribute in attributes)
        {
            ExtractAttribute(attribute, result);
        }

        return result;
    }

    private bool IsAssemblyAttribute(AttributeSyntax attribute)
    {
        // Check if attribute is applied at assembly level
        var attributeList = attribute.Parent as AttributeListSyntax;
        return attributeList?.Target?.Identifier.Text == "assembly";
    }

    private void ExtractAttribute(AttributeSyntax attribute, AssemblyInfoData result)
    {
        var attributeName = attribute.Name.ToString();
        var argument = attribute.ArgumentList?.Arguments.FirstOrDefault();
        var value = ExtractArgumentValue(argument);

        switch (attributeName)
        {
            case "AssemblyTitle":
                result.Title = value;
                break;

            case "AssemblyDescription":
                result.Description = value;
                break;

            case "AssemblyCompany":
                result.Company = value;
                break;

            case "AssemblyProduct":
                result.Product = value;
                break;

            case "AssemblyCopyright":
                result.Copyright = value;
                break;

            case "AssemblyVersion":
                result.AssemblyVersion = value;
                break;

            case "AssemblyFileVersion":
                result.FileVersion = value;
                break;

            case "AssemblyInformationalVersion":
                result.InformationalVersion = value;
                break;

            case "InternalsVisibleTo":
                result.InternalsVisibleTo.Add(value);
                break;

            case "Guid":
                result.Guid = value;
                break;

            case "ComVisible":
                if (bool.TryParse(value, out var comVisible))
                    result.ComVisible = comVisible;
                break;

            case "NeutralResourcesLanguage":
                result.NeutralLanguage = value;
                break;

            default:
                // Custom attribute - preserve it
                result.CustomAttributes.Add(new CustomAttribute
                {
                    Name = attributeName,
                    Arguments = attribute.ArgumentList?.Arguments
                        .Select(a => ExtractArgumentValue(a))
                        .ToList() ?? new()
                });
                break;
        }
    }

    private string ExtractArgumentValue(AttributeArgumentSyntax? argument)
    {
        if (argument?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        if (argument?.Expression is ConstantPatternSyntax constant)
        {
            return constant.Expression.ToString();
        }

        return argument?.Expression.ToString() ?? string.Empty;
    }
}

public class AssemblyInfoData
{
    public string FilePath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Company { get; set; }
    public string? Product { get; set; }
    public string? Copyright { get; set; }
    public string? AssemblyVersion { get; set; }
    public string? FileVersion { get; set; }
    public string? InformationalVersion { get; set; }
    public string? Guid { get; set; }
    public bool? ComVisible { get; set; }
    public string? NeutralLanguage { get; set; }
    public List<string> InternalsVisibleTo { get; set; } = new();
    public List<CustomAttribute> CustomAttributes { get; set; } = new();
}

public class CustomAttribute
{
    public string Name { get; set; } = string.Empty;
    public List<string> Arguments { get; set; } = new();
}
```

### Conversion to .csproj Properties:

```csharp
public class AssemblyInfoConverter
{
    public XElement CreatePropertyGroup(AssemblyInfoData assemblyInfo)
    {
        var propertyGroup = new XElement("PropertyGroup");

        AddPropertyIfNotNull(propertyGroup, "AssemblyTitle", assemblyInfo.Title);
        AddPropertyIfNotNull(propertyGroup, "Description", assemblyInfo.Description);
        AddPropertyIfNotNull(propertyGroup, "Company", assemblyInfo.Company);
        AddPropertyIfNotNull(propertyGroup, "Authors", assemblyInfo.Company); // Authors = Company
        AddPropertyIfNotNull(propertyGroup, "Product", assemblyInfo.Product);
        AddPropertyIfNotNull(propertyGroup, "Copyright", assemblyInfo.Copyright);

        AddPropertyIfNotNull(propertyGroup, "AssemblyVersion", assemblyInfo.AssemblyVersion);
        AddPropertyIfNotNull(propertyGroup, "FileVersion", assemblyInfo.FileVersion);
        AddPropertyIfNotNull(propertyGroup, "Version",
            assemblyInfo.InformationalVersion ?? assemblyInfo.FileVersion);

        AddPropertyIfNotNull(propertyGroup, "NeutralLanguage", assemblyInfo.NeutralLanguage);

        if (assemblyInfo.ComVisible.HasValue)
        {
            AddPropertyIfNotNull(propertyGroup, "ComVisible",
                assemblyInfo.ComVisible.Value.ToString().ToLower());
        }

        return propertyGroup;
    }

    public XElement? CreateInternalsVisibleToItemGroup(AssemblyInfoData assemblyInfo)
    {
        if (!assemblyInfo.InternalsVisibleTo.Any())
            return null;

        var itemGroup = new XElement("ItemGroup");

        foreach (var assembly in assemblyInfo.InternalsVisibleTo)
        {
            itemGroup.Add(new XElement("InternalsVisibleTo",
                new XAttribute("Include", assembly)));
        }

        return itemGroup;
    }

    private void AddPropertyIfNotNull(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parent.Add(new XElement(name, value));
        }
    }

    public List<ConversionWarning> GetWarnings(AssemblyInfoData assemblyInfo)
    {
        var warnings = new List<ConversionWarning>();

        // Warn about custom attributes that need manual handling
        foreach (var attr in assemblyInfo.CustomAttributes)
        {
            warnings.Add(new ConversionWarning
            {
                Severity = WarningSeverity.Warning,
                Message = $"Custom assembly attribute '{attr.Name}' found",
                FilePath = assemblyInfo.FilePath,
                Suggestion = "Review if this attribute needs to be preserved in a separate AssemblyInfo.cs file"
            });
        }

        return warnings;
    }
}
```

### Generating New AssemblyInfo.cs (for custom attributes):

```csharp
public class AssemblyInfoGenerator
{
    public string? GeneratePartialAssemblyInfo(AssemblyInfoData assemblyInfo)
    {
        // Only generate if there are custom attributes
        if (!assemblyInfo.CustomAttributes.Any())
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("// This file contains custom assembly attributes that couldn't be migrated to .csproj");
        sb.AppendLine("// Auto-generated by NetLift");
        sb.AppendLine();
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine();

        foreach (var attr in assemblyInfo.CustomAttributes)
        {
            sb.Append($"[assembly: {attr.Name}(");
            sb.Append(string.Join(", ", attr.Arguments.Select(a => $"\"{a}\"")));
            sb.AppendLine(")]");
        }

        return sb.ToString();
    }
}
```

### Handling Multiple AssemblyInfo Files:

```csharp
public class AssemblyInfoMerger
{
    public AssemblyInfoData MergeMultiple(List<AssemblyInfoData> assemblyInfos)
    {
        var merged = new AssemblyInfoData();

        foreach (var info in assemblyInfos)
        {
            // Last one wins for simple properties
            merged.Title ??= info.Title;
            merged.Description ??= info.Description;
            merged.Company ??= info.Company;
            merged.Product ??= info.Product;
            merged.Copyright ??= info.Copyright;
            merged.AssemblyVersion ??= info.AssemblyVersion;
            merged.FileVersion ??= info.FileVersion;
            merged.InformationalVersion ??= info.InformationalVersion;
            merged.Guid ??= info.Guid;
            merged.ComVisible ??= info.ComVisible;
            merged.NeutralLanguage ??= info.NeutralLanguage;

            // Merge collections
            merged.InternalsVisibleTo.AddRange(info.InternalsVisibleTo);
            merged.CustomAttributes.AddRange(info.CustomAttributes);
        }

        // Deduplicate
        merged.InternalsVisibleTo = merged.InternalsVisibleTo.Distinct().ToList();

        return merged;
    }
}
```

### Files to create/modify:

- `src/NetLift.Migration/Extractors/AssemblyInfoExtractor.cs` - Roslyn-based parser
- `src/NetLift.Migration/Converters/AssemblyInfoConverter.cs` - Convert to PropertyGroup
- `src/NetLift.Migration/Generators/AssemblyInfoGenerator.cs` - Generate partial file
- `src/NetLift.Migration/Models/AssemblyInfoData.cs` - Data model
- `tests/NetLift.Tests/Extractors/AssemblyInfoExtractorTests.cs` - Unit tests

### Key Decisions:

- Use Roslyn for parsing (more robust than regex)
- Preserve InternalsVisibleTo as ItemGroup in .csproj
- Generate partial AssemblyInfo.cs only for unmapped custom attributes
- Company maps to both Company and Authors properties
- Version defaults to InformationalVersion or FileVersion

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
