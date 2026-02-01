// Demo: AttributeRoutingTransformer Usage
// Shows transformation from convention-based routing to attribute routing

using NetLift.Transforms.Mvc.Rewriters;

var transformer = new AttributeRoutingTransformer();

var source = @"
using System.Web.Mvc;

namespace MyApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details(int id)
        {
            return View();
        }

        public ActionResult Create(Product product)
        {
            // Save product
            return RedirectToAction(""Index"");
        }

        public ActionResult Delete(int id)
        {
            // Delete product
            return RedirectToAction(""Index"");
        }

        public ActionResult GetByCategory(string category)
        {
            return View();
        }
    }
}";

var rewritten = transformer.Rewrite(source);

Console.WriteLine("=== TRANSFORMED CODE ===");
Console.WriteLine(rewritten);
Console.WriteLine();
Console.WriteLine("=== DIAGNOSTICS ===");
foreach (var diagnostic in transformer.Diagnostics)
{
    Console.WriteLine($"[{diagnostic.Severity}] {diagnostic.Message}");
}
Console.WriteLine();
Console.WriteLine($"Confidence Score: {transformer.ConfidenceScore}%");
Console.WriteLine($"Required Usings: {string.Join(", ", transformer.RequiredUsings)}");

// Expected output:
// - [Route("[controller]")] added to ProductsController
// - Index() gets [HttpGet]
// - Details(int id) gets [HttpGet("{id:int}")]
// - Create() gets [HttpPost]
// - Delete(int id) gets [HttpDelete("{id:int}")]
// - GetByCategory() gets [HttpGet] (inferred from name)
// - using Microsoft.AspNetCore.Mvc added
