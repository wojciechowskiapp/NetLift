using FluentAssertions;
using NetLift.Transforms.Mvc.Generators;
using System.Text.Json;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class PackageJsonGeneratorTests
{
    private readonly PackageJsonGenerator _generator = new();

    [Fact]
    public void GeneratesVitePackageJson()
    {
        // Act
        var result = _generator.Generate(useVite: true);

        // Assert
        result.Should().Contain("\"name\": \"netlift-migrated-app\"");
        result.Should().Contain("\"version\": \"1.0.0\"");
        result.Should().Contain("\"private\": true");
        result.Should().Contain("\"type\": \"module\"");
    }

    [Fact]
    public void GeneratesWebpackPackageJson()
    {
        // Act
        var result = _generator.Generate(useVite: false);

        // Assert
        result.Should().Contain("\"name\": \"netlift-migrated-app\"");
        result.Should().Contain("\"version\": \"1.0.0\"");
        result.Should().Contain("\"private\": true");
    }

    [Fact]
    public void IncludesViteScripts()
    {
        // Act
        var result = _generator.Generate(useVite: true);

        // Assert
        result.Should().Contain("\"dev\": \"vite\"");
        result.Should().Contain("\"build\": \"vite build\"");
        result.Should().Contain("\"preview\": \"vite preview\"");
        result.Should().Contain("\"watch\": \"vite build --watch\"");
    }

    [Fact]
    public void IncludesWebpackScripts()
    {
        // Act
        var result = _generator.Generate(useVite: false);

        // Assert
        result.Should().Contain("\"dev\": \"webpack serve --mode development\"");
        result.Should().Contain("\"build\": \"webpack --mode production\"");
        result.Should().Contain("\"watch\": \"webpack --watch --mode development\"");
    }

    [Fact]
    public void IncludesViteDependencies()
    {
        // Act
        var result = _generator.Generate(useVite: true);

        // Assert
        result.Should().Contain("\"vite\":");
        result.Should().Contain("\"@vitejs/plugin-legacy\":");
    }

    [Fact]
    public void IncludesWebpackDependencies()
    {
        // Act
        var result = _generator.Generate(useVite: false);

        // Assert
        result.Should().Contain("\"webpack\":");
        result.Should().Contain("\"webpack-cli\":");
        result.Should().Contain("\"webpack-dev-server\":");
        result.Should().Contain("\"webpack-manifest-plugin\":");
        result.Should().Contain("\"mini-css-extract-plugin\":");
        result.Should().Contain("\"css-loader\":");
        result.Should().Contain("\"sass-loader\":");
        result.Should().Contain("\"sass\":");
    }

    [Fact]
    public void GeneratesValidJson()
    {
        // Act
        var result = _generator.Generate(useVite: true);

        // Assert
        var parseAction = () => JsonDocument.Parse(result);
        parseAction.Should().NotThrow("the generated JSON should be valid");
    }

    [Fact]
    public void ViteJsonIsValid()
    {
        // Act
        var result = _generator.Generate(useVite: true);

        // Assert
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("name").GetString().Should().Be("netlift-migrated-app");
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("private").GetBoolean().Should().BeTrue();
        root.GetProperty("type").GetString().Should().Be("module");
        root.GetProperty("scripts").GetProperty("dev").GetString().Should().Be("vite");
        root.GetProperty("devDependencies").GetProperty("vite").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WebpackJsonIsValid()
    {
        // Act
        var result = _generator.Generate(useVite: false);

        // Assert
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("name").GetString().Should().Be("netlift-migrated-app");
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("private").GetBoolean().Should().BeTrue();
        root.GetProperty("scripts").GetProperty("build").GetString().Should().Be("webpack --mode production");
        root.GetProperty("devDependencies").GetProperty("webpack").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DefaultsToVite()
    {
        // Act
        var result = _generator.Generate();

        // Assert
        result.Should().Contain("\"vite\":");
        result.Should().NotContain("\"webpack\":");
    }

    [Fact]
    public void HasEmptyDependenciesObject()
    {
        // Act
        var result = _generator.Generate();

        // Assert
        result.Should().Contain("\"dependencies\": {");
        // Empty dependencies object should be present for users to add runtime deps
    }

    [Fact]
    public void HasDevDependenciesSection()
    {
        // Act
        var result = _generator.Generate();

        // Assert
        result.Should().Contain("\"devDependencies\": {");
    }

    [Fact]
    public void HasScriptsSection()
    {
        // Act
        var result = _generator.Generate();

        // Assert
        result.Should().Contain("\"scripts\": {");
    }
}
