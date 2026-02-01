using FluentAssertions;
using NetLift.Transforms.Ef.Rewriters;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

public sealed class IncludeThenIncludeRewriterTests
{
    private readonly IncludeThenIncludeRewriter _rewriter = new();

    [Fact]
    public void Rewrite_WithNestedSelect_TransformsToThenInclude()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().NotContain(".Select(");
        result.Should().Contain("using Microsoft.EntityFrameworkCore");

        _rewriter.RequiredUsings.Should().Contain("Microsoft.EntityFrameworkCore");
        _rewriter.ConfidenceScore.Should().Be(90);
        _rewriter.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public void Rewrite_WithMultipleLevels_TransformsToChainedThenInclude()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Items.Select(i => i.Product.Category))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("ThenInclude(z => z.Category)");
        result.Should().NotContain(".Select(");

        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void Rewrite_WithStringBasedInclude_TransformsToThenInclude()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include("Items.Product.Category")
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(x => x.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("ThenInclude(z => z.Category)");
        result.Should().NotContain("Include(\"Items.Product.Category\")");

        _rewriter.ConfidenceScore.Should().Be(75);
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("TODO"));
    }

    [Fact]
    public void Rewrite_WithSimpleInclude_RemainsUnchanged()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Customer)
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Customer)");
        result.Should().NotContain("ThenInclude");

        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_WithMultipleIncludes_TransformsAll()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .Include(o => o.Customer.Address)
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("Include(o => o.Customer)");
        result.Should().Contain("ThenInclude(y => y.Address)");

        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void Rewrite_WithStringBasedSingleLevel_RemainsUnchanged()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include("Customer")
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(\"Customer\")");
        result.Should().NotContain("ThenInclude");

        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Rewrite_WithComplexNestedSelect_TransformsCorrectly()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadData()
                {
                    var data = Customers
                        .Include(c => c.Orders.Select(o => o.Items.Select(i => i.Product)))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(c => c.Orders)");
        result.Should().Contain("ThenInclude");
        result.Should().NotContain(".Select(");

        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void Rewrite_WithNoEfCode_RemainsUnchanged()
    {
        // Arrange
        var source = """
            using System;

            public class MyClass
            {
                public void MyMethod()
                {
                    Console.WriteLine("Hello World");
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_WithEmptySource_ReturnsEmpty()
    {
        // Arrange
        var source = "";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
    }

    [Fact]
    public void Rewrite_WithWhitespaceOnly_ReturnsWhitespace()
    {
        // Arrange
        var source = "   \n\t  ";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
    }

    [Fact]
    public void Rewrite_PreservesNonIncludeCalls()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Where(o => o.Total > 100)
                        .Include(o => o.Items.Select(i => i.Product))
                        .OrderBy(o => o.Date)
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Where(o => o.Total > 100)");
        result.Should().Contain("OrderBy(o => o.Date)");
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
    }

    [Fact]
    public void Rewrite_WithExistingEfCoreUsing_DoesNotDuplicate()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;
            using Microsoft.EntityFrameworkCore;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        var usingCount = result.Split("using Microsoft.EntityFrameworkCore").Length - 1;
        usingCount.Should().Be(1, "should not duplicate existing using");
    }

    [Fact]
    public void Rewrite_WithMultipleBranches_TransformsEachBranch()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadData()
                {
                    var data = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .Include(o => o.Items.Select(i => i.Discount))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("ThenInclude(y => y.Discount)");
        result.Should().NotContain(".Select(");
    }

    [Fact]
    public void Rewrite_WithFourLevels_GeneratesCorrectParameterNames()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadData()
                {
                    var data = Orders
                        .Include("Items.Product.Category.Department")
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(x => x.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("ThenInclude(z => z.Category)");
        result.Should().Contain("ThenInclude(i => i.Department)");
    }

    [Fact]
    public void Rewrite_WithIncludeWithoutArguments_RemainsUnchanged()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadData()
                {
                    var method = typeof(DbContext).GetMethod("Include");
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void Rewrite_DiagnosticsContainRelevantInformation()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .ToList();
                }
            }
            """;

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("ThenInclude"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("2 levels"));
    }

    [Fact]
    public void Rewrite_StringBasedDiagnostics_ContainWarning()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include("Items.Product")
                        .ToList();
                }
            }
            """;

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Severity == Core.Interfaces.RewriterDiagnosticSeverity.Warning &&
            d.Message.Contains("TODO"));
    }

    [Fact]
    public void Rewrite_WithDifferentParameterNames_PreservesLogic()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .Include(order => order.Items.Select(item => item.Product))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(order => order.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
    }

    [Fact]
    public void Rewrite_MixedLambdaAndStringIncludes_TransformsBoth()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadData()
                {
                    var data = Orders
                        .Include(o => o.Items.Select(i => i.Product))
                        .Include("Customer.Address")
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
        result.Should().Contain("Include(x => x.Customer)");
        result.Should().Contain("ThenInclude(y => y.Address)");

        // Should have diagnostics from both types
        _rewriter.Diagnostics.Should().HaveCountGreaterThan(1);
        _rewriter.ConfidenceScore.Should().Be(75); // Lowest is string-based
    }

    [Fact]
    public void Rewrite_WithAsNoTracking_PreservesChain()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using System.Linq;

            public class MyContext : DbContext
            {
                public void LoadOrders()
                {
                    var orders = Orders
                        .AsNoTracking()
                        .Include(o => o.Items.Select(i => i.Product))
                        .ToList();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("AsNoTracking()");
        result.Should().Contain("Include(o => o.Items)");
        result.Should().Contain("ThenInclude(y => y.Product)");
    }
}
