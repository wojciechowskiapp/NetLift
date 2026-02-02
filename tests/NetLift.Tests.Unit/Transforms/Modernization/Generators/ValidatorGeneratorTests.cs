using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

public sealed class ValidatorGeneratorTests
{
    private readonly ValidatorGenerator _generator;

    public ValidatorGeneratorTests()
    {
        _generator = new ValidatorGenerator();
    }

    [Fact]
    public void Generate_WithSimpleRequiredValidation_GeneratesNotEmptyRule()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "LastName",
                    ValidationMethod = "NotEmpty"
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Application.Students.Commands;");
        result.Should().Contain("using FluentValidation;");
        result.Should().Contain("public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>");
        result.Should().Contain("RuleFor(x => x.LastName)");
        result.Should().Contain(".NotEmpty();");
    }

    [Fact]
    public void Generate_WithStringLengthValidation_GeneratesMaximumLengthRule()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "LastName",
                    ValidationMethod = "NotEmpty"
                },
                new ValidationRule
                {
                    PropertyName = "LastName",
                    ValidationMethod = "MaximumLength",
                    Parameters = new[] { "50" }
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.LastName)");
        result.Should().Contain(".NotEmpty()");
        result.Should().Contain(".MaximumLength(50);");
    }

    [Fact]
    public void Generate_WithMinAndMaxLengthValidation_GeneratesBothRules()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "FirstMidName",
                    ValidationMethod = "NotEmpty"
                },
                new ValidationRule
                {
                    PropertyName = "FirstMidName",
                    ValidationMethod = "MinimumLength",
                    Parameters = new[] { "2" }
                },
                new ValidationRule
                {
                    PropertyName = "FirstMidName",
                    ValidationMethod = "MaximumLength",
                    Parameters = new[] { "50" }
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.FirstMidName)");
        result.Should().Contain(".NotEmpty()");
        result.Should().Contain(".MinimumLength(2)");
        result.Should().Contain(".MaximumLength(50);");
    }

    [Fact]
    public void Generate_WithRangeValidation_GeneratesInclusiveBetweenRule()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "Age",
                    ValidationMethod = "InclusiveBetween",
                    Parameters = new[] { "0", "150" }
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Age)");
        result.Should().Contain(".InclusiveBetween(0, 150);");
    }

    [Fact]
    public void Generate_WithEmailValidation_GeneratesConditionalEmailAddressRule()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "Email",
                    ValidationMethod = "EmailAddress"
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Email)");
        result.Should().Contain(".EmailAddress()");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Email));");
    }

    [Fact]
    public void Generate_WithRegularExpressionValidation_GeneratesConditionalMatchesRule()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "Phone",
                    ValidationMethod = "Matches",
                    Parameters = new[] { @"@""^\d{3}-\d{3}-\d{4}$""" }
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Phone)");
        result.Should().Contain(@".Matches(@""^\d{3}-\d{3}-\d{4}$"")");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Phone));");
    }

    [Fact]
    public void Generate_WithCustomErrorMessage_IncludesWithMessage()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "Age",
                    ValidationMethod = "InclusiveBetween",
                    Parameters = new[] { "0", "150" },
                    ErrorMessage = "Age must be between 0 and 150"
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain(@".WithMessage(""Age must be between 0 and 150"");");
    }

    [Fact]
    public void Generate_WithMultipleProperties_GeneratesSeparateRuleForBlocks()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = new[]
            {
                new ValidationRule
                {
                    PropertyName = "LastName",
                    ValidationMethod = "NotEmpty"
                },
                new ValidationRule
                {
                    PropertyName = "LastName",
                    ValidationMethod = "MaximumLength",
                    Parameters = new[] { "50" }
                },
                new ValidationRule
                {
                    PropertyName = "FirstMidName",
                    ValidationMethod = "NotEmpty"
                },
                new ValidationRule
                {
                    PropertyName = "FirstMidName",
                    ValidationMethod = "MinimumLength",
                    Parameters = new[] { "2" }
                }
            },
            Confidence = 95
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.LastName)");
        result.Should().Contain("RuleFor(x => x.FirstMidName)");
    }

    [Fact]
    public void GenerateForCommand_WithRequiredProperties_GeneratesValidator()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "LastName",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required", "StringLength(50)" }
                },
                new CommandProperty
                {
                    Name = "FirstMidName",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required", "StringLength(50, MinimumLength=2)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create",
                LineNumber = 42
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Application.Students.Commands;");
        result.Should().Contain("public sealed class CreateStudentValidator : AbstractValidator<CreateStudentCommand>");
        result.Should().Contain("RuleFor(x => x.LastName)");
        result.Should().Contain(".NotEmpty()");
        result.Should().Contain(".MaximumLength(50);");
        result.Should().Contain("RuleFor(x => x.FirstMidName)");
        result.Should().Contain(".MinimumLength(2)");
        result.Should().Contain(".MaximumLength(50);");
    }

    [Fact]
    public void GenerateForCommand_WithoutRequiresValidation_ReturnsEmptyString()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = false,
            Properties = Array.Empty<CommandProperty>(),
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateForCommand_WithRangeValidation_GeneratesInclusiveBetween()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "Age",
                    Type = "int",
                    IsRequired = false,
                    IsNullable = false,
                    ValidationRules = new[] { "Range(0, 150)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Age)");
        result.Should().Contain(".InclusiveBetween(0, 150);");
    }

    [Fact]
    public void GenerateForCommand_WithEmailValidation_GeneratesConditionalRule()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "Email",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { "EmailAddress" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Email)");
        result.Should().Contain(".EmailAddress()");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Email));");
    }

    [Fact]
    public void GenerateForCommand_WithRegularExpressionValidation_GeneratesMatchesRule()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "Phone",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { @"RegularExpression(^\d{3}-\d{3}-\d{4}$)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.Phone)");
        result.Should().Contain(@".Matches(@""^\d{3}-\d{3}-\d{4}$"")");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Phone));");
    }

    [Fact]
    public void GenerateForQuery_WithPagination_GeneratesPaginationValidation()
    {
        // Arrange
        var queryInfo = new QueryInfo
        {
            Name = "GetStudentsQuery",
            Namespace = "ContosoUniversity.Application.Students.Queries",
            ReturnType = "PagedList<StudentDto>",
            RequiresValidation = true,
            SupportsPagination = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "PageNumber",
                    Type = "int",
                    IsRequired = false,
                    IsNullable = false,
                    ValidationRules = Array.Empty<string>()
                },
                new CommandProperty
                {
                    Name = "PageSize",
                    Type = "int",
                    IsRequired = false,
                    IsNullable = false,
                    ValidationRules = Array.Empty<string>()
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Index"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForQuery(queryInfo);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Application.Students.Queries;");
        result.Should().Contain("public sealed class GetStudentsValidator : AbstractValidator<GetStudentsQuery>");
        result.Should().Contain("RuleFor(x => x.PageNumber)");
        result.Should().Contain(".GreaterThan(0);");
        result.Should().Contain("RuleFor(x => x.PageSize)");
        result.Should().Contain(".GreaterThan(0)");
        result.Should().Contain(".LessThanOrEqualTo(100)");
        result.Should().Contain(@".WithMessage(""Page size cannot exceed 100 items"");");
    }

    [Fact]
    public void GenerateForQuery_WithoutRequiresValidation_ReturnsEmptyString()
    {
        // Arrange
        var queryInfo = new QueryInfo
        {
            Name = "GetStudentsQuery",
            Namespace = "ContosoUniversity.Application.Students.Queries",
            ReturnType = "List<StudentDto>",
            RequiresValidation = false,
            Properties = Array.Empty<CommandProperty>(),
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Index"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForQuery(queryInfo);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateForQuery_WithSearchParameter_GeneratesValidatorWithQueryProperties()
    {
        // Arrange
        var queryInfo = new QueryInfo
        {
            Name = "SearchStudentsQuery",
            Namespace = "ContosoUniversity.Application.Students.Queries",
            ReturnType = "List<StudentDto>",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "SearchString",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { "MaxLength(100)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Search"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForQuery(queryInfo);

        // Assert
        result.Should().Contain("public sealed class SearchStudentsValidator : AbstractValidator<SearchStudentsQuery>");
        result.Should().Contain("RuleFor(x => x.SearchString)");
        result.Should().Contain(".MaximumLength(100);");
    }

    [Fact]
    public void Generate_WithConfidenceLessThan100_IncludesConfidenceInDocumentation()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = Array.Empty<ValidationRule>(),
            Confidence = 85
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("/// Generated with 85% confidence.");
    }

    [Fact]
    public void Generate_WithSourceReference_IncludesSourceInDocumentation()
    {
        // Arrange
        var validatorInfo = new ValidatorInfo
        {
            Name = "CreateStudentCommandValidator",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ValidatedType = "CreateStudentCommand",
            Rules = Array.Empty<ValidationRule>(),
            Confidence = 95,
            SourceReference = "StudentController.Create"
        };

        // Act
        var result = _generator.Generate(validatorInfo);

        // Assert
        result.Should().Contain("/// Source: StudentController.Create");
    }

    [Fact]
    public void Generate_WithNullValidatorInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.Generate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateForCommand_WithNullCommandInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.GenerateForCommand(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateForQuery_WithNullQueryInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.GenerateForQuery(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateForCommand_WithCompareValidation_GeneratesEqualRule()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "ChangePasswordCommand",
            Namespace = "App.Commands",
            ReturnType = "Unit",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "Password",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required" }
                },
                new CommandProperty
                {
                    Name = "ConfirmPassword",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required", "Compare(Password)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\AccountController.cs",
                ControllerName = "AccountController",
                ActionName = "ChangePassword"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.ConfirmPassword)");
        result.Should().Contain(".Equal(x => x.Password);");
    }

    [Fact]
    public void GenerateForCommand_WithUrlValidation_GeneratesMustRule()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateWebsiteCommand",
            Namespace = "App.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "WebsiteUrl",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { "Url" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\WebsiteController.cs",
                ControllerName = "WebsiteController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("RuleFor(x => x.WebsiteUrl)");
        result.Should().Contain(".Must(BeValidUrl)");
        result.Should().Contain(@".WithMessage(""Must be a valid URL"")");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.WebsiteUrl));");
    }

    [Fact]
    public void GenerateForCommand_WithComplexModel_GeneratesCompleteValidator()
    {
        // Arrange
        var commandInfo = new CommandInfo
        {
            Name = "CreateStudentCommand",
            Namespace = "ContosoUniversity.Application.Students.Commands",
            ReturnType = "int",
            RequiresValidation = true,
            Properties = new[]
            {
                new CommandProperty
                {
                    Name = "LastName",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required", "StringLength(50)" }
                },
                new CommandProperty
                {
                    Name = "FirstMidName",
                    Type = "string",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required", "StringLength(50, MinimumLength=2)" }
                },
                new CommandProperty
                {
                    Name = "EnrollmentDate",
                    Type = "DateTime",
                    IsRequired = true,
                    IsNullable = false,
                    ValidationRules = new[] { "Required" }
                },
                new CommandProperty
                {
                    Name = "Age",
                    Type = "int",
                    IsRequired = false,
                    IsNullable = false,
                    ValidationRules = new[] { "Range(0, 150)" }
                },
                new CommandProperty
                {
                    Name = "Email",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { "EmailAddress" }
                },
                new CommandProperty
                {
                    Name = "Phone",
                    Type = "string",
                    IsRequired = false,
                    IsNullable = true,
                    ValidationRules = new[] { @"RegularExpression(^\d{3}-\d{3}-\d{4}$)" }
                }
            },
            Source = new SourceReference
            {
                FilePath = "F:\\test\\StudentController.cs",
                ControllerName = "StudentController",
                ActionName = "Create"
            },
            Confidence = 95
        };

        // Act
        var result = _generator.GenerateForCommand(commandInfo);

        // Assert
        result.Should().Contain("public sealed class CreateStudentValidator : AbstractValidator<CreateStudentCommand>");

        // LastName validation
        result.Should().Contain("RuleFor(x => x.LastName)");
        result.Should().Contain(".NotEmpty()");
        result.Should().Contain(".MaximumLength(50);");

        // FirstMidName validation
        result.Should().Contain("RuleFor(x => x.FirstMidName)");
        result.Should().Contain(".MinimumLength(2)");
        result.Should().Contain(".MaximumLength(50);");

        // EnrollmentDate validation
        result.Should().Contain("RuleFor(x => x.EnrollmentDate)");
        result.Should().Contain(".NotEmpty();");

        // Age validation
        result.Should().Contain("RuleFor(x => x.Age)");
        result.Should().Contain(".InclusiveBetween(0, 150);");

        // Email validation
        result.Should().Contain("RuleFor(x => x.Email)");
        result.Should().Contain(".EmailAddress()");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Email));");

        // Phone validation
        result.Should().Contain("RuleFor(x => x.Phone)");
        result.Should().Contain(@".Matches(@""^\d{3}-\d{3}-\d{4}$"")");
        result.Should().Contain(".When(x => !string.IsNullOrEmpty(x.Phone));");
    }
}
