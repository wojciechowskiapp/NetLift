# Task 041: Handle MVC Areas Migration

## Meta
- **Priority**: P2
- **Estimate**: 8 points
- **Sprint**: 4
- **Dependencies**: 040
- **Status**: Not Started

## Description
Implement transformation logic to migrate ASP.NET MVC Areas to ASP.NET Core convention. This includes converting AreaRegistration classes to folder-based routing, adding [Area] attributes, creating area-specific _ViewStart and _ViewImports files, and configuring area route prefixes.

## Acceptance Criteria
- [ ] AreaMigrationTransformer class implemented
- [ ] Parse AreaRegistration classes and extract area configuration
- [ ] Create proper folder structure (Areas/{AreaName}/Controllers, Views)
- [ ] Add [Area("AreaName")] attribute to area controllers
- [ ] Generate area-specific _ViewStart.cshtml
- [ ] Generate area-specific _ViewImports.cshtml
- [ ] Update route registration for MapAreaControllerRoute
- [ ] Handle area-specific routes and constraints
- [ ] Support shared views between areas
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### Area Registration Parser
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Parsers;

public record AreaDefinition
{
    public string Name { get; init; } = "";
    public string? RoutePrefix { get; init; }
    public List<RouteDefinition> Routes { get; init; } = new();
    public string? SourceFilePath { get; init; }
}

public class AreaRegistrationParser
{
    private readonly SemanticModel _semanticModel;

    public AreaRegistrationParser(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public AreaDefinition? ParseAreaRegistration(ClassDeclarationSyntax classNode)
    {
        // Check if inherits from AreaRegistration
        if (!InheritsFromAreaRegistration(classNode))
            return null;

        var areaName = ExtractAreaName(classNode);
        var routes = ExtractAreaRoutes(classNode);

        return new AreaDefinition
        {
            Name = areaName,
            Routes = routes,
            SourceFilePath = classNode.SyntaxTree.FilePath
        };
    }

    private bool InheritsFromAreaRegistration(ClassDeclarationSyntax node)
    {
        return node.BaseList?.Types.Any(t =>
            t.Type.ToString().Contains("AreaRegistration")) ?? false;
    }

    private string ExtractAreaName(ClassDeclarationSyntax classNode)
    {
        // Look for AreaName property override
        var areaNameProperty = classNode.Members
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == "AreaName");

        if (areaNameProperty?.ExpressionBody?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        // Fall back to class name convention: AdminAreaRegistration -> Admin
        var className = classNode.Identifier.Text;
        return className.Replace("AreaRegistration", "");
    }

    private List<RouteDefinition> ExtractAreaRoutes(ClassDeclarationSyntax classNode)
    {
        var routes = new List<RouteDefinition>();

        // Find RegisterArea method
        var registerMethod = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "RegisterArea");

        if (registerMethod == null)
            return routes;

        // Find MapRoute calls within RegisterArea
        var mapRouteCalls = registerMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString().Contains("MapRoute"));

        foreach (var call in mapRouteCalls)
        {
            var route = ParseMapRouteCall(call);
            if (route != null)
            {
                routes.Add(route);
            }
        }

        return routes;
    }

    private RouteDefinition? ParseMapRouteCall(InvocationExpressionSyntax call)
    {
        // Similar to RouteConfigParser implementation
        // ... (reuse from TASK-037)
        return null;
    }
}
```

### Area Migration Transformer
```csharp
namespace NetLift.Mvc.Transformers;

public class AreaMigrationTransformer
{
    private readonly ViewImportsGenerator _viewImportsGenerator;
    private readonly ViewStartGenerator _viewStartGenerator;

    public AreaMigrationTransformer()
    {
        _viewImportsGenerator = new ViewImportsGenerator();
        _viewStartGenerator = new ViewStartGenerator();
    }

    /// <summary>
    /// Generates the migration plan for an area
    /// </summary>
    public AreaMigrationPlan CreateMigrationPlan(
        AreaDefinition area,
        string projectRoot,
        string rootNamespace)
    {
        var plan = new AreaMigrationPlan
        {
            AreaName = area.Name,
            FoldersToCreate = GenerateFolderStructure(area.Name, projectRoot),
            FilesToGenerate = GenerateAreaFiles(area, projectRoot, rootNamespace),
            ControllersToUpdate = FindAreaControllers(area.Name, projectRoot),
            RouteRegistration = GenerateRouteRegistration(area)
        };

        return plan;
    }

    private List<string> GenerateFolderStructure(string areaName, string projectRoot)
    {
        return new List<string>
        {
            Path.Combine(projectRoot, "Areas", areaName),
            Path.Combine(projectRoot, "Areas", areaName, "Controllers"),
            Path.Combine(projectRoot, "Areas", areaName, "Views"),
            Path.Combine(projectRoot, "Areas", areaName, "Views", "Shared"),
            Path.Combine(projectRoot, "Areas", areaName, "Models"),
            Path.Combine(projectRoot, "Areas", areaName, "ViewModels")
        };
    }

    private List<GeneratedFile> GenerateAreaFiles(
        AreaDefinition area,
        string projectRoot,
        string rootNamespace)
    {
        var files = new List<GeneratedFile>();

        // _ViewImports.cshtml for the area
        var viewImportsContent = _viewImportsGenerator.GenerateForArea(
            area.Name, rootNamespace);

        files.Add(new GeneratedFile
        {
            Path = Path.Combine(projectRoot, "Areas", area.Name, "Views", "_ViewImports.cshtml"),
            Content = viewImportsContent
        });

        // _ViewStart.cshtml for the area
        var viewStartContent = _viewStartGenerator.GenerateForArea(area.Name);

        files.Add(new GeneratedFile
        {
            Path = Path.Combine(projectRoot, "Areas", area.Name, "Views", "_ViewStart.cshtml"),
            Content = viewStartContent
        });

        return files;
    }

    private List<ControllerUpdate> FindAreaControllers(string areaName, string projectRoot)
    {
        var updates = new List<ControllerUpdate>();

        var controllersPath = Path.Combine(projectRoot, "Areas", areaName, "Controllers");
        if (!Directory.Exists(controllersPath))
            return updates;

        foreach (var file in Directory.GetFiles(controllersPath, "*Controller.cs"))
        {
            updates.Add(new ControllerUpdate
            {
                FilePath = file,
                AreaAttribute = $"[Area(\"{areaName}\")]"
            });
        }

        return updates;
    }

    private string GenerateRouteRegistration(AreaDefinition area)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Add to Program.cs route configuration");
        sb.AppendLine($"app.MapAreaControllerRoute(");
        sb.AppendLine($"    name: \"{area.Name}Default\",");
        sb.AppendLine($"    areaName: \"{area.Name}\",");
        sb.AppendLine($"    pattern: \"{area.Name}/{{controller=Home}}/{{action=Index}}/{{id?}}\");");

        return sb.ToString();
    }

    /// <summary>
    /// Adds [Area] attribute to controller class
    /// </summary>
    public ClassDeclarationSyntax AddAreaAttribute(
        ClassDeclarationSyntax controller,
        string areaName)
    {
        // Check if already has [Area] attribute
        if (controller.AttributeLists.SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() == "Area"))
        {
            return controller;
        }

        var areaAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Area"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(areaName))))));

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(areaAttribute));

        return controller.AddAttributeLists(attributeList);
    }
}

public record AreaMigrationPlan
{
    public string AreaName { get; init; } = "";
    public List<string> FoldersToCreate { get; init; } = new();
    public List<GeneratedFile> FilesToGenerate { get; init; } = new();
    public List<ControllerUpdate> ControllersToUpdate { get; init; } = new();
    public string RouteRegistration { get; init; } = "";
}

public record GeneratedFile
{
    public string Path { get; init; } = "";
    public string Content { get; init; } = "";
}

public record ControllerUpdate
{
    public string FilePath { get; init; } = "";
    public string AreaAttribute { get; init; } = "";
}
```

### Example Transformation

**Before (Legacy Area Registration):**
```csharp
// Areas/Admin/AdminAreaRegistration.cs
public class AdminAreaRegistration : AreaRegistration
{
    public override string AreaName => "Admin";

    public override void RegisterArea(AreaRegistrationContext context)
    {
        context.MapRoute(
            "Admin_default",
            "Admin/{controller}/{action}/{id}",
            new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional }
        );
    }
}

// Areas/Admin/Controllers/DashboardController.cs
public class DashboardController : Controller
{
    public ActionResult Index()
    {
        return View();
    }
}
```

**After (ASP.NET Core Convention):**
```
Areas/
  Admin/
    Controllers/
      DashboardController.cs   [with [Area("Admin")] attribute]
    Views/
      _ViewImports.cshtml
      _ViewStart.cshtml
      Dashboard/
        Index.cshtml
      Shared/
        _Layout.cshtml
    Models/
    ViewModels/
```

**DashboardController.cs (After):**
```csharp
[Area("Admin")]
[Route("Admin/[controller]")]
public class DashboardController : Controller
{
    [HttpGet("[action]")]
    public IActionResult Index()
    {
        return View();
    }
}
```

**Program.cs Route Registration:**
```csharp
app.MapAreaControllerRoute(
    name: "AdminDefault",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

### Unit Tests
```csharp
public class AreaMigrationTransformerTests
{
    [Fact]
    public void ParsesAreaNameFromRegistration()
    {
        var source = @"
public class AdminAreaRegistration : AreaRegistration
{
    public override string AreaName => ""Admin"";
    public override void RegisterArea(AreaRegistrationContext context) { }
}";

        var area = ParseAreaRegistration(source);

        Assert.Equal("Admin", area.Name);
    }

    [Fact]
    public void GeneratesCorrectFolderStructure()
    {
        var area = new AreaDefinition { Name = "Admin" };
        var transformer = new AreaMigrationTransformer();

        var plan = transformer.CreateMigrationPlan(area, "/project", "MyApp");

        Assert.Contains("/project/Areas/Admin/Controllers", plan.FoldersToCreate);
        Assert.Contains("/project/Areas/Admin/Views", plan.FoldersToCreate);
        Assert.Contains("/project/Areas/Admin/Views/Shared", plan.FoldersToCreate);
    }

    [Fact]
    public void GeneratesAreaViewImports()
    {
        var area = new AreaDefinition { Name = "Admin" };
        var transformer = new AreaMigrationTransformer();

        var plan = transformer.CreateMigrationPlan(area, "/project", "MyApp");

        var viewImports = plan.FilesToGenerate
            .FirstOrDefault(f => f.Path.Contains("_ViewImports"));

        Assert.NotNull(viewImports);
        Assert.Contains("@using MyApp.Areas.Admin", viewImports.Content);
    }

    [Fact]
    public void AddsAreaAttributeToController()
    {
        var source = @"
public class DashboardController : Controller
{
    public ActionResult Index() => View();
}";

        var transformer = new AreaMigrationTransformer();
        var tree = CSharpSyntaxTree.ParseText(source);
        var controller = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>().First();

        var result = transformer.AddAreaAttribute(controller, "Admin");

        Assert.Contains("[Area(\"Admin\")]", result.ToFullString());
    }

    [Fact]
    public void GeneratesMapAreaControllerRoute()
    {
        var area = new AreaDefinition { Name = "Admin" };
        var transformer = new AreaMigrationTransformer();

        var plan = transformer.CreateMigrationPlan(area, "/project", "MyApp");

        Assert.Contains("MapAreaControllerRoute", plan.RouteRegistration);
        Assert.Contains("areaName: \"Admin\"", plan.RouteRegistration);
    }
}
```

## Progress Log
- [Created] - Task definition with area migration implementation details
