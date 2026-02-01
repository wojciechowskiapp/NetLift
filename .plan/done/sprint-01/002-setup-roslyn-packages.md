# [TASK-002] Setup Roslyn Packages

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | S |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-001
- **Blocks:** TASK-004, TASK-005

---

## Description

Add Microsoft.CodeAnalysis (Roslyn) packages to the relevant projects for code analysis and transformation capabilities.

---

## Acceptance Criteria

- [ ] Roslyn packages added to NetLift.Analysis
- [ ] Roslyn packages added to NetLift.Transforms
- [ ] MSBuild.Locator configured for workspace loading
- [ ] Basic Roslyn test passes (can parse simple C# code)
- [ ] Solution still builds cleanly

---

## Technical Notes

### Packages to add:

**NetLift.Analysis:**
```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />
<PackageReference Include="Microsoft.Build.Locator" Version="1.6.10" />
```

**NetLift.Transforms:**
```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
```

### MSBuild.Locator initialization:

Must be called before any MSBuild/Roslyn workspace usage:

```csharp
// In Program.cs or startup
if (!MSBuildLocator.IsRegistered)
{
    MSBuildLocator.RegisterDefaults();
}
```

### Verification test:

```csharp
[Fact]
public void CanParseSimpleCSharp()
{
    var code = "public class Foo { }";
    var tree = CSharpSyntaxTree.ParseText(code);
    var root = tree.GetRoot();

    var classDecl = root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .First();

    Assert.Equal("Foo", classDecl.Identifier.Text);
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
