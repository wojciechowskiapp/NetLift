using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

/// <summary>
/// Tests for SqlQueryRewriter.
/// Validates transformation of EF6 Database.SqlQuery and ExecuteSqlCommand patterns to EF Core patterns.
/// </summary>
public sealed class SqlQueryRewriterTests
{
    private readonly ISqlQueryRewriter _rewriter = new SqlQueryRewriter();

    [Fact]
    public void Rewrite_SimpleSqlQuery_TransformsToFromSqlRaw()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(int categoryId)
        {
            return Database.SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId).ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("Products.FromSqlRaw(\"SELECT * FROM Products WHERE CategoryId = {0}\", categoryId)");
        result.Should().NotContain("Database.SqlQuery");
        result.Should().NotContain("@p0");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.RequiredUsings.Should().Contain("Microsoft.EntityFrameworkCore");
        _rewriter.Diagnostics.Should().HaveCountGreaterThan(0);
        _rewriter.KeylessTypesDetected.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_InterpolatedString_TransformsToFromSqlInterpolated()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(decimal minPrice)
        {
            return Database.SqlQuery<Product>($""SELECT * FROM Products WHERE Price > {minPrice}"").ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("Products.FromSqlInterpolated($\"SELECT * FROM Products WHERE Price > {minPrice}\")");
        result.Should().NotContain("Database.SqlQuery");

        _rewriter.ConfidenceScore.Should().Be(90);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("FromSqlInterpolated") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
        _rewriter.KeylessTypesDetected.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_MultiplePlaceholders_ConvertsAll()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(int categoryId, decimal minPrice, decimal maxPrice)
        {
            return Database.SqlQuery<Product>(
                ""SELECT * FROM Products WHERE CategoryId = @p0 AND Price >= @p1 AND Price <= @p2"",
                categoryId, minPrice, maxPrice).ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain("CategoryId = {0}");
        result.Should().Contain("Price >= {1}");
        result.Should().Contain("Price <= {2}");
        result.Should().NotContain("@p0");
        result.Should().NotContain("@p1");
        result.Should().NotContain("@p2");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_ExecuteSqlCommand_TransformsToExecuteSqlRaw()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public void UpdatePrices(int categoryId)
        {
            Database.ExecuteSqlCommand(
                ""UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = @p0"",
                categoryId);
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("Database.ExecuteSqlRaw");
        result.Should().Contain("CategoryId = {0}");
        result.Should().NotContain("ExecuteSqlCommand");
        result.Should().NotContain("@p0");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("ExecuteSqlRaw") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void Rewrite_KeylessEntityType_UsesSetMethodAndAddsTodo()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public List<ProductSummary> GetSummaries()
        {
            return Database.SqlQuery<ProductSummary>(
                ""SELECT CategoryId, COUNT(*) AS Count FROM Products GROUP BY CategoryId"").ToList();
        }
    }

    public class ProductSummary
    {
        public int CategoryId { get; set; }
        public int Count { get; set; }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("using Microsoft.EntityFrameworkCore;");
        result.Should().Contain("Set<ProductSummary>().FromSqlRaw");
        result.Should().NotContain("ProductSummaries"); // Should not try to pluralize

        _rewriter.ConfidenceScore.Should().Be(80); // Lower confidence for keyless entity
        _rewriter.KeylessTypesDetected.Should().Contain("ProductSummary");
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("keyless entity") &&
            d.Message.Contains("ProductSummary") &&
            d.Severity == RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_MultipleInvocations_TransformsAll()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }

        public void ProcessData(int categoryId)
        {
            var products = Database.SqlQuery<Product>(
                ""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId).ToList();

            var categories = Database.SqlQuery<Category>(
                ""SELECT * FROM Categories WHERE Id = @p0"", categoryId).ToList();

            Database.ExecuteSqlCommand(""DELETE FROM Products WHERE CategoryId = @p0"", categoryId);
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain("Categories.FromSqlRaw");
        result.Should().Contain("Database.ExecuteSqlRaw");
        result.Should().NotContain("Database.SqlQuery");
        result.Should().NotContain("ExecuteSqlCommand");

        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().HaveCount(3);
    }

    [Fact]
    public void Rewrite_SqlParameterObjects_PreservesThem()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;
using System.Data.SqlClient;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(int categoryId)
        {
            var param = new SqlParameter(""@p0"", categoryId);
            return Database.SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", param).ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain("new SqlParameter");
        result.Should().Contain("CategoryId = {0}");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_NonEfCode_Unchanged()
    {
        // Arrange
        var sourceCode = @"namespace MyApp.Services
{
    public class ProductService
    {
        public void DoSomething()
        {
            var query = ""SELECT * FROM Products"";
            ExecuteSqlCommand(query);
        }

        private void ExecuteSqlCommand(string query)
        {
            // Custom implementation
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeEquivalentTo(sourceCode);
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.Diagnostics.Should().BeEmpty();
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
    public void Rewrite_MethodChaining_Preserves()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public Product GetFirstProduct(int categoryId)
        {
            return Database.SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId)
                .OrderBy(p => p.Name)
                .FirstOrDefault();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain(".OrderBy(p => p.Name)");
        result.Should().Contain(".FirstOrDefault()");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_ComplexQuery_HandlesCorrectly()
    {
        // Arrange - Direct inline SQL string
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(int categoryId, string searchTerm)
        {
            return Database.SqlQuery<Product>(
                @""SELECT p.*
                FROM Products p
                INNER JOIN Categories c ON p.CategoryId = c.Id
                WHERE p.CategoryId = @p0
                AND p.Name LIKE '%' + @p1 + '%'"",
                categoryId, searchTerm).ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain("p.CategoryId = {0}");
        result.Should().Contain("p.Name LIKE '%' + {1} + '%'");
        result.Should().NotContain("@p0");
        result.Should().NotContain("@p1");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_Pluralization_HandlesCommonCases()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Category> Categories { get; set; }
        public DbSet<Box> Boxes { get; set; }
        public DbSet<Entry> Entries { get; set; }

        public void Test()
        {
            var categories = Database.SqlQuery<Category>(""SELECT * FROM Categories"").ToList();
            var boxes = Database.SqlQuery<Box>(""SELECT * FROM Boxes"").ToList();
            var entries = Database.SqlQuery<Entry>(""SELECT * FROM Entries"").ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Categories.FromSqlRaw");
        result.Should().Contain("Boxes.FromSqlRaw");
        result.Should().Contain("Entries.FromSqlRaw");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_DtoTypes_UsesSetMethod()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public List<ProductDTO> GetProducts()
        {
            return Database.SqlQuery<ProductDTO>(""SELECT Id, Name FROM Products"").ToList();
        }
    }

    public class ProductDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Set<ProductDTO>().FromSqlRaw");
        result.Should().NotContain("ProductDTOs"); // Should not pluralize DTOs

        _rewriter.ConfidenceScore.Should().Be(80);
        _rewriter.KeylessTypesDetected.Should().Contain("ProductDTO");
    }

    [Fact]
    public void Rewrite_ViewModelTypes_UsesSetMethod()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public List<ProductViewModel> GetProducts()
        {
            return Database.SqlQuery<ProductViewModel>(""SELECT Id, Name FROM Products"").ToList();
        }
    }

    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Set<ProductViewModel>().FromSqlRaw");

        _rewriter.ConfidenceScore.Should().Be(80);
        _rewriter.KeylessTypesDetected.Should().Contain("ProductViewModel");
    }

    [Fact]
    public void Rewrite_NoPlaceholders_PreservesSql()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetAllProducts()
        {
            return Database.SqlQuery<Product>(""SELECT * FROM Products"").ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw(\"SELECT * FROM Products\")");
        result.Should().NotContain("Database.SqlQuery");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_MixedInterpolatedAndRegular_TransformsCorrectly()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public void Test(int categoryId, decimal minPrice)
        {
            var products1 = Database.SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId).ToList();
            var products2 = Database.SqlQuery<Product>($""SELECT * FROM Products WHERE Price > {minPrice}"").ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw(\"SELECT * FROM Products WHERE CategoryId = {0}\"");
        result.Should().Contain("Products.FromSqlInterpolated($\"SELECT * FROM Products WHERE Price > {minPrice}\")");

        _rewriter.ConfidenceScore.Should().Be(90); // Lowest of the two
    }

    [Fact]
    public void Rewrite_NestedContextUsage_TransformsCorrectly()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class Repository
    {
        private readonly AppDbContext _context;

        public Repository(AppDbContext context)
        {
            _context = context;
        }

        public List<Product> GetProducts(int categoryId)
        {
            return _context.Database.SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId).ToList();
        }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("_context.Products.FromSqlRaw");
        result.Should().Contain("CategoryId = {0}");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_VerbatimString_PreservesFormat()
    {
        // Arrange
        var sourceCode = @"using System.Data.Entity;

namespace MyApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public List<Product> GetProducts(int categoryId)
        {
            return Database.SqlQuery<Product>(@""SELECT *
                FROM Products
                WHERE CategoryId = @p0"", categoryId).ToList();
        }
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("Products.FromSqlRaw");
        result.Should().Contain("CategoryId = {0}");

        _rewriter.ConfidenceScore.Should().Be(95);
    }
}
