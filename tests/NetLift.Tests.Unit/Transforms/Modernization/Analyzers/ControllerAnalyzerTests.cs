using FluentAssertions;
using NetLift.Core.Models;
using NetLift.Transforms.Modernization.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Analyzers;

public sealed class ControllerAnalyzerTests
{
    private readonly ControllerAnalyzer _analyzer = new();

    #region Controller Detection Tests

    [Fact]
    public async Task AnalyzeAsync_ClassInheritingFromController_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.ClassName.Should().Be("HomeController");
        result.BaseClass.Should().Be("Controller");
        result.Namespace.Should().Be("TestApp.Controllers");
    }

    [Fact]
    public async Task AnalyzeAsync_ClassInheritingFromControllerBase_IsDetected()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ApiController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ApiController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.ClassName.Should().Be("ApiController");
        result.BaseClass.Should().Be("ControllerBase");
    }

    [Fact]
    public async Task AnalyzeAsync_ClassWithApiControllerAttribute_IsDetected()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    [ApiController]
    [Route(""api/[controller]"")]
    public class UsersController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("UsersController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.IsApiController.Should().BeTrue();
        result.RouteAttributes.Should().ContainSingle()
            .Which.Should().Be("api/[controller]");
    }

    [Fact]
    public async Task AnalyzeAsync_ClassEndingWithControllerButNoInheritance_IsDetected()
    {
        // Arrange
        var source = @"
namespace TestApp.Controllers
{
    public class CustomController
    {
        public string GetData()
        {
            return ""data"";
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("CustomController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.ClassName.Should().Be("CustomController");
        result.BaseClass.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_NonControllerClass_ReturnsNull()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class CustomerService
    {
        public string GetCustomer()
        {
            return ""customer"";
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("CustomerService.cs", source);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_EmptySource_ReturnsNull()
    {
        // Act
        var result = await _analyzer.AnalyzeAsync("Empty.cs", string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_FileScopedNamespace_ExtractsNamespaceCorrectly()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers;

public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Namespace.Should().Be("TestApp.Controllers");
    }

    #endregion

    #region Action Method Extraction Tests

    [Fact]
    public async Task AnalyzeAsync_MethodWithHttpGetAttribute_IsExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.Name.Should().Be("Index");
        action.HttpMethods.Should().ContainSingle().Which.Should().Be("GET");
    }

    [Fact]
    public async Task AnalyzeAsync_MethodWithHttpPostAttribute_IsExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        [HttpPost]
        public ActionResult Create(string name)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.Name.Should().Be("Create");
        action.HttpMethods.Should().ContainSingle().Which.Should().Be("POST");
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleActions_AllExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Details(int id)
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().HaveCount(3);
        result.Actions[0].Name.Should().Be("Index");
        result.Actions[1].Name.Should().Be("Details");
        result.Actions[2].Name.Should().Be("Create");
    }

    [Fact]
    public async Task AnalyzeAsync_MethodWithNonActionAttribute_IsExcluded()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [NonAction]
        public void HelperMethod()
        {
            // Helper logic
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        result.Actions[0].Name.Should().Be("Index");
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethod_IsExcluded()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        private ActionResult PrivateAction()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        result.Actions[0].Name.Should().Be("Index");
    }

    #endregion

    #region Command vs Query Classification Tests

    [Fact]
    public async Task AnalyzeAsync_GetMethod_IsClassifiedAsQuery()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            var products = db.Products.ToList();
            return View(products);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsQuery.Should().BeTrue();
        action.IsCommand.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_PostMethodWithSaveChanges_IsClassifiedAsCommand()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpPost]
        public ActionResult Create(Product product)
        {
            db.Products.Add(product);
            db.SaveChanges();
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsCommand.Should().BeTrue();
        action.IsQuery.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_MethodWithDbAdd_IsClassifiedAsCommand()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult PlaceOrder(Order order)
        {
            db.Orders.Add(order);
            return RedirectToAction(""Confirmation"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("OrdersController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsCommand.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_MethodWithOnlySelectWhere_IsClassifiedAsQuery()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Linq;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Search(string query)
        {
            var results = db.Products
                .Where(p => p.Name.Contains(query))
                .Select(p => new { p.Id, p.Name });
            return Json(results);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsQuery.Should().BeTrue();
        action.IsCommand.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_DeleteMethod_IsClassifiedAsCommand()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpDelete]
        public ActionResult Delete(int id)
        {
            var product = db.Products.Find(id);
            db.Products.Remove(product);
            db.SaveChanges();
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsCommand.Should().BeTrue();
        action.HttpMethods.Should().ContainSingle().Which.Should().Be("DELETE");
    }

    [Fact]
    public async Task AnalyzeAsync_PutMethod_IsClassifiedAsCommand()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpPut]
        public IActionResult Update(int id, Product product)
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsCommand.Should().BeTrue();
        action.HttpMethods.Should().ContainSingle().Which.Should().Be("PUT");
    }

    #endregion

    #region Parameter Extraction Tests

    [Fact]
    public async Task AnalyzeAsync_SimpleParameter_IsExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Details(int id)
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.Parameters.Should().ContainSingle();
        var param = action.Parameters[0];
        param.Name.Should().Be("id");
        param.Type.Should().Be("int");
        param.IsNullable.Should().BeFalse();
        param.HasDefaultValue.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ComplexParameter_IsExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpPost]
        public ActionResult Create(Product model)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.Parameters.Should().ContainSingle();
        var param = action.Parameters[0];
        param.Name.Should().Be("model");
        param.Type.Should().Be("Product");
    }

    [Fact]
    public async Task AnalyzeAsync_FromBodyAttribute_IsDetected()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpPost]
        public IActionResult Create([FromBody] Product product)
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        var param = action.Parameters[0];
        param.BindingSource.Should().Be("FromBody");
    }

    [Fact]
    public async Task AnalyzeAsync_FromQueryAttribute_IsDetected()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Search([FromQuery] string query)
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        var param = action.Parameters[0];
        param.BindingSource.Should().Be("FromQuery");
    }

    [Fact]
    public async Task AnalyzeAsync_BindAttribute_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpPost]
        public ActionResult Create([Bind(Include = ""Name,Price"")] Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        var param = action.Parameters[0];
        param.BindingSource.Should().Be("Bind");
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleParameters_AllExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Filter(string category, decimal minPrice, decimal maxPrice)
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.Parameters.Should().HaveCount(3);
        action.Parameters[0].Name.Should().Be("category");
        action.Parameters[0].Type.Should().Be("string");
        action.Parameters[1].Name.Should().Be("minPrice");
        action.Parameters[1].Type.Should().Be("decimal");
        action.Parameters[2].Name.Should().Be("maxPrice");
        action.Parameters[2].Type.Should().Be("decimal");
    }

    [Fact]
    public async Task AnalyzeAsync_NullableParameter_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Details(int? id)
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        var param = action.Parameters[0];
        param.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ParameterWithDefaultValue_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index(int page = 1)
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        var param = action.Parameters[0];
        param.HasDefaultValue.Should().BeTrue();
    }

    #endregion

    #region Additional Features Tests

    [Fact]
    public async Task AnalyzeAsync_AsyncAction_IsDetected()
    {
        // Arrange
        var source = @"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            await Task.Delay(100);
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.IsAsync.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithFilters_ExtractsFilters()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult Create(Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.Filters.Should().Contain("Authorize");
        action.Filters.Should().Contain("ValidateAntiForgeryToken");
        action.Filters.Should().NotContain("HttpPost"); // HTTP method attributes are excluded
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithRouteTemplate_ExtractsRoute()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet(""search"")]
        public IActionResult Search(string query)
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.RouteTemplate.Should().Be("search");
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithReturnType_ExtractsType()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public JsonResult GetData()
        {
            return Json(new { data = ""test"" });
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.ReturnType.Should().Be("JsonResult");
    }

    [Fact]
    public async Task AnalyzeAsync_MethodWithoutHttpAttribute_DefaultsToGet()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.HttpMethods.Should().ContainSingle().Which.Should().Be("GET");
    }

    #endregion

    #region Confidence Score Tests

    [Fact]
    public async Task AnalyzeAsync_WellStructuredController_HasHighConfidence()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Confidence.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public async Task AnalyzeAsync_ControllerWithNoActions_HasLowerConfidence()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class EmptyController : Controller
    {
        // No actions
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("EmptyController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Confidence.Should().BeLessOrEqualTo(70);
    }

    #endregion

    #region Project Analysis Tests

    [Fact]
    public async Task AnalyzeProjectAsync_MultipleControllers_AllDetected()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            FilePath = @"F:\src\TestApp\TestApp.csproj",
            Name = "TestApp",
            CompileItems = new List<CompileItem>
            {
                new() { Include = @"Controllers\HomeController.cs" },
                new() { Include = @"Controllers\ProductsController.cs" }
            }
        };

        // Create temporary controller files
        var homeControllerPath = Path.Combine(Path.GetDirectoryName(projectInfo.FilePath)!, "Controllers", "HomeController.cs");
        var productsControllerPath = Path.Combine(Path.GetDirectoryName(projectInfo.FilePath)!, "Controllers", "ProductsController.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(homeControllerPath)!);

        await File.WriteAllTextAsync(homeControllerPath, @"
using System.Web.Mvc;
namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index() => View();
    }
}");

        await File.WriteAllTextAsync(productsControllerPath, @"
using System.Web.Mvc;
namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index() => View();
    }
}");

        try
        {
            // Act
            var results = await _analyzer.AnalyzeProjectAsync(projectInfo);

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(c => c.ClassName == "HomeController");
            results.Should().Contain(c => c.ClassName == "ProductsController");
        }
        finally
        {
            // Cleanup
            if (File.Exists(homeControllerPath)) File.Delete(homeControllerPath);
            if (File.Exists(productsControllerPath)) File.Delete(productsControllerPath);
            if (Directory.Exists(Path.GetDirectoryName(homeControllerPath)))
                Directory.Delete(Path.GetDirectoryName(homeControllerPath)!, true);
        }
    }

    [Fact]
    public async Task AnalyzeProjectAsync_EmptyProject_ReturnsEmptyList()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            FilePath = @"F:\src\TestApp\TestApp.csproj",
            Name = "TestApp",
            CompileItems = new List<CompileItem>()
        };

        // Act
        var results = await _analyzer.AnalyzeProjectAsync(projectInfo);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeProjectAsync_NonExistentFiles_SkipsGracefully()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            FilePath = @"F:\src\TestApp\TestApp.csproj",
            Name = "TestApp",
            CompileItems = new List<CompileItem>
            {
                new() { Include = @"Controllers\NonExistent.cs" }
            }
        };

        // Act
        var results = await _analyzer.AnalyzeProjectAsync(projectInfo);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task AnalyzeAsync_ControllerWithInheritedApiController_DetectedAsApiController()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp.Controllers
{
    public class UsersController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("UsersController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.IsApiController.Should().BeTrue();
        result.BaseClass.Should().Be("ApiController");
    }

    [Fact]
    public async Task AnalyzeAsync_ControllerWithRoutePrefixAttribute_ExtractsRoute()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    [RoutePrefix(""api/products"")]
    public class ProductsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.RouteAttributes.Should().ContainSingle()
            .Which.Should().Be("api/products");
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithAcceptVerbsAttribute_ExtractsVerbs()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [AcceptVerbs(""GET"", ""POST"")]
        public ActionResult Flexible()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        var action = result!.Actions[0];
        action.HttpMethods.Should().Contain("GET");
        action.HttpMethods.Should().Contain("POST");
    }

    [Fact]
    public async Task AnalyzeAsync_ExpressionBodiedMember_IsAnalyzed()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok();
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        result.Actions[0].Name.Should().Be("Get");
    }

    #endregion

    #region Private Method Extraction Tests

    [Fact]
    public async Task AnalyzeAsync_ControllerWithPrivateMethodCalledByAction_ExtractsPrivateMethod()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ImageController : Controller
    {
        public ActionResult GetImage(string extension)
        {
            var mimeType = GetImageMimeTypeFromImageFileExtension(extension);
            return Content(mimeType);
        }

        private string GetImageMimeTypeFromImageFileExtension(string extension)
        {
            return extension switch
            {
                "".jpg"" => ""image/jpeg"",
                "".png"" => ""image/png"",
                _ => ""application/octet-stream""
            };
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ImageController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("GetImageMimeTypeFromImageFileExtension");
        privateMethod.Parameters.Should().ContainSingle()
            .Which.Name.Should().Be("extension");
        privateMethod.ReturnType.Should().Be("string");
        privateMethod.CallingActions.Should().ContainSingle()
            .Which.Should().Be("GetImage");
        privateMethod.IsAsync.Should().BeFalse();
        privateMethod.IsStatic.Should().BeFalse();
        privateMethod.Body.Should().Contain("switch");
    }

    [Fact]
    public async Task AnalyzeAsync_ControllerWithMultiplePrivateMethodsCalled_ExtractsAll()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class UriController : Controller
    {
        public ActionResult ProcessUri(string uri)
        {
            uri = AddUriPlaceHolder(uri);
            uri = NormalizeUri(uri);
            return Content(uri);
        }

        private string AddUriPlaceHolder(string uri)
        {
            return string.IsNullOrEmpty(uri) ? ""placeholder"" : uri;
        }

        private string NormalizeUri(string uri)
        {
            return uri.ToLowerInvariant();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("UriController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().HaveCount(2);
        result.PrivateMethods.Should().Contain(m => m.Name == "AddUriPlaceHolder");
        result.PrivateMethods.Should().Contain(m => m.Name == "NormalizeUri");

        var addMethod = result.PrivateMethods.First(m => m.Name == "AddUriPlaceHolder");
        addMethod.CallingActions.Should().ContainSingle().Which.Should().Be("ProcessUri");

        var normalizeMethod = result.PrivateMethods.First(m => m.Name == "NormalizeUri");
        normalizeMethod.CallingActions.Should().ContainSingle().Which.Should().Be("ProcessUri");
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethodCalledByMultipleActions_TracksAllCallers()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Create(Product product)
        {
            ValidateProduct(product);
            return RedirectToAction(""Index"");
        }

        public ActionResult Edit(int id, Product product)
        {
            ValidateProduct(product);
            return RedirectToAction(""Index"");
        }

        private void ValidateProduct(Product product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("ValidateProduct");
        privateMethod.CallingActions.Should().HaveCount(2);
        privateMethod.CallingActions.Should().Contain("Create");
        privateMethod.CallingActions.Should().Contain("Edit");
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethodNotCalledByActions_IsNotExtracted()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        private void UnusedHelper()
        {
            // This is never called by any action
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_AsyncPrivateMethod_MarkedAsAsync()
    {
        // Arrange
        var source = @"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetDataAsync()
        {
            var data = await FetchDataAsync();
            return Ok(data);
        }

        private async Task<string> FetchDataAsync()
        {
            await Task.Delay(100);
            return ""data"";
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("FetchDataAsync");
        privateMethod.IsAsync.Should().BeTrue();
        privateMethod.ReturnType.Should().Be("Task<string>");
    }

    [Fact]
    public async Task AnalyzeAsync_StaticPrivateMethod_MarkedAsStatic()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult FormatPrice(decimal price)
        {
            var formatted = FormatCurrency(price);
            return Content(formatted);
        }

        private static string FormatCurrency(decimal amount)
        {
            return $""${amount:N2}"";
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("FormatCurrency");
        privateMethod.IsStatic.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethodWithComplexParameters_ExtractsParameters()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Filter(string category)
        {
            var products = GetFilteredProducts(category, new List<string>());
            return View(products);
        }

        private List<Product> GetFilteredProducts(string category, List<string> tags)
        {
            return new List<Product>();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Parameters.Should().HaveCount(2);
        privateMethod.Parameters[0].Name.Should().Be("category");
        privateMethod.Parameters[0].Type.Should().Be("string");
        privateMethod.Parameters[1].Name.Should().Be("tags");
        privateMethod.Parameters[1].Type.Should().Be("List<string>");
        privateMethod.ReturnType.Should().Be("List<Product>");
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethodInExpressionBodiedAction_IsExtracted()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(GetDefaultMessage());

        private string GetDefaultMessage() => ""Hello World"";
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("GetDefaultMessage");
        privateMethod.CallingActions.Should().ContainSingle().Which.Should().Be("Get");
    }

    [Fact]
    public async Task AnalyzeAsync_ControllerWithNoPrivateMethods_ReturnsEmptyList()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("HomeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateMethodWithNoParameters_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class TimeController : Controller
    {
        public ActionResult GetTime()
        {
            var timestamp = GetCurrentTimestamp();
            return Content(timestamp);
        }

        private string GetCurrentTimestamp()
        {
            return System.DateTime.Now.ToString();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("TimeController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.PrivateMethods.Should().ContainSingle();

        var privateMethod = result.PrivateMethods[0];
        privateMethod.Name.Should().Be("GetCurrentTimestamp");
        privateMethod.Parameters.Should().BeEmpty();
        privateMethod.ReturnType.Should().Be("string");
    }

    #endregion

    #region Method Overload Tests

    [Fact]
    public async Task AnalyzeAsync_MethodOverloads_BothExtractedWithOverloadFlag()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StoreManagerController : Controller
    {
        // GET: Shows form
        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.GenreId = new SelectList(db.Genres, ""GenreId"", ""Name"");
            return View();
        }

        // POST: Processes form
        [HttpPost]
        public ActionResult Create(Album album)
        {
            if (ModelState.IsValid)
            {
                db.Albums.Add(album);
                db.SaveChanges();
                return RedirectToAction(""Index"");
            }
            return View(album);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StoreManagerController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().HaveCount(2);

        // Both should have HasOverload = true
        var getCreate = result.Actions.First(a => a.Parameters.Count == 0);
        var postCreate = result.Actions.First(a => a.Parameters.Count == 1);

        getCreate.Name.Should().Be("Create");
        getCreate.HasOverload.Should().BeTrue();
        getCreate.IsQuery.Should().BeTrue();
        getCreate.HttpMethods.Should().ContainSingle().Which.Should().Be("GET");

        postCreate.Name.Should().Be("Create");
        postCreate.HasOverload.Should().BeTrue();
        postCreate.IsCommand.Should().BeTrue();
        postCreate.HttpMethods.Should().ContainSingle().Which.Should().Be("POST");
    }

    [Fact]
    public async Task AnalyzeAsync_SingleMethodNoOverload_HasOverloadIsFalse()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().HaveCount(2);

        // Neither should have HasOverload = true (different names)
        result.Actions.Should().AllSatisfy(action => action.HasOverload.Should().BeFalse());
    }

    [Fact]
    public async Task AnalyzeAsync_ThreeOverloads_AllMarkedAsOverloaded()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class SearchController : Controller
    {
        public ActionResult Search()
        {
            return View();
        }

        public ActionResult Search(string query)
        {
            return View();
        }

        public ActionResult Search(string query, int page)
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("SearchController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().HaveCount(3);

        // All three should have HasOverload = true
        result.Actions.Should().AllSatisfy(action =>
        {
            action.Name.Should().Be("Search");
            action.HasOverload.Should().BeTrue();
        });

        // Each should have different parameter counts
        result.Actions.Select(a => a.Parameters.Count).Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public async Task AnalyzeAsync_EditOverloads_GetAndPostBothDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        // GET: Shows edit form
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var product = db.Products.Find(id);
            return View(product);
        }

        // POST: Updates product
        [HttpPost]
        public ActionResult Edit(int id, Product product)
        {
            if (ModelState.IsValid)
            {
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction(""Index"");
            }
            return View(product);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("ProductsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().HaveCount(2);

        var getEdit = result.Actions.First(a => a.Parameters.Count == 1);
        var postEdit = result.Actions.First(a => a.Parameters.Count == 2);

        getEdit.Name.Should().Be("Edit");
        getEdit.HasOverload.Should().BeTrue();
        getEdit.IsQuery.Should().BeTrue();

        postEdit.Name.Should().Be("Edit");
        postEdit.HasOverload.Should().BeTrue();
        postEdit.IsCommand.Should().BeTrue();
    }

    #endregion

    #region Trivial Action Detection Tests

    [Fact]
    public async Task AnalyzeAsync_TrivialActionReturnsView_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public ActionResult Create()
        {
            return View();
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.Name.Should().Be("Create");
        action.IsTrivial.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_TrivialActionReturnsViewWithNewModel_IsDetected()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public ActionResult Create()
        {
            return View(new StudentViewModel());
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithVariableDeclaration_IsNotTrivial()
    {
        // Arrange - Actions that create and manipulate variables are not trivial
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public ActionResult Create()
        {
            var model = new StudentViewModel();
            return View(model);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_TrivialActionExpressionBody_IsDetected()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public IActionResult Create() => View();
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithDatabaseOperation_IsNotTrivial()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public ActionResult Index()
        {
            var students = db.Students.ToList();
            return View(students);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithServiceCall_IsNotTrivial()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Threading.Tasks;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public async Task<ActionResult> Index()
        {
            var students = await _service.GetAllAsync();
            return View(students);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithComplexLogic_IsNotTrivial()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        public ActionResult Details(int id)
        {
            var student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ActionWithSaveChanges_IsNotTrivial()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        [HttpPost]
        public ActionResult Create(Student student)
        {
            db.Students.Add(student);
            db.SaveChanges();
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var result = await _analyzer.AnalyzeAsync("StudentsController.cs", source);

        // Assert
        result.Should().NotBeNull();
        result!.Actions.Should().ContainSingle();
        var action = result.Actions[0];
        action.IsTrivial.Should().BeFalse();
    }

    #endregion
}
