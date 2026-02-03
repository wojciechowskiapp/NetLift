using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

/// <summary>
/// Tests for the BusinessLogicBuilder class that transforms extracted controller logic
/// into CQRS handler business logic.
/// </summary>
public sealed class BusinessLogicBuilderTests
{
    private readonly BusinessLogicBuilder _builder = new();

    #region Service Call Transformation Tests

    [Fact]
    public void Build_FindEntityByIdPattern_TransformsToFirstOrDefaultAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var item = service.FindCatalogItem(id);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.CatalogItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)");
        result.Should().NotContain("service.FindCatalogItem");
    }

    [Fact]
    public void Build_GetEntityByIdPattern_TransformsToFirstOrDefaultAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var product = service.GetProductById(productId);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)");
        result.Should().NotContain("service.GetProductById");
    }

    [Fact]
    public void Build_GetAllEntitiesPattern_TransformsToToListAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var albums = service.GetAlbums();"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Albums.ToListAsync(cancellationToken)");
        result.Should().NotContain("service.GetAlbums");
    }

    [Fact]
    public void Build_GetAllWithPrefixPattern_TransformsToToListAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var students = service.GetAllStudents();"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Students.ToListAsync(cancellationToken)");
        result.Should().NotContain("service.GetAllStudents");
    }

    [Fact]
    public void Build_CreateEntityPattern_TransformsToAddAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.CreateOrder(order);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Orders.AddAsync(order, cancellationToken)");
        result.Should().NotContain("service.CreateOrder");
    }

    [Fact]
    public void Build_AddEntityPattern_TransformsToAddAsync()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.AddCustomer(customer);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Customers.AddAsync(customer, cancellationToken)");
        result.Should().NotContain("service.AddCustomer");
    }

    [Fact]
    public void Build_UpdateEntityPattern_TransformsToUpdate()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.UpdateProduct(product);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Products.Update(product)");
        result.Should().NotContain("service.UpdateProduct");
    }

    [Fact]
    public void Build_DeleteEntityPattern_TransformsToRemove()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.DeleteItem(item);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Items.Remove(item)");
        result.Should().NotContain("service.DeleteItem");
    }

    [Fact]
    public void Build_RemoveEntityPattern_TransformsToRemove()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.RemoveCategory(category);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Categories.Remove(category)");
        result.Should().NotContain("service.RemoveCategory");
    }

    [Fact]
    public void Build_EntityNameEndingInY_PluralizesToIes()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var category = service.FindCategory(id);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Categories.FirstOrDefaultAsync");
        result.Should().NotContain("_context.Categorys");
    }

    [Fact]
    public void Build_EntityNameEndingInSs_PluralizesToSses()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var address = service.FindAddress(id);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Addresses.FirstOrDefaultAsync");
    }

    [Fact]
    public void Build_EntityNameEndingInCh_PluralizesToChes()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var church = service.FindChurch(id);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("_context.Churches.FirstOrDefaultAsync");
    }

    [Fact]
    public void Build_UnrecognizedServiceCall_AddsTodoComment()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.CustomMethod(param1, param2);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().Contain("TODO: Verify this service call transformation");
        result.Should().Contain("_context.");
    }

    [Fact]
    public void Build_MultipleServiceCalls_TransformsAll()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Assignment,
                    SourceCode = "var product = service.FindProduct(id);"
                },
                new StatementInfo
                {
                    Type = StatementType.MethodCall,
                    SourceCode = "service.UpdateProduct(product);"
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)");
        result.Should().Contain("_context.Products.Update(product)");
        result.Should().NotContain("service.");
    }

    #endregion

    #region Empty Logic Tests

    [Fact]
    public void Build_EmptyLogic_ReturnsEmptyString()
    {
        // Arrange
        var logic = new ExtractedLogic();

        // Act
        var result = _builder.Build(logic, isCommand: false);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_NullLogic_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _builder.Build(null!, isCommand: false);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Private Method Inlining Tests

    [Fact]
    public void BuildFromActionContext_SingleUsePrivateMethod_InlinesMethodBody()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "GetImageMimeType",
            Body = @"private string GetImageMimeType(string extension)
{
    return extension switch
    {
        "".jpg"" => ""image/jpeg"",
        "".png"" => ""image/png"",
        _ => ""application/octet-stream""
    };
}",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "extension", Type = "string" }
            },
            ReturnType = "string",
            CallingActions = new List<string> { "Upload" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/CatalogController.cs",
                ClassName = "CatalogController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Upload",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "POST" },
                Parameters = new List<ActionParameter>
                {
                    new ActionParameter { Name = "extension", Type = "string" }
                }
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var mimeType = GetImageMimeType(extension);"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("GetImageMimeType(");
        result.Should().Contain("request.Extension"); // Parameter should be transformed to request.X
    }

    [Fact]
    public void BuildFromActionContext_MultiUsePrivateMethod_InlinesWithTodoComment()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "CalculateDiscount",
            Body = @"private decimal CalculateDiscount(decimal price)
{
    return price * 0.1m;
}",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "price", Type = "decimal" }
            },
            ReturnType = "decimal",
            CallingActions = new List<string> { "Create", "Update", "Apply" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/OrderController.cs",
                ClassName = "OrderController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Create",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "POST" },
                Parameters = new List<ActionParameter>
                {
                    new ActionParameter { Name = "price", Type = "decimal" }
                }
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var discount = CalculateDiscount(price);"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("CalculateDiscount(");
        result.Should().Contain("request.Price"); // Parameter should be transformed to request.X
    }

    [Fact]
    public void BuildFromActionContext_PrivateMethodWithParameterSubstitution_SubstitutesCorrectly()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "FormatName",
            Body = @"private string FormatName(string firstName, string lastName)
{
    return firstName + "" "" + lastName;
}",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "firstName", Type = "string" },
                new ActionParameter { Name = "lastName", Type = "string" }
            },
            ReturnType = "string",
            CallingActions = new List<string> { "Create" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/UserController.cs",
                ClassName = "UserController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Create",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "POST" },
                Parameters = new List<ActionParameter>
                {
                    new ActionParameter { Name = "firstName", Type = "string" },
                    new ActionParameter { Name = "lastName", Type = "string" }
                }
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var fullName = FormatName(firstName, lastName);"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("FormatName(");
        result.Should().Contain("request.FirstName");
        result.Should().Contain("request.LastName");
    }

    [Fact]
    public void BuildFromActionContext_ExpressionBodiedPrivateMethod_InlinesExpression()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "IsValidAge",
            Body = "private bool IsValidAge(int age) => age >= 18 && age <= 120;",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "age", Type = "int" }
            },
            ReturnType = "bool",
            CallingActions = new List<string> { "Register" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/UserController.cs",
                ClassName = "UserController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Register",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "POST" },
                Parameters = new List<ActionParameter>
                {
                    new ActionParameter { Name = "age", Type = "int" }
                }
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.If,
                        SourceCode = "if (IsValidAge(age))"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("IsValidAge(");
        result.Should().Contain("request.Age");
    }

    [Fact]
    public void BuildFromActionContext_NoPrivateMethodCalls_LeavesCodeUnchanged()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "HelperMethod",
            Body = "private void HelperMethod() { }",
            Parameters = new List<ActionParameter>(),
            ReturnType = "void",
            CallingActions = new List<string> { "OtherAction" }, // Not called by our action
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/TestController.cs",
                ClassName = "TestController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Index",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "GET" },
                Parameters = new List<ActionParameter>()
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var items = _context.Items.ToListAsync();"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = false,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert
        result.Should().Contain("_context.Items.ToListAsync(cancellationToken)");
        result.Should().NotContain("HelperMethod");
        result.Should().NotContain("TODO:");
    }

    [Fact]
    public void BuildFromActionContext_PrivateMethodWithComplexArguments_InlinesCorrectly()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "BuildQuery",
            Body = @"private IQueryable<Item> BuildQuery(IQueryable<Item> query)
{
    return query.Where(x => x.IsActive);
}",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "query", Type = "IQueryable<Item>" }
            },
            ReturnType = "IQueryable<Item>",
            CallingActions = new List<string> { "List" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/ItemController.cs",
                ClassName = "ItemController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "List",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "GET" },
                Parameters = new List<ActionParameter>()
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var filtered = BuildQuery(_context.Items);"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateQuery = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("BuildQuery(");
        result.Should().Contain("_context.Items");
    }

    [Fact]
    public void BuildFromActionContext_MultipleCallsToSamePrivateMethod_InlinesAll()
    {
        // Arrange
        var privateMethod = new PrivateMethodInfo
        {
            Name = "AddTax",
            Body = @"private decimal AddTax(decimal amount)
{
    return amount * 1.2m;
}",
            Parameters = new List<ActionParameter>
            {
                new ActionParameter { Name = "amount", Type = "decimal" }
            },
            ReturnType = "decimal",
            CallingActions = new List<string> { "Calculate" },
            IsAsync = false,
            IsStatic = false
        };

        var context = new ActionLogicContext
        {
            Controller = new ControllerInfo
            {
                FilePath = "/test/OrderController.cs",
                ClassName = "OrderController",
                Namespace = "TestApp.Controllers",
                PrivateMethods = new List<PrivateMethodInfo> { privateMethod }
            },
            Action = new ActionInfo
            {
                Name = "Calculate",
                ReturnType = "ActionResult",
                HttpMethods = new List<string> { "POST" },
                Parameters = new List<ActionParameter>
                {
                    new ActionParameter { Name = "price1", Type = "decimal" },
                    new ActionParameter { Name = "price2", Type = "decimal" }
                }
            },
            ActionLogic = new ExtractedLogic
            {
                Statements = new List<StatementInfo>
                {
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var total1 = AddTax(price1);"
                    },
                    new StatementInfo
                    {
                        Type = StatementType.Assignment,
                        SourceCode = "var total2 = AddTax(price2);"
                    }
                }
            },
            TargetNamespace = "TestApp.Application",
            GenerateCommand = true,
            Confidence = 95
        };

        // Act
        var result = _builder.BuildFromActionContext(context);

        // Assert - Private methods are NO LONGER inlined; they're kept as calls
        // and added separately to the handler class
        result.Should().Contain("AddTax(");
        result.Should().Contain("request.Price1");
        result.Should().Contain("request.Price2");
    }

    #endregion

    #region ModelState.IsValid Unwrapping Tests

    [Fact]
    public void Build_IfStatementWithModelStateIsValid_UnwrapsAndProcessesChildStatements()
    {
        // Arrange - POST Create with ModelState.IsValid check
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.If,
                    SourceCode = "if (ModelState.IsValid)",
                    ChildStatements = new List<StatementInfo>
                    {
                        new StatementInfo
                        {
                            Type = StatementType.MethodCall,
                            SourceCode = "db.Students.Add(student);"
                        },
                        new StatementInfo
                        {
                            Type = StatementType.MethodCall,
                            SourceCode = "db.SaveChanges();"
                        },
                        new StatementInfo
                        {
                            Type = StatementType.Return,
                            SourceCode = "return RedirectToAction(\"Index\");"
                        }
                    }
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("// TODO: Add validation");
        result.Should().Contain("_context.Students.Add"); // Should preserve Add
        result.Should().Contain("SaveChangesAsync"); // Should preserve SaveChanges (transformed to async)
        result.Should().NotContain("if (ModelState.IsValid)"); // Should not have the if wrapper
    }

    [Fact]
    public void Build_TryBlockWithModelStateIsValid_UnwrapsModelStateButPreservesTryCatch()
    {
        // Arrange - try-catch with ModelState.IsValid inside
        var tryCatchCode = @"try
{
    if (ModelState.IsValid)
    {
        db.Students.Add(student);
        db.SaveChanges();
        return RedirectToAction(""Index"");
    }
}
catch (RetryLimitExceededException)
{
    ModelState.AddModelError("""", ""Unable to save changes..."");
}";

        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Try,
                    SourceCode = tryCatchCode
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("// TODO: Add validation");
        result.Should().Contain("try"); // Should preserve try
        result.Should().Contain("catch"); // Should preserve catch
        result.Should().Contain("_context.Students"); // Should preserve Add (transformed)
        result.Should().Contain("SaveChangesAsync"); // Should preserve SaveChanges (transformed to async)
        result.Should().NotContain("if (ModelState.IsValid)"); // Should not have the if wrapper
        result.Should().Contain("DbUpdateException"); // RetryLimitExceededException should be transformed
    }

    [Fact]
    public void Build_SimpleModelStateIsValidIfStatement_ReturnsChildStatementsOnly()
    {
        // Arrange - simple if with one statement
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.If,
                    SourceCode = "if (ModelState.IsValid)",
                    ChildStatements = new List<StatementInfo>
                    {
                        new StatementInfo
                        {
                            Type = StatementType.MethodCall,
                            SourceCode = "db.Items.Add(item);"
                        }
                    }
                }
            }
        };

        // Act
        var result = _builder.Build(logic, isCommand: true);

        // Assert
        result.Should().Contain("_context.Items");
        result.Should().NotContain("if (");
        result.Should().Contain("// TODO: Add validation"); // Comment is OK
        result.Should().NotContain("if (ModelState.IsValid)"); // But the actual if statement should be removed
    }

    #endregion
}
