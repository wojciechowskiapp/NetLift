using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Generators;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class AreaMigrationTransformerTests
{
    private readonly AreaMigrationTransformer _transformer;

    public AreaMigrationTransformerTests()
    {
        var viewImportsGenerator = new ViewImportsGenerator();
        _transformer = new AreaMigrationTransformer(viewImportsGenerator);
    }

    [Fact]
    public void CreateMigrationPlan_NullArea_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _transformer.CreateMigrationPlan(null!, "C:\\Project", "MyApp"));
    }

    [Fact]
    public void CreateMigrationPlan_NullProjectRoot_ThrowsArgumentException()
    {
        // Arrange
        var area = new AreaDefinition { Name = "Admin" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _transformer.CreateMigrationPlan(area, null!, "MyApp"));
    }

    [Fact]
    public void CreateMigrationPlan_NullRootNamespace_ThrowsArgumentException()
    {
        // Arrange
        var area = new AreaDefinition { Name = "Admin" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _transformer.CreateMigrationPlan(area, "C:\\Project", null!));
    }

    [Fact]
    public void CreateMigrationPlan_ValidArea_GeneratesFolderStructure()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Equal("Admin", plan.AreaName);
        Assert.Equal(4, plan.FoldersToCreate.Count);
        Assert.Contains("Areas/Admin", plan.FoldersToCreate);
        Assert.Contains("Areas/Admin/Controllers", plan.FoldersToCreate);
        Assert.Contains("Areas/Admin/Views", plan.FoldersToCreate);
        Assert.Contains("Areas/Admin/Models", plan.FoldersToCreate);
    }

    [Fact]
    public void CreateMigrationPlan_ValidArea_GeneratesViewImportsFile()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Contains("Areas/Admin/Views/_ViewImports.cshtml", plan.FilesToGenerate.Keys);
        var viewImportsContent = plan.FilesToGenerate["Areas/Admin/Views/_ViewImports.cshtml"];
        Assert.Contains("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers", viewImportsContent);
        Assert.Contains("@using Microsoft.AspNetCore.Mvc", viewImportsContent);
        Assert.Contains("@using MyApp.Areas.Admin", viewImportsContent);
    }

    [Fact]
    public void CreateMigrationPlan_ValidArea_GeneratesViewStartFile()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Contains("Areas/Admin/Views/_ViewStart.cshtml", plan.FilesToGenerate.Keys);
        var viewStartContent = plan.FilesToGenerate["Areas/Admin/Views/_ViewStart.cshtml"];
        Assert.Contains("Layout = \"_Layout\"", viewStartContent);
    }

    [Fact]
    public void CreateMigrationPlan_AreaWithRoutes_GeneratesRouteRegistration()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>
            {
                new RouteDefinition
                {
                    Name = "Admin_default",
                    Template = "Admin/{controller}/{action}/{id}",
                    Defaults = new Dictionary<string, object?>
                    {
                        { "action", "Index" },
                        { "id", RouteDefinition.OptionalParameter }
                    }
                }
            }
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.NotEmpty(plan.RouteRegistration);
        Assert.Contains("app.MapAreaControllerRoute(", plan.RouteRegistration);
        Assert.Contains("name: \"Admin_default\"", plan.RouteRegistration);
        Assert.Contains("areaName: \"Admin\"", plan.RouteRegistration);
        Assert.Contains("pattern: \"Admin/{controller}/{action}/{id}\"", plan.RouteRegistration);
    }

    [Fact]
    public void CreateMigrationPlan_AreaWithMultipleRoutes_GeneratesAllRouteRegistrations()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>
            {
                new RouteDefinition
                {
                    Name = "Admin_users",
                    Template = "Admin/Users/{action}/{id}",
                    Defaults = new Dictionary<string, object?>()
                },
                new RouteDefinition
                {
                    Name = "Admin_default",
                    Template = "Admin/{controller}/{action}/{id}",
                    Defaults = new Dictionary<string, object?>()
                }
            }
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Contains("name: \"Admin_users\"", plan.RouteRegistration);
        Assert.Contains("name: \"Admin_default\"", plan.RouteRegistration);
    }

    [Fact]
    public void CreateMigrationPlan_AreaWithNoRoutes_GeneratesDefaultRoute()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Contains("app.MapAreaControllerRoute(", plan.RouteRegistration);
        Assert.Contains("name: \"Admin_default\"", plan.RouteRegistration);
        Assert.Contains("areaName: \"Admin\"", plan.RouteRegistration);
        Assert.Contains("pattern: \"Admin/{controller=Home}/{action=Index}/{id?}\"", plan.RouteRegistration);
    }

    [Fact]
    public void CreateMigrationPlan_ValidArea_HasHighConfidenceScore()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Equal(95, plan.ConfidenceScore);
    }

    [Fact]
    public void CreateMigrationPlan_ValidArea_GeneratesDiagnostics()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.NotEmpty(plan.Diagnostics);
        Assert.Contains(plan.Diagnostics, d => d.Contains("Area 'Admin'"));
    }

    [Fact]
    public void AddAreaAttribute_EmptySource_ReturnsEmptySource()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AddAreaAttribute_NullAreaName_ThrowsArgumentException()
    {
        // Arrange
        var source = "public class HomeController : Controller { }";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _transformer.AddAreaAttribute(source, null!));
    }

    [Fact]
    public void AddAreaAttribute_ControllerWithoutAreaAttribute_AddsAttribute()
    {
        // Arrange
        var source = """
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Areas.Admin.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        Assert.Contains("[Area(\"Admin\")]", result);
    }

    [Fact]
    public void AddAreaAttribute_ControllerWithAreaAttribute_DoesNotDuplicate()
    {
        // Arrange
        var source = """
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        // Count occurrences of [Area("Admin")]
        var count = System.Text.RegularExpressions.Regex.Matches(result, @"\[Area\(""Admin""\)\]").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void AddAreaAttribute_ControllerWithoutUsing_AddsUsingDirective()
    {
        // Arrange
        var source = """
namespace MyApp.Areas.Admin.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        Assert.Contains("using Microsoft.AspNetCore.Mvc;", result);
        Assert.Contains("[Area(\"Admin\")]", result);
    }

    [Fact]
    public void AddAreaAttribute_NonControllerClass_DoesNotAddAttribute()
    {
        // Arrange
        var source = """
namespace MyApp.Areas.Admin.Models
{
    public class UserViewModel
    {
        public string Name { get; set; }
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        Assert.DoesNotContain("[Area(\"Admin\")]", result);
    }

    [Fact]
    public void AddAreaAttribute_MultipleControllers_AddsAttributeToAll()
    {
        // Arrange
        var source = """
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Areas.Admin.Controllers
{
    public class HomeController : Controller
    {
    }

    public class UsersController : Controller
    {
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        var count = System.Text.RegularExpressions.Regex.Matches(result, @"\[Area\(""Admin""\)\]").Count;
        Assert.Equal(2, count);
    }

    [Fact]
    public void AddAreaAttribute_ControllerWithOtherAttributes_PreservesAttributes()
    {
        // Arrange
        var source = """
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Areas.Admin.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        Assert.Contains("[Authorize]", result);
        Assert.Contains("[Route(\"api/[controller]\")]", result);
        Assert.Contains("[Area(\"Admin\")]", result);
    }

    [Fact]
    public void AddAreaAttribute_ControllerWithAreaAttribute_UsingAreaAttribute_DoesNotDuplicate()
    {
        // Arrange
        var source = """
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Areas.Admin.Controllers
{
    [AreaAttribute("Admin")]
    public class HomeController : Controller
    {
    }
}
""";

        // Act
        var result = _transformer.AddAreaAttribute(source, "Admin");

        // Assert
        // Should not add another [Area] attribute
        Assert.Contains("[AreaAttribute(\"Admin\")]", result);
        Assert.DoesNotContain("[Area(\"Admin\")]", result);
    }

    [Fact]
    public void CreateMigrationPlan_RouteWithStringDefaults_GeneratesCorrectCode()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "Admin",
            RoutePrefix = "Admin",
            Routes = new List<RouteDefinition>
            {
                new RouteDefinition
                {
                    Name = "Admin_default",
                    Template = "Admin/{controller}/{action}",
                    Defaults = new Dictionary<string, object?>
                    {
                        { "controller", "Home" },
                        { "action", "Index" }
                    }
                }
            }
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Contains("controller = \"Home\"", plan.RouteRegistration);
        Assert.Contains("action = \"Index\"", plan.RouteRegistration);
    }

    [Fact]
    public void CreateMigrationPlan_MultiWordAreaName_GeneratesCorrectStructure()
    {
        // Arrange
        var area = new AreaDefinition
        {
            Name = "BackOffice",
            RoutePrefix = "BackOffice",
            Routes = new List<RouteDefinition>()
        };

        // Act
        var plan = _transformer.CreateMigrationPlan(area, "C:\\Project", "MyApp");

        // Assert
        Assert.Equal("BackOffice", plan.AreaName);
        Assert.Contains("Areas/BackOffice", plan.FoldersToCreate);
        Assert.Contains("Areas/BackOffice/Views/_ViewImports.cshtml", plan.FilesToGenerate.Keys);
    }
}
