# [TASK-019] Handle Project References Migration

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

- **Depends on:** TASK-005
- **Blocks:** TASK-015

---

## Description

Convert old-style project references to SDK-style format. Handle relative paths, project GUIDs, and ensure references work correctly in the new structure.

---

## Acceptance Criteria

- [ ] Parse old-style ProjectReference elements
- [ ] Convert to SDK-style format (remove GUID, metadata)
- [ ] Preserve relative paths (normalize if needed)
- [ ] Handle Name vs AssemblyName differences
- [ ] Remove unnecessary Include metadata
- [ ] Validate reference paths still exist
- [ ] Handle circular reference detection
- [ ] Generate warnings for broken references
- [ ] Unit tests with various reference patterns
- [ ] Integration test with multi-project solution

---

## Technical Notes

### Old-Style ProjectReference Format:

```xml
<ItemGroup>
  <ProjectReference Include="..\MyLibrary\MyLibrary.csproj">
    <Project>{12345678-1234-1234-1234-123456789012}</Project>
    <Name>MyLibrary</Name>
    <Private>True</Private>
  </ProjectReference>

  <ProjectReference Include="..\MyCore\MyCore.csproj">
    <Project>{87654321-4321-4321-4321-210987654321}</Project>
    <Name>MyCore</Name>
  </ProjectReference>
</ItemGroup>
```

### SDK-Style ProjectReference Format:

```xml
<ItemGroup>
  <ProjectReference Include="..\MyLibrary\MyLibrary.csproj" />
  <ProjectReference Include="..\MyCore\MyCore.csproj" />
</ItemGroup>
```

### Project Reference Converter:

```csharp
public class ProjectReferenceConverter
{
    private readonly ILogger<ProjectReferenceConverter> _logger;

    public XElement ConvertProjectReferences(
        List<ProjectReference> references,
        string sourceProjectPath)
    {
        var itemGroup = new XElement("ItemGroup");

        foreach (var reference in references.OrderBy(r => r.Include))
        {
            var converted = ConvertReference(reference, sourceProjectPath);
            if (converted != null)
                itemGroup.Add(converted);
        }

        return itemGroup;
    }

    private XElement? ConvertReference(
        ProjectReference reference,
        string sourceProjectPath)
    {
        // Validate reference path exists
        var absolutePath = ResolveRelativePath(sourceProjectPath, reference.Include);

        if (!File.Exists(absolutePath))
        {
            _logger.LogWarning(
                "Project reference not found: {Path} (referenced from {Source})",
                reference.Include,
                sourceProjectPath);

            return CreateCommentedReference(reference, "Path not found");
        }

        // Create simple ProjectReference (no GUID, no Name)
        var projectRef = new XElement("ProjectReference",
            new XAttribute("Include", NormalizePath(reference.Include)));

        // Only add additional elements if absolutely necessary

        // Preserve ReferenceOutputAssembly if explicitly set to false
        if (reference.Metadata.TryGetValue("ReferenceOutputAssembly", out var refOutput)
            && refOutput.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            projectRef.Add(new XElement("ReferenceOutputAssembly", "false"));
        }

        // Preserve PrivateAssets (for build-only references)
        if (reference.Metadata.TryGetValue("PrivateAssets", out var privateAssets))
        {
            projectRef.Add(new XElement("PrivateAssets", privateAssets));
        }

        return projectRef;
    }

    private string ResolveRelativePath(string basePath, string relativePath)
    {
        var baseDir = Path.GetDirectoryName(basePath) ?? string.Empty;
        var combined = Path.Combine(baseDir, relativePath);
        return Path.GetFullPath(combined);
    }

    private string NormalizePath(string path)
    {
        // Ensure consistent path separators
        return path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);
    }

    private XElement CreateCommentedReference(ProjectReference reference, string reason)
    {
        // Return commented XML for manual review
        return new XElement("ProjectReference",
            new XComment($" MIGRATION WARNING: {reason} "),
            new XAttribute("Include", reference.Include));
    }
}

public class ProjectReference
{
    public string Include { get; set; } = string.Empty;
    public Guid? ProjectGuid { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

### Project Reference Validator:

```csharp
public class ProjectReferenceValidator
{
    public async Task<ValidationResult> ValidateReferencesAsync(
        List<ProjectReference> references,
        string sourceProjectPath,
        SolutionInfo solution)
    {
        var result = new ValidationResult();

        foreach (var reference in references)
        {
            var validation = await ValidateReferenceAsync(
                reference,
                sourceProjectPath,
                solution);

            result.Results.Add(validation);
        }

        return result;
    }

    private async Task<ReferenceValidation> ValidateReferenceAsync(
        ProjectReference reference,
        string sourceProjectPath,
        SolutionInfo solution)
    {
        var validation = new ReferenceValidation
        {
            Reference = reference,
            IsValid = true
        };

        // Check if file exists
        var absolutePath = ResolveRelativePath(sourceProjectPath, reference.Include);
        if (!File.Exists(absolutePath))
        {
            validation.IsValid = false;
            validation.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Message = $"Project file not found: {reference.Include}",
                Suggestion = "Update the reference path or remove this reference"
            });
            return validation;
        }

        // Check if referenced project is in solution
        var referencedProject = solution.Projects
            .FirstOrDefault(p => p.FilePath.Equals(
                absolutePath,
                StringComparison.OrdinalIgnoreCase));

        if (referencedProject == null)
        {
            validation.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Message = $"Referenced project not in solution: {reference.Include}",
                Suggestion = "This reference may need to be added to the solution"
            });
        }

        // Check for circular references
        if (HasCircularReference(sourceProjectPath, absolutePath, solution))
        {
            validation.IsValid = false;
            validation.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                Message = $"Circular reference detected: {reference.Include}",
                Suggestion = "Refactor to remove circular dependency"
            });
        }

        return validation;
    }

    private bool HasCircularReference(
        string projectPath,
        string referencePath,
        SolutionInfo solution,
        HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!visited.Add(projectPath))
            return true; // Already visited = circular

        var project = solution.Projects
            .FirstOrDefault(p => p.FilePath.Equals(
                referencePath,
                StringComparison.OrdinalIgnoreCase));

        if (project == null)
            return false;

        foreach (var reference in project.ProjectReferences)
        {
            var refAbsPath = ResolveRelativePath(referencePath, reference.Include);

            if (refAbsPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                return true; // Direct circular reference

            if (HasCircularReference(projectPath, refAbsPath, solution, visited))
                return true; // Transitive circular reference
        }

        return false;
    }
}

public class ValidationResult
{
    public List<ReferenceValidation> Results { get; set; } = new();
    public bool IsValid => Results.All(r => r.IsValid);
    public List<ValidationIssue> AllIssues =>
        Results.SelectMany(r => r.Issues).ToList();
}

public class ReferenceValidation
{
    public ProjectReference Reference { get; set; } = null!;
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
}

public class ValidationIssue
{
    public IssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}
```

### Handling Cross-Framework References:

```csharp
public class CrossFrameworkReferenceHandler
{
    public void HandleCrossFrameworkReferences(
        ProjectInfo sourceProject,
        ProjectInfo referencedProject,
        XElement projectRefElement)
    {
        // If source is .NET 8 and reference is .NET Framework 4.8
        if (IsNetCore(sourceProject.TargetFramework) &&
            IsNetFramework(referencedProject.TargetFramework))
        {
            _logger.LogWarning(
                "Cross-framework reference: {Source} ({SourceFx}) → {Target} ({TargetFx})",
                sourceProject.Name,
                sourceProject.TargetFramework.Moniker,
                referencedProject.Name,
                referencedProject.TargetFramework.Moniker);

            // This might require multi-targeting the referenced project
            projectRefElement.Add(new XComment(
                " WARNING: Cross-framework reference. " +
                "Consider multi-targeting the referenced project. "));
        }
    }

    private bool IsNetCore(TargetFramework framework)
    {
        return framework.Moniker.StartsWith("net") &&
               !framework.Moniker.StartsWith("net4");
    }

    private bool IsNetFramework(TargetFramework framework)
    {
        return framework.Moniker.StartsWith("net4");
    }
}
```

### Path Normalization:

```csharp
public class ProjectPathNormalizer
{
    public string NormalizeProjectReference(string include)
    {
        // Convert to forward slashes for consistency (works on all platforms)
        var normalized = include.Replace('\\', '/');

        // Simplify path (remove redundant ../ segments)
        normalized = SimplifyPath(normalized);

        return normalized;
    }

    private string SimplifyPath(string path)
    {
        var segments = path.Split('/', '\\');
        var stack = new Stack<string>();

        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                if (stack.Count > 0 && stack.Peek() != "..")
                    stack.Pop();
                else
                    stack.Push(segment);
            }
            else if (segment != "." && !string.IsNullOrEmpty(segment))
            {
                stack.Push(segment);
            }
        }

        return string.Join("/", stack.Reverse());
    }
}
```

### Metadata Preservation:

```csharp
public class ProjectReferenceMetadataHandler
{
    private static readonly HashSet<string> PreserveMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReferenceOutputAssembly",  // Build-time only references
        "PrivateAssets",            // Analyzer/tools references
        "IncludeAssets",            // Asset filtering
        "ExcludeAssets",            // Asset filtering
        "Aliases",                  // Extern aliases
        "EmbedInteropTypes"         // COM interop
    };

    public void PreserveImportantMetadata(
        ProjectReference reference,
        XElement projectRefElement)
    {
        foreach (var metadata in reference.Metadata)
        {
            if (PreserveMetadata.Contains(metadata.Key))
            {
                projectRefElement.Add(new XElement(metadata.Key, metadata.Value));
            }
        }
    }
}
```

### Files to create/modify:

- `src/NetLift.Migration/Converters/ProjectReferenceConverter.cs` - Main converter
- `src/NetLift.Migration/Validators/ProjectReferenceValidator.cs` - Validation logic
- `src/NetLift.Migration/Handlers/CrossFrameworkReferenceHandler.cs` - Cross-framework handling
- `src/NetLift.Migration/Utilities/ProjectPathNormalizer.cs` - Path utilities
- `tests/NetLift.Tests/Converters/ProjectReferenceConverterTests.cs` - Unit tests

### Key Decisions:

- Remove project GUIDs (not needed in SDK-style)
- Remove Name metadata (redundant)
- Preserve only essential metadata
- Validate all references exist
- Detect and warn about circular references
- Normalize paths for cross-platform compatibility
- Flag cross-framework references for review

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
