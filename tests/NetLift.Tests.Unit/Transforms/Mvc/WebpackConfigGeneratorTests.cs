using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class WebpackConfigGeneratorTests
{
    private readonly WebpackConfigGenerator _generator = new();

    [Fact]
    public void GeneratesBasicWebpackConfig()
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
        result.Should().Contain("const path = require('path')");
        result.Should().Contain("const MiniCssExtractPlugin = require('mini-css-extract-plugin')");
        result.Should().Contain("const { WebpackManifestPlugin } = require('webpack-manifest-plugin')");
        result.Should().Contain("module.exports = {");
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
    public void ConfiguresOutputDirectory()
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
        result.Should().Contain("output: {");
        result.Should().Contain("path: path.resolve(__dirname, 'wwwroot/dist')");
        result.Should().Contain("filename: 'js/[name].[contenthash].js'");
        result.Should().Contain("clean: true");
    }

    [Fact]
    public void IncludesCssLoaders()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
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
        result.Should().Contain("module: {");
        result.Should().Contain("rules: [");
        result.Should().Contain("test: /\\.css$/");
        result.Should().Contain("use: [MiniCssExtractPlugin.loader, 'css-loader']");
    }

    [Fact]
    public void IncludesSassLoaders()
    {
        // Arrange
        var bundles = new List<BundleDefinition>
        {
            new()
            {
                VirtualPath = "~/Content/scss",
                Type = BundleType.Style,
                IncludedFiles = new List<string> { "~/Content/main.scss" }
            }
        };

        // Act
        var result = _generator.Generate(bundles);

        // Assert
        result.Should().Contain("test: /\\.s[ac]ss$/");
        result.Should().Contain("use: [MiniCssExtractPlugin.loader, 'css-loader', 'sass-loader']");
    }

    [Fact]
    public void IncludesAssetResourceHandling()
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
        result.Should().Contain("test: /\\.(png|svg|jpg|jpeg|gif|woff|woff2|eot|ttf|otf)$/");
        result.Should().Contain("type: 'asset/resource'");
        result.Should().Contain("filename: 'assets/[name].[hash][ext]'");
    }

    [Fact]
    public void IncludesPlugins()
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
        result.Should().Contain("plugins: [");
        result.Should().Contain("new MiniCssExtractPlugin({");
        result.Should().Contain("filename: 'css/[name].[contenthash].css'");
        result.Should().Contain("new WebpackManifestPlugin({");
        result.Should().Contain("fileName: 'manifest.json'");
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
        result.Should().Contain("'@': path.resolve(__dirname, 'src')");
        result.Should().Contain("'~/Scripts': path.resolve(__dirname, 'src/js')");
        result.Should().Contain("'~/Content': path.resolve(__dirname, 'src/css')");
    }

    [Fact]
    public void ConfiguresModeAndDevtool()
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
        result.Should().Contain("mode: process.env.NODE_ENV === 'production' ? 'production' : 'development'");
        result.Should().Contain("devtool: process.env.NODE_ENV === 'production' ? 'source-map' : 'eval-source-map'");
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
        result.Should().StartWith("const path = require('path')");
        result.Should().EndWith("};");
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
        result.Should().Contain("const path = require('path')");
        result.Should().Contain("module.exports = {");
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
