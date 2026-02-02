using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace NetLift.Tests.Unit.Transforms.Modernization;

/// <summary>
/// Tests for method overload selection logic in ModernizationOrchestrator.
/// Uses reflection to test private static methods.
/// </summary>
public sealed class MethodOverloadSelectionTests
{
    private const string OrchestratorTypeName = "NetLift.Transforms.Modernization.ModernizationOrchestrator";

    #region Helper Methods

    private static MethodInfo GetPrivateMethod(string methodName)
    {
        var assembly = Assembly.Load("NetLift.Transforms");
        var type = assembly.GetType(OrchestratorTypeName);
        var method = type?.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"method {methodName} should exist");
        return method!;
    }

    private static string ExtractActionMethodBody(string controllerSource, string actionName, int parameterCount)
    {
        var method = GetPrivateMethod("ExtractActionMethodBody");
        var result = method.Invoke(null, new object[] { controllerSource, actionName, parameterCount });
        return result as string ?? string.Empty;
    }

    private static List<MethodDeclarationSyntax> ParseMethods(string source)
    {
        // Wrap in a class if not already wrapped
        if (!source.Contains("class "))
        {
            source = $@"
public class TestClass
{{
    {source}
}}";
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var root = syntaxTree.GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToList();
    }

    #endregion

    #region Single Method Tests

    [Fact]
    public void ExtractActionMethodBody_SingleMethod_ReturnsMethod()
    {
        // Arrange
        var controllerSource = @"
public class TestController : Controller
{
    public ActionResult Index()
    {
        var items = db.Items.ToList();
        return View(items);
    }
}";

        // Act
        var result = ExtractActionMethodBody(controllerSource, "Index", parameterCount: 0);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("Index()");
        result.Should().Contain("return View(items);");
    }

    [Fact]
    public void ExtractActionMethodBody_NoMatchingMethod_ReturnsEmpty()
    {
        // Arrange
        var controllerSource = @"
public class TestController : Controller
{
    public ActionResult Index() { return View(); }
}";

        // Act
        var result = ExtractActionMethodBody(controllerSource, "NonExistent", parameterCount: 0);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GET/POST Overload Tests

    [Fact]
    public void ExtractActionMethodBody_CreateOverloads_ZeroParamsSelectsGet()
    {
        // Arrange - typical Create pattern: GET returns form, POST processes submission
        var controllerSource = @"
public class CatalogController : Controller
{
    // GET: Catalog/Create
    public IActionResult Create()
    {
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), ""Id"", ""Brand"");
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CatalogItem catalogItem)
    {
        if (ModelState.IsValid)
        {
            service.CreateCatalogItem(catalogItem);
            return RedirectToAction(""Index"");
        }
        return View(catalogItem);
    }
}";

        // Act - 0 parameters should select the GET overload
        var result = ExtractActionMethodBody(controllerSource, "Create", parameterCount: 0);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("public IActionResult Create()");
        result.Should().Contain("new CatalogItem()");
        result.Should().NotContain("service.CreateCatalogItem");
    }

    [Fact]
    public void ExtractActionMethodBody_CreateOverloads_OneParamSelectsPost()
    {
        // Arrange - same Create pattern
        var controllerSource = @"
public class CatalogController : Controller
{
    // GET: Catalog/Create
    public IActionResult Create()
    {
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), ""Id"", ""Brand"");
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CatalogItem catalogItem)
    {
        if (ModelState.IsValid)
        {
            service.CreateCatalogItem(catalogItem);
            return RedirectToAction(""Index"");
        }
        return View(catalogItem);
    }
}";

        // Act - 1 parameter should select the POST overload
        var result = ExtractActionMethodBody(controllerSource, "Create", parameterCount: 1);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpPost]");
        result.Should().Contain("CatalogItem catalogItem");
        result.Should().Contain("service.CreateCatalogItem");
    }

    [Fact]
    public void ExtractActionMethodBody_EditOverloads_QuerySelectsGet()
    {
        // Arrange - typical Edit pattern
        var controllerSource = @"
public class CatalogController : Controller
{
    [HttpGet]
    public IActionResult Edit(int? id)
    {
        if (id == null) return BadRequest();
        var item = service.FindCatalogItem(id.Value);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CatalogItem catalogItem)
    {
        if (ModelState.IsValid)
        {
            service.UpdateCatalogItem(catalogItem);
            return RedirectToAction(""Index"");
        }
        return View(catalogItem);
    }
}";

        // Act
        var result = ExtractActionMethodBody(controllerSource, "Edit", parameterCount: 1);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpGet]");
        result.Should().Contain("int? id");
        result.Should().Contain("FindCatalogItem");
        result.Should().NotContain("UpdateCatalogItem");
    }

    [Fact]
    public void ExtractActionMethodBody_EditOverloads_BothHaveOneParam_SelectsFirstMatch()
    {
        // Arrange - both overloads have 1 parameter - can't differentiate by count alone
        // This test verifies the method returns the first match when parameter counts are equal
        var controllerSource = @"
public class CatalogController : Controller
{
    [HttpGet]
    public IActionResult Edit(int? id)
    {
        if (id == null) return BadRequest();
        var item = service.FindCatalogItem(id.Value);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CatalogItem catalogItem)
    {
        if (ModelState.IsValid)
        {
            service.UpdateCatalogItem(catalogItem);
            return RedirectToAction(""Index"");
        }
        return View(catalogItem);
    }
}";

        // Act - both methods have 1 parameter, so we get the first one
        var result = ExtractActionMethodBody(controllerSource, "Edit", parameterCount: 1);

        // Assert - should get the first method (GET)
        result.Should().NotBeEmpty();
        result.Should().Contain("Edit(int? id)"); // First match
    }

    #endregion

    #region AddressAndPayment Pattern Tests (Original Issue)

    [Fact]
    public void ExtractActionMethodBody_AddressAndPaymentOverloads_QuerySelectsEmptyGet()
    {
        // Arrange - the problematic pattern from the original issue
        var controllerSource = @"
public class CheckoutController : Controller
{
    // GET: Empty method, just returns view
    public ActionResult AddressAndPayment()
    {
        return View();
    }

    // POST: Has actual business logic
    [HttpPost]
    public ActionResult AddressAndPayment(IFormCollection values)
    {
        var order = new Order();
        order.Username = User.Identity.Name;
        order.OrderDate = DateTime.Now;

        storeDB.Orders.Add(order);
        storeDB.SaveChanges();

        return RedirectToAction(""Complete"", new { id = order.OrderId });
    }
}";

        // Act - 0 parameters should select the GET (empty one)
        var result = ExtractActionMethodBody(controllerSource, "AddressAndPayment", parameterCount: 0);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("public ActionResult AddressAndPayment()");
        result.Should().Contain("return View();");
        result.Should().NotContain("IFormCollection");
        result.Should().NotContain("storeDB.Orders.Add");
    }

    [Fact]
    public void ExtractActionMethodBody_AddressAndPaymentOverloads_CommandSelectsPost()
    {
        // Arrange - same pattern
        var controllerSource = @"
public class CheckoutController : Controller
{
    // GET: Empty method, just returns view
    public ActionResult AddressAndPayment()
    {
        return View();
    }

    // POST: Has actual business logic
    [HttpPost]
    public ActionResult AddressAndPayment(IFormCollection values)
    {
        var order = new Order();
        order.Username = User.Identity.Name;
        order.OrderDate = DateTime.Now;

        storeDB.Orders.Add(order);
        storeDB.SaveChanges();

        return RedirectToAction(""Complete"", new { id = order.OrderId });
    }
}";

        // Act - 1 parameter should select the POST (with business logic)
        var result = ExtractActionMethodBody(controllerSource, "AddressAndPayment", parameterCount: 1);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpPost]");
        result.Should().Contain("IFormCollection values");
        result.Should().Contain("storeDB.Orders.Add");
        result.Should().Contain("storeDB.SaveChanges");
    }

    #endregion

    #region Delete Pattern Tests

    [Fact]
    public void ExtractActionMethodBody_DeleteOverloads_QuerySelectsGet()
    {
        // Arrange - Delete typically has GET (confirmation page) and POST (actual delete)
        var controllerSource = @"
public class CatalogController : Controller
{
    [HttpGet]
    public IActionResult Delete(int? id)
    {
        if (id == null) return BadRequest();
        var item = service.FindCatalogItem(id.Value);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName(""Delete"")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var item = service.FindCatalogItem(id);
        service.RemoveCatalogItem(item);
        return RedirectToAction(""Index"");
    }
}";

        // Act
        var result = ExtractActionMethodBody(controllerSource, "Delete", parameterCount: 1);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpGet]");
        result.Should().Contain("int? id");
        result.Should().Contain("FindCatalogItem");
        result.Should().NotContain("RemoveCatalogItem");
    }

    [Fact]
    public void ExtractActionMethodBody_DeleteWithHttpDelete_BothHaveOneParam_SelectsFirstMatch()
    {
        // Arrange - both have 1 parameter, can't differentiate by count
        var controllerSource = @"
public class CatalogController : Controller
{
    [HttpGet]
    public IActionResult Delete(int? id)
    {
        var item = service.FindCatalogItem(id.Value);
        return View(item);
    }

    [HttpDelete]
    public IActionResult Delete(int id)
    {
        service.RemoveCatalogItem(id);
        return Ok();
    }
}";

        // Act - both have 1 parameter, returns first match
        var result = ExtractActionMethodBody(controllerSource, "Delete", parameterCount: 1);

        // Assert - should get first method (GET)
        result.Should().NotBeEmpty();
        result.Should().Contain("Delete(int? id)");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ExtractActionMethodBody_ThreeOverloads_SelectsByParameterCount()
    {
        // Arrange - unusual case with 3 overloads with different parameter counts
        var controllerSource = @"
public class TestController : Controller
{
    public IActionResult Process()
    {
        return View();
    }

    public IActionResult Process(int id)
    {
        var item = db.Items.Find(id);
        return View(item);
    }

    [HttpPost]
    public IActionResult Process(int id, Model model)
    {
        db.Models.Add(model);
        db.SaveChanges();
        return RedirectToAction(""Index"");
    }
}";

        // Act - 2 parameters should select the POST with 2 params
        var result = ExtractActionMethodBody(controllerSource, "Process", parameterCount: 2);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpPost]");
        result.Should().Contain("int id, Model model");
        result.Should().Contain("SaveChanges");
    }

    [Fact]
    public void ExtractActionMethodBody_HttpPut_SelectsByParameterCount()
    {
        // Arrange
        var controllerSource = @"
public class ApiController : Controller
{
    [HttpGet]
    public IActionResult Update(int id)
    {
        var item = db.Items.Find(id);
        return View(item);
    }

    [HttpPut]
    public IActionResult Update(int id, Model model)
    {
        db.Models.Update(model);
        db.SaveChanges();
        return Ok();
    }
}";

        // Act - 2 parameters for PUT
        var result = ExtractActionMethodBody(controllerSource, "Update", parameterCount: 2);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpPut]");
        result.Should().Contain("int id, Model model");
        result.Should().Contain("SaveChanges");
    }

    [Fact]
    public void ExtractActionMethodBody_HttpPatch_SelectsByParameterCount()
    {
        // Arrange
        var controllerSource = @"
public class ApiController : Controller
{
    [HttpGet]
    public IActionResult Patch(int id)
    {
        return Ok();
    }

    [HttpPatch]
    public IActionResult Patch(int id, PatchModel model)
    {
        db.ApplyPatch(model);
        return Ok();
    }
}";

        // Act - 2 parameters for PATCH
        var result = ExtractActionMethodBody(controllerSource, "Patch", parameterCount: 2);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[HttpPatch]");
        result.Should().Contain("int id, PatchModel model");
    }

    #endregion
}
