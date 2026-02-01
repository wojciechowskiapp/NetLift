using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class AttributeRoutingTransformerTests
{
    private readonly AttributeRoutingTransformer _transformer = new();

    [Fact]
    public void AddsRouteAttributeToController()
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
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("[Route(\"[controller]\")]");
        rewritten.Should().Contain("using Microsoft.AspNetCore.Mvc;");
        _transformer.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _transformer.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Added [Route(\"[controller]\")]") &&
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void AddsHttpGetToGetAction()
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
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("[HttpGet]");
        rewritten.Should().Contain("public ActionResult Index()");
        _transformer.ConfidenceScore.Should().BeGreaterOrEqualTo(95);
    }

    [Fact]
    public void AddsHttpPostToPostAction()
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
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("[HttpPost]");
        rewritten.Should().Contain("public ActionResult Create(Product product)");
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("[HttpPost]"));
    }

    [Fact]
    public void AddsHttpDeleteToDeleteAction()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Delete(int id)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("[HttpDelete(\"{id:int}\")]");
        rewritten.Should().Contain("public ActionResult Delete(int id)");
        _transformer.Diagnostics.Should().Contain(d =>
            d.Message.Contains("[HttpDelete") && d.Message.Contains("{id:int}"));
    }

    [Fact]
    public void InfersHttpMethodFromName()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult GetProduct(int id)
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("[HttpGet(\"{id:int}\")]");
        rewritten.Should().Contain("public ActionResult GetProduct(int id)");
        _transformer.ConfidenceScore.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void ConvertsIntConstraint()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult Details(int id)
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        rewritten.Should().Contain("{id:int}");
        rewritten.Should().NotContain(@"\d+");
        _transformer.ConfidenceScore.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void HandlesOptionalParameter()
    {
        // Arrange - method without parameters should not generate route template
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

        public ActionResult Search(string query)
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        // Index() should get [HttpGet] without route template
        rewritten.Should().Contain("[HttpGet]");
        rewritten.Should().Contain("public ActionResult Index()");

        // Search with string parameter should not auto-generate route (complex case)
        rewritten.Should().Contain("public ActionResult Search(string query)");

        // Count of [HttpGet] attributes should be 2
        var httpGetCount = System.Text.RegularExpressions.Regex.Matches(rewritten, @"\[HttpGet\]").Count;
        httpGetCount.Should().Be(2);
    }

    [Fact]
    public void PreservesExistingAttributes()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    [Route(""[controller]"")]
    public class ProductsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Product product)
        {
            return RedirectToAction(""Index"");
        }
    }
}";

        // Act
        var rewritten = _transformer.Rewrite(source);

        // Assert
        // Should not add duplicate [Route] attribute
        var routeCount = System.Text.RegularExpressions.Regex.Matches(rewritten, @"\[Route\(").Count;
        routeCount.Should().Be(1);

        // Should not add duplicate [HttpGet] or [HttpPost]
        var httpGetCount = System.Text.RegularExpressions.Regex.Matches(rewritten, @"\[HttpGet\]").Count;
        httpGetCount.Should().Be(1);

        var httpPostCount = System.Text.RegularExpressions.Regex.Matches(rewritten, @"\[HttpPost\]").Count;
        httpPostCount.Should().Be(1);

        // Diagnostics should be empty since no changes were made
        _transformer.Diagnostics.Should().BeEmpty();
    }
}
