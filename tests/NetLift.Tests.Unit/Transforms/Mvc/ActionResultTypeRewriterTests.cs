using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ActionResultTypeRewriterTests
{
    private readonly ActionResultTypeRewriter _rewriter = new();

    [Fact]
    public void RewriteActionResultToIActionResult_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
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
        rewritten.Should().Contain("public IActionResult Index()");
        rewritten.Should().NotContain("public ActionResult Index()");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().ContainSingle(d => d.Message.Contains("ActionResult") && d.Message.Contains("IActionResult"));
    }

    [Fact]
    public void RewriteTaskActionResultToTaskIActionResult_Success()
    {
        // Arrange
        var source = @"
using System.Threading.Tasks;
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public async Task<ActionResult> GetAsync()
        {
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public async Task<IActionResult> GetAsync()");
        rewritten.Should().NotContain("Task<ActionResult>");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteIHttpActionResultToIActionResult_Success()
    {
        // Arrange
        var source = @"
using System.Web.Http;

namespace TestApp
{
    public class ApiController
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
        rewritten.Should().Contain("public IActionResult Get()");
        rewritten.Should().NotContain("public IHttpActionResult Get()");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
    }

    [Fact]
    public void RemoveJsonRequestBehaviorParameter_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult GetData()
        {
            var data = new { Name = ""Test"" };
            return Json(data, JsonRequestBehavior.AllowGet);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return Json(data);");
        rewritten.Should().NotContain("JsonRequestBehavior");
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("JsonRequestBehavior"));
    }

    [Fact]
    public void RewriteHttpNotFoundToNotFound_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Details(int id)
        {
            var item = FindItem(id);
            if (item == null)
                return HttpNotFound();
            return View(item);
        }

        private object FindItem(int id) => null;
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return NotFound();");
        rewritten.Should().NotContain("return HttpNotFound();");
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("HttpNotFound") && d.Message.Contains("NotFound"));
    }

    [Fact]
    public void RewriteHttpStatusCodeResultToStatusCode_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Custom()
        {
            return new HttpStatusCodeResult(404);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return StatusCode(404);");
        rewritten.Should().NotContain("new HttpStatusCodeResult");
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("HttpStatusCodeResult") && d.Message.Contains("StatusCode"));
    }

    [Fact]
    public void RewriteHttpStatusCodeResultWithVariable_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Custom(int code)
        {
            return new HttpStatusCodeResult(code);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return StatusCode(code);");
        rewritten.Should().NotContain("new HttpStatusCodeResult");
    }

    [Fact]
    public void RewriteRedirectToRouteToRedirectToAction_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Login()
        {
            return RedirectToRoute(new { controller = ""Account"", action = ""Login"" });
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return RedirectToAction(\"Login\", \"Account\");");
        rewritten.Should().NotContain("RedirectToRoute");
        _rewriter.ConfidenceScore.Should().Be(90);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("RedirectToRoute") && d.Message.Contains("RedirectToAction"));
    }

    [Fact]
    public void RewriteRedirectToRouteActionOnly_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Login()
        {
            return RedirectToRoute(new { action = ""Index"" });
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return RedirectToAction(\"Index\");");
        rewritten.Should().NotContain("RedirectToRoute");
    }

    [Fact]
    public void MultipleReturnStatements_AllRewritten()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Details(int id)
        {
            if (id < 0)
                return new HttpStatusCodeResult(400);

            var item = FindItem(id);
            if (item == null)
                return HttpNotFound();

            return View(item);
        }

        private object FindItem(int id) => null;
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public IActionResult Details(int id)");
        rewritten.Should().Contain("return StatusCode(400);");
        rewritten.Should().Contain("return NotFound();");
        rewritten.Should().NotContain("public ActionResult Details");
        rewritten.Should().NotContain("new HttpStatusCodeResult");
        rewritten.Should().NotContain("return HttpNotFound();");
    }

    [Fact]
    public void MethodWithoutActionResult_NoChanges()
    {
        // Arrange
        var source = @"
using System;

namespace TestApp
{
    public class HomeController
    {
        public string GetName()
        {
            return ""Test"";
        }

        public void DoSomething()
        {
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public string GetName()");
        rewritten.Should().Contain("public void DoSomething()");
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void ExpressionBodiedMember_Rewritten()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Index() => View();
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public IActionResult Index() => View();");
        rewritten.Should().NotContain("public ActionResult Index()");
    }

    [Fact]
    public void PreserveOtherReturnTypes_Success()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Index() => View();

        public ViewResult Details() => View();

        public JsonResult GetJson() => Json(new { });

        public string GetString() => ""test"";
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public IActionResult Index()");
        rewritten.Should().Contain("public ViewResult Details()");
        rewritten.Should().Contain("public JsonResult GetJson()");
        rewritten.Should().Contain("public string GetString()");
    }

    [Fact]
    public void QualifiedActionResultType_Rewritten()
    {
        // Arrange
        var source = @"
namespace TestApp
{
    public class HomeController
    {
        public System.Web.Mvc.ActionResult Index()
        {
            return null;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("public IActionResult Index()");
        rewritten.Should().NotContain("System.Web.Mvc.ActionResult");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
    }

    [Fact]
    public void JsonCallWithoutJsonRequestBehavior_NoChanges()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult GetData()
        {
            return Json(new { Name = ""Test"" });
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return Json(new { Name = \"Test\" });");
    }

    [Fact]
    public void EmptySourceCode_ReturnsEmpty()
    {
        // Arrange
        var source = "";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().BeEmpty();
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void NullSourceCode_ReturnsNull()
    {
        // Arrange
        string source = null!;

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().BeNull();
    }

    [Fact]
    public void MultipleJsonCalls_AllRewritten()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult GetData1()
        {
            return Json(new { Id = 1 }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetData2()
        {
            return Json(new { Id = 2 }, JsonRequestBehavior.AllowGet);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("return Json(new { Id = 1 });");
        rewritten.Should().Contain("return Json(new { Id = 2 });");
        rewritten.Should().NotContain("JsonRequestBehavior");
        _rewriter.Diagnostics.Where(d => d.Message.Contains("JsonRequestBehavior")).Should().HaveCount(2);
    }

    [Fact]
    public void ComplexRedirectToRoute_LowersConfidence()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public ActionResult Login()
        {
            var routeValues = GetRouteValues();
            return RedirectToRoute(routeValues);
        }

        private object GetRouteValues() => null;
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.ConfidenceScore.Should().BeLessThan(90);
        _rewriter.Diagnostics.Should().Contain(d => d.Severity == NetLift.Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void RewriteAddsUsingDirectiveIfNotPresent()
    {
        // Arrange
        var source = @"
namespace TestApp
{
    public class HomeController
    {
        public System.Web.Mvc.ActionResult Index()
        {
            return null;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using Microsoft.AspNetCore.Mvc;");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
    }

    [Fact]
    public void RewriteDoesNotDuplicateExistingUsing()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp
{
    public class HomeController
    {
        public System.Web.Mvc.ActionResult Index()
        {
            return null;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        var usingCount = rewritten.Split("using Microsoft.AspNetCore.Mvc;").Length - 1;
        usingCount.Should().Be(1);
    }
}
