using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ControllerBaseClassRewriterTests
{
    private readonly ControllerBaseClassRewriter _rewriter = new();

    [Fact]
    public void RewriteSimpleController_AddsLoggerInjection()
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
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("ILogger<HomeController>");
        rewritten.Should().Contain("private readonly ILogger<HomeController> _logger;");
        rewritten.Should().Contain("public HomeController(ILogger<HomeController> logger)");
        rewritten.Should().Contain("_logger = logger;");
        rewritten.Should().Contain("using Microsoft.Extensions.Logging;");
        _rewriter.AddedDependencies.Should().ContainSingle();
        _rewriter.AddedDependencies.First().TypeName.Should().Be("ILogger<HomeController>");
        _rewriter.AddedDependencies.First().ParameterName.Should().Be("logger");
        _rewriter.AddedDependencies.First().FieldName.Should().Be("_logger");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteApiController_ConvertsToControllerBase_AddsAttributes()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp.Controllers
{
    public class ProductsController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain(": ControllerBase");
        rewritten.Should().NotContain(": ApiController");
        rewritten.Should().Contain("[ApiController]");
        rewritten.Should().Contain("[Route(\"api/[controller]\")]");
        rewritten.Should().Contain("ILogger<ProductsController>");
        rewritten.Should().Contain("using Microsoft.AspNetCore.Mvc;");
        rewritten.Should().Contain("using Microsoft.Extensions.Logging;");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithExistingConstructor_AddsLoggerParameter()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly ILogger<OrdersController> _logger;");
        rewritten.Should().Contain("public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)");
        rewritten.Should().Contain("_logger = logger;");
        rewritten.Should().Contain("_orderService = orderService;");
        _rewriter.AddedDependencies.Should().ContainSingle();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithMultipleDependencies_PreservesAll()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ComplexController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;

        public ComplexController(IOrderService orderService, IProductService productService)
        {
            _orderService = orderService;
            _productService = productService;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly ILogger<ComplexController> _logger;");
        rewritten.Should().Contain("IOrderService orderService, IProductService productService, ILogger<ComplexController> logger");
        rewritten.Should().Contain("_orderService = orderService;");
        rewritten.Should().Contain("_productService = productService;");
        rewritten.Should().Contain("_logger = logger;");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteCustomBaseController_DetectsAndReports()
    {
        // Arrange
        var source = @"
namespace TestApp.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain(": BaseController"); // Name preserved
        rewritten.Should().Contain("ILogger<HomeController>");
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("custom base controller"));
        _rewriter.ConfidenceScore.Should().Be(90); // Lower confidence for custom controllers
    }

    [Fact]
    public void RewriteClassWithoutBaseClass_NoChanges()
    {
        // Arrange
        var source = @"
namespace TestApp
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().NotContain("ILogger");
        rewritten.Should().NotContain("using Microsoft.Extensions.Logging;");
        _rewriter.AddedDependencies.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteNonControllerClass_NoChanges()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class OrderService : IOrderService
    {
        public void ProcessOrder()
        {
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().NotContain("ILogger");
        rewritten.Should().NotContain("using Microsoft.Extensions.Logging;");
        _rewriter.AddedDependencies.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithMultipleConstructors_LowersConfidence()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IOrderService _orderService;

        public HomeController()
        {
        }

        public HomeController(IOrderService orderService)
        {
            _orderService = orderService;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("ILogger<HomeController>");
        _rewriter.ConfidenceScore.Should().Be(60); // Lower confidence due to multiple constructors
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("multiple constructors"));
    }

    [Fact]
    public void RewriteControllerWithExistingAttributes_PreservesAttributes()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    [Authorize]
    [ValidateAntiForgeryToken]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("[Authorize]");
        rewritten.Should().Contain("[ValidateAntiForgeryToken]");
        rewritten.Should().Contain("ILogger<HomeController>");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteApiControllerWithExistingRouteAttribute_DoesNotDuplicate()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp.Controllers
{
    [Route(""api/custom"")]
    public class ProductsController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("[Route(\"api/custom\")]");
        rewritten.Should().NotContain("[Route(\"api/[controller]\")]");
        rewritten.Should().Contain("[ApiController]");
        rewritten.Should().Contain(": ControllerBase");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithXmlDocumentation_PreservesDocumentation()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    /// <summary>
    /// Home controller for the application.
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// Index action method.
        /// </summary>
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("/// <summary>");
        rewritten.Should().Contain("/// Home controller for the application.");
        rewritten.Should().Contain("/// Index action method.");
        rewritten.Should().Contain("ILogger<HomeController>");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithQualifiedBaseName_HandlesCorrectly()
    {
        // Arrange
        var source = @"
namespace TestApp.Controllers
{
    public class HomeController : System.Web.Mvc.Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain(": Controller");
        rewritten.Should().NotContain("System.Web.Mvc.Controller");
        rewritten.Should().Contain("ILogger<HomeController>");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteEmptyFile_ReturnsOriginal()
    {
        // Arrange
        var source = @"";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().Be(source);
        _rewriter.AddedDependencies.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerWithExistingLogger_DoesNotAddDuplicate()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using Microsoft.Extensions.Logging;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        // Should not add duplicate logger field or parameter
        var loggerFieldCount = CountOccurrences(rewritten, "private readonly ILogger<HomeController> _logger;");
        loggerFieldCount.Should().Be(1);
        _rewriter.AddedDependencies.Should().BeEmpty(); // No new dependencies added
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("already has ILogger field"));
    }

    [Fact]
    public void RewriteApiController_AddsAllRequiredUsings()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp.Controllers
{
    public class ProductsController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.Extensions.Logging");
    }

    [Fact]
    public void RewriteMultipleControllers_InSingleFile()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index() => View();
    }

    public class AboutController : Controller
    {
        public ActionResult Index() => View();
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("ILogger<HomeController>");
        rewritten.Should().Contain("ILogger<AboutController>");
        rewritten.Should().Contain("public HomeController(ILogger<HomeController> logger)");
        rewritten.Should().Contain("public AboutController(ILogger<AboutController> logger)");
        _rewriter.AddedDependencies.Should().HaveCount(2);
    }

    [Fact]
    public void RewriteNestedControllerClass_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class OuterClass
    {
        public class HomeController : Controller
        {
            public ActionResult Index() => View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("ILogger<HomeController>");
        rewritten.Should().Contain("public HomeController(ILogger<HomeController> logger)");
        _rewriter.AddedDependencies.Should().ContainSingle();
    }

    [Fact]
    public void TrackDiagnostics_ForEachTransformation()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp.Controllers
{
    public class ProductsController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Updated base class"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("[ApiController]"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("[Route"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("ILogger"));
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
