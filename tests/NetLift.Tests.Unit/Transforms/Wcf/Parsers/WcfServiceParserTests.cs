using FluentAssertions;
using NetLift.Transforms.Wcf.Parsers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Parsers;

public sealed class WcfServiceParserTests
{
    private readonly WcfServiceParser _parser = new();

    [Fact]
    public void ParsesBasicServiceContract()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            namespace MyApp.Services
            {
                [ServiceContract]
                public interface IProductService
                {
                    [OperationContract]
                    Product GetProduct(int id);

                    [OperationContract]
                    void UpdateProduct(Product product);
                }
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.InterfaceName.Should().Be("IProductService");
        contract.FullyQualifiedName.Should().Be("MyApp.Services.IProductService");
        contract.Operations.Should().HaveCount(2);

        var getOp = contract.Operations[0];
        getOp.Name.Should().Be("GetProduct");
        getOp.ReturnType.Should().Be("Product");
        getOp.IsAsync.Should().BeFalse();
        getOp.IsOneWay.Should().BeFalse();
        getOp.Parameters.Should().HaveCount(1);
        getOp.Parameters[0].Name.Should().Be("id");
        getOp.Parameters[0].Type.Should().Be("int");

        var updateOp = contract.Operations[1];
        updateOp.Name.Should().Be("UpdateProduct");
        updateOp.ReturnType.Should().Be("void");
        updateOp.Parameters.Should().HaveCount(1);
        updateOp.Parameters[0].Name.Should().Be("product");
        updateOp.Parameters[0].Type.Should().Be("Product");
    }

    [Fact]
    public void ParsesServiceContractWithNamespaceAndName()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract(Namespace = "http://mycompany.com/services", Name = "ProductServiceContract")]
            public interface IProductService
            {
                [OperationContract]
                string GetProductName(int id);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Namespace.Should().Be("http://mycompany.com/services");
        contract.Name.Should().Be("ProductServiceContract");
    }

    [Fact]
    public void ParsesOperationContractWithIsOneWay()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface INotificationService
            {
                [OperationContract(IsOneWay = true)]
                void SendNotification(string message);

                [OperationContract(IsOneWay = false)]
                string GetStatus();
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(2);

        var sendOp = contract.Operations[0];
        sendOp.Name.Should().Be("SendNotification");
        sendOp.IsOneWay.Should().BeTrue();

        var getOp = contract.Operations[1];
        getOp.Name.Should().Be("GetStatus");
        getOp.IsOneWay.Should().BeFalse();
    }

    [Fact]
    public void ParsesAsyncOperations()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;
            using System.Threading.Tasks;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                Task<Product> GetProductAsync(int id);

                [OperationContract]
                Task UpdateProductAsync(Product product);

                [OperationContract]
                Product GetProductSync(int id);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(3);

        var getAsync = contract.Operations[0];
        getAsync.Name.Should().Be("GetProductAsync");
        getAsync.IsAsync.Should().BeTrue();
        getAsync.ReturnType.Should().Contain("Task");

        var updateAsync = contract.Operations[1];
        updateAsync.Name.Should().Be("UpdateProductAsync");
        updateAsync.IsAsync.Should().BeTrue();

        var getSync = contract.Operations[2];
        getSync.Name.Should().Be("GetProductSync");
        getSync.IsAsync.Should().BeFalse();
    }

    [Fact]
    public void ParsesComplexParameters()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;
            using System.Collections.Generic;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                List<Product> GetProducts(int[] categoryIds, string searchTerm);

                [OperationContract]
                void UpdateProducts(Product[] products);

                [OperationContract]
                Dictionary<int, string> GetProductNames(List<int> ids);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(3);

        var getOp = contract.Operations[0];
        getOp.Parameters.Should().HaveCount(2);
        getOp.Parameters[0].Name.Should().Be("categoryIds");
        getOp.Parameters[0].IsArray.Should().BeTrue();
        getOp.Parameters[1].Name.Should().Be("searchTerm");
        getOp.ReturnType.Should().Contain("List");

        var updateOp = contract.Operations[1];
        updateOp.Parameters.Should().HaveCount(1);
        updateOp.Parameters[0].IsArray.Should().BeTrue();

        var getNamesOp = contract.Operations[2];
        getNamesOp.Parameters.Should().HaveCount(1);
        getNamesOp.Parameters[0].IsGeneric.Should().BeTrue();
    }

    [Fact]
    public void ParsesOperationContractWithActionAndReplyAction()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract(Action = "http://mycompany.com/GetProduct",
                                   ReplyAction = "http://mycompany.com/GetProductResponse")]
                Product GetProduct(int id);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(1);

        var operation = contract.Operations[0];
        operation.Action.Should().Be("http://mycompany.com/GetProduct");
        operation.ReplyAction.Should().Be("http://mycompany.com/GetProductResponse");
    }

    [Fact]
    public void ParsesXmlDocumentation()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IProductService
            {
                /// <summary>
                /// Gets a product by its unique identifier.
                /// </summary>
                [OperationContract]
                Product GetProduct(int id);

                /// <summary>
                /// Updates an existing product in the database.
                /// </summary>
                [OperationContract]
                void UpdateProduct(Product product);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(2);

        var getOp = contract.Operations[0];
        getOp.XmlDocumentation.Should().NotBeNullOrEmpty();
        getOp.XmlDocumentation.Should().Contain("Gets a product");

        var updateOp = contract.Operations[1];
        updateOp.XmlDocumentation.Should().NotBeNullOrEmpty();
        updateOp.XmlDocumentation.Should().Contain("Updates an existing product");
    }

    [Fact]
    public void ParsesNullableParameters()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;
            using System;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                Product GetProduct(int? id, DateTime? lastModified);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(1);

        var operation = contract.Operations[0];
        operation.Parameters.Should().HaveCount(2);
        operation.Parameters[0].Name.Should().Be("id");
        operation.Parameters[0].IsNullable.Should().BeTrue();
        operation.Parameters[1].Name.Should().Be("lastModified");
        operation.Parameters[1].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void ParsesMultipleServiceContracts()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            namespace MyApp.Services
            {
                [ServiceContract]
                public interface IProductService
                {
                    [OperationContract]
                    Product GetProduct(int id);
                }

                [ServiceContract]
                public interface IOrderService
                {
                    [OperationContract]
                    Order GetOrder(int id);

                    [OperationContract]
                    void CreateOrder(Order order);
                }
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(2);
        contracts[0].InterfaceName.Should().Be("IProductService");
        contracts[0].Operations.Should().HaveCount(1);
        contracts[1].InterfaceName.Should().Be("IOrderService");
        contracts[1].Operations.Should().HaveCount(2);
    }

    [Fact]
    public void IgnoresInterfacesWithoutServiceContract()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            namespace MyApp.Services
            {
                [ServiceContract]
                public interface IProductService
                {
                    [OperationContract]
                    Product GetProduct(int id);
                }

                public interface IRepository
                {
                    Product GetProduct(int id);
                }
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        contracts[0].InterfaceName.Should().Be("IProductService");
    }

    [Fact]
    public void IgnoresMethodsWithoutOperationContract()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                Product GetProduct(int id);

                // This method should be ignored
                void HelperMethod(string value);
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(1);
        contract.Operations[0].Name.Should().Be("GetProduct");
    }

    [Fact]
    public void HandlesEmptyOrNullSourceCode()
    {
        // Act & Assert
        _parser.Parse("").Should().BeEmpty();
        _parser.Parse(null!).Should().BeEmpty();
        _parser.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void HandlesServiceContractWithoutOperations()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IEmptyService
            {
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.InterfaceName.Should().Be("IEmptyService");
        contract.Operations.Should().BeEmpty();
    }

    [Fact]
    public void ParsesServiceContractWithCallbackContract()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract(CallbackContract = "IProductServiceCallback")]
            public interface IProductService
            {
                [OperationContract]
                void SubscribeToUpdates();
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.CallbackContract.Should().Be("IProductServiceCallback");
    }

    [Fact]
    public void ParsesOperationWithNoParameters()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                int GetProductCount();

                [OperationContract]
                void ClearCache();
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(2);
        contract.Operations[0].Parameters.Should().BeEmpty();
        contract.Operations[1].Parameters.Should().BeEmpty();
    }

    [Fact]
    public void DiagnosticsAreEmptyForValidCode()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                Product GetProduct(int id);
            }
            """;

        // Act
        _parser.Parse(sourceCode);

        // Assert
        _parser.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ParsesGenericReturnTypes()
    {
        // Arrange
        var sourceCode = """
            using System.ServiceModel;
            using System.Collections.Generic;

            [ServiceContract]
            public interface IProductService
            {
                [OperationContract]
                List<Product> GetAllProducts();

                [OperationContract]
                Dictionary<int, Product> GetProductDictionary();
            }
            """;

        // Act
        var contracts = _parser.Parse(sourceCode);

        // Assert
        contracts.Should().HaveCount(1);
        var contract = contracts[0];
        contract.Operations.Should().HaveCount(2);
        contract.Operations[0].ReturnType.Should().Contain("List");
        contract.Operations[1].ReturnType.Should().Contain("Dictionary");
    }
}
