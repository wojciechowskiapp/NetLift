using FluentAssertions;
using NetLift.Transforms.Ef.Rewriters;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

public sealed class FluentApiRelationshipRewriterTests
{
    private readonly FluentApiRelationshipRewriter _rewriter = new();

    [Fact]
    public void Rewrite_WithHasRequired_TransformsToHasOneWithIsRequired()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders)
                        .HasForeignKey(o => o.CustomerId);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(o => o.Customer)");
        result.Should().Contain("WithMany(c => c.Orders)");
        result.Should().Contain("HasForeignKey(o => o.CustomerId)");
        result.Should().Contain(".IsRequired(true)");
        result.Should().NotContain("HasRequired");

        _rewriter.RequiredUsings.Should().Contain("Microsoft.EntityFrameworkCore");
        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public void Rewrite_WithHasOptional_TransformsToHasOneWithIsRequiredFalse()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Employee>()
                        .HasOptional(e => e.Manager)
                        .WithMany(m => m.Subordinates);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(e => e.Manager)");
        result.Should().Contain("WithMany(m => m.Subordinates)");
        result.Should().Contain(".IsRequired(false)");
        result.Should().NotContain("HasOptional");

        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_WithHasOptionalAndWithRequired_TransformsCorrectly()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Book>()
                        .HasOptional(b => b.Author)
                        .WithRequired(a => a.PrimaryBook);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(b => b.Author)");
        result.Should().Contain("WithOne(a => a.PrimaryBook)");
        result.Should().Contain(".IsRequired(true)"); // WithRequired sets it to true
        result.Should().NotContain("HasOptional");
        result.Should().NotContain("WithRequired");

        _rewriter.ConfidenceScore.Should().BeLessThanOrEqualTo(95);
    }

    [Fact]
    public void Rewrite_WithHasRequiredAndWithOptional_TransformsCorrectly()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Product>()
                        .HasRequired(p => p.Category)
                        .WithOptional(c => c.FeaturedProduct);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(p => p.Category)");
        result.Should().Contain("WithOne(c => c.FeaturedProduct)");
        result.Should().Contain(".IsRequired(false)"); // WithOptional overrides to false
        result.Should().NotContain("HasRequired");
        result.Should().NotContain("WithOptional");
    }

    [Fact]
    public void Rewrite_WithOptionalPrincipal_TransformsToWithOne()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<User>()
                        .HasOptional(u => u.Profile)
                        .WithOptionalPrincipal(p => p.User);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(u => u.Profile)");
        result.Should().Contain("WithOne(p => p.User)");
        result.Should().Contain(".IsRequired(false)");
        result.Should().NotContain("HasOptional");
        result.Should().NotContain("WithOptionalPrincipal");

        _rewriter.ConfidenceScore.Should().Be(75); // Lower confidence for WithOptionalPrincipal
    }

    [Fact]
    public void Rewrite_WithOptionalDependent_TransformsToWithOne()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Account>()
                        .HasOptional(a => a.Settings)
                        .WithOptionalDependent();
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(a => a.Settings)");
        result.Should().Contain("WithOne()");
        result.Should().Contain(".IsRequired(false)");
        result.Should().NotContain("HasOptional");
        result.Should().NotContain("WithOptionalDependent");

        _rewriter.ConfidenceScore.Should().Be(75); // Lower confidence for WithOptionalDependent
    }

    [Fact]
    public void Rewrite_WithComplexChain_TransformsAllMethods()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Invoice>()
                        .HasRequired(i => i.Customer)
                        .WithMany(c => c.Invoices)
                        .HasForeignKey(i => i.CustomerId)
                        .WillCascadeOnDelete(false);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(i => i.Customer)");
        result.Should().Contain("WithMany(c => c.Invoices)");
        result.Should().Contain("HasForeignKey(i => i.CustomerId)");
        result.Should().Contain("WillCascadeOnDelete(false)");
        result.Should().Contain(".IsRequired(true)");
        result.Should().NotContain("HasRequired");
    }

    [Fact]
    public void Rewrite_WithMultipleRelationships_TransformsAll()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders);

                    modelBuilder.Entity<Employee>()
                        .HasOptional(e => e.Manager)
                        .WithMany(m => m.Subordinates);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(o => o.Customer)");
        result.Should().Contain("HasOne(e => e.Manager)");
        result.Should().NotContain("HasRequired");
        result.Should().NotContain("HasOptional");

        // Should have two IsRequired calls
        result.Split(".IsRequired(").Length.Should().Be(3); // Original + 2 splits
    }

    [Fact]
    public void Rewrite_WithNoEfCode_ReturnsUnchanged()
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
    public void Rewrite_PreservesOtherFluentApiCalls()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Product>()
                        .HasKey(p => p.Id);

                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders);

                    modelBuilder.Entity<Category>()
                        .Property(c => c.Name)
                        .IsRequired()
                        .HasMaxLength(100);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasKey(p => p.Id)");
        result.Should().Contain("Property(c => c.Name)");
        result.Should().Contain("HasMaxLength(100)");
        result.Should().Contain("HasOne(o => o.Customer)");
    }

    [Fact]
    public void Rewrite_WithNestedGenericTypes_TransformsCorrectly()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order<int>>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders)
                        .HasForeignKey(o => o.CustomerId);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("HasOne(o => o.Customer)");
        result.Should().Contain(".IsRequired(true)");
    }

    [Fact]
    public void Rewrite_AddsUsingsOnlyOnce()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders);

                    modelBuilder.Entity<Invoice>()
                        .HasRequired(i => i.Customer)
                        .WithMany(c => c.Invoices);
                }
            }
            """;

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        _rewriter.RequiredUsings.Should().ContainSingle();
        _rewriter.RequiredUsings.Should().Contain("Microsoft.EntityFrameworkCore");

        // Count occurrences of the using directive in result
        var usingCount = result.Split("using Microsoft.EntityFrameworkCore").Length - 1;
        usingCount.Should().Be(1);
    }

    [Fact]
    public void Rewrite_WithExistingEfCoreUsing_DoesNotDuplicate()
    {
        // Arrange
        var source = """
            using System.Data.Entity;
            using Microsoft.EntityFrameworkCore;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders);
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
    public void Rewrite_DiagnosticsContainRelevantInformation()
    {
        // Arrange
        var source = """
            using System.Data.Entity;

            public class MyContext : DbContext
            {
                protected override void OnModelCreating(DbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasRequired(o => o.Customer)
                        .WithMany(c => c.Orders);
                }
            }
            """;

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("HasRequired"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("IsRequired"));
    }
}
