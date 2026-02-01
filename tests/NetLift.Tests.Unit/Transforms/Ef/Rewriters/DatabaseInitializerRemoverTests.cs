using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

/// <summary>
/// Tests for DatabaseInitializerRemover.
/// Validates removal of EF6 Database.SetInitializer calls and addition of migration guidance.
/// </summary>
public sealed class DatabaseInitializerRemoverTests
{
    private readonly IDatabaseInitializerRemover _remover = new DatabaseInitializerRemover();

    [Fact]
    public void Rewrite_CreateDatabaseIfNotExists_RemovesAndAddsGuidance()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        static ApplicationDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<ApplicationDbContext>());
        }

        public DbSet<Product> Products { get; set; }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance for CreateDatabaseIfNotExists");
        result.Should().Contain("db.Database.Migrate()");
        result.Should().Contain("db.Database.EnsureCreated()");
        result.Should().Contain("dotnet ef migrations add InitialCreate");
        result.Should().Contain("// REMOVED: Database.SetInitializer(new CreateDatabaseIfNotExists<ApplicationDbContext>());");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.ConfidenceScore.Should().Be(95);
        _remover.RequiredUsings.Should().BeEmpty();
        _remover.RemovedInitializers.Should().HaveCount(1);
        _remover.RemovedInitializers.First().InitializerType.Should().Be("CreateDatabaseIfNotExists");
        _remover.RemovedInitializers.First().ContextType.Should().Be("ApplicationDbContext");

        _remover.Diagnostics.Should().ContainSingle(d =>
            d.Message.Contains("CreateDatabaseIfNotExists") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void Rewrite_MigrateDatabaseToLatestVersion_RemovesAndAddsGuidance()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;
using System.Data.Entity.Migrations;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppDbContext, Configuration>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance for MigrateDatabaseToLatestVersion");
        result.Should().Contain("db.Database.Migrate()");
        result.Should().Contain("dotnet ef migrations add MigrationName");
        result.Should().Contain("dotnet ef migrations list");
        result.Should().Contain("// REMOVED: Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppDbContext, Configuration>());");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.ConfidenceScore.Should().Be(95);
        _remover.RemovedInitializers.Should().HaveCount(1);
        _remover.RemovedInitializers.First().InitializerType.Should().Be("MigrateDatabaseToLatestVersion");
        _remover.RemovedInitializers.First().ContextType.Should().Be("AppDbContext");
    }

    [Fact]
    public void Rewrite_DropCreateDatabaseIfModelChanges_RemovesAndAddsWarning()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class TestDbContext : DbContext
    {
        static TestDbContext()
        {
            Database.SetInitializer(new DropCreateDatabaseIfModelChanges<TestDbContext>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance for DropCreateDatabaseIfModelChanges");
        result.Should().Contain("// WARNING: No direct equivalent in EF Core");
        result.Should().Contain("db.Database.EnsureDeleted()");
        result.Should().Contain("db.Database.EnsureCreated()");
        result.Should().Contain("Production approach: Use migrations instead");
        result.Should().Contain("// REMOVED: Database.SetInitializer(new DropCreateDatabaseIfModelChanges<TestDbContext>());");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.ConfidenceScore.Should().Be(95);
        _remover.RemovedInitializers.First().InitializerType.Should().Be("DropCreateDatabaseIfModelChanges");
    }

    [Fact]
    public void Rewrite_DropCreateDatabaseAlways_RemovesAndAddsWarning()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class TestDbContext : DbContext
    {
        public TestDbContext()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<TestDbContext>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance for DropCreateDatabaseAlways");
        result.Should().Contain("// WARNING: Only suitable for development/testing");
        result.Should().Contain("db.Database.EnsureDeleted()");
        result.Should().Contain("db.Database.EnsureCreated()");
        result.Should().Contain("DEVELOPMENT ONLY");
        result.Should().Contain("For production: Use migrations");
        result.Should().Contain("// REMOVED: Database.SetInitializer(new DropCreateDatabaseAlways<TestDbContext>());");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.ConfidenceScore.Should().Be(95);
        _remover.RemovedInitializers.First().InitializerType.Should().Be("DropCreateDatabaseAlways");
    }

    [Fact]
    public void Rewrite_NullInitializer_RemovesAndAddsGuidance()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer<AppDbContext>(null);
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance");
        result.Should().Contain("Database.SetInitializer<AppDbContext>(null) has been removed");
        result.Should().Contain("EF Core does not auto-initialize databases by default");
        result.Should().Contain("context.Database.Migrate()");
        result.Should().Contain("context.Database.EnsureCreated()");
        result.Should().Contain("// REMOVED: Database.SetInitializer<AppDbContext>(null);");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"),
            "the active statement should be removed");

        _remover.ConfidenceScore.Should().Be(95);
        _remover.RemovedInitializers.First().InitializerType.Should().Be("null");
        _remover.RemovedInitializers.First().ContextType.Should().Be("AppDbContext");
    }

    [Fact]
    public void Rewrite_StaticConstructorWithOnlyInitializer_PreservesStructure()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
        }

        public DbSet<Order> Orders { get; set; }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("static AppDbContext()");
        result.Should().Contain("// TODO: EF Core migration guidance");
        result.Should().Contain("// REMOVED: Database.SetInitializer");
        result.Should().Contain("public DbSet<Order> Orders { get; set; }");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.RemovedInitializers.Should().HaveCount(1);
    }

    [Fact]
    public void Rewrite_StaticConstructorWithAdditionalCode_PreservesOtherCode()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
            // Other initialization code
            ConfigureLogging();
        }

        private static void ConfigureLogging() { }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("ConfigureLogging()");
        result.Should().Contain("// TODO: EF Core migration guidance");
        result.Should().Contain("// REMOVED: Database.SetInitializer");

        // Verify the active (non-commented) statement is removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"));

        _remover.RemovedInitializers.Should().HaveCount(1);
    }

    [Fact]
    public void Rewrite_MultipleInitializersInDifferentContexts_RemovesAll()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
        }
    }

    public class TestDbContext : DbContext
    {
        static TestDbContext()
        {
            Database.SetInitializer<TestDbContext>(null);
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance for CreateDatabaseIfNotExists");
        result.Should().Contain("// TODO: EF Core migration guidance");
        result.Should().Contain("// REMOVED: Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());");
        result.Should().Contain("// REMOVED: Database.SetInitializer<TestDbContext>(null);");

        // Verify the active (non-commented) statements are removed
        var lines = result.Split('\n');
        lines.Should().NotContain(line =>
            line.Contains("Database.SetInitializer") &&
            !line.TrimStart().StartsWith("//"),
            "active statements should be removed");

        _remover.RemovedInitializers.Should().HaveCount(2);
        _remover.RemovedInitializers.Should().Contain(r => r.InitializerType == "CreateDatabaseIfNotExists");
        _remover.RemovedInitializers.Should().Contain(r => r.InitializerType == "null");
    }

    [Fact]
    public void Rewrite_IndentationPreserved_MaintainsFormatting()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        // The guidance comment should be indented properly within the static constructor
        result.Should().Contain("        {");
        result.Should().Contain("            // TODO: EF Core migration guidance");
        result.Should().Contain("            // REMOVED:");
    }

    [Fact]
    public void Rewrite_NonInitializerCode_RemainsUnchanged()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().HasKey(p => p.Id);
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Be(sourceCode, "no Database.SetInitializer calls should leave code unchanged");
        _remover.RemovedInitializers.Should().BeEmpty();
        _remover.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Rewrite_EmptyOrNullSource_ReturnsUnchanged()
    {
        // Act & Assert
        _remover.Rewrite("").Should().Be("");
        _remover.Rewrite("   ").Should().Be("   ");
        _remover.Rewrite(null!).Should().Be(null);
    }

    [Fact]
    public void Rewrite_ContextTypeExtraction_HandlesGenericParameter()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class MyCustomDbContext : DbContext
    {
        static MyCustomDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<MyCustomDbContext>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        _remover.RemovedInitializers.Should().HaveCount(1);
        _remover.RemovedInitializers.First().ContextType.Should().Be("MyCustomDbContext");
        _remover.RemovedInitializers.First().InitializerType.Should().Be("CreateDatabaseIfNotExists");

        result.Should().Contain("GetRequiredService<MyCustomDbContext>");
    }

    [Fact]
    public void Rewrite_GuidanceComments_ContainProgramCsExample()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
        }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Add to Program.cs after app.Build():");
        result.Should().Contain("using (var scope = app.Services.CreateScope())");
        result.Should().Contain("GetRequiredService<AppDbContext>");
    }

    [Fact]
    public void Rewrite_CustomInitializer_AddsGenericGuidance()
    {
        // Note: This test covers edge cases where a custom initializer might be used
        // The parser should handle unknown initializer types gracefully
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer(new MyCustomInitializer<AppDbContext>());
        }
    }

    public class MyCustomInitializer<T> : IDatabaseInitializer<T> where T : DbContext
    {
        public void InitializeDatabase(T context) { }
    }
}";

        // Act
        var result = _remover.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// TODO: EF Core migration guidance");
        result.Should().Contain("Custom initializer 'MyCustomInitializer' has been removed");
        result.Should().Contain("OnModelCreating()");
        result.Should().Contain("Database.Migrate()");

        _remover.RemovedInitializers.First().InitializerType.Should().Be("MyCustomInitializer");
    }
}
