using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Generators;

/// <summary>
/// Tests for the ProtoGenerator class.
/// </summary>
public sealed class ProtoGeneratorTests
{
    private readonly ProtoGenerator _generator = new();

    [Fact]
    public void Generate_BasicService_GeneratesValidProto()
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
                            FullTypeName = "System.Int32",
                            IsArray = false,
                            IsNullable = false,
                            IsGeneric = false
                        }
                    ]
                }
            ]
        };

        var dataContracts = new List<WcfDataContract>
        {
            new()
            {
                TypeName = "Customer",
                FullyQualifiedName = "MyCompany.Models.Customer",
                IsClass = true,
                IsEnum = false,
                Properties =
                [
                    new WcfDataMember
                    {
                        Name = "CustomerId",
                        Type = "int",
                        FullTypeName = "System.Int32",
                        Order = 1,
                        IsRequired = true,
                        IsNullable = false,
                        IsCollection = false
                    },
                    new WcfDataMember
                    {
                        Name = "CustomerName",
                        Type = "string",
                        FullTypeName = "System.String",
                        Order = 2,
                        IsRequired = true,
                        IsNullable = false,
                        IsCollection = false
                    }
                ]
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, dataContracts);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("customer_service.proto");
        result.PackageName.Should().Be("mycompany.services");
        result.CSharpNamespace.Should().Be("MyCompany.Services.Grpc");

        result.Content.Should().Contain("syntax = \"proto3\";");
        result.Content.Should().Contain("package mycompany.services;");
        result.Content.Should().Contain("option csharp_namespace = \"MyCompany.Services.Grpc\";");
        result.Content.Should().Contain("service CustomerService {");
        result.Content.Should().Contain("rpc GetCustomer (GetCustomerRequest) returns (GetCustomerResponse);");
        result.Content.Should().Contain("message GetCustomerRequest {");
        result.Content.Should().Contain("int32 customer_id = 1;");
        result.Content.Should().Contain("message GetCustomerResponse {");
        result.Content.Should().Contain("message Customer {");
        result.Content.Should().Contain("int32 customer_id = 1;");
        result.Content.Should().Contain("string customer_name = 2;");

        _generator.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Generate_EnumDataContract_GeneratesEnumWithUnspecified()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IStatusService",
            FullyQualifiedName = "MyApp.Services.IStatusService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetStatus",
                    ReturnType = "OrderStatus",
                    Parameters = []
                }
            ]
        };

        var dataContracts = new List<WcfDataContract>
        {
            new()
            {
                TypeName = "OrderStatus",
                FullyQualifiedName = "MyApp.Models.OrderStatus",
                IsClass = false,
                IsEnum = true,
                EnumMembers =
                [
                    new WcfEnumMember { Name = "Pending", Value = 1 },
                    new WcfEnumMember { Name = "Shipped", Value = 2 },
                    new WcfEnumMember { Name = "Delivered", Value = 3 }
                ]
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, dataContracts);

        // Assert
        result.Content.Should().Contain("enum OrderStatus {");
        result.Content.Should().Contain("ORDERSTATUS_UNSPECIFIED = 0;");
        result.Content.Should().Contain("PENDING = 1;");
        result.Content.Should().Contain("SHIPPED = 2;");
        result.Content.Should().Contain("DELIVERED = 3;");

        _generator.Diagnostics.Should().Contain(d => d.Contains("does not have a zero value"));
        _generator.ConfidenceScore.Should().BeLessThan(100);
    }

    [Fact]
    public void Generate_EnumWithZeroValue_DoesNotAddUnspecified()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IStatusService",
            FullyQualifiedName = "MyApp.Services.IStatusService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetStatus",
                    ReturnType = "OrderStatus",
                    Parameters = []
                }
            ]
        };

        var dataContracts = new List<WcfDataContract>
        {
            new()
            {
                TypeName = "OrderStatus",
                FullyQualifiedName = "MyApp.Models.OrderStatus",
                IsClass = false,
                IsEnum = true,
                EnumMembers =
                [
                    new WcfEnumMember { Name = "Unknown", Value = 0 },
                    new WcfEnumMember { Name = "Pending", Value = 1 },
                    new WcfEnumMember { Name = "Shipped", Value = 2 }
                ]
            }
        };

        // Act
        var result = _generator.Generate(serviceContract, dataContracts);

        // Assert
        result.Content.Should().Contain("enum OrderStatus {");
        result.Content.Should().Contain("UNKNOWN = 0;");
        result.Content.Should().NotContain("UNSPECIFIED");

        _generator.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Generate_PrimitiveTypeMappings_GeneratesCorrectTypes()
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
                    Name = "TestTypes",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter { Name = "intValue", Type = "int", FullTypeName = "System.Int32" },
                        new WcfParameter { Name = "longValue", Type = "long", FullTypeName = "System.Int64" },
                        new WcfParameter { Name = "boolValue", Type = "bool", FullTypeName = "System.Boolean" },
                        new WcfParameter { Name = "stringValue", Type = "string", FullTypeName = "System.String" },
                        new WcfParameter { Name = "doubleValue", Type = "double", FullTypeName = "System.Double" },
                        new WcfParameter { Name = "floatValue", Type = "float", FullTypeName = "System.Single" }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("int32 int_value = 1;");
        result.Content.Should().Contain("int64 long_value = 2;");
        result.Content.Should().Contain("bool bool_value = 3;");
        result.Content.Should().Contain("string string_value = 4;");
        result.Content.Should().Contain("double double_value = 5;");
        result.Content.Should().Contain("float float_value = 6;");
    }

    [Fact]
    public void Generate_NullableTypes_GeneratesWrapperTypes()
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
                    Name = "TestNullable",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "nullableInt",
                            Type = "int?",
                            FullTypeName = "System.Nullable<System.Int32>",
                            IsNullable = true
                        },
                        new WcfParameter
                        {
                            Name = "nullableBool",
                            Type = "bool?",
                            FullTypeName = "System.Nullable<System.Boolean>",
                            IsNullable = true
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("google.protobuf.Int32Value nullable_int = 1;");
        result.Content.Should().Contain("google.protobuf.BoolValue nullable_bool = 2;");
        result.Imports.Should().Contain("google/protobuf/wrappers.proto");
    }

    [Fact]
    public void Generate_DateTimeTypes_GeneratesTimestamp()
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
                    Name = "TestDateTime",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "createdAt",
                            Type = "DateTime",
                            FullTypeName = "System.DateTime"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("google.protobuf.Timestamp created_at = 1;");
        result.Imports.Should().Contain("google/protobuf/timestamp.proto");
    }

    [Fact]
    public void Generate_ArrayTypes_GeneratesRepeatedFields()
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
                    Name = "GetItems",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "ids",
                            Type = "int[]",
                            FullTypeName = "System.Int32[]",
                            IsArray = true
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("repeated int32 ids = 1;");
    }

    [Fact]
    public void Generate_CollectionTypes_GeneratesRepeatedFields()
    {
        // Arrange
        var dataContract = new WcfDataContract
        {
            TypeName = "Order",
            FullyQualifiedName = "Test.Order",
            IsClass = true,
            IsEnum = false,
            Properties =
            [
                new WcfDataMember
                {
                    Name = "Items",
                    Type = "List<string>",
                    FullTypeName = "System.Collections.Generic.List<System.String>",
                    IsCollection = true
                }
            ]
        };

        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IOrderService",
            FullyQualifiedName = "Test.IOrderService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetOrder",
                    ReturnType = "Order",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, [dataContract]);

        // Assert
        result.Content.Should().Contain("repeated string items = 1;");
    }

    [Fact]
    public void Generate_AsyncOperation_HandlesTaskReturnType()
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
                    Name = "GetDataAsync",
                    ReturnType = "Task<string>",
                    IsAsync = true,
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("message GetDataAsyncResponse {");
        result.Content.Should().Contain("string string = 1;");
    }

    [Fact]
    public void Generate_OneWayOperation_AddsWarningAndGeneratesEmptyResponse()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "INotificationService",
            FullyQualifiedName = "Test.INotificationService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "SendNotification",
                    ReturnType = "void",
                    IsOneWay = true,
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "message",
                            Type = "string",
                            FullTypeName = "System.String"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("rpc SendNotification (SendNotificationRequest) returns (SendNotificationResponse);");
        result.Content.Should().Contain("message SendNotificationResponse {");

        _generator.Diagnostics.Should().Contain(d => d.Contains("one-way"));
        _generator.ConfidenceScore.Should().BeLessThan(100);
    }

    [Fact]
    public void Generate_XmlDocumentation_IncludedAsComments()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Test.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "Customer",
                    XmlDocumentation = "Gets a customer by ID",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("// Gets a customer by ID");
    }

    [Fact]
    public void Generate_MultipleOperations_GeneratesAllRequestResponsePairs()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "IProductService",
            FullyQualifiedName = "Test.IProductService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetProduct",
                    ReturnType = "Product",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                },
                new WcfOperation
                {
                    Name = "ListProducts",
                    ReturnType = "Product[]",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "DeleteProduct",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("rpc GetProduct (GetProductRequest) returns (GetProductResponse);");
        result.Content.Should().Contain("rpc ListProducts (ListProductsRequest) returns (ListProductsResponse);");
        result.Content.Should().Contain("rpc DeleteProduct (DeleteProductRequest) returns (DeleteProductResponse);");

        result.Content.Should().Contain("message GetProductRequest {");
        result.Content.Should().Contain("message GetProductResponse {");
        result.Content.Should().Contain("message ListProductsRequest {");
        result.Content.Should().Contain("message ListProductsResponse {");
        result.Content.Should().Contain("message DeleteProductRequest {");
        result.Content.Should().Contain("message DeleteProductResponse {");
    }

    [Fact]
    public void Generate_SnakeCaseConversion_ConvertsFieldNames()
    {
        // Arrange
        var dataContract = new WcfDataContract
        {
            TypeName = "CustomerData",
            FullyQualifiedName = "Test.CustomerData",
            IsClass = true,
            IsEnum = false,
            Properties =
            [
                new WcfDataMember
                {
                    Name = "FirstName",
                    Type = "string",
                    FullTypeName = "System.String"
                },
                new WcfDataMember
                {
                    Name = "LastName",
                    Type = "string",
                    FullTypeName = "System.String"
                },
                new WcfDataMember
                {
                    Name = "EmailAddress",
                    Type = "string",
                    FullTypeName = "System.String"
                }
            ]
        };

        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "Test",
                    ReturnType = "CustomerData",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, [dataContract]);

        // Assert
        result.Content.Should().Contain("string first_name = 1;");
        result.Content.Should().Contain("string last_name = 2;");
        result.Content.Should().Contain("string email_address = 3;");
    }

    [Fact]
    public void Generate_DecimalType_MapsToDoubleWithWarning()
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
                    Name = "CalculatePrice",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter
                        {
                            Name = "amount",
                            Type = "decimal",
                            FullTypeName = "System.Decimal"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.Content.Should().Contain("double amount = 1;");
        _generator.Diagnostics.Should().Contain(d => d.Contains("Decimal") && d.Contains("double"));
        _generator.ConfidenceScore.Should().BeLessThan(100);
    }

    [Fact]
    public void Generate_ServiceContractWithCustomName_UsesCustomName()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "Test.ICustomerService",
            Name = "CustomerManagementService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "void",
                    Parameters = []
                }
            ]
        };

        // Act
        var result = _generator.Generate(serviceContract, []);

        // Assert
        result.FileName.Should().Be("customer_management_service.proto");
        result.Content.Should().Contain("service CustomerManagementService {");
    }

    [Fact]
    public void Generate_NullServiceContract_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.Generate(null!, []);
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceContract");
    }

    [Fact]
    public void Generate_NullDataContracts_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceContract = new WcfServiceContract
        {
            InterfaceName = "ITestService",
            FullyQualifiedName = "Test.ITestService",
            Operations = []
        };

        // Act & Assert
        var act = () => _generator.Generate(serviceContract, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dataContracts");
    }
}
