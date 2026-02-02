using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Generators;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

public sealed class CommandGeneratorTests
{
    private readonly CommandGenerator _generator = new();

    [Fact]
    public void Generate_CreatesSimpleCommandWithNoProperties()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "RefreshCacheCommand",
            Namespace = "ContosoUniversity.Application.Cache.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference
            {
                FilePath = "/Controllers/CacheController.cs",
                ControllerName = "CacheController",
                ActionName = "Refresh"
            },
            Properties = []
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Application.Cache.Commands;");
        result.Should().Contain("using ContosoUniversity.Application.Common;");
        result.Should().Contain("public record RefreshCacheCommand : IRequest<Result>;");
        // Handler is also in same file now
        result.Should().Contain("public sealed class RefreshCacheHandler");
    }

    [Fact]
    public void Generate_CreatesSinglePropertyCommand()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "DeleteStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 100,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Delete"
            },
            Properties =
            [
                new CommandProperty
                {
                    Name = "Id",
                    Type = "int",
                    IsRequired = true
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public record DeleteStudentCommand(int Id) : IRequest<Result>;");
    }

    [Fact]
    public void Generate_CreatesMultiPropertyCommand()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties =
            [
                new CommandProperty
                {
                    Name = "LastName",
                    Type = "string",
                    IsRequired = true
                },
                new CommandProperty
                {
                    Name = "FirstMidName",
                    Type = "string",
                    IsRequired = true
                },
                new CommandProperty
                {
                    Name = "EnrollmentDate",
                    Type = "DateTime",
                    IsRequired = true
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public record CreateStudentCommand(");
        result.Should().Contain("string LastName,");
        result.Should().Contain("string FirstMidName,");
        result.Should().Contain("DateTime EnrollmentDate");
        result.Should().Contain(") : IRequest<Result<int>>;");
    }

    [Fact]
    public void Generate_HandlesNullableProperties()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "UpdateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Update"
            },
            Properties =
            [
                new CommandProperty
                {
                    Name = "Id",
                    Type = "int",
                    IsRequired = true
                },
                new CommandProperty
                {
                    Name = "Email",
                    Type = "string",
                    IsNullable = true
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("int Id,");
        result.Should().Contain("string? Email");
    }

    [Fact]
    public void Generate_IncludesXmlDocumentation()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties = []
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Command to create a new Student.");
        result.Should().Contain("/// Generated with 95% confidence from StudentController.Create.");
        result.Should().Contain("/// </summary>");
    }

    [Fact]
    public void Generate_IncludesHandlerInSameFile()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties =
            [
                new CommandProperty { Name = "LastName", Type = "string", IsRequired = true },
                new CommandProperty { Name = "FirstMidName", Type = "string", IsRequired = true }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Command and Handler in same file
        result.Should().Contain("namespace ContosoUniversity.Application.Students.Commands;");
        result.Should().Contain("using ContosoUniversity.Application.Common;");
        result.Should().Contain("public record CreateStudentCommand("); // Command record
        result.Should().Contain("public sealed class CreateStudentHandler : IRequestHandler<CreateStudentCommand, Result<int>>"); // Handler class
        result.Should().Contain("private readonly IApplicationDbContext _context;");
        result.Should().Contain("public CreateStudentHandler(IApplicationDbContext context)");
        result.Should().Contain("public async Task<Result<int>> Handle(CreateStudentCommand request, CancellationToken cancellationToken)");
    }

    [Fact]
    public void Generate_IncludesCreateImplementationInHandler()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 85,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties =
            [
                new CommandProperty { Name = "LastName", Type = "string", IsRequired = true },
                new CommandProperty { Name = "FirstMidName", Type = "string", IsRequired = true }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler implementation is in the same file
        result.Should().Contain("var entity = new Student");
        result.Should().Contain("LastName = request.LastName,");
        result.Should().Contain("FirstMidName = request.FirstMidName,");
        result.Should().Contain("_context.Students.Add(entity);");
        result.Should().Contain("await _context.SaveChangesAsync(cancellationToken);");
        result.Should().Contain("return Result<int>.Success(entity.Id);");
    }

    [Fact]
    public void Generate_IncludesUpdateImplementationInHandler()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "UpdateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 85,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Update"
            },
            Properties =
            [
                new CommandProperty { Name = "Id", Type = "int", IsRequired = true },
                new CommandProperty { Name = "LastName", Type = "string", IsRequired = true }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler implementation is in the same file
        result.Should().Contain("var entity = await _context.Students.FindAsync");
        result.Should().Contain("if (entity == null)");
        result.Should().Contain("return Result.Failure(\"Student not found\");");
        result.Should().Contain("entity.LastName = request.LastName;");
        result.Should().Contain("await _context.SaveChangesAsync(cancellationToken);");
        result.Should().Contain("return Result.Success();");
    }

    [Fact]
    public void Generate_IncludesDeleteImplementationInHandler()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "DeleteStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Delete"
            },
            Properties =
            [
                new CommandProperty { Name = "Id", Type = "int", IsRequired = true }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler implementation is in the same file
        result.Should().Contain("var entity = await _context.Students.FindAsync");
        result.Should().Contain("if (entity == null)");
        result.Should().Contain("_context.Students.Remove(entity);");
        result.Should().Contain("await _context.SaveChangesAsync(cancellationToken);");
    }

    [Fact]
    public void Generate_AddsLowConfidenceTodoCommentInHandler()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 75,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties = []
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler has TODO comment in same file
        result.Should().Contain("/// TODO: Review implementation - generated with 75% confidence.");
    }

    [Fact]
    public void Generate_UsesProvidedBusinessLogicInHandler()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "ProcessOrderCommand",
            Namespace = "ContosoUniversity.Application.Orders.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/OrderController.cs",
                ControllerName = "OrderController",
                ActionName = "Process"
            },
            Properties = [],
            BusinessLogic = "var order = await _context.Orders.FindAsync(request.Id);\norder.Status = OrderStatus.Processed;\nawait _context.SaveChangesAsync(cancellationToken);\nreturn Result.Success();"
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler uses business logic in same file
        result.Should().Contain("// Business logic from OrderController.Process");
        result.Should().Contain("var order = await _context.Orders.FindAsync(request.Id);");
        result.Should().Contain("order.Status = OrderStatus.Processed;");
    }

    [Fact]
    public void Generate_GeneratesSyncHandlerWhenNotAsync()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "DeleteStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result",
            IsAsync = false,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Delete"
            },
            Properties =
            [
                new CommandProperty { Name = "Id", Type = "int", IsRequired = true }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Sync handler in same file
        result.Should().Contain("public Task<Result> Handle(DeleteStudentCommand request, CancellationToken cancellationToken)");
        result.Should().Contain("var entity = _context.Students.Find(request.Id);");
        result.Should().Contain("_context.SaveChanges();");
        result.Should().Contain("return Task.FromResult(Result.Success());");
    }

    [Fact]
    public void Generate_ThrowsNotImplementedForVeryLowConfidence()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "ComplexBusinessCommand",
            Namespace = "ContosoUniversity.Application.Business.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 45,
            Source = new SourceReference
            {
                FilePath = "/Controllers/BusinessController.cs",
                ControllerName = "BusinessController",
                ActionName = "ComplexOperation"
            },
            Properties = []
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Handler has NotImplementedException in same file
        result.Should().Contain("// TODO: Implement command handler logic");
        result.Should().Contain("// Original action: BusinessController.ComplexOperation");
        result.Should().Contain("// Confidence: 45%");
        result.Should().Contain("throw new NotImplementedException(\"Command handler requires manual implementation\");");
    }

    [Fact]
    public void GenerateHandler_ReturnsEmptyString()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "TestCommand",
            Namespace = "Test.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference { FilePath = "/test.cs", ControllerName = "Test", ActionName = "Test" },
            Properties = []
        };

        // Act
        var result = _generator.GenerateHandler(commandInfo);

        // Assert - GenerateHandler now returns empty since handler is in Generate()
        result.Should().BeEmpty();
    }

    [Fact]
    public void Generate_CreatesResponseDtoForViewBagMutations()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateCatalogItemCommand",
            Namespace = "eShop.Application.Catalog.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/CatalogController.cs",
                ControllerName = "CatalogController",
                ActionName = "Create"
            },
            Properties = [],
            BusinessLogic = "result.CatalogBrandId = brands;\nreturn Result<int>.Success(1);",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "CatalogBrandId",
                    AssignedValue = "new SelectList(_context.CatalogBrands, \"Id\", \"Brand\")",
                    LineNumber = 10
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - Response DTO is generated
        result.Should().Contain("public record CatalogItemResponseDto");
        result.Should().Contain("public IEnumerable<SelectListItem> CatalogBrandId { get; init; }");
        result.Should().Contain("Response DTO for CreateCatalogItemCommand containing ViewBag/ViewData properties.");

        // Assert - using statement for SelectListItem
        result.Should().Contain("using Microsoft.AspNetCore.Mvc.Rendering;");

        // Assert - result variable is declared
        result.Should().Contain("var result = new CatalogItemResponseDto();");
        result.Should().Contain("result.CatalogBrandId = brands;");
    }

    [Fact]
    public void Generate_InfersStringTypeFromStringLiteral()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "TestCommand",
            Namespace = "Test.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference { FilePath = "/test.cs", ControllerName = "Test", ActionName = "Test" },
            Properties = [],
            BusinessLogic = "result.Message = \"Hello\";",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Message",
                    AssignedValue = "\"Test message\"",
                    LineNumber = 10
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public string Message { get; init; }");
    }

    [Fact]
    public void Generate_InfersIntTypeFromNumericLiteral()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "TestCommand",
            Namespace = "Test.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference { FilePath = "/test.cs", ControllerName = "Test", ActionName = "Test" },
            Properties = [],
            BusinessLogic = "result.Count = 42;",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Count",
                    AssignedValue = "42",
                    LineNumber = 10
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public int Count { get; init; }");
    }

    [Fact]
    public void Generate_InfersBoolTypeFromBooleanLiteral()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "TestCommand",
            Namespace = "Test.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference { FilePath = "/test.cs", ControllerName = "Test", ActionName = "Test" },
            Properties = [],
            BusinessLogic = "result.IsActive = true;",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "IsActive",
                    AssignedValue = "true",
                    LineNumber = 10
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public bool IsActive { get; init; }");
    }

    [Fact]
    public void Generate_UsesObjectNullableForUnknownTypes()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "TestCommand",
            Namespace = "Test.Commands",
            ReturnType = "Result",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference { FilePath = "/test.cs", ControllerName = "Test", ActionName = "Test" },
            Properties = [],
            BusinessLogic = "result.Data = someVariable;",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Data",
                    AssignedValue = "someComplexExpression.GetValue()",
                    LineNumber = 10
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert
        result.Should().Contain("public object? Data { get; init; }");
        result.Should().Contain("TODO: Review type inference");
    }

    [Fact]
    public void Generate_HandlesMultipleViewBagMutations()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateCatalogItemCommand",
            Namespace = "eShop.Application.Catalog.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 90,
            Source = new SourceReference
            {
                FilePath = "/Controllers/CatalogController.cs",
                ControllerName = "CatalogController",
                ActionName = "Create"
            },
            Properties = [],
            BusinessLogic = "result.Brands = brands;\nresult.Types = types;\nresult.Title = \"Create\";\nreturn Result<int>.Success(1);",
            ViewModelMutations =
            [
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Brands",
                    AssignedValue = "new SelectList(_context.CatalogBrands, \"Id\", \"Brand\")",
                    LineNumber = 10
                },
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Types",
                    AssignedValue = "new SelectList(_context.CatalogTypes, \"Id\", \"Type\")",
                    LineNumber = 11
                },
                new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = "Title",
                    AssignedValue = "\"Create Catalog Item\"",
                    LineNumber = 12
                }
            ]
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - All properties in DTO
        result.Should().Contain("public IEnumerable<SelectListItem> Brands { get; init; }");
        result.Should().Contain("public IEnumerable<SelectListItem> Types { get; init; }");
        result.Should().Contain("public string Title { get; init; }");
    }

    [Fact]
    public void Generate_DoesNotGenerateDtoWhenNoViewBagMutations()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "Result<int>",
            IsAsync = true,
            Confidence = 95,
            Source = new SourceReference
            {
                FilePath = "/Controllers/StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Properties =
            [
                new CommandProperty { Name = "LastName", Type = "string", IsRequired = true }
            ],
            BusinessLogic = "var student = new Student { LastName = request.LastName };\nreturn Result<int>.Success(1);",
            ViewModelMutations = null
        };

        // Act
        var result = _generator.Generate(commandInfo);

        // Assert - No DTO generated
        result.Should().NotContain("ResponseDto");
        result.Should().NotContain("var result = new");
    }
}
