using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Generators;

/// <summary>
/// Tests for the RestControllerGenerator class.
/// </summary>
public sealed class RestControllerGeneratorTests
{
    private readonly RestControllerGenerator _generator = new();

    [Fact]
    public void Generate_BasicGetOperation_GeneratesControllerWithHttpGet()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "Customer",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "customerId",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.Should().NotBeNull();
        result.ClassName.Should().Be("CustomerController");
        result.Namespace.Should().Be("MyCompany.Api.Controllers");
        result.RoutePrefix.Should().Be("api/customers");
        result.Actions.Should().HaveCount(1);

        var action = result.Actions[0];
        action.Name.Should().Be("GetCustomer");
        action.HttpMethod.Should().Be("GET");
        action.Route.Should().Be("{customerId:int}");
        action.ReturnType.Should().Be("Customer");

        result.ControllerCode.Should().Contain("namespace MyCompany.Api.Controllers;");
        result.ControllerCode.Should().Contain("[ApiController]");
        result.ControllerCode.Should().Contain("[Route(\"api/customers\")]");
        result.ControllerCode.Should().Contain("[Produces(\"application/json\")]");
        result.ControllerCode.Should().Contain("public class CustomerController : ControllerBase");
        result.ControllerCode.Should().Contain("[HttpGet(\"{customerId:int}\")]");
        result.ControllerCode.Should().Contain("public async Task<ActionResult<Customer>> GetCustomer([FromRoute] int customerId)");
        result.ControllerCode.Should().Contain("[ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]");
        result.ControllerCode.Should().Contain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]");

        _generator.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Generate_PostOperation_GeneratesControllerWithHttpPost()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "CreateCustomer",
                    ReturnType = "Customer",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "customer",
                            Type = "CustomerDto",
                            FullTypeName = "MyCompany.Models.CustomerDto"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.HttpMethod.Should().Be("POST");
        action.Route.Should().BeEmpty();
        action.Parameters.Should().HaveCount(1);
        action.Parameters[0].Source.Should().Be("FromBody");

        result.ControllerCode.Should().Contain("[HttpPost]");
        result.ControllerCode.Should().Contain("public async Task<ActionResult<Customer>> CreateCustomer([FromBody] CustomerDto customer)");
    }

    [Fact]
    public void Generate_PutOperation_GeneratesControllerWithHttpPut()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "UpdateCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "customerId",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        },
                        new WcfParameter
                        {
                            Name = "customer",
                            Type = "CustomerDto",
                            FullTypeName = "MyCompany.Models.CustomerDto"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.HttpMethod.Should().Be("PUT");
        action.Route.Should().Be("{customerId:int}");
        action.ReturnType.Should().Be("void");
        action.Parameters.Should().HaveCount(2);
        action.Parameters[0].Source.Should().Be("FromRoute");
        action.Parameters[1].Source.Should().Be("FromBody");

        result.ControllerCode.Should().Contain("[HttpPut(\"{customerId:int}\")]");
        result.ControllerCode.Should().Contain("public async Task<IActionResult> UpdateCustomer([FromRoute] int customerId, [FromBody] CustomerDto customer)");
    }

    [Fact]
    public void Generate_DeleteOperation_GeneratesControllerWithHttpDelete()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "DeleteCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "customerId",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.HttpMethod.Should().Be("DELETE");
        action.Route.Should().Be("{customerId:int}");

        result.ControllerCode.Should().Contain("[HttpDelete(\"{customerId:int}\")]");
    }

    [Fact]
    public void Generate_SearchOperation_GeneratesPostWithSearchRoute()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "SearchCustomers",
                    ReturnType = "List<Customer>",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "criteria",
                            Type = "SearchCriteria",
                            FullTypeName = "MyCompany.Models.SearchCriteria"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.HttpMethod.Should().Be("POST");
        action.Route.Should().Be("search");
        action.Parameters[0].Source.Should().Be("FromBody");

        result.ControllerCode.Should().Contain("[HttpPost(\"search\")]");
    }

    [Fact]
    public void Generate_MultipleOperations_GeneratesAllActions()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "Customer",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                },
                new WcfOperation
                {
                    Name = "CreateCustomer",
                    ReturnType = "Customer",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "customer",
                            Type = "CustomerDto",
                            FullTypeName = "MyCompany.Models.CustomerDto"
                        }
                    ]
                },
                new WcfOperation
                {
                    Name = "DeleteCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.Actions.Should().HaveCount(3);
        result.Actions[0].HttpMethod.Should().Be("GET");
        result.Actions[1].HttpMethod.Should().Be("POST");
        result.Actions[2].HttpMethod.Should().Be("DELETE");
    }

    [Fact]
    public void Generate_AsyncOperation_StripTaskWrapper()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomerAsync",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.ReturnType.Should().Be("Customer");
        result.ControllerCode.Should().Contain("Task<ActionResult<Customer>>");
    }

    [Fact]
    public void Generate_GuidIdParameter_UsesGuidConstraint()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "MyCompany.Services.IOrderService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetOrder",
                    ReturnType = "Order",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "orderId",
                            Type = "Guid",
                            FullTypeName = "System.Guid"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.Route.Should().Be("{orderId:guid}");
        result.ControllerCode.Should().Contain("[HttpGet(\"{orderId:guid}\")]");
    }

    [Fact]
    public void Generate_LongIdParameter_UsesLongConstraint()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "MyCompany.Services.IProductService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetProduct",
                    ReturnType = "Product",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "productId",
                            Type = "long",
                            FullTypeName = "System.Int64"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.Route.Should().Be("{productId:long}");
    }

    [Fact]
    public void Generate_GetWithMultipleParameters_UsesQueryParameters()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "MyCompany.Services.IProductService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "FindProducts",
                    ReturnType = "List<Product>",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "category",
                            Type = "string",
                            FullTypeName = "System.String"
                        },
                        new WcfParameter
                        {
                            Name = "minPrice",
                            Type = "decimal",
                            FullTypeName = "System.Decimal"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.Route.Should().BeEmpty();
        action.Parameters.Should().AllSatisfy(p => p.Source.Should().Be("FromQuery"));

        result.ControllerCode.Should().Contain("[FromQuery] string category");
        result.ControllerCode.Should().Contain("[FromQuery] decimal minPrice");
    }

    [Fact]
    public void Generate_XmlDocumentation_IncludesInController()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "Customer",
                    XmlDocumentation = "Retrieves a customer by ID.",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.ControllerCode.Should().Contain("/// Retrieves a customer by ID.");
    }

    [Fact]
    public void Generate_ServiceWithoutServiceSuffix_AddsControllerSuffix()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomer",
            FullyQualifiedName = "MyCompany.Services.ICustomer",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.ClassName.Should().Be("CustomerController");
    }

    [Fact]
    public void Generate_UnknownOperationName_DefaultsToPostAndLowersConfidence()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "ProcessCustomer", // Unknown pattern
                    ReturnType = "void",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        var action = result.Actions[0];
        action.HttpMethod.Should().Be("POST");
        _generator.ConfidenceScore.Should().BeLessThan(100);
        _generator.Diagnostics.Should().Contain(d => d.Contains("does not follow standard naming conventions"));
    }

    [Fact]
    public void Generate_IncludesLoggerAndDIConstructor()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.ControllerCode.Should().Contain("private readonly ILogger<CustomerController> _logger;");
        result.ControllerCode.Should().Contain("public CustomerController(ILogger<CustomerController> logger)");
        result.ControllerCode.Should().Contain("_logger = logger ?? throw new ArgumentNullException(nameof(logger));");
        result.ControllerCode.Should().Contain("// TODO: Inject business services via DI");
    }

    [Fact]
    public void Generate_IncludesTODOComments()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "Customer",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.ControllerCode.Should().Contain("// TODO: Implement business logic");
        result.ControllerCode.Should().Contain("// TODO: Call appropriate service layer");
    }

    [Fact]
    public void Generate_VoidReturnType_GeneratesIActionResult()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "DeleteCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "id",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyCompany.Api.Controllers");

        // Assert
        result.ControllerCode.Should().Contain("public async Task<IActionResult> DeleteCustomer");
        result.ControllerCode.Should().NotContain("ActionResult<void>");
    }

    [Fact]
    public void Generate_NullServiceContract_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.Generate(null!, "MyCompany.Api.Controllers");
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceContract");
    }

    [Fact]
    public void Generate_NullTargetNamespace_ThrowsArgumentException()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyCompany.Services.ICustomerService",
            Operations = []
        };

        // Act & Assert
        var act = () => _generator.Generate(serviceContract, null!);
        act.Should().Throw<ArgumentException>().WithParameterName("targetNamespace");
    }
}
