using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ViteConfigGeneratorTests
{
    private readonly ViteConfigGenerator _generator = new();

    [Fact]
    public void GeneratesBasicViteConfig()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/jquery",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/jquery-3.6.0.js" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("import { defineConfig } from 'vite'");
        result.Should().Contain("import { resolve } from 'path'");
        result.Should().Contain("export default defineConfig({");
        result.Should().Contain("outDir: 'wwwroot/dist'");
        result.Should().Contain("manifest: true");
        result.Should().Contain("jquery:");
    }

    [Fact]
    public void GeneratesMultipleEntryPoints()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/jquery",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/jquery-3.6.0.js" }
            },
            new()
            {
                VirtualPath = "~/bundles/bootstrap",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/bootstrap.js" }
            },
            new()
            {
                VirtualPath = "~/Content/css",
                Type = BundleType.Style,
                IncludedFiles = new List<string> { "~/Content/site.css" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("jquery:");
        result.Should().Contain("bootstrap:");
        result.Should().Contain("css:");
    }

    [Fact]
    public void ConvertsVirtualPathsToModernStructure()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/scripts",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/app.js" }
            },
            new()
            {
                VirtualPath = "~/bundles/styles",
                Type = BundleType.Style,
                IncludedFiles = new List<string> { "~/Content/main.css" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("src/js/app.js");
        result.Should().Contain("src/css/main.css");
    }

    [Fact]
    public void IncludesResolveAliases()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/app",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/app.js" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("resolve: {");
        result.Should().Contain("alias: {");
        result.Should().Contain("'@': resolve(__dirname, 'src')");
        result.Should().Contain("'~/Scripts': resolve(__dirname, 'src/js')");
        result.Should().Contain("'~/Content': resolve(__dirname, 'src/css')");
    }

    [Fact]
    public void IncludesServerConfiguration()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/app",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/app.js" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("server: {");
        result.Should().Contain("port: 5173");
        result.Should().Contain("strictPort: true");
    }

    [Fact]
    public void GeneratesValidJavaScript()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/test",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/test.js" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().StartWith("import { defineConfig }");
        result.Should().EndWith("});");
        result.Should().NotContain("undefined");
    }

    [Fact]
    public void HandlesEmptyBundles()
    {
        // Arrange
        var bundles = new List<BundleDefinition>();

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("import { defineConfig } from 'vite'");
        result.Should().Contain("export default defineConfig({");
    }

    [Fact]
    public void ThrowsOnNullBundles()
    {
        // Act & Assert
        var act = () => _generator.Generate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HandlesBundlesWithoutIncludedFiles()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/empty",
                Type = BundleType.Script,
                IncludedFiles = new List<string>()
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("empty:");
        result.Should().Contain("src/js/empty.js");
    }

    [Fact]
    public void NormalizesBundleNames()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/bundles/jquery/validation",
                Type = BundleType.Script,
                IncludedFiles = new List<string> { "~/Scripts/jquery.validate.js" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("jquery-validation:");
    }
}
