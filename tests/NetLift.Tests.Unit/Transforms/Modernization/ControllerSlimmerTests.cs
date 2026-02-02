using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Transformers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization;

public sealed class ControllerSlimmerTests
{
    private readonly ControllerSlimmer _transformer = new();

    [Fact]
    public async Task TransformsSimpleQueryActionToMediatR()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Details(int id)
        {
            var product = _service.GetProduct(id);
            if (product == null)
                return NotFound();
            return View(product);
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "ProductsController.cs",
                ClassName = "ProductsController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Details",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters =
                [
                    new ActionParameter
                    {
                        Name = "id",
                        Type = "int",
                        IsNullable = false,
                        HasDefaultValue = false
                    }
                ],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 95
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        result.TransformedSource.Should().Contain("using TestApp.Application.Common.Interfaces;");
        result.TransformedSource.Should().Contain("private readonly IMediator _mediator;");
        result.TransformedSource.Should().Contain("public ProductsController(IMediator mediator)");
        result.TransformedSource.Should().Contain("_mediator = mediator;");
        result.TransformedSource.Should().Contain("public async Task<IActionResult> Details([FromRoute] int id)");
        result.TransformedSource.Should().Contain("await _mediator.Send(new ProductsGetByIdQuery { Id = id })");
        result.TransformedSource.Should().Contain("result.IsSuccess ? View(result.Value) : NotFound()");
        result.RequiredUsings.Should().Contain("TestApp.Application.Common.Interfaces");
        result.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        result.TransformedActions.Should().Contain("Details");
        result.Confidence.Should().Be(95);
    }

    [Fact]
    public async Task TransformsCommandActionToMediatR()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Create(Product product)
        {
            _service.CreateProduct(product);
            return RedirectToAction(""Index"");
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "ProductsController.cs",
                ClassName = "ProductsController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Create",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = false,
                IsCommand = true,
                HttpMethods = ["POST"],
                Parameters =
                [
                    new ActionParameter
                    {
                        Name = "product",
                        Type = "Product",
                        IsNullable = false,
                        HasDefaultValue = false
                    }
                ],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Commands",
            GenerateQuery = false,
            GenerateCommand = true,
            Confidence = 90
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        result.TransformedSource.Should().Contain("using TestApp.Application.Common.Interfaces;");
        result.TransformedSource.Should().Contain("public async Task<IActionResult> Create([FromBody] Product product)");
        result.TransformedSource.Should().Contain("await _mediator.Send(new ProductsCreateCommand { Product = product })");
        result.TransformedSource.Should().Contain("CreatedAtAction");
        result.TransformedActions.Should().Contain("Create");
    }

    [Fact]
    public async Task HandlesNullableParametersCorrectly()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult Details(int? id)
        {
            if (id == null) return BadRequest();
            var order = _service.GetOrder(id.Value);
            return View(order);
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "OrdersController.cs",
                ClassName = "OrdersController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Details",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters =
                [
                    new ActionParameter
                    {
                        Name = "id",
                        Type = "int?",
                        IsNullable = true,
                        HasDefaultValue = false
                    }
                ],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 85
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        // Should convert int? to required int with FromRoute
        result.TransformedSource.Should().Contain("public async Task<IActionResult> Details([FromRoute] int id)");
        result.TransformedSource.Should().NotContain("int? id");
    }

    [Fact]
    public async Task AddsAsyncModifierToSyncAction()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var data = _service.GetData();
            return View(data);
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "HomeController.cs",
                ClassName = "HomeController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Index",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters = [],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 100
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        result.TransformedSource.Should().Contain("public async Task<IActionResult> Index()");
        result.TransformedSource.Should().NotContain("public ActionResult Index()");
    }

    [Fact]
    public async Task PreservesExistingMediatorField()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IMediator _mediator;

        public ProductsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "ProductsController.cs",
                ClassName = "ProductsController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Index",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters = [],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 100
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        // Should not add duplicate IMediator field or constructor parameter
        var mediatorFieldCount = System.Text.RegularExpressions.Regex.Matches(
            result.TransformedSource, @"private readonly IMediator _mediator;").Count;
        mediatorFieldCount.Should().Be(1);
    }

    [Fact]
    public async Task HandlesMultipleActions()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
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
            return RedirectToAction(""Index"");
        }
    }
}";

        var actionContexts = new[]
        {
            new ActionLogicContext
            {
                Controller = new ControllerInfo
                {
                    FilePath = "ProductsController.cs",
                    ClassName = "ProductsController",
                    Namespace = "TestApp.Controllers",
                    BaseClass = "Controller",
                    Confidence = 100
                },
                Action = new ActionInfo
                {
                    Name = "Index",
                    ReturnType = "ActionResult",
                    IsAsync = false,
                    IsQuery = true,
                    IsCommand = false,
                    HttpMethods = ["GET"],
                    Parameters = [],
                    Confidence = 100
                },
                TargetNamespace = "TestApp.Application.Queries",
                GenerateQuery = true,
                GenerateCommand = false,
                Confidence = 100
            },
            new ActionLogicContext
            {
                Controller = new ControllerInfo
                {
                    FilePath = "ProductsController.cs",
                    ClassName = "ProductsController",
                    Namespace = "TestApp.Controllers",
                    BaseClass = "Controller",
                    Confidence = 100
                },
                Action = new ActionInfo
                {
                    Name = "Details",
                    ReturnType = "ActionResult",
                    IsAsync = false,
                    IsQuery = true,
                    IsCommand = false,
                    HttpMethods = ["GET"],
                    Parameters =
                    [
                        new ActionParameter
                        {
                            Name = "id",
                            Type = "int",
                            IsNullable = false,
                            HasDefaultValue = false
                        }
                    ],
                    Confidence = 100
                },
                TargetNamespace = "TestApp.Application.Queries",
                GenerateQuery = true,
                GenerateCommand = false,
                Confidence = 100
            },
            new ActionLogicContext
            {
                Controller = new ControllerInfo
                {
                    FilePath = "ProductsController.cs",
                    ClassName = "ProductsController",
                    Namespace = "TestApp.Controllers",
                    BaseClass = "Controller",
                    Confidence = 100
                },
                Action = new ActionInfo
                {
                    Name = "Create",
                    ReturnType = "ActionResult",
                    IsAsync = false,
                    IsQuery = false,
                    IsCommand = true,
                    HttpMethods = ["POST"],
                    Parameters =
                    [
                        new ActionParameter
                        {
                            Name = "product",
                            Type = "Product",
                            IsNullable = false,
                            HasDefaultValue = false
                        }
                    ],
                    Confidence = 100
                },
                TargetNamespace = "TestApp.Application.Commands",
                GenerateQuery = false,
                GenerateCommand = true,
                Confidence = 100
            }
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, actionContexts);

        // Assert
        result.Should().NotBeNull();
        result.TransformedActions.Should().HaveCount(3);
        result.TransformedActions.Should().Contain("Index");
        result.TransformedActions.Should().Contain("Details");
        result.TransformedActions.Should().Contain("Create");
        result.TransformedSource.Should().Contain("await _mediator.Send(new ProductsGetListQuery())");
        result.TransformedSource.Should().Contain("await _mediator.Send(new ProductsGetByIdQuery { Id = id })");
        result.TransformedSource.Should().Contain("await _mediator.Send(new ProductsCreateCommand { Product = product })");
    }

    [Fact]
    public async Task TransformActionAsyncWorksIndependently()
    {
        // Arrange
        var actionSource = @"
public ActionResult Details(int id)
{
    var product = _service.GetProduct(id);
    if (product == null)
        return NotFound();
    return View(product);
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "ProductsController.cs",
                ClassName = "ProductsController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "Details",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters =
                [
                    new ActionParameter
                    {
                        Name = "id",
                        Type = "int",
                        IsNullable = false,
                        HasDefaultValue = false
                    }
                ],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 95
        };

        // Act
        var transformedAction = await _transformer.TransformActionAsync(actionSource, actionContext);

        // Assert
        transformedAction.Should().NotBeNullOrWhiteSpace();
        transformedAction.Should().Contain("public async Task<IActionResult> Details([FromRoute] int id)");
        transformedAction.Should().Contain("await _mediator.Send(new ProductsGetByIdQuery { Id = id })");
    }

    [Fact]
    public async Task ReturnsOriginalSourceWhenNoActionsToTransform()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

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

        // Act - empty action contexts list
        var result = await _transformer.TransformAsync(controllerSource, []);

        // Assert
        result.Should().NotBeNull();
        result.TransformedSource.Should().Be(controllerSource);
        result.TransformedActions.Should().BeEmpty();
        result.Confidence.Should().Be(100);
    }

    [Fact]
    public async Task AddsLowConfidenceWarning()
    {
        // Arrange
        var controllerSource = @"
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ComplexController : Controller
    {
        public ActionResult ComplexAction(int id)
        {
            // Complex logic
            return View();
        }
    }
}";

        var actionContext = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "ComplexController.cs",
                ClassName = "ComplexController",
                Namespace = "TestApp.Controllers",
                BaseClass = "Controller",
                Confidence = 100
            },
            Action = new ActionInfo
            {
                Name = "ComplexAction",
                ReturnType = "ActionResult",
                IsAsync = false,
                IsQuery = true,
                IsCommand = false,
                HttpMethods = ["GET"],
                Parameters =
                [
                    new ActionParameter
                    {
                        Name = "id",
                        Type = "int",
                        IsNullable = false,
                        HasDefaultValue = false
                    }
                ],
                Confidence = 100
            },
            TargetNamespace = "TestApp.Application.Queries",
            GenerateQuery = true,
            GenerateCommand = false,
            Confidence = 75 // Low confidence
        };

        // Act
        var result = await _transformer.TransformAsync(controllerSource, [actionContext]);

        // Assert
        result.Should().NotBeNull();
        result.Confidence.Should().Be(75);
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w =>
            w.ActionName == "ComplexController.ComplexAction(int id)" &&
            w.Message.Contains("75%") &&
            w.Severity == "Info");
    }
}
