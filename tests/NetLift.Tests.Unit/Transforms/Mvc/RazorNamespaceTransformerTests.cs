using FluentAssertions;
using NetLift.Transforms.Mvc.Rewriters;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class RazorNamespaceTransformerTests
{
    private readonly RazorNamespaceTransformer _transformer = new();

    [Fact]
    public void TransformsPagedListMvcUsingDirective()
    {
        // Arrange
        var viewContent = "@using PagedList.Mvc";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().NotContain("@using PagedList.Mvc");
    }

    [Fact]
    public void TransformsPagedListUsingDirective()
    {
        // Arrange
        var viewContent = "@using PagedList";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().NotContain("@using PagedList");
    }

    [Fact]
    public void TransformsPagedListMvcWithSemicolon()
    {
        // Arrange
        var viewContent = "@using PagedList.Mvc;";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().NotContain("@using PagedList.Mvc");
    }

    [Fact]
    public void TransformsPagedListWithSemicolon()
    {
        // Arrange
        var viewContent = "@using PagedList;";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().NotContain("@using PagedList;");
    }

    [Fact]
    public void TransformsIPagedListInModelDeclaration()
    {
        // Arrange
        var viewContent = "@model PagedList.IPagedList<MyApp.Models.Product>";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Be("@model X.PagedList.IPagedList<MyApp.Models.Product>");
    }

    [Fact]
    public void TransformsPagedListTypeInModelDeclaration()
    {
        // Arrange
        var viewContent = "@model PagedList.PagedList<MyApp.Models.Product>";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@model X.PagedList.PagedList<MyApp.Models.Product>");
        result.Should().NotContain("@model PagedList.PagedList");
    }

    [Fact]
    public void TransformsMultipleOccurrences()
    {
        // Arrange
        var viewContent = """
            @using PagedList
            @using PagedList.Mvc
            @model PagedList.IPagedList<Product>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().Contain("@model X.PagedList.IPagedList<Product>");
        result.Should().NotContain("@using PagedList.Mvc");
        result.Should().NotContain("@using PagedList\n");
    }

    [Fact]
    public void HandlesRealWorldIndexView()
    {
        // Arrange
        var viewContent = """
            @using PagedList
            @using PagedList.Mvc
            @model PagedList.IPagedList<MyApp.Models.Product>

            <h2>Products</h2>

            <table class="table">
                @foreach (var product in Model)
                {
                    <tr>
                        <td>@product.Name</td>
                    </tr>
                }
            </table>

            @Html.PagedListPager(Model, page => Url.Action("Index", new { page }))
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().Contain("@model X.PagedList.IPagedList<MyApp.Models.Product>");
        result.Should().Contain("@Html.PagedListPager"); // Should remain unchanged
    }

    [Fact]
    public void PreservesOtherContent()
    {
        // Arrange
        var viewContent = """
            @model MyApp.Models.ProductViewModel
            @using MyApp.Services

            <h1>Product List</h1>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Be(viewContent);
    }

    [Fact]
    public void HandlesEmptyContent()
    {
        // Arrange
        var viewContent = "";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HandlesNullContent()
    {
        // Arrange
        string? viewContent = null;

        // Act
        var result = _transformer.TransformRazorView(viewContent!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HandlesWhitespaceContent()
    {
        // Arrange
        var viewContent = "   \n\t  ";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Be(viewContent);
    }

    [Fact]
    public void TransformsWithVariousWhitespace()
    {
        // Arrange
        var viewContent = """
            @using   PagedList
            @using     PagedList.Mvc
            @model  PagedList.IPagedList<Product>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().Contain("@model  X.PagedList.IPagedList<Product>");
    }

    [Fact]
    public void DoesNotTransformNonPagedListNamespaces()
    {
        // Arrange
        var viewContent = """
            @using System.Collections.Generic
            @using MyApp.PagedList.Extensions
            @model List<Product>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Be(viewContent);
    }

    [Fact]
    public void TransformsOnlyPagedListNotSubNamespaces()
    {
        // Arrange
        var viewContent = """
            @using PagedList
            @using PagedList.Mvc
            @using MyCustom.PagedList.Extension
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().Contain("@using X.PagedList.Mvc.Core");
        result.Should().Contain("@using MyCustom.PagedList.Extension"); // Should remain unchanged
    }

    [Fact]
    public void TransformsPagedListMvcBeforePagedList()
    {
        // Arrange
        // This tests that PagedList.Mvc is transformed correctly and not partially transformed
        var viewContent = "@using PagedList.Mvc";

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Be("@using X.PagedList.Mvc.Core");
    }

    [Fact]
    public void HandlesComplexGenericTypes()
    {
        // Arrange
        var viewContent = """
            @model PagedList.IPagedList<Dictionary<string, List<Product>>>
            @model PagedList.PagedList<Tuple<int, string>>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@model X.PagedList.IPagedList<Dictionary<string, List<Product>>>");
        result.Should().Contain("@model X.PagedList.PagedList<Tuple<int, string>>");
    }

    [Fact]
    public void TransformsInCodeBlocks()
    {
        // Arrange
        var viewContent = """
            @{
                var items = Model as PagedList.IPagedList<Product>;
                var list = new PagedList.PagedList<int>();
            }
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("X.PagedList.IPagedList<Product>");
        result.Should().Contain("X.PagedList.PagedList<int>");
    }

    [Fact]
    public void PreservesLayoutAndOtherRazorDirectives()
    {
        // Arrange
        var viewContent = """
            @using PagedList
            @model PagedList.IPagedList<Product>
            @{
                ViewBag.Title = "Products";
                Layout = "~/Views/Shared/_Layout.cshtml";
            }

            <h2>@ViewBag.Title</h2>
            """;

        // Act
        var result = _transformer.TransformRazorView(viewContent);

        // Assert
        result.Should().Contain("@using X.PagedList");
        result.Should().Contain("@model X.PagedList.IPagedList<Product>");
        result.Should().Contain("ViewBag.Title = \"Products\"");
        result.Should().Contain("Layout = \"~/Views/Shared/_Layout.cshtml\"");
        result.Should().Contain("<h2>@ViewBag.Title</h2>");
    }
}
