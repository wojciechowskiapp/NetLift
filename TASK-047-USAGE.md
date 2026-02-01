# IncludeThenIncludeRewriter - Quick Reference

## Basic Usage

```csharp
using NetLift.Transforms.Ef.Rewriters;

var rewriter = new IncludeThenIncludeRewriter();
var transformedCode = rewriter.Rewrite(sourceCode);

// Check confidence
var confidence = rewriter.ConfidenceScore; // 100, 90, or 75

// Review diagnostics
foreach (var diagnostic in rewriter.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}");
}

// Check if new usings needed
var usings = rewriter.RequiredUsings; // e.g., ["Microsoft.EntityFrameworkCore"]
```

## Transformation Patterns

| EF6 Pattern | EF Core Output | Confidence |
|-------------|----------------|------------|
| `Include(o => o.Customer)` | No change | 100% |
| `Include(o => o.Items.Select(i => i.Product))` | `Include(o => o.Items).ThenInclude(y => y.Product)` | 90% |
| `Include("Items.Product")` | `Include(x => x.Items).ThenInclude(y => y.Product)` | 75% |

## Auto-Apply Guidelines

- **95-100%**: Auto-apply, no review needed
- **80-94%**: Auto-apply with INFO comment
- **60-79%**: Apply with TODO, recommend review (string-based falls here)
- **<60%**: Don't auto-apply, generate manual task

## Files

- **Interface**: `src/NetLift.Core/Interfaces/IIncludeThenIncludeRewriter.cs`
- **Implementation**: `src/NetLift.Transforms/Ef/Rewriters/IncludeThenIncludeRewriter.cs`
- **Tests**: `tests/NetLift.Tests.Unit/Transforms/Ef/Rewriters/IncludeThenIncludeRewriterTests.cs`
