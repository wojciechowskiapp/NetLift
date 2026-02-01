using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Generators;

public sealed class ClientProxyGeneratorTests
{
    private readonly ClientProxyGenerator _generator = new();

    [Fact]
    public void Generate_ThrowsArgumentNullException_WhenServiceContractIsNull()
    {
        // Act
        var act = () => _generator.Generate(null!, "Test.Namespace");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceContract");
    }

    [Fact]
    public void Generate_ThrowsArgumentException_WhenTargetNamespaceIsNullOrWhitespace()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Operations = []
        };

        // Act
        var act1 = () => _generator.Generate(serviceContract, null!);
        var act2 = () => _generator.Generate(serviceContract, "");
        var act3 = () => _generator.Generate(serviceContract, "   ");

        // Assert
        act1.Should().Throw<ArgumentException>().WithParameterName("targetNamespace");
        act2.Should().Throw<ArgumentException>().WithParameterName("targetNamespace");
        act3.Should().Throw<ArgumentException>().WithParameterName("targetNamespace");
    }

    [Fact]
    public void Generate_CreatesClientInterface_WithCorrectName()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Name = "CustomerService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.InterfaceName.Should().Be("ICustomerServiceClient");
        result.Namespace.Should().Be("MyApp.Clients");
        result.InterfaceCode.Should().Contain("public interface ICustomerServiceClient");
        result.InterfaceCode.Should().Contain("namespace MyApp.Clients;");
    }

    [Fact]
    public void Generate_CreatesAsyncMethods_InClientInterface()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "MyApp.Services.IProductService",
            Name = "ProductService",
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
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.InterfaceCode.Should().Contain("Task<Product> GetProductAsync(int productId, CancellationToken cancellationToken = default);");
    }

    [Fact]
    public void Generate_CreatesGrpcClient_WithCorrectImplementation()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "MyApp.Services.IOrderService",
            Name = "OrderService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "CreateOrder",
                    ReturnType = "Order",
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
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.GrpcClientCode.Should().Contain("public sealed class OrderServiceGrpcClient : IOrderServiceClient");
        result.GrpcClientCode.Should().Contain("private readonly OrderService.OrderServiceClient _grpcClient;");
        result.GrpcClientCode.Should().Contain("private readonly ILogger<OrderServiceGrpcClient> _logger;");
        result.GrpcClientCode.Should().Contain("public async Task<Order> CreateOrderAsync(int customerId, CancellationToken cancellationToken = default)");
        result.GrpcClientCode.Should().Contain("var request = new CreateOrderRequest");
        result.GrpcClientCode.Should().Contain("catch (RpcException ex)");
        result.GrpcClientCode.Should().Contain("throw MapGrpcException(ex);");
    }

    [Fact]
    public void Generate_CreatesHttpClient_WithCorrectImplementation()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Name = "CustomerService",
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
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.HttpClientCode.Should().Contain("public sealed class CustomerServiceHttpClient : ICustomerServiceClient");
        result.HttpClientCode.Should().Contain("private readonly HttpClient _httpClient;");
        result.HttpClientCode.Should().Contain("private readonly ILogger<CustomerServiceHttpClient> _logger;");
        result.HttpClientCode.Should().Contain("public async Task<Customer> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default)");
        result.HttpClientCode.Should().Contain("var response = await _httpClient.GetAsync");
        result.HttpClientCode.Should().Contain("response.EnsureSuccessStatusCode();");
        result.HttpClientCode.Should().Contain("ReadFromJsonAsync<Customer>");
    }

    [Fact]
    public void Generate_UsesPostMethod_ForNonQueryOperations()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "MyApp.Services.IOrderService",
            Name = "OrderService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "CreateOrder",
                    ReturnType = "Order",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "UpdateOrder",
                    ReturnType = "void",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "DeleteOrder",
                    ReturnType = "void",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.HttpClientCode.Should().Contain("PostAsJsonAsync");
        result.HttpClientCode.Should().NotContain("var response = await _httpClient.GetAsync",
            "non-query operations should use POST");
    }

    [Fact]
    public void Generate_UsesGetMethod_ForQueryOperations()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "MyApp.Services.IProductService",
            Name = "ProductService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetProduct",
                    ReturnType = "Product",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "FindProducts",
                    ReturnType = "List<Product>",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "SearchProducts",
                    ReturnType = "Product[]",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "ListProducts",
                    ReturnType = "IEnumerable<Product>",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.HttpClientCode.Should().Contain("var response = await _httpClient.GetAsync");
        result.HttpClientCode.Should().NotContain("PostAsJsonAsync",
            "query operations should use GET");
    }

    [Fact]
    public void Generate_CreatesDIExtensions_WithGrpcAndHttpRegistration()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IPaymentService",
            FullyQualifiedName = "MyApp.Services.IPaymentService",
            Name = "PaymentService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "MyApp.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("public static class PaymentServiceClientExtensions");
        result.ExtensionsCode.Should().Contain("public static IServiceCollection AddPaymentServiceGrpcClient");
        result.ExtensionsCode.Should().Contain("public static IServiceCollection AddPaymentServiceHttpClient");
        result.ExtensionsCode.Should().Contain("AddGrpcClient<PaymentService.PaymentServiceClient>");
        result.ExtensionsCode.Should().Contain("AddHttpClient<IPaymentServiceClient, PaymentServiceHttpClient>");
        result.ExtensionsCode.Should().Contain("services.AddSingleton<IPaymentServiceClient, PaymentServiceGrpcClient>");
    }

    [Fact]
    public void Generate_IncludesPollyPolicies_InExtensions()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("using Polly;");
        result.ExtensionsCode.Should().Contain("using Polly.Extensions.Http;");
        result.ExtensionsCode.Should().Contain("AddPolicyHandler(GetRetryPolicy())");
        result.ExtensionsCode.Should().Contain("AddPolicyHandler(GetCircuitBreakerPolicy())");
        result.ExtensionsCode.Should().Contain("private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()");
        result.ExtensionsCode.Should().Contain("private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()");
        result.ExtensionsCode.Should().Contain("WaitAndRetryAsync");
        result.ExtensionsCode.Should().Contain("CircuitBreakerAsync");
    }

    [Fact]
    public void Generate_IncludesExceptionMapping_ForGrpc()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("private static Exception MapGrpcException(RpcException ex)");
        result.ExtensionsCode.Should().Contain("StatusCode.NotFound");
        result.ExtensionsCode.Should().Contain("StatusCode.InvalidArgument");
        result.ExtensionsCode.Should().Contain("StatusCode.PermissionDenied");
        result.ExtensionsCode.Should().Contain("StatusCode.Unauthenticated");
        result.ExtensionsCode.Should().Contain("StatusCode.Unavailable");
        result.ExtensionsCode.Should().Contain("StatusCode.DeadlineExceeded");
    }

    [Fact]
    public void Generate_MapsVoidReturnType_ToBool()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "DoSomething",
                    ReturnType = "void",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.InterfaceCode.Should().Contain("Task<bool> DoSomethingAsync");
    }

    [Fact]
    public void Generate_HandlesMultipleParameters_InMethods()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "Test.IOrderService",
            Name = "OrderService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "CreateOrder",
                    ReturnType = "Order",
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
                            Name = "productId",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        },
                        new WcfParameter
                        {
                            Name = "quantity",
                            Type = "int",
                            FullTypeName = "System.Int32"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.InterfaceCode.Should().Contain("Task<Order> CreateOrderAsync(int customerId, int productId, int quantity, CancellationToken cancellationToken = default);");
    }

    [Fact]
    public void Generate_IncludesXmlDocumentation_WhenAvailable()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetData",
                    ReturnType = "string",
                    XmlDocumentation = "Retrieves data from the service.",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.InterfaceCode.Should().Contain("/// Retrieves data from the service.");
    }

    [Fact]
    public void Generate_Returns100ConfidenceScore_ForBasicService()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetData",
                    ReturnType = "string",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        _generator.ConfidenceScore.Should().Be(100);
        _generator.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ReducesConfidenceScore_ForSessionRequiredService()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            SessionRequired = true,
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        _generator.ConfidenceScore.Should().BeLessThan(100);
        _generator.Diagnostics.Should().Contain(d => d.Contains("sessions"));
    }

    [Fact]
    public void Generate_ReducesConfidenceScore_ForDuplexService()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            CallbackContract = "ITestCallback",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        _generator.ConfidenceScore.Should().BeLessThan(100);
        _generator.Diagnostics.Should().Contain(d => d.Contains("duplex"));
    }

    [Fact]
    public void Generate_ReducesConfidenceScore_ForOneWayOperations()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "FireAndForget",
                    ReturnType = "void",
                    IsOneWay = true,
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        _generator.ConfidenceScore.Should().BeLessThan(100);
        _generator.Diagnostics.Should().Contain(d => d.Contains("one-way"));
    }

    [Fact]
    public void Generate_IncludesConfigurableAddresses_InExtensions()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("string grpcAddress = \"https://localhost:5001\"");
        result.ExtensionsCode.Should().Contain("string httpBaseAddress = \"https://localhost:5000\"");
        result.ExtensionsCode.Should().Contain("options.Address = new Uri(grpcAddress)");
        result.ExtensionsCode.Should().Contain("client.BaseAddress = new Uri(httpBaseAddress)");
    }

    [Fact]
    public void Generate_IncludesTimeout_InHttpClientConfiguration()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("client.Timeout = TimeSpan.FromSeconds(30)");
    }

    [Fact]
    public void Generate_IncludesRetryConfiguration_With3AttemptsAndExponentialBackoff()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("retryCount: 3");
        result.ExtensionsCode.Should().Contain("TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))");
    }

    [Fact]
    public void Generate_IncludesCircuitBreakerConfiguration()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations = []
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        result.ExtensionsCode.Should().Contain("handledEventsAllowedBeforeBreaking: 5");
        result.ExtensionsCode.Should().Contain("durationOfBreak: TimeSpan.FromSeconds(30)");
    }

    [Fact]
    public void Generate_HandlesAsyncWcfOperations_ByRemovingTaskWrapper()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Name = "TestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetDataAsync",
                    ReturnType = "Task<string>",
                    IsAsync = true,
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, "Test.Clients");

        // Assert
        // Should map Task<string> to just string in the client interface (which then wraps in Task again)
        result.InterfaceCode.Should().Contain("Task<string> GetDataAsyncAsync");
    }
}
