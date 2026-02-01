using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class AssetReferenceTransformerTests
{
    private readonly AssetReferenceTransformer _transformer = new();

    [Fact]
    public void TransformsStylesRenderToLinkTag()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <head>
                @Styles.Render("~/bundles/css")
            </head>
            </html>
            """;

        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/css",
                Type = BundleType.Style
            }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/css.css\" asp-append-version=\"true\" />");
        result.Should().NotContain("@Styles.Render");
    }

    [Fact]
    public void TransformsScriptsRenderToScriptTag()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <body>
                @Scripts.Render("~/bundles/jquery")
            </body>
            </html>
            """;

        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/jquery",
                Type = BundleType.Script
            }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<script src=\"/dist/js/jquery.js\" asp-append-version=\"true\"></script>");
        result.Should().NotContain("@Scripts.Render");
    }

    [Fact]
    public void TransformsMultipleBundleReferences()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <head>
                @Styles.Render("~/bundles/css")
                @Styles.Render("~/bundles/bootstrap-css")
            </head>
            <body>
                @Scripts.Render("~/bundles/jquery")
                @Scripts.Render("~/bundles/bootstrap")
            </body>
            </html>
            """;

        var bundles = new List<BundleDefinition>
        {
            new() { VirtualPath = "~/bundles/css", Type = BundleType.Style },
            new() { VirtualPath = "~/bundles/bootstrap-css", Type = BundleType.Style },
            new() { VirtualPath = "~/bundles/jquery", Type = BundleType.Script },
            new() { VirtualPath = "~/bundles/bootstrap", Type = BundleType.Script }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/css.css\" asp-append-version=\"true\" />");
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/bootstrap-css.css\" asp-append-version=\"true\" />");
        result.Should().Contain("<script src=\"/dist/js/jquery.js\" asp-append-version=\"true\"></script>");
        result.Should().Contain("<script src=\"/dist/js/bootstrap.js\" asp-append-version=\"true\"></script>");
        result.Should().NotContain("@Styles.Render");
        result.Should().NotContain("@Scripts.Render");
    }

    [Fact]
    public void AddsTodoCommentForUnmappedStyleBundle()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <head>
                @Styles.Render("~/bundles/unknown")
            </head>
            </html>
            """;

        var bundles = new List<BundleDefinition>();

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("@* TODO: Map Style bundle '~/bundles/unknown' to modern asset pipeline *@");
        result.Should().Contain("@* Original: @Styles.Render(\"~/bundles/unknown\") *@");
    }

    [Fact]
    public void AddsTodoCommentForUnmappedScriptBundle()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <body>
                @Scripts.Render("~/bundles/unknown")
            </body>
            </html>
            """;

        var bundles = new List<BundleDefinition>();

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("@* TODO: Map Script bundle '~/bundles/unknown' to modern asset pipeline *@");
        result.Should().Contain("@* Original: @Scripts.Render(\"~/bundles/unknown\") *@");
    }

    [Fact]
    public void HandlesSingleQuotesInBundleReferences()
    {
        // Arrange
        var viewContent = "@Styles.Render('~/bundles/css')";

        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/css",
                Type = BundleType.Style
            }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/css.css\" asp-append-version=\"true\" />");
    }

    [Fact]
    public void IncludesAspAppendVersion()
    {
        // Arrange
        var viewContent = "@Styles.Render(\"~/bundles/css\")";

        var bundles = new List<BundleDefinition>
        {
            new() { VirtualPath = "~/bundles/css", Type = BundleType.Style }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("asp-append-version=\"true\"");
    }

    [Fact]
    public void GeneratesCorrectPathForNestedBundles()
    {
        // Arrange
        var viewContent = "@Scripts.Render(\"~/bundles/app/main\")";

        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/app/main",
                Type = BundleType.Script
            }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<script src=\"/dist/js/app-main.js\" asp-append-version=\"true\"></script>");
    }

    [Fact]
    public void HandlesEmptyViewContent()
    {
        // Arrange
        var viewContent = "";
        var bundles = new List<BundleDefinition>();

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HandlesNullViewContent()
    {
        // Arrange
        string? viewContent = null;
        var bundles = new List<BundleDefinition>();

        // Act
        var result = _transformer.TransformRazorView(viewContent!, bundles);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ThrowsOnNullBundles()
    {
        // Arrange
        var viewContent = "@Styles.Render(\"~/bundles/css\")";

        // Act & Assert
        var act = () => _transformer.TransformRazorView(viewContent, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PreservesViewContentWithoutBundleReferences()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Test</title>
            </head>
            <body>
                <h1>Hello World</h1>
            </body>
            </html>
            """;

        var bundles = new List<BundleDefinition>();

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Be(viewContent);
    }

    [Fact]
    public void IsCaseInsensitiveForBundlePaths()
    {
        // Arrange
        var viewContent = "@Styles.Render(\"~/BUNDLES/CSS\")";

        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/css",
                Type = BundleType.Style
            }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/css.css\" asp-append-version=\"true\" />");
    }

    [Fact]
    public void HandlesRealWorldLayoutFile()
    {
        // Arrange
        var viewContent = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>@ViewBag.Title - My ASP.NET Application</title>
                @Styles.Render("~/Content/css")
            </head>
            <body>
                @RenderBody()
                @Scripts.Render("~/bundles/jquery")
                @Scripts.Render("~/bundles/bootstrap")
                @RenderSection("scripts", required: false)
            </body>
            </html>
            """;

        var bundles = new List<BundleDefinition>
        {
            new() { VirtualPath = "~/Content/css", Type = BundleType.Style },
            new() { VirtualPath = "~/bundles/jquery", Type = BundleType.Script },
            new() { VirtualPath = "~/bundles/bootstrap", Type = BundleType.Script }
        };

        // Act
        var result = _transformer.TransformRazorView(viewContent, bundles);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"/dist/css/css.css\" asp-append-version=\"true\" />");
        result.Should().Contain("<script src=\"/dist/js/jquery.js\" asp-append-version=\"true\"></script>");
        result.Should().Contain("<script src=\"/dist/js/bootstrap.js\" asp-append-version=\"true\"></script>");
        result.Should().Contain("@RenderBody()");
        result.Should().Contain("@RenderSection(\"scripts\", required: false)");
        result.Should().NotContain("@Styles.Render");
        result.Should().NotContain("@Scripts.Render");
    }
}
