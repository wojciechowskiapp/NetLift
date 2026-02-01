using FluentAssertions;
using NetLift.Transforms.Mvc.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ViewImportsGeneratorTests
{
    [Fact]
    public void GeneratesDefaultTagHelper()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
    }

    [Fact]
    public void GeneratesDefaultUsings()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc.Rendering");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc.ViewFeatures");
    }

    [Fact]
    public void AddsRootNamespace()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.Generate("MyApp");

        // Assert
        result.Should().Contain("@using MyApp");
        result.Should().Contain("@using MyApp.Models");
        result.Should().Contain("@using MyApp.ViewModels");
    }

    [Fact]
    public void MapsLegacyNamespaces()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("System.Web.Mvc");
        generator.AddNamespace("System.Web.Mvc.Html");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc.Rendering");
        result.Should().NotContain("System.Web.Mvc");
        result.Should().NotContain("System.Web.Mvc.Html");
    }

    [Fact]
    public void SkipsSystemWebNamespaces()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("System.Web.Optimization");
        generator.AddNamespace("System.Web.Security");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotContain("System.Web.Optimization");
        result.Should().NotContain("System.Web.Security");
        result.Should().NotContain("@using System.Web");
    }

    [Fact]
    public void GeneratesAreaViewImports()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.GenerateForArea("Admin", "MyApp");

        // Assert
        result.Should().Contain("@using MyApp");
        result.Should().Contain("@using MyApp.Areas.Admin");
        result.Should().Contain("@using MyApp.Areas.Admin.Models");
        result.Should().Contain("@using MyApp.Models");
        result.Should().Contain("@using MyApp.ViewModels");
    }

    [Fact]
    public void AddsInjectDeclarations()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddInjectDeclaration("Microsoft.Extensions.Configuration.IConfiguration", "Configuration");
        generator.AddInjectDeclaration("Microsoft.Extensions.Logging.ILogger", "Logger");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@inject Microsoft.Extensions.Configuration.IConfiguration Configuration");
        result.Should().Contain("@inject Microsoft.Extensions.Logging.ILogger Logger");
    }

    [Fact]
    public void AddsCustomTagHelperAssembly()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddTagHelperAssembly("MyApp.TagHelpers");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        result.Should().Contain("@addTagHelper *, MyApp.TagHelpers");
    }

    [Fact]
    public void PreservesNonSystemWebNamespaces()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("MyCompany.Shared");
        generator.AddNamespace("ThirdParty.Utilities");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@using MyCompany.Shared");
        result.Should().Contain("@using ThirdParty.Utilities");
    }

    [Fact]
    public void MapsSystemWebRouting()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("System.Web.Routing");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@using Microsoft.AspNetCore.Routing");
        result.Should().NotContain("System.Web.Routing");
    }

    [Fact]
    public void GenerateWithoutRootNamespace()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.Generate(null);

        // Assert
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc");
        result.Should().NotContain("@using .Models");
        result.Should().NotContain("@using .ViewModels");
    }

    [Fact]
    public void ThrowsWhenAreaNameIsNull()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var act = () => generator.GenerateForArea(null!, "MyApp");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("areaName");
    }

    [Fact]
    public void ThrowsWhenInjectTypeNameIsNull()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var act = () => generator.AddInjectDeclaration(null!, "Property");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("typeName");
    }

    [Fact]
    public void ThrowsWhenInjectPropertyNameIsNull()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var act = () => generator.AddInjectDeclaration("IConfiguration", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void SortsNamespacesAlphabetically()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("Zebra.Namespace");
        generator.AddNamespace("Alpha.Namespace");
        generator.AddNamespace("Beta.Namespace");

        // Act
        var result = generator.Generate();

        // Assert
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var customNamespaces = lines
            .Where(l => l.Contains("Alpha.") || l.Contains("Beta.") || l.Contains("Zebra."))
            .ToList();

        customNamespaces.Should().HaveCount(3);
        customNamespaces[0].Should().Contain("Alpha.Namespace");
        customNamespaces[1].Should().Contain("Beta.Namespace");
        customNamespaces[2].Should().Contain("Zebra.Namespace");
    }

    [Fact]
    public void DoesNotAddDuplicateNamespaces()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("Microsoft.AspNetCore.Mvc");
        generator.AddNamespace("Microsoft.AspNetCore.Mvc"); // Duplicate

        // Act
        var result = generator.Generate();

        // Assert
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var mvcUsingCount = lines.Count(l => l == "@using Microsoft.AspNetCore.Mvc");

        // Should appear only once from defaults (custom namespace was already in defaults so it gets mapped and deduplicated)
        mvcUsingCount.Should().Be(1);
    }

    [Fact]
    public void HandlesEmptyNamespaceGracefully()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("");
        generator.AddNamespace("  ");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
    }

    [Fact]
    public void HandlesEmptyTagHelperAssemblyGracefully()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddTagHelperAssembly("");
        generator.AddTagHelperAssembly("  ");

        // Act
        var result = generator.Generate();

        // Assert
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        var tagHelperCount = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Count(l => l.StartsWith("@addTagHelper"));

        // Only the default one should be present
        tagHelperCount.Should().Be(1);
    }

    [Fact]
    public void SortsInjectDeclarationsByPropertyName()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddInjectDeclaration("IZebra", "Zebra");
        generator.AddInjectDeclaration("IAlpha", "Alpha");
        generator.AddInjectDeclaration("IBeta", "Beta");

        // Act
        var result = generator.Generate();

        // Assert
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var injectLines = lines.Where(l => l.StartsWith("@inject")).ToList();

        injectLines.Should().HaveCount(3);
        injectLines[0].Should().Contain("Alpha");
        injectLines[1].Should().Contain("Beta");
        injectLines[2].Should().Contain("Zebra");
    }

    [Fact]
    public void GenerateForAreaThrowsOnEmptyAreaName()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var act = () => generator.GenerateForArea("", "MyApp");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("areaName");
    }

    [Fact]
    public void GenerateForAreaWithoutRootNamespace()
    {
        // Arrange
        var generator = new ViewImportsGenerator();

        // Act
        var result = generator.GenerateForArea("Admin", null);

        // Assert
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc");
        result.Should().NotContain("@using .Areas.Admin");
    }

    [Fact]
    public void CompleteExample_WithAllFeatures()
    {
        // Arrange
        var generator = new ViewImportsGenerator();
        generator.AddNamespace("System.Web.Mvc.Html");
        generator.AddNamespace("MyCompany.Shared");
        generator.AddNamespace("System.Web.Optimization"); // Should be filtered out
        generator.AddTagHelperAssembly("MyApp.TagHelpers");
        generator.AddInjectDeclaration("IConfiguration", "Configuration");

        // Act
        var result = generator.Generate("MyApp");

        // Assert
        result.Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        result.Should().Contain("@addTagHelper *, MyApp.TagHelpers");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc.Rendering");
        result.Should().Contain("@using Microsoft.AspNetCore.Mvc.ViewFeatures");
        result.Should().Contain("@using MyApp");
        result.Should().Contain("@using MyApp.Models");
        result.Should().Contain("@using MyApp.ViewModels");
        result.Should().Contain("@using MyCompany.Shared");
        result.Should().Contain("@inject IConfiguration Configuration");
        result.Should().NotContain("System.Web.Optimization");
        result.Should().NotContain("System.Web.Mvc.Html");
    }
}
