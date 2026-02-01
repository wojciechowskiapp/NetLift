using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Generators;

namespace NetLift.Tests.Unit.Wcf.Generators;

public class GrpcServiceGeneratorTests
{
    private readonly GrpcServiceGenerator _generator;

    public GrpcServiceGeneratorTests()
    {
        _generator = new GrpcServiceGenerator();
    }

    [Fact]
    public void Generate_WithNullServiceContract_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _generator.Generate(null!, "Company.Services.Grpc");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceContract");
    }

    [Fact]
    public void Generate_WithNullNamespace_ShouldThrowArgumentException()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService"
        };

        // Act
        var act = () => _generator.Generate(serviceContract, null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetNamespace");
    }

    [Fact]
    public void Generate_WithEmptyNamespace_ShouldThrowArgumentException()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService"
        };

        // Act
        var act = () => _generator.Generate(serviceContract, "   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetNamespace");
    }

    [Fact]
    public void Generate_SimpleService_ShouldGenerateBasicServiceClass()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>
            {
                new()
                {
                    Name = "GetCustomer",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    Parameters = new List<WcfParameter>
                    {
                        new()
                        {
                            Name = "customerId",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    }
                }
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.Should().NotBeNull();
        result.ClassName.Should().Be("CustomerServiceGrpc");
        result.Namespace.Should().Be("Company.Services.Grpc");
        result.BaseClassName.Should().Be("CustomerService.CustomerServiceBase");
        result.Methods.Should().HaveCount(1);
        result.Methods[0].Name.Should().Be("GetCustomer");
        result.Methods[0].RequestType.Should().Be("GetCustomerRequest");
        result.Methods[0].ResponseType.Should().Be("GetCustomerResponse");
    }

    [Fact]
    public void Generate_SimpleService_ShouldGenerateServiceCodeWithNamespace()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "Company.Services.IProductService",
            Name = "ProductService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("namespace Company.Services.Grpc;");
        result.ServiceCode.Should().Contain("public class ProductServiceGrpc : ProductService.ProductServiceBase");
    }

    [Fact]
    public void Generate_SimpleService_ShouldGenerateConstructorWithDependencies()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "Company.Services.IOrderService",
            Name = "OrderService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("private readonly ILogger<OrderServiceGrpc> _logger;");
        result.ServiceCode.Should().Contain("private readonly IOrderServiceBusinessService _businessService;");
        result.ServiceCode.Should().Contain("public OrderServiceGrpc(ILogger<OrderServiceGrpc> logger, IOrderServiceBusinessService businessService)");
        result.ServiceCode.Should().Contain("_logger = logger ?? throw new ArgumentNullException(nameof(logger));");
        result.ServiceCode.Should().Contain("_businessService = businessService ?? throw new ArgumentNullException(nameof(businessService));");
    }

    [Fact]
    public void Generate_WithOperation_ShouldGenerateAsyncMethodOverride()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>
            {
                new()
                {
                    Name = "GetCustomer",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    Parameters = new List<WcfParameter>()
                }
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("public override async Task<GetCustomerResponse> GetCustomer(GetCustomerRequest request, ServerCallContext context)");
        result.ServiceCode.Should().Contain("_logger.LogDebug(\"GetCustomer called with request: {@Request}\", request);");
        result.ServiceCode.Should().Contain("throw new RpcException(new Status(StatusCode.Unimplemented, \"Method not implemented yet\"));");
    }

    [Fact]
    public void Generate_WithXmlDocumentation_ShouldIncludeInMethodComment()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>
            {
                new()
                {
                    Name = "GetCustomer",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    XmlDocumentation = "Gets a customer by ID.",
                    Parameters = new List<WcfParameter>()
                }
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("/// Gets a customer by ID.");
        result.Methods[0].Documentation.Should().Be("Gets a customer by ID.");
    }

    [Fact]
    public void Generate_WithMultipleOperations_ShouldGenerateAllMethods()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>
            {
                new()
                {
                    Name = "GetCustomer",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    Parameters = new List<WcfParameter>()
                },
                new()
                {
                    Name = "CreateCustomer",
                    ReturnType = "Task<Customer>",
                    IsAsync = true,
                    Parameters = new List<WcfParameter>()
                },
                new()
                {
                    Name = "DeleteCustomer",
                    ReturnType = "Task",
                    IsAsync = true,
                    Parameters = new List<WcfParameter>()
                }
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.Methods.Should().HaveCount(3);
        result.Methods.Select(m => m.Name).Should().BeEquivalentTo("GetCustomer", "CreateCustomer", "DeleteCustomer");
        result.ServiceCode.Should().Contain("public override async Task<GetCustomerResponse> GetCustomer");
        result.ServiceCode.Should().Contain("public override async Task<CreateCustomerResponse> CreateCustomer");
        result.ServiceCode.Should().Contain("public override async Task<DeleteCustomerResponse> DeleteCustomer");
    }

    [Fact]
    public void Generate_WithOneWayOperation_ShouldGenerateWarningAndTodoComment()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "INotificationService",
            FullyQualifiedName = "Company.Services.INotificationService",
            Name = "NotificationService",
            Operations = new List<WcfOperation>
            {
                new()
                {
                    Name = "SendNotification",
                    ReturnType = "void",
                    IsOneWay = true,
                    Parameters = new List<WcfParameter>()
                }
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("// TODO: Original WCF operation was one-way. gRPC requires request-response pattern.");
        _generator.Diagnostics.Should().Contain(d => d.Contains("one-way") && d.Contains("SendNotification"));
        _generator.ConfidenceScore.Should().BeLessOrEqualTo(80);
    }

    [Fact]
    public void Generate_WithSessionRequired_ShouldLowerConfidenceScore()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ISessionService",
            FullyQualifiedName = "Company.Services.ISessionService",
            Name = "SessionService",
            SessionRequired = true,
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        _generator.Diagnostics.Should().Contain(d => d.Contains("sessions"));
        _generator.ConfidenceScore.Should().BeLessOrEqualTo(70);
    }

    [Fact]
    public void Generate_WithCallbackContract_ShouldLowerConfidenceScore()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IDuplexService",
            FullyQualifiedName = "Company.Services.IDuplexService",
            Name = "DuplexService",
            CallbackContract = "IDuplexCallback",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        _generator.Diagnostics.Should().Contain(d => d.Contains("duplex") && d.Contains("IDuplexCallback"));
        _generator.ConfidenceScore.Should().BeLessOrEqualTo(65);
    }

    [Fact]
    public void Generate_ShouldGenerateExtensionMethodsCode()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ExtensionsCode.Should().NotBeNullOrEmpty();
        result.ExtensionsCode.Should().Contain("namespace Company.Services.Grpc;");
        result.ExtensionsCode.Should().Contain("public static class CustomerServiceGrpcExtensions");
    }

    [Fact]
    public void Generate_ExtensionMethods_ShouldIncludeDIRegistration()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "Company.Services.IProductService",
            Name = "ProductService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ExtensionsCode.Should().Contain("public static IServiceCollection AddProductService(this IServiceCollection services)");
        result.ExtensionsCode.Should().Contain("services.AddGrpc();");
        result.ExtensionsCode.Should().Contain("// TODO: Register business service implementation");
    }

    [Fact]
    public void Generate_ExtensionMethods_ShouldIncludeEndpointMapping()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "Company.Services.IOrderService",
            Name = "OrderService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ExtensionsCode.Should().Contain("public static GrpcServiceEndpointConventionBuilder MapOrderService(this IEndpointRouteBuilder builder)");
        result.ExtensionsCode.Should().Contain("return builder.MapGrpcService<OrderServiceGrpc>();");
    }

    [Fact]
    public void Generate_ServiceWithoutNameAttribute_ShouldDeriveFromInterfaceName()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IInventoryService",
            FullyQualifiedName = "Company.Services.IInventoryService",
            Name = null, // No explicit Name attribute
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ClassName.Should().Be("InventoryServiceGrpc");
        result.BaseClassName.Should().Be("InventoryService.InventoryServiceBase");
    }

    [Fact]
    public void Generate_MultipleConsecutiveCalls_ShouldResetState()
    {
        // Arrange
        var serviceContract1 = new WcfServiceContract
        {
            InterfaceName = "IService1",
            FullyQualifiedName = "Company.IService1",
            SessionRequired = true,
            Operations = new List<WcfOperation>()
        };

        var serviceContract2 = new WcfServiceContract
        {
            InterfaceName = "IService2",
            FullyQualifiedName = "Company.IService2",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result1 = _generator.Generate(serviceContract1, "Company.Grpc");
        var score1 = _generator.ConfidenceScore;
        var diagnostics1 = _generator.Diagnostics.Count;

        var result2 = _generator.Generate(serviceContract2, "Company.Grpc");
        var score2 = _generator.ConfidenceScore;
        var diagnostics2 = _generator.Diagnostics.Count;

        // Assert
        score1.Should().BeLessOrEqualTo(70); // First service has session
        diagnostics1.Should().BeGreaterThan(0);

        score2.Should().Be(100); // Second service is simple
        diagnostics2.Should().Be(0); // Should be reset
    }

    [Fact]
    public void Generate_ShouldIncludeRequiredUsings()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Company.Services.ITestService",
            Name = "TestService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("using Grpc.Core;");
        result.ServiceCode.Should().Contain("using Microsoft.Extensions.Logging;");

        result.ExtensionsCode.Should().Contain("using Microsoft.AspNetCore.Builder;");
        result.ExtensionsCode.Should().Contain("using Microsoft.AspNetCore.Routing;");
        result.ExtensionsCode.Should().Contain("using Microsoft.Extensions.DependencyInjection;");
    }

    [Fact]
    public void Generate_ShouldIncludeXmlDocumentation()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Company.Services.ICustomerService",
            Name = "CustomerService",
            Operations = new List<WcfOperation>()
        };

        // Act
        var result = _generator.Generate(serviceContract, "Company.Services.Grpc");

        // Assert
        result.ServiceCode.Should().Contain("/// <summary>");
        result.ServiceCode.Should().Contain("/// gRPC service implementation for CustomerService.");
        result.ServiceCode.Should().Contain("/// Generated by NetLift from WCF service contract.");
        result.ServiceCode.Should().Contain("/// </summary>");

        result.ExtensionsCode.Should().Contain("/// <summary>");
        result.ExtensionsCode.Should().Contain("/// Extension methods for CustomerServiceGrpc service registration and endpoint mapping.");
        result.ExtensionsCode.Should().Contain("/// </summary>");
    }
}
