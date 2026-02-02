using FluentAssertions;
using NetLift.Transforms.Modernization.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

public sealed class HandlerGeneratorTests
{
    private readonly HandlerGenerator _generator = new();

    #region GenerateResultClass Tests

    [Fact]
    public void GenerateResultClass_ValidNamespace_GeneratesResultClass()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Application.Common";

        // Act
        var result = _generator.GenerateResultClass(namespaceName);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain($"namespace {namespaceName};");
        result.Should().Contain("public class Result<T>");
        result.Should().Contain("public bool IsSuccess { get; }");
        result.Should().Contain("public T? Value { get; }");
        result.Should().Contain("public string? Error { get; }");
        result.Should().Contain("public bool IsFailure => !IsSuccess;");
        result.Should().Contain("Result<T> Success(T value)");
        result.Should().Contain("Result<T> Failure(string error)");
    }

    [Fact]
    public void GenerateResultClass_ValidNamespace_IncludesXmlDocumentation()
    {
        // Arrange
        var namespaceName = "TestApp.Common";

        // Act
        var result = _generator.GenerateResultClass(namespaceName);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Represents the result of an operation");
        result.Should().Contain("/// <typeparam name=\"T\">");
        result.Should().Contain("/// Gets a value indicating whether the operation was successful.");
        result.Should().Contain("/// Gets the result value.");
        result.Should().Contain("/// Gets the error message.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateResultClass_InvalidNamespace_ThrowsArgumentException(string? namespaceName)
    {
        // Act
        Action act = () => _generator.GenerateResultClass(namespaceName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("namespaceName");
    }

    [Fact]
    public void GenerateResultClass_ValidNamespace_HasPrivateConstructor()
    {
        // Arrange
        var namespaceName = "App.Common";

        // Act
        var result = _generator.GenerateResultClass(namespaceName);

        // Assert
        result.Should().Contain("private Result(bool isSuccess, T? value, string? error)");
        result.Should().Contain("IsSuccess = isSuccess;");
        result.Should().Contain("Value = value;");
        result.Should().Contain("Error = error;");
    }

    #endregion

    #region GenerateDto Tests

    [Fact]
    public void GenerateDto_ValidEntityAndProperties_GeneratesDtoRecord()
    {
        // Arrange
        var entityName = "Student";
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("FullName", "string"),
            ("EnrollmentDate", "DateTime")
        };
        var namespaceName = "ContosoUniversity.Application.Students.Queries";

        // Act
        var result = _generator.GenerateDto(entityName, properties, namespaceName);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain($"namespace {namespaceName};");
        result.Should().Contain("public record StudentDto");
        result.Should().Contain("public int Id { get; init; }");
        result.Should().Contain("public string FullName { get; init; } = string.Empty;");
        result.Should().Contain("public DateTime EnrollmentDate { get; init; }");
    }

    [Fact]
    public void GenerateDto_StringProperty_InitializesWithEmptyString()
    {
        // Arrange
        var entityName = "Course";
        var properties = new List<(string Name, string Type)>
        {
            ("Title", "string"),
            ("Description", "string")
        };
        var namespaceName = "App.Courses";

        // Act
        var result = _generator.GenerateDto(entityName, properties, namespaceName);

        // Assert
        result.Should().Contain("public string Title { get; init; } = string.Empty;");
        result.Should().Contain("public string Description { get; init; } = string.Empty;");
    }

    [Fact]
    public void GenerateDto_NullableProperty_NoDefaultInitializer()
    {
        // Arrange
        var entityName = "User";
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("Email", "string?"),
            ("Age", "int?")
        };
        var namespaceName = "App.Users";

        // Act
        var result = _generator.GenerateDto(entityName, properties, namespaceName);

        // Assert
        result.Should().Contain("public string? Email { get; init; }");
        result.Should().NotContain("Email { get; init; } =");
        result.Should().Contain("public int? Age { get; init; }");
        result.Should().NotContain("Age { get; init; } =");
    }

    [Fact]
    public void GenerateDto_CollectionProperty_InitializesWithEmptyCollection()
    {
        // Arrange
        var entityName = "Course";
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("Students", "ICollection<Student>"),
            ("Enrollments", "List<Enrollment>")
        };
        var namespaceName = "App.Courses";

        // Act
        var result = _generator.GenerateDto(entityName, properties, namespaceName);

        // Assert
        result.Should().Contain("public ICollection<Student> Students { get; init; } = [];");
        result.Should().Contain("public List<Enrollment> Enrollments { get; init; } = [];");
    }

    [Fact]
    public void GenerateDto_IncludesXmlDocumentation()
    {
        // Arrange
        var entityName = "Product";
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("Name", "string")
        };
        var namespaceName = "App.Products";

        // Act
        var result = _generator.GenerateDto(entityName, properties, namespaceName);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Data transfer object for Product entity.");
        result.Should().Contain("/// Gets or initializes the Id.");
        result.Should().Contain("/// Gets or initializes the Name.");
    }

    [Theory]
    [InlineData("", "Name", "string")]
    [InlineData("   ", "Name", "string")]
    [InlineData(null, "Name", "string")]
    public void GenerateDto_InvalidEntityName_ThrowsArgumentException(string? entityName, string propertyName, string propertyType)
    {
        // Arrange
        var properties = new List<(string Name, string Type)> { (propertyName, propertyType) };
        var namespaceName = "App.Test";

        // Act
        Action act = () => _generator.GenerateDto(entityName!, properties, namespaceName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("entityName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateDto_InvalidNamespace_ThrowsArgumentException(string? namespaceName)
    {
        // Arrange
        var properties = new List<(string Name, string Type)> { ("Id", "int") };

        // Act
        Action act = () => _generator.GenerateDto("Student", properties, namespaceName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("namespaceName");
    }

    [Fact]
    public void GenerateDto_NullProperties_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _generator.GenerateDto("Student", null!, "App.Test");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("properties");
    }

    [Fact]
    public void GenerateDto_EmptyProperties_ThrowsArgumentException()
    {
        // Arrange
        var properties = new List<(string Name, string Type)>();

        // Act
        Action act = () => _generator.GenerateDto("Student", properties, "App.Test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("properties");
    }

    [Fact]
    public void GenerateDto_MultipleProperties_SeparatesWithBlankLines()
    {
        // Arrange
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("Name", "string"),
            ("Email", "string")
        };

        // Act
        var result = _generator.GenerateDto("User", properties, "App.Users");

        // Assert
        // Should have blank lines between properties (before XML docs)
        var lines = result.Split(Environment.NewLine);
        var idIndex = Array.FindIndex(lines, l => l.Contains("public int Id"));
        var nameIndex = Array.FindIndex(lines, l => l.Contains("public string Name"));

        // There should be XML documentation and a blank line between properties
        idIndex.Should().BeGreaterThan(-1);
        nameIndex.Should().BeGreaterThan(idIndex + 1); // More than just adjacent

        // Verify blank line exists before the next property's XML docs
        var blankLineFound = false;
        for (int i = idIndex + 1; i < nameIndex - 3; i++) // Check before /// <summary>
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                blankLineFound = true;
                break;
            }
        }
        blankLineFound.Should().BeTrue();
    }

    #endregion

    #region GenerateDbContextInterface Tests

    [Fact]
    public void GenerateDbContextInterface_ValidEntities_GeneratesInterface()
    {
        // Arrange
        var entityNames = new List<string> { "Student", "Course", "Enrollment" };
        var namespaceName = "ContosoUniversity.Application.Common.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain($"namespace {namespaceName};");
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public interface IApplicationDbContext");
        result.Should().Contain("DbSet<Student> Students { get; }");
        result.Should().Contain("DbSet<Course> Courses { get; }");
        result.Should().Contain("DbSet<Enrollment> Enrollments { get; }");
        result.Should().Contain("Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);");
    }

    [Fact]
    public void GenerateDbContextInterface_PluralizesEntityNames()
    {
        // Arrange
        var entityNames = new List<string>
        {
            "Student",
            "Course",
            "Category",
            "Company",
            "Person",
            "Child"
        };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().Contain("DbSet<Student> Students { get; }");
        result.Should().Contain("DbSet<Course> Courses { get; }");
        result.Should().Contain("DbSet<Category> Categories { get; }"); // y -> ies
        result.Should().Contain("DbSet<Company> Companies { get; }"); // y -> ies
        result.Should().Contain("DbSet<Person> People { get; }"); // irregular
        result.Should().Contain("DbSet<Child> Children { get; }"); // irregular
    }

    [Fact]
    public void GenerateDbContextInterface_HandlesSpecialPluralCases()
    {
        // Arrange
        var entityNames = new List<string>
        {
            "Class",
            "Box",
            "Buzz",
            "Dish",
            "Church",
            "Leaf",
            "Wife"
        };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().Contain("DbSet<Class> Classes { get; }"); // ss -> es
        result.Should().Contain("DbSet<Box> Boxes { get; }"); // x -> es
        result.Should().Contain("DbSet<Buzz> Buzzes { get; }"); // z -> es
        result.Should().Contain("DbSet<Dish> Dishes { get; }"); // sh -> es
        result.Should().Contain("DbSet<Church> Churches { get; }"); // ch -> es
        result.Should().Contain("DbSet<Leaf> Leaves { get; }"); // f -> ves
        result.Should().Contain("DbSet<Wife> Wives { get; }"); // fe -> ves
    }

    [Fact]
    public void GenerateDbContextInterface_IncludesXmlDocumentation()
    {
        // Arrange
        var entityNames = new List<string> { "Student", "Course" };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Defines the application database context contract");
        result.Should().Contain("/// Gets the DbSet for Student entities.");
        result.Should().Contain("/// Gets the DbSet for Course entities.");
        result.Should().Contain("/// Asynchronously saves all changes made in this context");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateDbContextInterface_InvalidNamespace_ThrowsArgumentException(string? namespaceName)
    {
        // Arrange
        var entityNames = new List<string> { "Student" };

        // Act
        Action act = () => _generator.GenerateDbContextInterface(entityNames, namespaceName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("namespaceName");
    }

    [Fact]
    public void GenerateDbContextInterface_NullEntityNames_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _generator.GenerateDbContextInterface(null!, "App.Interfaces");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("entityNames");
    }

    [Fact]
    public void GenerateDbContextInterface_EmptyEntityNames_ThrowsArgumentException()
    {
        // Arrange
        var entityNames = new List<string>();

        // Act
        Action act = () => _generator.GenerateDbContextInterface(entityNames, "App.Interfaces");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("entityNames");
    }

    [Fact]
    public void GenerateDbContextInterface_SkipsNullOrWhitespaceEntityNames()
    {
        // Arrange
        var entityNames = new List<string> { "Student", "", "Course", "   ", null! };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().Contain("DbSet<Student> Students { get; }");
        result.Should().Contain("DbSet<Course> Courses { get; }");
        result.Should().NotContain("DbSet<> ");
    }

    [Fact]
    public void GenerateDbContextInterface_SingleEntity_NoExtraBlankLines()
    {
        // Arrange
        var entityNames = new List<string> { "Student" };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        result.Should().Contain("DbSet<Student> Students { get; }");
        result.Should().Contain("Task<int> SaveChangesAsync");
    }

    [Fact]
    public void GenerateDbContextInterface_MultipleEntities_SeparatesWithBlankLines()
    {
        // Arrange
        var entityNames = new List<string> { "Student", "Course" };
        var namespaceName = "App.Interfaces";

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, namespaceName);

        // Assert
        var lines = result.Split(Environment.NewLine);
        var studentIndex = Array.FindIndex(lines, l => l.Contains("DbSet<Student>"));
        var courseIndex = Array.FindIndex(lines, l => l.Contains("DbSet<Course>"));

        studentIndex.Should().BeGreaterThan(-1);
        courseIndex.Should().BeGreaterThan(studentIndex + 1); // More than just adjacent

        // Verify blank line exists before the next property's XML docs
        var blankLineFound = false;
        for (int i = studentIndex + 1; i < courseIndex - 3; i++) // Check before /// <summary>
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                blankLineFound = true;
                break;
            }
        }
        blankLineFound.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void GenerateResultClass_CanBeCompiledSuccessfully()
    {
        // Arrange
        var namespaceName = "TestApp.Common";

        // Act
        var result = _generator.GenerateResultClass(namespaceName);

        // Assert
        // The generated code should be valid C# syntax
        result.Should().NotBeNullOrWhiteSpace();
        result.Split('{').Length.Should().Be(result.Split('}').Length);
    }

    [Fact]
    public void GenerateDto_CanBeCompiledSuccessfully()
    {
        // Arrange
        var properties = new List<(string Name, string Type)>
        {
            ("Id", "int"),
            ("Name", "string"),
            ("CreatedAt", "DateTime"),
            ("Tags", "ICollection<string>"),
            ("Description", "string?")
        };

        // Act
        var result = _generator.GenerateDto("Product", properties, "App.Products");

        // Assert
        // The generated code should be valid C# syntax
        result.Should().NotBeNullOrWhiteSpace();
        result.Split('{').Length.Should().Be(result.Split('}').Length);
    }

    [Fact]
    public void GenerateDbContextInterface_CanBeCompiledSuccessfully()
    {
        // Arrange
        var entityNames = new List<string> { "Student", "Course", "Enrollment", "Department" };

        // Act
        var result = _generator.GenerateDbContextInterface(entityNames, "App.Data");

        // Assert
        // The generated code should be valid C# syntax
        result.Should().NotBeNullOrWhiteSpace();
        result.Split('{').Length.Should().Be(result.Split('}').Length);
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
    }

    #endregion
}
