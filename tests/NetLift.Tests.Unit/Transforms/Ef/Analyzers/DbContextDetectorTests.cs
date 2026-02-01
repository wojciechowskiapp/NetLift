using FluentAssertions;
using NetLift.Transforms.Ef.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Ef.Analyzers;

public sealed class DbContextDetectorTests
{
    private readonly DbContextDetector _detector = new();

    [Fact]
    public void Detect_SimpleDbContext_ReturnsBasicInfo()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.ClassName.Should().Be("AppDbContext");
        context.Namespace.Should().Be("TestApp.Data");
        context.HasOnModelCreating.Should().BeFalse();
        context.DbSets.Should().BeEmpty();
        context.Constructors.Should().BeEmpty();
        context.UsesConnectionStringName.Should().BeFalse();
        context.ConnectionStringName.Should().BeNull();
    }

    [Fact]
    public void Detect_QualifiedDbContext_DetectsCorrectly()
    {
        // Arrange
        var source = @"
namespace TestApp.Data
{
    public class AppDbContext : System.Data.Entity.DbContext
    {
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].ClassName.Should().Be("AppDbContext");
        result[0].Namespace.Should().Be("TestApp.Data");
    }

    [Fact]
    public void Detect_DbContextWithDbSets_ExtractsAllDbSets()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public virtual DbSet<Order> Orders { get; set; }
        public DbSet<Product> Products { get; set; }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.DbSets.Should().HaveCount(3);
        context.DbSets[0].PropertyName.Should().Be("Customers");
        context.DbSets[0].EntityTypeName.Should().Be("Customer");
        context.DbSets[1].PropertyName.Should().Be("Orders");
        context.DbSets[1].EntityTypeName.Should().Be("Order");
        context.DbSets[2].PropertyName.Should().Be("Products");
        context.DbSets[2].EntityTypeName.Should().Be("Product");
    }

    [Fact]
    public void Detect_DbContextWithOnModelCreating_DetectsMethod()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasOnModelCreating.Should().BeTrue();
    }

    [Fact]
    public void Detect_ParameterlessConstructor_DetectsConstructor()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.Constructors.Should().HaveCount(1);
        context.Constructors[0].Parameters.Should().BeEmpty();
        context.Constructors[0].HasBaseCall.Should().BeFalse();
        context.Constructors[0].BaseCallArgument.Should().BeNull();
    }

    [Fact]
    public void Detect_ConstructorWithConnectionStringName_ExtractsConnectionString()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.Constructors.Should().HaveCount(1);
        context.Constructors[0].HasBaseCall.Should().BeTrue();
        context.Constructors[0].BaseCallArgument.Should().Be("\"name=DefaultConnection\"");
        context.UsesConnectionStringName.Should().BeTrue();
        context.ConnectionStringName.Should().Be("DefaultConnection");
    }

    [Fact]
    public void Detect_ConstructorWithDirectConnectionStringName_ExtractsName()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""AppConnection"")
        {
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.UsesConnectionStringName.Should().BeTrue();
        context.ConnectionStringName.Should().Be("AppConnection");
    }

    [Fact]
    public void Detect_ConstructorWithParameter_DetectsParameter()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(string connectionString) : base(connectionString)
        {
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.Constructors.Should().HaveCount(1);
        context.Constructors[0].Parameters.Should().HaveCount(1);
        context.Constructors[0].Parameters[0].Name.Should().Be("connectionString");
        context.Constructors[0].Parameters[0].TypeName.Should().Be("string");
        context.Constructors[0].HasBaseCall.Should().BeTrue();
        context.Constructors[0].BaseCallArgument.Should().Be("connectionString");
    }

    [Fact]
    public void Detect_MultipleConstructors_DetectsAll()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }

        public AppDbContext(string connectionString) : base(connectionString)
        {
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.Constructors.Should().HaveCount(2);
        context.Constructors[0].Parameters.Should().BeEmpty();
        context.Constructors[0].HasBaseCall.Should().BeTrue();
        context.Constructors[1].Parameters.Should().HaveCount(1);
        context.Constructors[1].HasBaseCall.Should().BeTrue();
    }

    [Fact]
    public void Detect_ComplexDbContext_ExtractsAllInformation()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }

        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }

        public AppDbContext(string connectionString) : base(connectionString)
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>()
                .HasKey(c => c.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.ClassName.Should().Be("AppDbContext");
        context.Namespace.Should().Be("TestApp.Data");
        context.DbSets.Should().HaveCount(2);
        context.Constructors.Should().HaveCount(2);
        context.HasOnModelCreating.Should().BeTrue();
        context.UsesConnectionStringName.Should().BeTrue();
        context.ConnectionStringName.Should().Be("DefaultConnection");
    }

    [Fact]
    public void Detect_MultipleDbContexts_DetectsAll()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
    }

    public class ReportingDbContext : DbContext
    {
        public DbSet<Report> Reports { get; set; }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(2);
        result[0].ClassName.Should().Be("AppDbContext");
        result[0].DbSets.Should().HaveCount(1);
        result[0].DbSets[0].PropertyName.Should().Be("Customers");
        result[1].ClassName.Should().Be("ReportingDbContext");
        result[1].DbSets.Should().HaveCount(1);
        result[1].DbSets[0].PropertyName.Should().Be("Reports");
    }

    [Fact]
    public void Detect_NonDbContextClass_ReturnsEmpty()
    {
        // Arrange
        var source = @"
namespace TestApp.Data
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Detect_EmptySource_ReturnsEmpty()
    {
        // Act
        var result = _detector.Detect(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Detect_NullSource_ReturnsEmpty()
    {
        // Act
        var result = _detector.Detect(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ContainsDbContext_WithDbContext_ReturnsTrue()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
    }
}";

        // Act
        var result = _detector.ContainsDbContext(source);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsDbContext_WithoutDbContext_ReturnsFalse()
    {
        // Arrange
        var source = @"
namespace TestApp.Data
{
    public class Customer
    {
        public int Id { get; set; }
    }
}";

        // Act
        var result = _detector.ContainsDbContext(source);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Detect_FileScopedNamespace_ExtractsNamespace()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Namespace.Should().Be("TestApp.Data");
    }

    [Fact]
    public void Detect_NestedDbContext_DetectsCorrectly()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public static class DbContexts
    {
        public class AppDbContext : DbContext
        {
            public DbSet<Customer> Customers { get; set; }
        }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].ClassName.Should().Be("AppDbContext");
        result[0].Namespace.Should().Be("TestApp.Data");
        result[0].DbSets.Should().HaveCount(1);
    }

    [Fact]
    public void Detect_DbContextWithComplexGenericType_ExtractsEntityType()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Dictionary<string, object>> ComplexEntities { get; set; }
    }
}";

        // Act
        var result = _detector.Detect(source);

        // Assert
        result.Should().HaveCount(1);
        var context = result[0];
        context.DbSets.Should().HaveCount(1);
        context.DbSets[0].PropertyName.Should().Be("ComplexEntities");
        context.DbSets[0].EntityTypeName.Should().Be("Dictionary<string, object>");
    }
}
