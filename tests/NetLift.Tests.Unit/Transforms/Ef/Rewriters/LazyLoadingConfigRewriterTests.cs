using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

public sealed class LazyLoadingConfigRewriterTests
{
    private readonly ILazyLoadingConfigRewriter _rewriter = new LazyLoadingConfigRewriter();

    [Fact]
    public void Rewrite_DetectsLazyLoadingEnabledTrue()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        _rewriter.LazyLoadingWasEnabled.Should().BeTrue();
        _rewriter.Diagnostics.Should().ContainSingle(d =>
            d.Message.Contains("LazyLoadingEnabled = True") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void Rewrite_DetectsLazyLoadingEnabledFalse()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = false;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        _rewriter.LazyLoadingWasEnabled.Should().BeFalse();
        _rewriter.Diagnostics.Should().ContainSingle(d =>
            d.Message.Contains("LazyLoadingEnabled = False") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void Rewrite_DetectsProxyCreationEnabled()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.ProxyCreationEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        _rewriter.ProxyCreationWasEnabled.Should().BeTrue();
        _rewriter.Diagnostics.Should().ContainSingle(d =>
            d.Message.Contains("ProxyCreationEnabled = True") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
    }

    [Fact]
    public void Rewrite_RemovesLazyLoadingEnabledAssignment()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().NotContain("Configuration.LazyLoadingEnabled");
    }

    [Fact]
    public void Rewrite_RemovesProxyCreationEnabledAssignment()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.ProxyCreationEnabled = false;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().NotContain("Configuration.ProxyCreationEnabled");
    }

    [Fact]
    public void Rewrite_RemovesBothConfigurationSettings()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
        Configuration.ProxyCreationEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().NotContain("Configuration.LazyLoadingEnabled");
        result.Should().NotContain("Configuration.ProxyCreationEnabled");
        _rewriter.LazyLoadingWasEnabled.Should().BeTrue();
        _rewriter.ProxyCreationWasEnabled.Should().BeTrue();
    }

    [Fact]
    public void Rewrite_AddsTodoCommentWhenLazyLoadingWasEnabled()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("TODO: EF6 had lazy loading enabled by default");
        result.Should().Contain("options.UseLazyLoadingProxies()");
        result.Should().Contain("Microsoft.EntityFrameworkCore.Proxies");
        result.Should().Contain("Navigation properties must be virtual");
    }

    [Fact]
    public void Rewrite_AddsInfoCommentWhenLazyLoadingWasDisabled()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = false;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Lazy loading is disabled (EF Core default)");
        result.Should().Contain("Use Include() for eager loading");
    }

    [Fact]
    public void Rewrite_HandlesNoExplicitSettings_AssumesEf6Defaults()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        // EF6 defaults: lazy loading = true, proxy creation = true
        _rewriter.LazyLoadingWasEnabled.Should().BeTrue();
        _rewriter.ProxyCreationWasEnabled.Should().BeTrue();
        _rewriter.ConfidenceScore.Should().Be(75); // Lower confidence when no explicit setting
    }

    [Fact]
    public void Rewrite_ConfidenceScore95_WhenExplicitDisable()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = false;
    }
}";

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.ConfidenceScore.Should().Be(95);
    }

    [Fact]
    public void Rewrite_ConfidenceScore85_WhenExplicitEnable()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.ConfidenceScore.Should().Be(85);
    }

    [Fact]
    public void Rewrite_NonDbContextCodeUnchanged()
    {
        // Arrange
        var source = @"
public class MyService
{
    public void DoSomething()
    {
        var enabled = true;
        // This should not be touched
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_PreservesOtherConstructorStatements()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
        Database.SetInitializer<AppContext>(null);
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("Database.SetInitializer<AppContext>(null);");
        result.Should().NotContain("Configuration.LazyLoadingEnabled");
    }

    [Fact]
    public void Rewrite_HandlesMultipleDbContexts()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class FirstContext : DbContext
{
    public FirstContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}

public class SecondContext : DbContext
{
    public SecondContext()
    {
        Configuration.LazyLoadingEnabled = false;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().NotContain("Configuration.LazyLoadingEnabled");
        result.Should().Contain("TODO: EF6 had lazy loading enabled by default");
        result.Should().Contain("Lazy loading is disabled (EF Core default)");
    }

    [Fact]
    public void Rewrite_HandlesEmptySourceCode()
    {
        // Arrange
        var source = "";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be("");
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_HandlesWhitespaceOnlySourceCode()
    {
        // Arrange
        var source = "   \n\t  \n  ";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Be(source);
        _rewriter.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_IncludesContextNameInTodoComment()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class ProductDbContext : DbContext
{
    public ProductDbContext()
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().Contain("AddDbContext<ProductDbContext>");
    }

    [Fact]
    public void Rewrite_GeneratesDiagnosticWhenRemovingSettings()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.LazyLoadingEnabled = true;
        Configuration.ProxyCreationEnabled = true;
    }
}";

        // Act
        _rewriter.Rewrite(source);

        // Assert
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("LazyLoadingEnabled = True") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("ProxyCreationEnabled = True") &&
            d.Severity == RewriterDiagnosticSeverity.Info);
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("added TODO comment") &&
            d.Severity == RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_HandlesConstructorWithBaseInitializer()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext() : base(""name=DefaultConnection"")
    {
        Configuration.LazyLoadingEnabled = true;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        result.Should().NotContain("Configuration.LazyLoadingEnabled");
        result.Should().Contain("base(\"name=DefaultConnection\")"); // Preserve base initializer
        result.Should().Contain("TODO: EF6 had lazy loading enabled by default");
    }

    [Fact]
    public void Rewrite_HandlesProxyCreationOnlyWithoutLazyLoading()
    {
        // Arrange
        var source = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public AppContext()
    {
        Configuration.ProxyCreationEnabled = false;
    }
}";

        // Act
        var result = _rewriter.Rewrite(source);

        // Assert
        // Lazy loading defaults to true in EF6, but proxy creation is disabled
        // This is an unusual configuration but should be handled
        _rewriter.ProxyCreationWasEnabled.Should().BeFalse();
        _rewriter.LazyLoadingWasEnabled.Should().BeTrue(); // EF6 default
    }
}
