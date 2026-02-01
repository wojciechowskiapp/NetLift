using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class HttpContextCurrentRewriterTests
{
    private readonly HttpContextCurrentRewriter _rewriter = new();

    [Fact]
    public void RewriteControllerHttpContextCurrentUser_ReplacesWithUserProperty()
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
            var userName = HttpContext.Current.User.Identity.Name;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("User.Identity.Name");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ClassesNeedingAccessor.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Replaced HttpContext.Current.User"));
    }

    [Fact]
    public void RewriteControllerHttpContextCurrentRequest_ReplacesWithRequestProperty()
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
            var url = HttpContext.Current.Request.Url;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Request.Url");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteControllerHttpContextCurrentResponse_ReplacesWithResponseProperty()
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
            HttpContext.Current.Response.StatusCode = 404;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Response.StatusCode");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteNonControllerClass_AddsIHttpContextAccessor()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        rewritten.Should().Contain("public UserService(IHttpContextAccessor httpContextAccessor)");
        rewritten.Should().Contain("_httpContextAccessor = httpContextAccessor;");
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.Name");
        rewritten.Should().Contain("using Microsoft.AspNetCore.Http;");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ClassesNeedingAccessor.Should().Contain("UserService");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteSessionAccess_AddsIHttpContextAccessor()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class SessionService
    {
        public void StoreValue(string key, object value)
        {
            HttpContext.Current.Session[key] = value;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.Session[key]");
        rewritten.Should().NotContain("HttpContext.Current.Session");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(60); // Lower confidence for Session
    }

    [Fact]
    public void RewriteItemsAccess_AddsIHttpContextAccessor()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class ItemsService
    {
        public void StoreValue(string key, object value)
        {
            HttpContext.Current.Items[key] = value;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.Items[key]");
        rewritten.Should().NotContain("HttpContext.Current.Items");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void RewriteMultipleHttpContextCurrentUsages_ReplacesAll()
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
            var user = HttpContext.Current.User;
            var request = HttpContext.Current.Request;
            var response = HttpContext.Current.Response;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("var user = User;");
        rewritten.Should().Contain("var request = Request;");
        rewritten.Should().Contain("var response = Response;");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteClassWithExistingConstructor_AddsHttpContextAccessorParameter()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        private readonly ILogger _logger;

        public UserService(ILogger logger)
        {
            _logger = logger;
        }

        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        rewritten.Should().Contain("public UserService(ILogger logger, IHttpContextAccessor httpContextAccessor)");
        rewritten.Should().Contain("_logger = logger;");
        rewritten.Should().Contain("_httpContextAccessor = httpContextAccessor;");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteFullyQualifiedHttpContextCurrent_HandlesCorrectly()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public string GetCurrentUser()
        {
            return System.Web.HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.Name");
        rewritten.Should().NotContain("System.Web.HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewritePropertyChainPreservation_MaintainsFullChain()
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
            var url = HttpContext.Current.Request.Url.AbsolutePath;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Request.Url.AbsolutePath");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteNullConditionalOperators_AddsSafety()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public string GetCurrentUserName()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.Name");
        rewritten.Should().Contain("?.");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteClassWithMultipleConstructors_LowersConfidence()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        private readonly ILogger _logger;

        public UserService()
        {
        }

        public UserService(ILogger logger)
        {
            _logger = logger;
        }

        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(70);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("multiple constructors"));
    }

    [Fact]
    public void RewriteClassWithExistingHttpContextAccessor_SkipsInjection()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Http;

namespace TestApp.Services
{
    public class UserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.Name");
        rewritten.Should().NotContain("HttpContext.Current");
        // Should not add duplicate accessor field
        var accessorFieldCount = CountOccurrences(rewritten, "private readonly IHttpContextAccessor _httpContextAccessor;");
        accessorFieldCount.Should().Be(1);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("already has IHttpContextAccessor"));
    }

    [Fact]
    public void RewriteDirectHttpContextCurrentAccess_ReplacesCorrectly()
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
            var context = HttpContext.Current;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("var context = HttpContext;");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteNonControllerDirectHttpContextCurrentAccess_UsesAccessor()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class ContextService
    {
        public object GetContext()
        {
            return HttpContext.Current;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_httpContextAccessor.HttpContext");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
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
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteClassWithoutHttpContextUsage_NoChanges()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
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
        rewritten.Should().NotContain("IHttpContextAccessor");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteMultipleClassesInSingleFile_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var user = HttpContext.Current.User;
            return View();
        }
    }

    public class UserService
    {
        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("var user = User;"); // Controller uses base property
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.Name"); // Service uses accessor
        rewritten.Should().Contain("private readonly IHttpContextAccessor _httpContextAccessor;");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ClassesNeedingAccessor.Should().Contain("UserService");
        _rewriter.ClassesNeedingAccessor.Should().NotContain("HomeController");
    }

    [Fact]
    public void RewriteControllerBaseClass_RecognizesAsController()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ApiController : ControllerBase
    {
        public IActionResult Get()
        {
            var user = HttpContext.Current.User;
            return Ok();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("var user = User;");
        rewritten.Should().NotContain("HttpContext.Current");
        _rewriter.RequiresHttpContextAccessor.Should().BeFalse();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteNestedPropertyChain_PreservesChain()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public bool IsAuthenticated()
        {
            return HttpContext.Current.User.Identity.IsAuthenticated;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_httpContextAccessor.HttpContext?.User.Identity.IsAuthenticated");
        _rewriter.RequiresHttpContextAccessor.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteAddsRequiredUsings_WhenNeeded()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using Microsoft.AspNetCore.Http;");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Http");
    }

    [Fact]
    public void TrackDiagnostics_ForEachTransformation()
    {
        // Arrange
        var source = @"
namespace TestApp.Services
{
    public class UserService
    {
        public string GetCurrentUser()
        {
            return HttpContext.Current.User.Identity.Name;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Replaced HttpContext.Current"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Added IHttpContextAccessor"));
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
