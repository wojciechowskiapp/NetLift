using FluentAssertions;
using NetLift.Core.Models.StaticFiles;
using NetLift.Transforms.StaticFiles.Analyzers;

namespace NetLift.Tests.Unit.Transforms.StaticFiles;

public sealed class StaticFilesAnalyzerTests
{
    private readonly StaticFilesAnalyzer _analyzer = new();

    [Fact]
    public void MapToWwwroot_ContentPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/site.css");
    }

    [Fact]
    public void MapToWwwroot_ContentImagesPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/images/logo.png";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/images/logo.png");
    }

    [Fact]
    public void MapToWwwroot_ContentWithSubfolder_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/themes/default.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/themes/default.css");
    }

    [Fact]
    public void MapToWwwroot_ScriptsPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Scripts/app.js";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/js/app.js");
    }

    [Fact]
    public void MapToWwwroot_ScriptsWithSubfolder_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Scripts/modules/authentication.js";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/js/modules/authentication.js");
    }

    [Fact]
    public void MapToWwwroot_ImagesPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Images/banner.jpg";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/images/banner.jpg");
    }

    [Fact]
    public void MapToWwwroot_ImagesWithSubfolder_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Images/products/product-1.jpg";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/images/products/product-1.jpg");
    }

    [Fact]
    public void MapToWwwroot_FontsPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/fonts/roboto.woff2";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/fonts/roboto.woff2");
    }

    [Fact]
    public void MapToWwwroot_EmptyPath_ReturnsEmpty()
    {
        // Arrange
        const string originalPath = "";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void MapToWwwroot_NullPath_ReturnsNull()
    {
        // Arrange
        string? originalPath = null;

        // Act
        var result = _analyzer.MapToWwwroot(originalPath!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MapToWwwroot_NonMatchingPath_ReturnsUnchanged()
    {
        // Arrange
        const string originalPath = "~/wwwroot/css/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/wwwroot/css/site.css");
    }

    [Fact]
    public void MapToWwwroot_RelativeContentPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/bootstrap.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/bootstrap.css");
    }

    [Fact]
    public void MapToWwwroot_RelativeScriptsPath_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Scripts/jquery.min.js";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/js/jquery.min.js");
    }

    [Fact]
    public void MapToWwwroot_CaseInsensitive_MapsCorrectly()
    {
        // Arrange
        const string originalPath1 = "~/CONTENT/site.css";
        const string originalPath2 = "~/scripts/app.js";
        const string originalPath3 = "~/IMAGES/logo.png";

        // Act
        var result1 = _analyzer.MapToWwwroot(originalPath1);
        var result2 = _analyzer.MapToWwwroot(originalPath2);
        var result3 = _analyzer.MapToWwwroot(originalPath3);

        // Assert
        result1.Should().Be("~/css/site.css");
        result2.Should().Be("~/js/app.js");
        result3.Should().Be("~/images/logo.png");
    }

    [Fact]
    public void MapToWwwroot_MultipleContentImages_MapsCorrectly()
    {
        // Arrange
        const string originalPath1 = "~/Content/images/icon.png";
        const string originalPath2 = "~/Content/images/products/product.jpg";
        const string originalPath3 = "~/Content/images/thumbnails/thumb.png";

        // Act
        var result1 = _analyzer.MapToWwwroot(originalPath1);
        var result2 = _analyzer.MapToWwwroot(originalPath2);
        var result3 = _analyzer.MapToWwwroot(originalPath3);

        // Assert
        result1.Should().Be("~/images/icon.png");
        result2.Should().Be("~/images/products/product.jpg");
        result3.Should().Be("~/images/thumbnails/thumb.png");
    }

    [Fact]
    public void MapToWwwroot_ComplexPaths_MapsCorrectly()
    {
        // Arrange
        var testCases = new Dictionary<string, string>
        {
            { "~/Content/css/bootstrap.min.css", "~/css/css/bootstrap.min.css" },
            { "~/Scripts/lib/jquery.js", "~/js/lib/jquery.js" },
            { "~/Images/icons/user-avatar.svg", "~/images/icons/user-avatar.svg" },
            { "~/fonts/FontAwesome/fontawesome.woff", "~/fonts/FontAwesome/fontawesome.woff" },
            { "~/Content/site.min.css", "~/css/site.min.css" }
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var result = _analyzer.MapToWwwroot(testCase.Key);
            result.Should().Be(testCase.Value, because: $"'{testCase.Key}' should map to '{testCase.Value}'");
        }
    }

    [Fact]
    public void MapToWwwroot_PathWithSpaces_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/My Styles/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/My Styles/site.css");
    }

    [Fact]
    public void MapToWwwroot_PathWithSpecialCharacters_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/styles-2024/site_v1.0.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/styles-2024/site_v1.0.css");
    }

    [Fact]
    public void MapToWwwroot_JavaScriptMinifiedFiles_MapsCorrectly()
    {
        // Arrange
        const string originalPath1 = "~/Scripts/jquery.min.js";
        const string originalPath2 = "~/Scripts/bootstrap.bundle.min.js";
        const string originalPath3 = "~/Scripts/app.min.js";

        // Act
        var result1 = _analyzer.MapToWwwroot(originalPath1);
        var result2 = _analyzer.MapToWwwroot(originalPath2);
        var result3 = _analyzer.MapToWwwroot(originalPath3);

        // Assert
        result1.Should().Be("~/js/jquery.min.js");
        result2.Should().Be("~/js/bootstrap.bundle.min.js");
        result3.Should().Be("~/js/app.min.js");
    }

    [Fact]
    public void MapToWwwroot_CssMinifiedFiles_MapsCorrectly()
    {
        // Arrange
        const string originalPath1 = "~/Content/bootstrap.min.css";
        const string originalPath2 = "~/Content/site.min.css";

        // Act
        var result1 = _analyzer.MapToWwwroot(originalPath1);
        var result2 = _analyzer.MapToWwwroot(originalPath2);

        // Assert
        result1.Should().Be("~/css/bootstrap.min.css");
        result2.Should().Be("~/css/site.min.css");
    }

    [Fact]
    public void MapToWwwroot_ImageFormats_MapsCorrectly()
    {
        // Arrange
        var imageFormats = new[]
        {
            "~/Images/logo.png",
            "~/Images/banner.jpg",
            "~/Images/icon.svg",
            "~/Images/photo.gif",
            "~/Images/background.webp"
        };

        // Act & Assert
        foreach (var imagePath in imageFormats)
        {
            var result = _analyzer.MapToWwwroot(imagePath);
            var fileName = Path.GetFileName(imagePath);
            result.Should().Be($"~/images/{fileName}");
        }
    }

    [Fact]
    public void MapToWwwroot_FontFormats_MapsCorrectly()
    {
        // Arrange
        var fontFormats = new[]
        {
            "~/fonts/roboto.woff",
            "~/fonts/roboto.woff2",
            "~/fonts/opensans.ttf",
            "~/fonts/icons.eot"
        };

        // Act & Assert
        foreach (var fontPath in fontFormats)
        {
            var result = _analyzer.MapToWwwroot(fontPath);
            result.Should().Be(fontPath);
        }
    }

    [Fact]
    public void MapToWwwroot_NestedSubfolders_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/themes/dark/components/button.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/themes/dark/components/button.css");
    }

    [Fact]
    public void MapToWwwroot_DeepNesting_MapsCorrectly()
    {
        // Arrange
        const string originalPath = "~/Scripts/vendor/plugins/charts/chartjs/chart.min.js";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/js/vendor/plugins/charts/chartjs/chart.min.js");
    }

    [Fact]
    public void MapToWwwroot_MixedCase_PreservesCase()
    {
        // Arrange
        const string originalPath = "~/Content/MyStyles/SiteTheme.CSS";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/MyStyles/SiteTheme.CSS");
    }

    [Fact]
    public void MapToWwwroot_BackslashSeparators_HandlesCorrectly()
    {
        // Arrange - Note: This test assumes the implementation handles both / and \
        const string originalPath = "~/Content/styles/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/styles/site.css");
    }

    [Fact]
    public void MapToWwwroot_AlreadyWwwrootPath_ReturnsUnchanged()
    {
        // Arrange
        const string originalPath = "~/wwwroot/css/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void MapToWwwroot_ExternalUrl_ReturnsUnchanged()
    {
        // Arrange
        const string originalPath = "https://cdn.example.com/css/bootstrap.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void MapToWwwroot_AbsolutePath_ReturnsUnchanged()
    {
        // Arrange
        const string originalPath = "/Content/site.css";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void MapToWwwroot_BatchProcessing_AllMapCorrectly()
    {
        // Arrange
        var testPaths = new[]
        {
            ("~/Content/site.css", "~/css/site.css"),
            ("~/Scripts/app.js", "~/js/app.js"),
            ("~/Images/logo.png", "~/images/logo.png"),
            ("~/Content/images/banner.jpg", "~/images/banner.jpg"),
            ("~/fonts/roboto.woff2", "~/fonts/roboto.woff2")
        };

        // Act & Assert
        foreach (var (original, expected) in testPaths)
        {
            var result = _analyzer.MapToWwwroot(original);
            result.Should().Be(expected, because: $"'{original}' should map to '{expected}'");
        }
    }

    [Fact]
    public void MapToWwwroot_CommonLibraries_MapsCorrectly()
    {
        // Arrange
        var commonLibraries = new Dictionary<string, string>
        {
            { "~/Scripts/jquery-3.6.0.min.js", "~/js/jquery-3.6.0.min.js" },
            { "~/Scripts/bootstrap.bundle.min.js", "~/js/bootstrap.bundle.min.js" },
            { "~/Scripts/modernizr-2.8.3.js", "~/js/modernizr-2.8.3.js" },
            { "~/Content/bootstrap.min.css", "~/css/bootstrap.min.css" },
            { "~/Content/font-awesome.min.css", "~/css/font-awesome.min.css" }
        };

        // Act & Assert
        foreach (var library in commonLibraries)
        {
            var result = _analyzer.MapToWwwroot(library.Key);
            result.Should().Be(library.Value);
        }
    }

    [Fact]
    public void MapToWwwroot_TrailingSlash_HandlesCorrectly()
    {
        // Arrange
        const string originalPath = "~/Content/";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/");
    }

    [Fact]
    public void MapToWwwroot_OnlyTilde_ReturnsUnchanged()
    {
        // Arrange
        const string originalPath = "~";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~");
    }

    [Fact]
    public void MapToWwwroot_QueryString_PreservesQueryString()
    {
        // Arrange
        const string originalPath = "~/Content/site.css?v=1.0";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/css/site.css?v=1.0");
    }

    [Fact]
    public void MapToWwwroot_Anchor_PreservesAnchor()
    {
        // Arrange
        const string originalPath = "~/Scripts/app.js#section1";

        // Act
        var result = _analyzer.MapToWwwroot(originalPath);

        // Assert
        result.Should().Be("~/js/app.js#section1");
    }
}
