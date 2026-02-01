using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class SystemWebMvcNamespaceRewriterTests
{
    private readonly SystemWebMvcNamespaceRewriter _rewriter = new();

    [Fact]
    public void RewriteSimpleUsingDirective_Success()
    {
        // Arrange
        var source = @"
using System;
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using Microsoft.AspNetCore.Mvc;");
        rewritten.Should().NotContain("using System.Web.Mvc;");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteAliasedUsing_PreservesAlias()
    {
        // Arrange
        var source = @"
using Mvc = System.Web.Mvc;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using Mvc = Microsoft.AspNetCore.Mvc;");
        rewritten.Should().NotContain("System.Web.Mvc");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteStaticUsing_Success()
    {
        // Arrange
        var source = @"
using static System.Web.Mvc.Html.InputExtensions;

namespace TestApp
{
    public class HtmlHelper { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using static Microsoft.AspNetCore.Mvc.Rendering.InputExtensions;");
        rewritten.Should().NotContain("System.Web.Mvc.Html");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc.Rendering");
    }

    [Fact]
    public void RewriteGlobalUsing_Success()
    {
        // Arrange
        var source = @"
global using System.Web.Mvc;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("global using Microsoft.AspNetCore.Mvc;");
        rewritten.Should().NotContain("System.Web.Mvc");
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteFullyQualifiedName_InCode()
    {
        // Arrange
        var source = @"
namespace TestApp
{
    public class HomeController : System.Web.Mvc.Controller
    {
        public System.Web.Mvc.ActionResult Index()
        {
            return null;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc.Controller");
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc.ActionResult");
        rewritten.Should().NotContain("System.Web.Mvc");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
    }

    [Fact]
    public void RemoveDuplicateUsings_AfterRewrite()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Mvc;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();

        // Count occurrences of Microsoft.AspNetCore.Mvc using
        var usingCount = CountOccurrences(rewritten, "using Microsoft.AspNetCore.Mvc;");

        // We should have one for Mvc and one for Rendering
        usingCount.Should().BeLessOrEqualTo(2);
        rewritten.Should().NotContain("System.Web.Mvc");
    }

    [Fact]
    public void MergeMultipleNamespaces_ToSingle()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Mvc.Async;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc");
        rewritten.Should().NotContain("System.Web.Mvc");

        // All three should map to Microsoft.AspNetCore.Mvc
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");

        // Confidence should be 90 for merged namespaces
        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void PreserveTrivia_CommentsAndWhitespace()
    {
        // Arrange
        var source = @"
// This is a comment
using System.Web.Mvc; // MVC namespace

namespace TestApp
{
    // Controller class
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("// This is a comment");
        rewritten.Should().Contain("// Controller class");
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc");
    }

    [Fact]
    public void CalculateConfidenceScore_DirectMapping()
    {
        // Arrange
        var source = @"
using System.Web.Routing;

namespace TestApp
{
    public class RouteConfig { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Routing");
    }

    [Fact]
    public void CalculateConfidenceScore_MergedNamespace()
    {
        // Arrange - Multiple namespaces that map to the same target
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Ajax;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void TrackRequiredUsings_Correctly()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc.Rendering");
        _rewriter.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Routing");
        _rewriter.RequiredUsings.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public void TrackDiagnostics_ForEachRewrite()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Routing;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("System.Web.Mvc"));
        _rewriter.Diagnostics.Should().Contain(d => d.Message.Contains("System.Web.Routing"));
    }

    [Fact]
    public void SortUsings_SystemFirst()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System;
using System.Collections.Generic;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();

        // System namespaces should come before Microsoft namespaces
        var systemIndex = rewritten.IndexOf("using System;", StringComparison.Ordinal);
        var microsoftIndex = rewritten.IndexOf("using Microsoft.AspNetCore.Mvc;", StringComparison.Ordinal);

        systemIndex.Should().BeLessThan(microsoftIndex);
    }

    [Fact]
    public void RewriteMultipleNamespaces_InSingleFile()
    {
        // Arrange
        var source = @"
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Web.Mvc.Filters;

namespace TestApp
{
    public class HomeController : System.Web.Mvc.Controller
    {
        public System.Web.Mvc.ActionResult Index()
        {
            return null;
        }
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().NotContain("System.Web.Mvc");
        rewritten.Should().NotContain("System.Web.Routing");
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc");
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc.Rendering");
        rewritten.Should().Contain("Microsoft.AspNetCore.Routing");
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc.Filters");
    }

    [Fact]
    public void DoNotRewrite_UnrelatedNamespaces()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestApp
{
    public class HomeController { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using System;");
        rewritten.Should().Contain("using System.Collections.Generic;");
        rewritten.Should().Contain("using System.Linq;");
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteWebOptimization_ToWebOptimizer()
    {
        // Arrange
        var source = @"
using System.Web.Optimization;

namespace TestApp
{
    public class BundleConfig { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using WebOptimizer;");
        rewritten.Should().NotContain("System.Web.Optimization");
        _rewriter.RequiredUsings.Should().Contain("WebOptimizer");
    }

    [Fact]
    public void HandleEmptyFile_ReturnsOriginal()
    {
        // Arrange
        var source = @"";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().Be(source);
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void HandleFileWithNoMvcReferences_NoChanges()
    {
        // Arrange
        var source = @"
using System;

namespace TestApp
{
    public class Calculator
    {
        public int Add(int a, int b) => a + b;
    }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("using System;");
        _rewriter.RequiredUsings.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RewriteChildNamespace_Correctly()
    {
        // Arrange
        var source = @"
using System.Web.Mvc.Html.SomeChildNamespace;

namespace TestApp
{
    public class Helper { }
}";

        // Act
        var rewritten = _rewriter.Rewrite(source);

        // Assert
        rewritten.Should().NotBeNullOrEmpty();
        rewritten.Should().Contain("Microsoft.AspNetCore.Mvc.Rendering.SomeChildNamespace");
        rewritten.Should().NotContain("System.Web.Mvc.Html");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
