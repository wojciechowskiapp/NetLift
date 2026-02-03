using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

/// <summary>
/// Tests for DbContextConstructorRewriter.
/// Validates transformation of EF6 DbContext constructor patterns to EF Core patterns.
/// </summary>
public sealed class DbContextConstructorRewriterTests
{
    private readonly IDbContextConstructorRewriter _rewriter = new DbContextConstructorRewriter();

    [Fact]
    public void Rewrite_ParameterlessConstructor_TransformsToEfCorePattern()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().NotContain("public AppDbContext()");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.RequiredUsings.Should().Contain("Microsoft.EntityFrameworkCore");
        _rewriter.Diagnostics.Should().HaveCountGreaterThan(0);
        _rewriter.RemovedConnectionStrings.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_ConnectionStringNameConstructor_TransformsAndTracksConnectionString()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().NotContain("name=DefaultConnection");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.RemovedConnectionStrings.Should().HaveCount(1);
        _rewriter.RemovedConnectionStrings.First().ContextName.Should().Be("AppDbContext");
        _rewriter.RemovedConnectionStrings.First().ConnectionStringName.Should().Be("DefaultConnection");

        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("DefaultConnection") &&
            d.Severity == RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_ConnectionStringParameterConstructor_TransformsToEfCorePattern()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(string connectionString) : base(connectionString)
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().NotContain("string connectionString");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.RemovedConnectionStrings.Should().BeEmpty(); // No "name=" pattern
    }

    [Fact]
    public void Rewrite_ConstructorWithCustomLogic_PreservesLogic()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().Contain("Configuration.LazyLoadingEnabled = false;");
        result.Should().Contain("Configuration.ProxyCreationEnabled = false;");

        _rewriter.ConfidenceScore.Should().Be(75); // Lower confidence due to custom logic
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("custom logic") &&
            d.Severity == RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_MultipleConstructors_TransformsRuntimeConstructorOnly()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
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
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("DbContextOptions<AppDbContext>");

        _rewriter.ConfidenceScore.Should().Be(80); // Lower confidence for multiple constructors
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("2 constructors") &&
            d.Severity == RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_AlreadyCorrectConstructor_NoChanges()
    {
        // Arrange
        var sourceCode = @"using Microsoft.EntityFrameworkCore;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeEquivalentTo(sourceCode);
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("already has EF Core constructor"));
    }

    [Fact]
    public void Rewrite_QualifiedDbContextBaseName_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : System.Data.Entity.DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.RemovedConnectionStrings.Should().HaveCount(1);
    }

    [Fact]
    public void Rewrite_NoConstructors_AddsEfCoreConstructor()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert - Constructor should be added for DI support
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("no explicit constructors") && d.Message.Contains("adding EF Core constructor"));
    }

    [Fact]
    public void Rewrite_NonDbContextClass_NoChanges()
    {
        // Arrange
        var sourceCode = @"namespace MyApp.Services
{
    public class UserService
    {
        public UserService()
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeEquivalentTo(sourceCode);
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.RequiredUsings.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_EmptySourceCode_ReturnsEmpty()
    {
        // Arrange
        var sourceCode = "";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Rewrite_ConstructorWithBaseOptionsParameter_PreservesIt()
    {
        // Arrange
        var sourceCode = @"using Microsoft.EntityFrameworkCore;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeEquivalentTo(sourceCode);
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("already has EF Core constructor"));
    }

    [Fact]
    public void Rewrite_DesignTimeConstructor_PreservesWithRuntimeConstructor()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        // Design-time constructor for migrations
        public AppDbContext() : base(""name=DesignTimeConnection"")
        {
        }

        public AppDbContext(string connectionString) : base(connectionString)
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("DbContextOptions<AppDbContext>");
        _rewriter.ConfidenceScore.Should().Be(80);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("design-time constructor"));
    }

    [Fact]
    public void Rewrite_MultipleDbContextClasses_TransformsAll()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
        }
    }

    public class AuditDbContext : DbContext
    {
        public AuditDbContext() : base(""name=AuditConnection"")
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options)");
        result.Should().Contain("public AuditDbContext(DbContextOptions<AuditDbContext> options)");

        _rewriter.RemovedConnectionStrings.Should().HaveCount(2);
        _rewriter.RemovedConnectionStrings.Should().Contain(c =>
            c.ContextName == "AppDbContext" && c.ConnectionStringName == "DefaultConnection");
        _rewriter.RemovedConnectionStrings.Should().Contain(c =>
            c.ContextName == "AuditDbContext" && c.ConnectionStringName == "AuditConnection");
    }

    [Fact]
    public void Rewrite_NestedDbContext_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class DataModule
    {
        public class AppDbContext : DbContext
        {
            public AppDbContext()
            {
            }
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_ConstructorWithComments_PreservesComments()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Creates a new instance of AppDbContext.
        /// </summary>
        public AppDbContext()
        {
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("DbContextOptions<AppDbContext>");
        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_ComplexCustomLogic_PreservesAllStatements()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base(""name=DefaultConnection"")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
            Database.SetInitializer<AppDbContext>(null);
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)");
        result.Should().Contain("Configuration.LazyLoadingEnabled = false;");
        result.Should().Contain("Configuration.ProxyCreationEnabled = false;");
        result.Should().Contain("Database.SetInitializer<AppDbContext>(null);");

        _rewriter.ConfidenceScore.Should().Be(75);
        _rewriter.RemovedConnectionStrings.Should().HaveCount(1);
        _rewriter.RemovedConnectionStrings.First().ConnectionStringName.Should().Be("DefaultConnection");
    }
}
