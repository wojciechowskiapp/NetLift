using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Analyzers;

public class FaultContractTransformerTests
{
    private readonly FaultContractTransformer _transformer = new();

    [Fact]
    public void Transform_WithEmptyList_ReturnsEmptyResult()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>();

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Should().NotBeNull();
        result.Exceptions.Should().BeEmpty();
        result.InterceptorCode.Should().BeEmpty();
        result.ExceptionHandlerCode.Should().BeEmpty();
        result.ExceptionClassesCode.Should().BeEmpty();
        _transformer.Diagnostics.Should().Contain("No fault contracts provided");
    }

    [Fact]
    public void Transform_WithNotFoundFault_MapsToNotFound404()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = new List<WcfDataMember>
                {
                    new WcfDataMember
                    {
                        Name = "CustomerId",
                        Type = "int",
                        FullTypeName = "System.Int32"
                    },
                    new WcfDataMember
                    {
                        Name = "Message",
                        Type = "string",
                        FullTypeName = "System.String"
                    }
                }
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.FaultTypeName.Should().Be("CustomerNotFoundFault");
        exception.ExceptionClassName.Should().Be("CustomerNotFoundException");
        exception.ExceptionCode.Should().Be("CUSTOMER_NOT_FOUND");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.NotFound);
        exception.HttpStatusCode.Should().Be(404);
        exception.Properties.Should().HaveCount(2);
        exception.Properties[0].Name.Should().Be("CustomerId");
        exception.Properties[0].Type.Should().Be("int");
        exception.Properties[1].Name.Should().Be("Message");
        exception.Properties[1].Type.Should().Be("string");

        _transformer.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Transform_WithValidationFault_MapsToInvalidArgument400()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "ValidationFault",
                FullyQualifiedName = "MyApp.Faults.ValidationFault",
                IsClass = true,
                Properties = new List<WcfDataMember>
                {
                    new WcfDataMember
                    {
                        Name = "Errors",
                        Type = "List<string>",
                        FullTypeName = "System.Collections.Generic.List<System.String>"
                    }
                }
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.ExceptionClassName.Should().Be("ValidationException");
        exception.ExceptionCode.Should().Be("VALIDATION");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.InvalidArgument);
        exception.HttpStatusCode.Should().Be(400);
    }

    [Fact]
    public void Transform_WithAuthorizationFault_MapsToPermissionDenied403()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "AuthorizationFault",
                FullyQualifiedName = "MyApp.Faults.AuthorizationFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.ExceptionClassName.Should().Be("AuthorizationException");
        exception.ExceptionCode.Should().Be("AUTHORIZATION");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.PermissionDenied);
        exception.HttpStatusCode.Should().Be(403);
    }

    [Fact]
    public void Transform_WithAuthenticationFault_MapsToUnauthenticated401()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "AuthenticationFault",
                FullyQualifiedName = "MyApp.Faults.AuthenticationFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.ExceptionClassName.Should().Be("AuthenticationException");
        exception.ExceptionCode.Should().Be("AUTHENTICATION");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.Unauthenticated);
        exception.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public void Transform_WithConflictFault_MapsToAlreadyExists409()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "DuplicateCustomerFault",
                FullyQualifiedName = "MyApp.Faults.DuplicateCustomerFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.ExceptionClassName.Should().Be("DuplicateCustomerException");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.AlreadyExists);
        exception.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Transform_WithUnknownFault_MapsToInternal500()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "ServiceFault",
                FullyQualifiedName = "MyApp.Faults.ServiceFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.ExceptionClassName.Should().Be("ServiceException");
        exception.GrpcStatus.Should().Be(GrpcStatusCode.Internal);
        exception.HttpStatusCode.Should().Be(500);
    }

    [Fact]
    public void Transform_GeneratesExceptionClasses()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = new List<WcfDataMember>
                {
                    new WcfDataMember
                    {
                        Name = "CustomerId",
                        Type = "int",
                        FullTypeName = "System.Int32"
                    }
                }
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.ExceptionClassesCode.Should().NotBeNullOrEmpty();
        result.ExceptionClassesCode.Should().Contain("public abstract class DomainException : Exception");
        result.ExceptionClassesCode.Should().Contain("public sealed class CustomerNotFoundException : DomainException");
        result.ExceptionClassesCode.Should().Contain("public int CustomerId { get; }");
        result.ExceptionClassesCode.Should().Contain("public CustomerNotFoundException(string message, int customerId)");
        result.ExceptionClassesCode.Should().Contain("base(\"CUSTOMER_NOT_FOUND\", message)");
    }

    [Fact]
    public void Transform_GeneratesGrpcInterceptor()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = []
            },
            new WcfDataContract
            {
                TypeName = "ValidationFault",
                FullyQualifiedName = "MyApp.Faults.ValidationFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.InterceptorCode.Should().NotBeNullOrEmpty();
        result.InterceptorCode.Should().Contain("public class ExceptionInterceptor : Interceptor");
        result.InterceptorCode.Should().Contain("catch (CustomerNotFoundException ex)");
        result.InterceptorCode.Should().Contain("throw new RpcException(new Status(StatusCode.NotFound, ex.Message));");
        result.InterceptorCode.Should().Contain("catch (ValidationException ex)");
        result.InterceptorCode.Should().Contain("throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));");
        result.InterceptorCode.Should().Contain("catch (Exception ex)");
        result.InterceptorCode.Should().Contain("StatusCode.Internal");
    }

    [Fact]
    public void Transform_GeneratesRestExceptionHandler()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.ExceptionHandlerCode.Should().NotBeNullOrEmpty();
        result.ExceptionHandlerCode.Should().Contain("public class GlobalExceptionHandler : IExceptionHandler");
        result.ExceptionHandlerCode.Should().Contain("CustomerNotFoundException ex => new ProblemDetails");
        result.ExceptionHandlerCode.Should().Contain("Status = 404");
        result.ExceptionHandlerCode.Should().Contain("await httpContext.Response.WriteAsJsonAsync(problemDetails");
    }

    [Fact]
    public void Transform_WithMultipleFaults_TransformsAll()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = []
            },
            new WcfDataContract
            {
                TypeName = "ValidationFault",
                FullyQualifiedName = "MyApp.Faults.ValidationFault",
                IsClass = true,
                Properties = []
            },
            new WcfDataContract
            {
                TypeName = "AuthorizationFault",
                FullyQualifiedName = "MyApp.Faults.AuthorizationFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(3);
        result.Exceptions[0].ExceptionClassName.Should().Be("CustomerNotFoundException");
        result.Exceptions[1].ExceptionClassName.Should().Be("ValidationException");
        result.Exceptions[2].ExceptionClassName.Should().Be("AuthorizationException");

        result.ExceptionClassesCode.Should().Contain("CustomerNotFoundException");
        result.ExceptionClassesCode.Should().Contain("ValidationException");
        result.ExceptionClassesCode.Should().Contain("AuthorizationException");

        result.InterceptorCode.Should().Contain("CustomerNotFoundException");
        result.InterceptorCode.Should().Contain("ValidationException");
        result.InterceptorCode.Should().Contain("AuthorizationException");
    }

    [Fact]
    public void Transform_WithNonFaultContract_SkipsIt()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerDto", // Not a fault
                FullyQualifiedName = "MyApp.Dtos.CustomerDto",
                IsClass = true,
                Properties = []
            },
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        result.Exceptions[0].FaultTypeName.Should().Be("CustomerNotFoundFault");
        _transformer.Diagnostics.Should().Contain(d => d.Contains("Skipping 'CustomerDto'"));
    }

    [Fact]
    public void Transform_WithComplexProperties_PreservesProperties()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "ValidationFault",
                FullyQualifiedName = "MyApp.Faults.ValidationFault",
                IsClass = true,
                Properties = new List<WcfDataMember>
                {
                    new WcfDataMember
                    {
                        Name = "ErrorCode",
                        Type = "string",
                        FullTypeName = "System.String"
                    },
                    new WcfDataMember
                    {
                        Name = "FieldErrors",
                        Type = "Dictionary<string, string>",
                        FullTypeName = "System.Collections.Generic.Dictionary<System.String, System.String>"
                    },
                    new WcfDataMember
                    {
                        Name = "Timestamp",
                        Type = "DateTime",
                        FullTypeName = "System.DateTime"
                    }
                }
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        var exception = result.Exceptions[0];
        exception.Properties.Should().HaveCount(3);
        exception.Properties[0].Name.Should().Be("ErrorCode");
        exception.Properties[0].Type.Should().Be("string");
        exception.Properties[1].Name.Should().Be("FieldErrors");
        exception.Properties[1].Type.Should().Be("Dictionary<string, string>");
        exception.Properties[2].Name.Should().Be("Timestamp");
        exception.Properties[2].Type.Should().Be("DateTime");

        result.ExceptionClassesCode.Should().Contain("public string ErrorCode { get; }");
        result.ExceptionClassesCode.Should().Contain("public Dictionary<string, string> FieldErrors { get; }");
        result.ExceptionClassesCode.Should().Contain("public DateTime Timestamp { get; }");
    }

    [Fact]
    public void Transform_ExceptionCodeGeneration_HandlesPascalCase()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundFault",
                FullyQualifiedName = "MyApp.Faults.CustomerNotFoundFault",
                IsClass = true,
                Properties = []
            },
            new WcfDataContract
            {
                TypeName = "OrderAlreadyProcessedFault",
                FullyQualifiedName = "MyApp.Faults.OrderAlreadyProcessedFault",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions[0].ExceptionCode.Should().Be("CUSTOMER_NOT_FOUND");
        result.Exceptions[1].ExceptionCode.Should().Be("ORDER_ALREADY_PROCESSED");
    }

    [Fact]
    public void Transform_WithErrorSuffix_RemovesItCorrectly()
    {
        // Arrange
        var faultContracts = new List<WcfDataContract>
        {
            new WcfDataContract
            {
                TypeName = "CustomerNotFoundError",
                FullyQualifiedName = "MyApp.Errors.CustomerNotFoundError",
                IsClass = true,
                Properties = []
            }
        };

        // Act
        var result = _transformer.Transform(faultContracts);

        // Assert
        result.Exceptions.Should().HaveCount(1);
        result.Exceptions[0].ExceptionClassName.Should().Be("CustomerNotFoundException");
    }
}
