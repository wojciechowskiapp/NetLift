using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Analyzers;

public class BusinessLogicExtractorTests
{
    private readonly BusinessLogicExtractor _extractor = new();

    [Fact]
    public void Extract_WithBasicImplementation_ReturnsExtractedServiceInfo()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public CustomerDto GetCustomer(int id)
        {
            return new CustomerDto { Id = id, Name = ""Test"" };
        }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        result.Should().NotBeNull();
        result.InterfaceName.Should().Be("ICustomerService");
        result.ClassName.Should().Be("CustomerService");
        result.Namespace.Should().Be("MyApp.Services");
        result.Methods.Should().HaveCount(1);

        var method = result.Methods[0];
        method.Name.Should().Be("GetCustomer");
        method.AsyncName.Should().Be("GetCustomerAsync");
        method.ReturnType.Should().Be("CustomerDto");
        method.AsyncReturnType.Should().Be("Task<CustomerDto>");
        method.Parameters.Should().HaveCount(1);
        method.Parameters[0].Name.Should().Be("id");
        method.Parameters[0].Type.Should().Be("int");

        result.InterfaceCode.Should().Contain("Task<CustomerDto> GetCustomerAsync");
        result.InterfaceCode.Should().Contain("CancellationToken cancellationToken = default");
        result.ImplementationCode.Should().Contain("public sealed class CustomerService");
        result.ImplementationCode.Should().Contain("Task<CustomerDto> GetCustomerAsync");
    }

    [Fact]
    public void Extract_WithConstructorDependencies_DetectsDependencies()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _repository;
        private readonly ILogger _logger;

        public CustomerService(ICustomerRepository repository, ILogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public CustomerDto GetCustomer(int id)
        {
            _logger.Log(""Getting customer"");
            return _repository.Find(id);
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        result.Dependencies.Should().HaveCount(2);

        var repoDep = result.Dependencies.FirstOrDefault(d => d.ParameterName == "repository");
        repoDep.Should().NotBeNull();
        repoDep!.InterfaceType.Should().Be("ICustomerRepository");
        repoDep.IsLogger.Should().BeFalse();

        var loggerDep = result.Dependencies.FirstOrDefault(d => d.ParameterName == "logger");
        loggerDep.Should().NotBeNull();
        loggerDep!.InterfaceType.Should().Be("ILogger");
        loggerDep.IsLogger.Should().BeTrue();

        result.ImplementationCode.Should().Contain("private readonly ICustomerRepository _repository;");
        result.ImplementationCode.Should().Contain("private readonly ILogger _logger;");
        result.ImplementationCode.Should().Contain("public CustomerService(");
        result.ImplementationCode.Should().Contain("ICustomerRepository repository,");
        result.ImplementationCode.Should().Contain("ILogger logger");
    }

    [Fact]
    public void Extract_WithInlineDependencies_DetectsAndConvertsToDI()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public CustomerDto GetCustomer(int id)
        {
            var repository = new CustomerRepository();
            return repository.Find(id);
        }
    }

    public class CustomerRepository
    {
        public CustomerDto Find(int id) => null;
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        result.Dependencies.Should().HaveCount(1);

        var dep = result.Dependencies[0];
        dep.TypeName.Should().Be("CustomerRepository");
        dep.ParameterName.Should().Be("customerRepository");
        dep.InterfaceType.Should().Be("ICustomerRepository");

        _extractor.Diagnostics.Should().Contain(d => d.Contains("new CustomerRepository()"));
        _extractor.ConfidenceScore.Should().BeLessThanOrEqualTo(85);
    }

    [Fact]
    public void Extract_WithAsyncMethods_KeepsAsyncSignature()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public async Task<CustomerDto> GetCustomerAsync(int id)
        {
            await Task.Delay(100);
            return new CustomerDto { Id = id };
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomerAsync",
                    ReturnType = "Task<CustomerDto>",
                    IsAsync = true,
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        var method = result.Methods[0];
        method.Name.Should().Be("GetCustomerAsync");
        method.AsyncName.Should().Be("GetCustomerAsync");
        method.ReturnType.Should().Be("Task<CustomerDto>");
        method.AsyncReturnType.Should().Be("Task<CustomerDto>");
    }

    [Fact]
    public void Extract_WithTransactionScope_AddsWarning()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;
using System.Transactions;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public void UpdateCustomer(CustomerDto customer)
        {
            using (var scope = new TransactionScope())
            {
                // Update logic
                scope.Complete();
            }
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "UpdateCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter { Name = "customer", Type = "CustomerDto", FullTypeName = "MyApp.Services.CustomerDto" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        var method = result.Methods[0];
        method.HasTransactionScope.Should().BeTrue();

        result.Warnings.Should().Contain(w => w.Contains("TransactionScope"));
        result.ImplementationCode.Should().Contain("// TODO: TransactionScope detected");

        _extractor.ConfidenceScore.Should().BeLessThanOrEqualTo(75);
    }

    [Fact]
    public void Extract_WithFaultException_AddsWarning()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public CustomerDto GetCustomer(int id)
        {
            if (id <= 0)
            {
                throw new FaultException<ValidationError>(
                    new ValidationError { Message = ""Invalid ID"" },
                    ""Customer ID must be positive"");
            }
            return new CustomerDto { Id = id };
        }
    }

    public class ValidationError
    {
        public string Message { get; set; }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        var method = result.Methods[0];
        method.HasFaultException.Should().BeTrue();

        result.Warnings.Should().Contain(w => w.Contains("FaultException"));
        result.ImplementationCode.Should().Contain("// TODO: FaultException detected");

        _extractor.ConfidenceScore.Should().BeLessThanOrEqualTo(85);
    }

    [Fact]
    public void Extract_WithXmlDocumentation_PreservesDocumentation()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        /// <summary>
        /// Retrieves a customer by their unique identifier.
        /// </summary>
        public CustomerDto GetCustomer(int id)
        {
            return new CustomerDto { Id = id };
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        var method = result.Methods[0];
        method.Documentation.Should().Contain("Retrieves a customer");

        result.InterfaceCode.Should().Contain("/// <summary>");
        result.InterfaceCode.Should().Contain("/// Retrieves a customer");
    }

    [Fact]
    public void Extract_WithVoidMethod_ConvertsToTask()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public void DeleteCustomer(int id)
        {
            // Delete logic
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "DeleteCustomer",
                    ReturnType = "void",
                    Parameters =
                    [
                        new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }
                    ]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        var method = result.Methods[0];
        method.ReturnType.Should().Be("void");
        method.AsyncReturnType.Should().Be("Task");

        result.InterfaceCode.Should().Contain("Task DeleteCustomerAsync");
    }

    [Fact]
    public void Extract_WithMultipleMethods_ExtractsAllMethods()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;
using System.Collections.Generic;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        public CustomerDto GetCustomer(int id)
        {
            return new CustomerDto { Id = id };
        }

        public List<CustomerDto> GetAllCustomers()
        {
            return new List<CustomerDto>();
        }

        public void UpdateCustomer(CustomerDto customer)
        {
            // Update logic
        }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters = [new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }]
                },
                new WcfOperation
                {
                    Name = "GetAllCustomers",
                    ReturnType = "List<CustomerDto>",
                    Parameters = []
                },
                new WcfOperation
                {
                    Name = "UpdateCustomer",
                    ReturnType = "void",
                    Parameters = [new WcfParameter { Name = "customer", Type = "CustomerDto", FullTypeName = "MyApp.Services.CustomerDto" }]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        result.Methods.Should().HaveCount(3);

        result.Methods[0].Name.Should().Be("GetCustomer");
        result.Methods[0].AsyncReturnType.Should().Be("Task<CustomerDto>");

        result.Methods[1].Name.Should().Be("GetAllCustomers");
        result.Methods[1].AsyncReturnType.Should().Be("Task<List<CustomerDto>>");

        result.Methods[2].Name.Should().Be("UpdateCustomer");
        result.Methods[2].AsyncReturnType.Should().Be("Task");
    }

    [Fact]
    public void Extract_WithFieldInitialization_DetectsAsDependency()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly CustomerRepository _repository = new CustomerRepository();

        public CustomerDto GetCustomer(int id)
        {
            return _repository.Find(id);
        }
    }

    public class CustomerRepository
    {
        public CustomerDto Find(int id) => null;
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations =
            [
                new WcfOperation
                {
                    Name = "GetCustomer",
                    ReturnType = "CustomerDto",
                    Parameters = [new WcfParameter { Name = "id", Type = "int", FullTypeName = "System.Int32" }]
                }
            ]
        };

        // Act
        var result = _extractor.Extract(sourceCode, contract);

        // Assert
        result.Dependencies.Should().HaveCount(1);
        result.Dependencies[0].TypeName.Should().Be("CustomerRepository");
        result.Dependencies[0].ParameterName.Should().Be("repository");
        result.Dependencies[0].InterfaceType.Should().Be("ICustomerRepository");

        _extractor.Diagnostics.Should().Contain(d => d.Contains("field initialization"));
    }

    [Fact]
    public void Extract_WithNoImplementation_ThrowsException()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.ServiceModel;

namespace MyApp.Services
{
    public class SomeOtherService
    {
        public void DoSomething() { }
    }
}";

        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations = []
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _extractor.Extract(sourceCode, contract));

        exception.Message.Should().Contain("Could not find class implementing service contract");
        _extractor.ConfidenceScore.Should().Be(0);
    }

    [Fact]
    public void Extract_WithEmptySourceCode_ThrowsArgumentException()
    {
        // Arrange
        var contract = new WcfServiceContract
        {
            InterfaceName = "ICustomerService",
            FullyQualifiedName = "MyApp.Services.ICustomerService",
            Operations = []
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _extractor.Extract("", contract));
        Assert.Throws<ArgumentException>(() => _extractor.Extract("   ", contract));
    }

    [Fact]
    public void Extract_WithNullContract_ThrowsArgumentNullException()
    {
        // Arrange
        var sourceCode = "public class Test {}";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _extractor.Extract(sourceCode, null!));
    }
}
