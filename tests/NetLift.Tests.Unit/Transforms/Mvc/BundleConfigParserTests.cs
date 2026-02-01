using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Parsers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class BundleConfigParserTests
{
    private readonly BundleConfigParser _parser = new();

    [Fact]
    public void ParsesSimpleScriptBundle()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.VirtualPath.Should().Be("~/bundles/jquery");
        bundle.Type.Should().Be(BundleType.Script);
        bundle.IncludedFiles.Should().ContainSingle()
            .Which.Should().Be("~/Scripts/jquery-{version}.js");
    }

    [Fact]
    public void ParsesSimpleStyleBundle()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new StyleBundle("~/bundles/css").Include(
                        "~/Content/bootstrap.css",
                        "~/Content/site.css"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.VirtualPath.Should().Be("~/bundles/css");
        bundle.Type.Should().Be(BundleType.Style);
        bundle.IncludedFiles.Should().HaveCount(2);
        bundle.IncludedFiles[0].Should().Be("~/Content/bootstrap.css");
        bundle.IncludedFiles[1].Should().Be("~/Content/site.css");
    }

    [Fact]
    public void ParsesMultipleBundles()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

                    bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                        "~/Scripts/bootstrap.js"));

                    bundles.Add(new StyleBundle("~/bundles/css").Include(
                        "~/Content/bootstrap.css",
                        "~/Content/site.css"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(3);
        result[0].VirtualPath.Should().Be("~/bundles/jquery");
        result[0].Type.Should().Be(BundleType.Script);
        result[1].VirtualPath.Should().Be("~/bundles/bootstrap");
        result[1].Type.Should().Be(BundleType.Script);
        result[2].VirtualPath.Should().Be("~/bundles/css");
        result[2].Type.Should().Be(BundleType.Style);
    }

    [Fact]
    public void ParsesChainedIncludeCalls()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/modernizr")
                        .Include("~/Scripts/modernizr-*")
                        .Include("~/Scripts/respond.js"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.IncludedFiles.Should().HaveCount(2);
        bundle.IncludedFiles[0].Should().Be("~/Scripts/modernizr-*");
        bundle.IncludedFiles[1].Should().Be("~/Scripts/respond.js");
    }

    [Fact]
    public void ParsesBundleWithCdnPath()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/jquery",
                        "https://ajax.googleapis.com/ajax/libs/jquery/3.6.0/jquery.min.js")
                        .Include("~/Scripts/jquery-{version}.js"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.CdnPath.Should().Be("https://ajax.googleapis.com/ajax/libs/jquery/3.6.0/jquery.min.js");
    }

    [Fact]
    public void ParsesIncludeDirectory()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/plugins")
                        .IncludeDirectory("~/Scripts/plugins", "*.js"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.IncludedDirectories.Should().HaveCount(2);
        bundle.IncludedDirectories[0].Should().Be("~/Scripts/plugins");
        bundle.IncludedDirectories[1].Should().Be("*.js");
    }

    [Fact]
    public void ParsesMixedIncludeAndIncludeDirectory()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle("~/bundles/app")
                        .Include("~/Scripts/jquery-{version}.js")
                        .IncludeDirectory("~/Scripts/app", "*.js")
                        .Include("~/Scripts/site.js"));
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(1);
        var bundle = result[0];
        bundle.IncludedFiles.Should().HaveCount(2);
        bundle.IncludedFiles[0].Should().Be("~/Scripts/jquery-{version}.js");
        bundle.IncludedFiles[1].Should().Be("~/Scripts/site.js");
        bundle.IncludedDirectories.Should().HaveCount(2);
    }

    [Fact]
    public void ParsesEmptySourceCode()
    {
        // Act
        var result = _parser.Parse("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsesNullSourceCode()
    {
        // Act
        var result = _parser.Parse(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsesSourceCodeWithoutBundles()
    {
        // Arrange
        var sourceCode = """
            public class SomeOtherClass
            {
                public void SomeMethod()
                {
                    var x = 42;
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void IgnoresInvalidBundleDefinitions()
    {
        // Arrange
        var sourceCode = """
            public class BundleConfig
            {
                public static void RegisterBundles(BundleCollection bundles)
                {
                    bundles.Add(new ScriptBundle());
                    bundles.Add(null);
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsesRealWorldBundleConfig()
    {
        // Arrange
        var sourceCode = """
            using System.Web.Optimization;

            namespace MyApp
            {
                public class BundleConfig
                {
                    public static void RegisterBundles(BundleCollection bundles)
                    {
                        bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                                    "~/Scripts/jquery-{version}.js"));

                        bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                                    "~/Scripts/jquery.validate*"));

                        bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                                    "~/Scripts/modernizr-*"));

                        bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                                  "~/Scripts/bootstrap.js"));

                        bundles.Add(new StyleBundle("~/Content/css").Include(
                                  "~/Content/bootstrap.css",
                                  "~/Content/site.css"));
                    }
                }
            }
            """;

        // Act
        var result = _parser.Parse(sourceCode);

        // Assert
        result.Should().HaveCount(5);
        result.Should().Contain(b => b.VirtualPath == "~/bundles/jquery" && b.Type == BundleType.Script);
        result.Should().Contain(b => b.VirtualPath == "~/bundles/jqueryval" && b.Type == BundleType.Script);
        result.Should().Contain(b => b.VirtualPath == "~/bundles/modernizr" && b.Type == BundleType.Script);
        result.Should().Contain(b => b.VirtualPath == "~/bundles/bootstrap" && b.Type == BundleType.Script);
        result.Should().Contain(b => b.VirtualPath == "~/Content/css" && b.Type == BundleType.Style);
    }
}
