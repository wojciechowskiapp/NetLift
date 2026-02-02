using FluentAssertions;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Transforms.Modernization.Generators;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

public sealed class RepositoryGeneratorTests
{
    private readonly RepositoryGenerator _generator = new();

    #region GenerateBaseInterface Tests

    [Fact]
    public void GenerateBaseInterface_CreatesCorrectStructure()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Domain.Repositories";

        // Act
        var result = _generator.GenerateBaseInterface(namespaceName);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Domain.Repositories;");
        result.Should().Contain("public interface IRepository<T> where T : class");
        result.Should().Contain("Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);");
        result.Should().Contain("Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);");
        result.Should().Contain("Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);");
        result.Should().Contain("Task UpdateAsync(T entity, CancellationToken cancellationToken = default);");
        result.Should().Contain("Task DeleteAsync(T entity, CancellationToken cancellationToken = default);");
        result.Should().Contain("IQueryable<T> AsQueryable();");
    }

    [Fact]
    public void GenerateBaseInterface_IncludesXmlDocumentation()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Domain.Repositories";

        // Act
        var result = _generator.GenerateBaseInterface(namespaceName);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Base repository interface for generic entity operations.");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("/// <typeparam name=\"T\">The entity type</typeparam>");
        result.Should().Contain("/// Gets an entity by its identifier.");
        result.Should().Contain("/// <param name=\"id\">The entity identifier</param>");
        result.Should().Contain("/// <param name=\"cancellationToken\">The cancellation token</param>");
    }

    [Fact]
    public void GenerateBaseInterface_ThrowsForNullNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateBaseInterface(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateBaseInterface_ThrowsForEmptyNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateBaseInterface(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GenerateBaseClass Tests

    [Fact]
    public void GenerateBaseClass_CreatesCorrectStructure()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Infrastructure.Persistence.Repositories";

        // Act
        var result = _generator.GenerateBaseClass(namespaceName);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Infrastructure.Persistence.Repositories;");
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public class Repository<T> : IRepository<T> where T : class");
        result.Should().Contain("protected readonly ApplicationDbContext Context;");
        result.Should().Contain("protected readonly DbSet<T> DbSet;");
        result.Should().Contain("public Repository(ApplicationDbContext context)");
        result.Should().Contain("Context = context;");
        result.Should().Contain("DbSet = context.Set<T>();");
    }

    [Fact]
    public void GenerateBaseClass_ImplementsAllInterfaceMethods()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Infrastructure.Persistence.Repositories";

        // Act
        var result = _generator.GenerateBaseClass(namespaceName);

        // Assert
        result.Should().Contain("public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)");
        result.Should().Contain("return await DbSet.FindAsync(new object[] { id }, cancellationToken);");

        result.Should().Contain("public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)");
        result.Should().Contain("return await DbSet.ToListAsync(cancellationToken);");

        result.Should().Contain("public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)");
        result.Should().Contain("await DbSet.AddAsync(entity, cancellationToken);");
        result.Should().Contain("return entity;");

        result.Should().Contain("public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)");
        result.Should().Contain("Context.Entry(entity).State = EntityState.Modified;");
        result.Should().Contain("return Task.CompletedTask;");

        result.Should().Contain("public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)");
        result.Should().Contain("DbSet.Remove(entity);");

        result.Should().Contain("public virtual IQueryable<T> AsQueryable() => DbSet.AsQueryable();");
    }

    [Fact]
    public void GenerateBaseClass_IncludesXmlDocumentation()
    {
        // Arrange
        var namespaceName = "ContosoUniversity.Infrastructure.Persistence.Repositories";

        // Act
        var result = _generator.GenerateBaseClass(namespaceName);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Base repository implementation for generic entity operations.");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("/// <typeparam name=\"T\">The entity type</typeparam>");
        result.Should().Contain("/// The database context.");
        result.Should().Contain("/// The database set for the entity type.");
        result.Should().Contain("/// <inheritdoc />");
    }

    [Fact]
    public void GenerateBaseClass_ThrowsForNullNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateBaseClass(null!);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GenerateInterface Tests

    [Fact]
    public void GenerateInterface_CreatesBasicInterfaceWithNoCustomMethods()
    {
        // Arrange
        var entityName = "Student";
        var namespaceName = "ContosoUniversity.Domain.Repositories";
        var methods = Enumerable.Empty<RepositoryMethod>();

        // Act
        var result = _generator.GenerateInterface(entityName, namespaceName, methods);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Domain.Repositories;");
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Repository interface for Student entity.");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("public interface IStudentRepository : IRepository<Student>");
        result.Should().Contain("{");
        result.Should().Contain("}");
    }

    [Fact]
    public void GenerateInterface_IncludesCustomAsyncMethod()
    {
        // Arrange
        var entityName = "Student";
        var namespaceName = "ContosoUniversity.Domain.Repositories";
        var methods = new List<RepositoryMethod>
        {
            new()
            {
                Name = "GetByIdWithEnrollmentsAsync",
                ReturnType = "Task<Student?>",
                Parameters = new List<(string Type, string Name)>
                {
                    ("int", "id")
                },
                IsAsync = true
            }
        };

        // Act
        var result = _generator.GenerateInterface(entityName, namespaceName, methods);

        // Assert
        result.Should().Contain("public interface IStudentRepository : IRepository<Student>");
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// GetByIdWithEnrollmentsAsync");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("/// <param name=\"id\">The id</param>");
        result.Should().Contain("/// <param name=\"cancellationToken\">The cancellation token</param>");
        result.Should().Contain("/// <returns>Task<Student?></returns>");
        result.Should().Contain("Task<Student?> GetByIdWithEnrollmentsAsync(int id, CancellationToken cancellationToken = default);");
    }

    [Fact]
    public void GenerateInterface_IncludesMultipleCustomMethods()
    {
        // Arrange
        var entityName = "Student";
        var namespaceName = "ContosoUniversity.Domain.Repositories";
        var methods = new List<RepositoryMethod>
        {
            new()
            {
                Name = "GetByIdWithEnrollmentsAsync",
                ReturnType = "Task<Student?>",
                Parameters = new List<(string Type, string Name)>
                {
                    ("int", "id")
                },
                IsAsync = true
            },
            new()
            {
                Name = "GetPagedAsync",
                ReturnType = "Task<IPagedList<Student>>",
                Parameters = new List<(string Type, string Name)>
                {
                    ("string?", "searchString"),
                    ("string?", "sortOrder"),
                    ("int", "pageNumber"),
                    ("int", "pageSize")
                },
                IsAsync = true
            }
        };

        // Act
        var result = _generator.GenerateInterface(entityName, namespaceName, methods);

        // Assert
        result.Should().Contain("Task<Student?> GetByIdWithEnrollmentsAsync(int id, CancellationToken cancellationToken = default);");
        result.Should().Contain("Task<IPagedList<Student>> GetPagedAsync(string? searchString, string? sortOrder, int pageNumber, int pageSize, CancellationToken cancellationToken = default);");

        // Verify blank line between methods
        var lines = result.Split('\n', StringSplitOptions.TrimEntries);
        var getByIdIndex = Array.FindIndex(lines, l => l.Contains("GetByIdWithEnrollmentsAsync"));
        var getPagedIndex = Array.FindIndex(lines, l => l.Contains("GetPagedAsync"));

        // Should have documentation and a blank line between methods
        getPagedIndex.Should().BeGreaterThan(getByIdIndex + 1);
    }

    [Fact]
    public void GenerateInterface_HandlesSyncMethod()
    {
        // Arrange
        var entityName = "Student";
        var namespaceName = "ContosoUniversity.Domain.Repositories";
        var methods = new List<RepositoryMethod>
        {
            new()
            {
                Name = "GetCount",
                ReturnType = "int",
                Parameters = new List<(string Type, string Name)>(),
                IsAsync = false
            }
        };

        // Act
        var result = _generator.GenerateInterface(entityName, namespaceName, methods);

        // Assert
        result.Should().Contain("int GetCount();");
        result.Should().NotContain("cancellationToken");
    }

    [Fact]
    public void GenerateInterface_ThrowsForNullEntityName()
    {
        // Act & Assert
        var act = () => _generator.GenerateInterface(null!, "Namespace", Enumerable.Empty<RepositoryMethod>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateInterface_ThrowsForNullNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateInterface("Student", null!, Enumerable.Empty<RepositoryMethod>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateInterface_ThrowsForNullMethods()
    {
        // Act & Assert
        var act = () => _generator.GenerateInterface("Student", "Namespace", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GenerateImplementation Tests

    [Fact]
    public void GenerateImplementation_CreatesCorrectStructure()
    {
        // Arrange
        var entityName = "Student";
        var interfaceNamespace = "ContosoUniversity.Domain.Repositories";
        var implNamespace = "ContosoUniversity.Infrastructure.Persistence.Repositories";

        // Act
        var result = _generator.GenerateImplementation(entityName, interfaceNamespace, implNamespace);

        // Assert
        result.Should().Contain("namespace ContosoUniversity.Infrastructure.Persistence.Repositories;");
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("using ContosoUniversity.Domain.Repositories;");
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Repository implementation for Student entity.");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("public sealed class StudentRepository : Repository<Student>, IStudentRepository");
    }

    [Fact]
    public void GenerateImplementation_IncludesConstructor()
    {
        // Arrange
        var entityName = "Student";
        var interfaceNamespace = "ContosoUniversity.Domain.Repositories";
        var implNamespace = "ContosoUniversity.Infrastructure.Persistence.Repositories";

        // Act
        var result = _generator.GenerateImplementation(entityName, interfaceNamespace, implNamespace);

        // Assert
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Initializes a new instance of the <see cref=\"StudentRepository\"/> class.");
        result.Should().Contain("/// </summary>");
        result.Should().Contain("/// <param name=\"context\">The database context</param>");
        result.Should().Contain("public StudentRepository(ApplicationDbContext context) : base(context) { }");
    }

    [Fact]
    public void GenerateImplementation_ThrowsForNullEntityName()
    {
        // Act & Assert
        var act = () => _generator.GenerateImplementation(null!, "Interface.Namespace", "Impl.Namespace");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateImplementation_ThrowsForNullInterfaceNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateImplementation("Student", null!, "Impl.Namespace");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateImplementation_ThrowsForNullImplNamespace()
    {
        // Act & Assert
        var act = () => _generator.GenerateImplementation("Student", "Interface.Namespace", null!);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void GenerateInterface_ProducesCompilableCode()
    {
        // Arrange
        var entityName = "Product";
        var namespaceName = "ECommerce.Domain.Repositories";
        var methods = new List<RepositoryMethod>
        {
            new()
            {
                Name = "GetBySkuAsync",
                ReturnType = "Task<Product?>",
                Parameters = new List<(string Type, string Name)>
                {
                    ("string", "sku")
                },
                IsAsync = true
            },
            new()
            {
                Name = "GetByCategoryAsync",
                ReturnType = "Task<IEnumerable<Product>>",
                Parameters = new List<(string Type, string Name)>
                {
                    ("int", "categoryId")
                },
                IsAsync = true
            }
        };

        // Act
        var result = _generator.GenerateInterface(entityName, namespaceName, methods);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("namespace ECommerce.Domain.Repositories;");
        result.Should().Contain("public interface IProductRepository : IRepository<Product>");
        result.Should().Contain("Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);");
        result.Should().Contain("Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);");
    }

    [Fact]
    public void GenerateImplementation_ProducesCompilableCode()
    {
        // Arrange
        var entityName = "Order";
        var interfaceNamespace = "ECommerce.Domain.Repositories";
        var implNamespace = "ECommerce.Infrastructure.Repositories";

        // Act
        var result = _generator.GenerateImplementation(entityName, interfaceNamespace, implNamespace);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("namespace ECommerce.Infrastructure.Repositories;");
        result.Should().Contain("using ECommerce.Domain.Repositories;");
        result.Should().Contain("public sealed class OrderRepository : Repository<Order>, IOrderRepository");
        result.Should().Contain("public OrderRepository(ApplicationDbContext context) : base(context) { }");
    }

    [Fact]
    public void GenerateBaseInterface_And_GenerateBaseClass_AreCompatible()
    {
        // Arrange
        var interfaceNamespace = "App.Domain.Repositories";
        var classNamespace = "App.Infrastructure.Repositories";

        // Act
        var interfaceCode = _generator.GenerateBaseInterface(interfaceNamespace);
        var classCode = _generator.GenerateBaseClass(classNamespace);

        // Assert - both should have compatible method signatures
        interfaceCode.Should().Contain("Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);");
        classCode.Should().Contain("public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)");

        interfaceCode.Should().Contain("IQueryable<T> AsQueryable();");
        classCode.Should().Contain("public virtual IQueryable<T> AsQueryable() => DbSet.AsQueryable();");
    }

    #endregion
}
