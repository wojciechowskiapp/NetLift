using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ControllerMethodBodyRewriterTests
{
    private readonly ControllerMethodBodyRewriter _rewriter = new();

    [Fact]
    public void Rewrite_TransformsStoreDBToContext()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StoreController : Controller
    {
        private StoreDB storeDB = new StoreDB();

        public ActionResult Index()
        {
            var albums = storeDB.Albums.ToList();
            return View(albums);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_context.Albums");
        rewritten.Should().NotContain("storeDB.Albums");
        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Transformed 'storeDB.' to '_context.'"));
    }

    [Fact]
    public void Rewrite_TransformsDbToContext()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class UsersController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public ActionResult Index()
        {
            var users = db.Users.ToList();
            return View(users);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_context.Users");
        rewritten.Should().NotContain("db.Users");
        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_TransformsTryUpdateModelToAsync()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Edit(int id, Product product)
        {
            if (ModelState.IsValid)
            {
                TryUpdateModel(product);
                db.SaveChanges();
                return RedirectToAction(""Index"");
            }
            return View(product);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("await TryUpdateModelAsync(product)");
        rewritten.Should().NotContain("TryUpdateModel(product)");
        rewritten.Should().Contain("async");
        rewritten.Should().Contain("Task<ActionResult>");
        rewritten.Should().Contain("using System.Threading.Tasks;");
        _rewriter.RequiredUsings.Should().Contain("System.Threading.Tasks");
        _rewriter.ConfidenceScore.Should().Be(90);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("TryUpdateModelAsync"));
    }

    [Fact]
    public void Rewrite_TransformsFormCollectionToIFormCollection()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class FormsController : Controller
    {
        public ActionResult Submit(FormCollection form)
        {
            var name = form[""name""];
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("IFormCollection form");
        rewritten.Should().NotContain("Submit(FormCollection"); // Should not have the old FormCollection parameter
        rewritten.Should().Contain("using Microsoft.AspNetCore.Http;");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Http");
        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("FormCollection") && d.Message.Contains("IFormCollection"));
    }

    [Fact]
    public void Rewrite_AddsTODOForMembershipCreateUser()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Security;

namespace TestApp.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Register(string username, string password, string email)
        {
            MembershipCreateStatus status;
            Membership.CreateUser(username, password, email, null, null, true, null, out status);
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("TODO");
        rewritten.Should().Contain("UserManager");
        rewritten.Should().Contain("Identity");
        _rewriter.ConfidenceScore.Should().Be(40);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Membership.CreateUser") &&
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_AddsTODOForMembershipValidateUser()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Security;

namespace TestApp.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login(string username, string password)
        {
            if (Membership.ValidateUser(username, password))
            {
                return RedirectToAction(""Index"", ""Home"");
            }
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("TODO");
        rewritten.Should().Contain("SignInManager");
        _rewriter.ConfidenceScore.Should().Be(40);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Membership.ValidateUser") &&
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_AddsTODOForFormsAuthenticationSetAuthCookie()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Security;

namespace TestApp.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login(string username, bool rememberMe)
        {
            FormsAuthentication.SetAuthCookie(username, rememberMe);
            return RedirectToAction(""Index"", ""Home"");
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("TODO");
        rewritten.Should().Contain("SignInManager");
        _rewriter.ConfidenceScore.Should().Be(40);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("FormsAuthentication.SetAuthCookie") &&
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_AddsTODOForMembershipCreateStatus()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Security;

namespace TestApp.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Register()
        {
            MembershipCreateStatus status;
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("TODO");
        rewritten.Should().Contain("IdentityResult");
        _rewriter.ConfidenceScore.Should().Be(40);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("MembershipCreateStatus") &&
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_HandlesMultipleTransformations()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class AlbumsController : Controller
    {
        private StoreDB storeDB = new StoreDB();

        public ActionResult Edit(int id, Album album, FormCollection form)
        {
            if (ModelState.IsValid)
            {
                var existing = storeDB.Albums.Find(id);
                TryUpdateModel(existing);
                storeDB.SaveChanges();
                return RedirectToAction(""Index"");
            }
            return View(album);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();

        // DbContext transformation
        rewritten.Should().Contain("_context.Albums");
        rewritten.Should().Contain("_context.SaveChanges");

        // TryUpdateModel transformation
        rewritten.Should().Contain("await TryUpdateModelAsync(existing)");
        rewritten.Should().Contain("async");
        rewritten.Should().Contain("Task<ActionResult>");

        // FormCollection transformation
        rewritten.Should().Contain("IFormCollection form");

        // Required usings
        rewritten.Should().Contain("using System.Threading.Tasks;");
        rewritten.Should().Contain("using Microsoft.AspNetCore.Http;");

        _rewriter.RequiredUsings.Should().Contain("System.Threading.Tasks");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Http");
        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void Rewrite_DoesNotTransformAlreadyAsyncMethod()
    {
        // Arrange
        var source = @"
using System.Threading.Tasks;
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public async Task<ActionResult> Edit(int id, Product product)
        {
            if (ModelState.IsValid)
            {
                await TryUpdateModelAsync(product);
                db.SaveChanges();
                return RedirectToAction(""Index"");
            }
            return View(product);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("async Task<ActionResult>");
        rewritten.Should().Contain("await TryUpdateModelAsync(product)");
        // Should not have duplicate async keywords or double-wrapped Task
        rewritten.Should().NotContain("async async");
        rewritten.Should().NotContain("Task<Task<");
    }

    [Fact]
    public void Rewrite_HandlesVoidReturnType()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class TestController : Controller
    {
        public void Update(Product product)
        {
            TryUpdateModel(product);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("async Task Update");
        rewritten.Should().Contain("await TryUpdateModelAsync(product)");
        rewritten.Should().NotContain("void Update");
    }

    [Fact]
    public void Rewrite_HandlesEmptySource()
    {
        // Arrange
        var source = "";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_HandlesNullSource()
    {
        // Arrange
        string? source = null;

        // Act
        var rewritten = _rewriter.Rewrite(source!);

        // Assert
        rewritten.Should().BeNull();
    }

    [Fact]
    public void Rewrite_PreservesTrivia()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class TestController : Controller
    {
        // This is a comment
        public ActionResult Edit(Product product)
        {
            // Update the model
            TryUpdateModel(product);
            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().Contain("// This is a comment");
        rewritten.Should().Contain("// Update the model");
    }

    [Fact]
    public void Rewrite_HandlesComplexAuthenticationScenario()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Security;

namespace TestApp.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Register(string username, string password, string email)
        {
            MembershipCreateStatus status;
            var user = Membership.CreateUser(username, password, email, null, null, true, null, out status);

            if (status == MembershipCreateStatus.Success)
            {
                FormsAuthentication.SetAuthCookie(username, false);
                return RedirectToAction(""Index"", ""Home"");
            }

            return View();
        }

        public ActionResult Login(string username, string password, bool rememberMe)
        {
            if (Membership.ValidateUser(username, password))
            {
                FormsAuthentication.SetAuthCookie(username, rememberMe);
                return RedirectToAction(""Index"", ""Home"");
            }

            return View();
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("TODO");

        // Should have warnings for all authentication patterns
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Membership.CreateUser"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("Membership.ValidateUser"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("FormsAuthentication.SetAuthCookie"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("MembershipCreateStatus"));

        // Confidence should be lowest of all transformations
        _rewriter.ConfidenceScore.Should().Be(40);
    }

    [Fact]
    public void Rewrite_ReplacesDbContextFieldWithDIInjection()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class StoreController : Controller
    {
        MusicStoreEntities storeDB = new MusicStoreEntities();

        public ActionResult Index()
        {
            var albums = storeDB.Albums.ToList();
            return View(albums);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();

        // Old field should be removed
        rewritten.Should().NotContain("MusicStoreEntities storeDB = new MusicStoreEntities()");

        // New DI pattern should be present
        rewritten.Should().Contain("private readonly MusicStoreEntities _context;");
        rewritten.Should().Contain("public StoreController(MusicStoreEntities context)");
        rewritten.Should().Contain("_context = context;");

        // Method body should use _context
        rewritten.Should().Contain("_context.Albums");

        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Removed direct DbContext instantiation"));
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("DbContext constructor injection"));
    }

    [Fact]
    public void Rewrite_HandlesNonStandardDbContextFieldNames()
    {
        // Arrange - test with unusual field names to verify generic detection
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        NorthwindEntities dataContext = new NorthwindEntities();

        public ActionResult Index()
        {
            var products = dataContext.Products.ToList();
            return View(products);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert - should work with any field name when type is detected
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_context.Products");
        rewritten.Should().NotContain("dataContext.Products");
        rewritten.Should().Contain("private readonly NorthwindEntities _context;");
        rewritten.Should().Contain("NorthwindEntities context");
    }

    [Fact]
    public void Rewrite_HandlesCustomDbContextTypeName()
    {
        // Arrange - test with custom type name ending in DbContext
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class OrdersController : Controller
    {
        MyAppDbContext database = new MyAppDbContext();

        public ActionResult Index()
        {
            var orders = database.Orders.ToList();
            return View(orders);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_context.Orders");
        rewritten.Should().NotContain("database.Orders");
        rewritten.Should().Contain("private readonly MyAppDbContext _context;");
    }

    [Fact]
    public void Rewrite_WithKnownDbContextTypes_DetectsNonStandardTypeName()
    {
        // Arrange - test with a type name that doesn't match any pattern
        // but IS in the known types set (simulating pre-analysis detection)
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class InventoryController : Controller
    {
        MyCustomDataStore storage = new MyCustomDataStore();

        public ActionResult Index()
        {
            var items = storage.Items.ToList();
            return View(items);
        }
    }
}";

        // Create known types set (as if detected by DbContextDetector)
        var knownTypes = new HashSet<string> { "MyCustomDataStore" };

        // Act - use the overload with known types
        var rewritten = _rewriter.Rewrite(source, knownTypes);

        // Assert - should detect even with non-standard naming because it's in known types
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("_context.Items");
        rewritten.Should().NotContain("storage.Items");
        rewritten.Should().Contain("private readonly MyCustomDataStore _context;");
        rewritten.Should().Contain("MyCustomDataStore context");
    }

    [Fact]
    public void Rewrite_WithoutKnownTypes_DoesNotDetectNonStandardTypeName()
    {
        // Arrange - same source but WITHOUT known types
        var source = @"
using System.Web.Mvc;

namespace TestApp.Controllers
{
    public class InventoryController : Controller
    {
        MyCustomDataStore storage = new MyCustomDataStore();

        public ActionResult Index()
        {
            var items = storage.Items.ToList();
            return View(items);
        }
    }
}";

        // Act - WITHOUT known types, pattern matching should NOT detect this
        var rewritten = _rewriter.Rewrite(source, null);

        // Assert - type name doesn't match patterns, so should NOT be transformed
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("storage.Items"); // NOT transformed
        rewritten.Should().NotContain("_context.Items");
    }

    [Fact]
    public void Rewrite_AddsDbContextToExistingConstructor()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using Microsoft.Extensions.Logging;

namespace TestApp.Controllers
{
    public class StoreController : Controller
    {
        private readonly ILogger<StoreController> _logger;
        MusicStoreEntities storeDB = new MusicStoreEntities();

        public StoreController(ILogger<StoreController> logger)
        {
            _logger = logger;
        }

        public ActionResult Index()
        {
            var albums = storeDB.Albums.ToList();
            return View(albums);
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();

        // Old field should be removed
        rewritten.Should().NotContain("MusicStoreEntities storeDB = new MusicStoreEntities()");

        // New field should be present
        rewritten.Should().Contain("private readonly MusicStoreEntities _context;");

        // Constructor should have both logger and context
        rewritten.Should().Contain("MusicStoreEntities context");
        rewritten.Should().Contain("ILogger<StoreController> logger");
        rewritten.Should().Contain("_context = context;");

        // Method body should use _context
        rewritten.Should().Contain("_context.Albums");
    }
}
