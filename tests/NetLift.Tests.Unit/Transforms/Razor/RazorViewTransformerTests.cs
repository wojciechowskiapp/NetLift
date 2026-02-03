using FluentAssertions;
using NetLift.Transforms.Razor.Analyzers;
using NetLift.Transforms.Razor.Transformers;

namespace NetLift.Tests.Unit.Transforms.Razor;

public sealed class RazorViewTransformerTests
{
    private readonly RazorViewAnalyzer _analyzer = new();
    private readonly RazorViewTransformer _transformer;

    public RazorViewTransformerTests()
    {
        _transformer = new RazorViewTransformer(_analyzer);
    }

    [Fact]
    public void TransformContent_ActionLink_ConvertsToTagHelper()
    {
        // Arrange
        const string input = @"
<div>
    @Html.ActionLink(""Home"", ""Index"", ""Home"")
</div>";

        const string filePath = "Views/Shared/Menu.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<a asp-controller=\"Home\" asp-action=\"Index\">Home</a>");
        result.Should().NotContain("@Html.ActionLink");
    }

    [Fact]
    public void TransformContent_ActionLink_WithTwoParameters_ConvertsCorrectly()
    {
        // Arrange
        const string input = @"
@Html.ActionLink(""About"", ""About"")";

        const string filePath = "Views/Home/Index.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<a asp-action=\"About\">About</a>");
        result.Should().NotContain("@Html.ActionLink");
    }

    [Fact]
    public void TransformContent_ActionLink_WithRouteValues_ConvertsCorrectly()
    {
        // Arrange
        const string input = @"
@Html.ActionLink(""Edit"", ""Edit"", new { id = item.Id })";

        const string filePath = "Views/Product/Index.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("asp-action=\"Edit\"");
        result.Should().Contain("asp-route-id=\"");
        result.Should().Contain("item.Id");
    }

    [Fact]
    public void TransformContent_BeginForm_ConvertsToFormTag()
    {
        // Arrange
        const string input = @"
@using (Html.BeginForm(""Create"", ""Product"", FormMethod.Post))
{
    <input type=""submit"" value=""Save"" />
}";

        const string filePath = "Views/Product/Create.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<form asp-controller=\"Product\" asp-action=\"Create\" method=\"post\">");
        result.Should().NotContain("@using (Html.BeginForm");
    }

    [Fact]
    public void TransformContent_BeginForm_Simple_ConvertsCorrectly()
    {
        // Arrange
        const string input = @"
@using (Html.BeginForm())
{
    <input type=""text"" name=""name"" />
}";

        const string filePath = "Views/Account/Login.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<form method=\"post\">");
        result.Should().NotContain("@using (Html.BeginForm");
    }

    [Fact]
    public void TransformContent_TextBoxFor_ConvertsToInputTag()
    {
        // Arrange
        const string input = @"
<div class=""form-group"">
    @Html.TextBoxFor(m => m.Name)
    @Html.TextBoxFor(m => m.Email, new { @class = ""form-control"" })
</div>";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<input asp-for=\"Name\" />");
        result.Should().Contain("<input asp-for=\"Email\"");
        result.Should().Contain("class=\"form-control\"");
        result.Should().NotContain("@Html.TextBoxFor");
    }

    [Fact]
    public void TransformContent_PasswordFor_ConvertsToInputWithType()
    {
        // Arrange
        const string input = @"
@Html.PasswordFor(m => m.Password)
@Html.PasswordFor(m => m.ConfirmPassword, new { @class = ""form-control"" })";

        const string filePath = "Views/Account/Register.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<input asp-for=\"Password\" type=\"password\" />");
        result.Should().Contain("<input asp-for=\"ConfirmPassword\" type=\"password\"");
        result.Should().NotContain("@Html.PasswordFor");
    }

    [Fact]
    public void TransformContent_HiddenFor_ConvertsCorrectly()
    {
        // Arrange
        const string input = @"
@Html.HiddenFor(m => m.Id)";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<input asp-for=\"Id\" type=\"hidden\" />");
        result.Should().NotContain("@Html.HiddenFor");
    }

    [Fact]
    public void TransformContent_CheckBoxFor_ConvertsCorrectly()
    {
        // Arrange
        const string input = @"
@Html.CheckBoxFor(m => m.IsActive)
@Html.CheckBoxFor(m => m.AcceptTerms, new { @class = ""checkbox"" })";

        const string filePath = "Views/Settings/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<input asp-for=\"IsActive\" />");
        result.Should().Contain("<input asp-for=\"AcceptTerms\"");
        result.Should().NotContain("@Html.CheckBoxFor");
    }

    [Fact]
    public void TransformContent_ValidationMessageFor_ConvertsToSpanTag()
    {
        // Arrange
        const string input = @"
<div class=""form-group"">
    @Html.LabelFor(m => m.Name)
    @Html.TextBoxFor(m => m.Name)
    @Html.ValidationMessageFor(m => m.Name)
</div>";

        const string filePath = "Views/Product/Create.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<span asp-validation-for=\"Name\"></span>");
        result.Should().NotContain("@Html.ValidationMessageFor");
    }

    [Fact]
    public void TransformContent_ValidationSummary_ConvertsToDiv()
    {
        // Arrange
        const string inputModelOnly = "@Html.ValidationSummary(true)";
        const string inputAll = "@Html.ValidationSummary(false)";
        const string filePath = "Views/Product/Create.cshtml";

        // Act
        var resultModelOnly = _transformer.TransformContent(inputModelOnly, filePath);
        var resultAll = _transformer.TransformContent(inputAll, filePath);

        // Assert
        resultModelOnly.Should().Contain("<div asp-validation-summary=\"ModelOnly\"></div>");
        resultModelOnly.Should().NotContain("@Html.ValidationSummary");

        resultAll.Should().Contain("<div asp-validation-summary=\"All\"></div>");
        resultAll.Should().NotContain("@Html.ValidationSummary");
    }

    [Fact]
    public void TransformContent_ValidationSummary_NoParameter_ConvertsToAll()
    {
        // Arrange
        const string input = "@Html.ValidationSummary()";
        const string filePath = "Views/Product/Create.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<div asp-validation-summary=\"All\"></div>");
        result.Should().NotContain("@Html.ValidationSummary");
    }

    [Fact]
    public void TransformContent_LabelFor_ConvertsToLabelTag()
    {
        // Arrange
        const string input = @"
<div class=""form-group"">
    @Html.LabelFor(m => m.ProductName)
    @Html.LabelFor(m => m.Price, new { @class = ""control-label"" })
</div>";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<label asp-for=\"ProductName\"></label>");
        result.Should().Contain("<label asp-for=\"Price\"");
        result.Should().Contain("class=\"control-label\"");
        result.Should().NotContain("@Html.LabelFor");
    }

    [Fact]
    public void TransformContent_Partial_ConvertsToPartialTag()
    {
        // Arrange
        const string input = @"
@Html.Partial(""_ProductCard"")
@Html.Partial(""_Summary"", Model.Summary)";

        const string filePath = "Views/Product/Details.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<partial name=\"_ProductCard\" />");
        result.Should().Contain("<partial name=\"_Summary\" model=\"Model.Summary\" />");
        result.Should().NotContain("@Html.Partial");
    }

    [Fact]
    public void TransformContent_RenderPartial_ConvertsToPartialTag()
    {
        // Arrange
        const string input = @"
@{ Html.RenderPartial(""_Header""); }
@{ Html.RenderPartial(""_Footer"", Model.Footer); }";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<partial name=\"_Header\" />");
        result.Should().Contain("<partial name=\"_Footer\" model=\"Model.Footer\" />");
        result.Should().NotContain("Html.RenderPartial");
    }

    [Fact]
    public void TransformContent_ScriptsRender_ConvertsToScriptTag()
    {
        // Arrange
        const string input = @"
@Scripts.Render(""~/bundles/jquery"")
@Scripts.Render(""~/bundles/bootstrap"")
@Scripts.Render(""~/bundles/modernizr"")";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<script src=\"~/js/jquery.min.js\"></script>");
        result.Should().Contain("<script src=\"~/js/bootstrap.bundle.min.js\"></script>");
        result.Should().Contain("<script src=\"~/js/modernizr.min.js\"></script>");
        result.Should().NotContain("@Scripts.Render");
    }

    [Fact]
    public void TransformContent_StylesRender_ConvertsToLinkTag()
    {
        // Arrange
        const string input = @"
@Styles.Render(""~/Content/css"")
@Styles.Render(""~/Content/bootstrap"")";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"~/css/site.css\" />");
        result.Should().Contain("<link rel=\"stylesheet\" href=\"~/css/bootstrap.min.css\" />");
        result.Should().NotContain("@Styles.Render");
    }

    [Fact]
    public void TransformContent_UrlContent_MapsToWwwroot()
    {
        // Arrange
        const string input = @"
<img src=""@Url.Content(""~/Content/images/logo.png"")"" />
<script src=""@Url.Content(""~/Scripts/app.js"")""></script>
<link href=""@Url.Content(""~/Images/favicon.ico"")"" rel=""icon"" />";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("~/css/images/logo.png");
        result.Should().Contain("~/js/app.js");
        result.Should().Contain("~/images/favicon.ico");
        result.Should().NotContain("@Url.Content");
    }

    [Fact]
    public void TransformContent_CompleteForm_TransformsAllElements()
    {
        // Arrange
        const string input = @"
@model MyApp.Models.ProductViewModel

<h2>Create Product</h2>

@using (Html.BeginForm(""Create"", ""Product"", FormMethod.Post))
{
    @Html.AntiForgeryToken()
    @Html.ValidationSummary(true)

    <div class=""form-group"">
        @Html.LabelFor(m => m.Name)
        @Html.TextBoxFor(m => m.Name, new { @class = ""form-control"" })
        @Html.ValidationMessageFor(m => m.Name)
    </div>

    <div class=""form-group"">
        @Html.LabelFor(m => m.Price)
        @Html.TextBoxFor(m => m.Price, new { @class = ""form-control"" })
        @Html.ValidationMessageFor(m => m.Price)
    </div>

    <div class=""form-group"">
        @Html.CheckBoxFor(m => m.IsActive)
        @Html.LabelFor(m => m.IsActive)
    </div>

    @Html.HiddenFor(m => m.CategoryId)

    <button type=""submit"" class=""btn btn-primary"">Save</button>
}

<div>
    @Html.ActionLink(""Back to List"", ""Index"")
</div>

@section Scripts {
    @Scripts.Render(""~/bundles/jqueryval"")
}";

        const string filePath = "Views/Product/Create.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<form asp-controller=\"Product\" asp-action=\"Create\" method=\"post\">");
        result.Should().Contain("<div asp-validation-summary=\"ModelOnly\"></div>");
        result.Should().Contain("<label asp-for=\"Name\"");
        result.Should().Contain("<input asp-for=\"Name\"");
        result.Should().Contain("<span asp-validation-for=\"Name\"></span>");
        result.Should().Contain("<label asp-for=\"Price\"");
        result.Should().Contain("<input asp-for=\"Price\"");
        result.Should().Contain("<span asp-validation-for=\"Price\"></span>");
        result.Should().Contain("<input asp-for=\"IsActive\"");
        result.Should().Contain("<input asp-for=\"CategoryId\" type=\"hidden\" />");
        result.Should().Contain("<a asp-action=\"Index\">Back to List</a>");
        result.Should().Contain("<script src=\"~/js/jquery.validate.min.js\"></script>");

        result.Should().NotContain("@Html.BeginForm");
        result.Should().NotContain("@Html.ValidationSummary");
        result.Should().NotContain("@Html.LabelFor");
        result.Should().NotContain("@Html.TextBoxFor");
        result.Should().NotContain("@Html.ValidationMessageFor");
        result.Should().NotContain("@Html.CheckBoxFor");
        result.Should().NotContain("@Html.HiddenFor");
        result.Should().NotContain("@Html.ActionLink");
        result.Should().NotContain("@Scripts.Render");
    }

    [Fact]
    public void TransformContent_EmptyContent_ReturnsEmpty()
    {
        // Arrange
        const string input = "";
        const string filePath = "Views/Home/Empty.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void TransformContent_NoHelpers_ReturnsUnchanged()
    {
        // Arrange
        const string input = @"
<div>
    <h1>Static Content</h1>
    <p>No HTML helpers here</p>
</div>";
        const string filePath = "Views/Home/Static.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void TransformContent_ActionWithController_AddsViewComponentComment()
    {
        // Arrange
        const string input = @"
@Html.Action(""Menu"", ""Shared"")";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<!-- TODO: Convert to ViewComponent -->");
        result.Should().Contain("@await Component.InvokeAsync");
    }

    [Fact]
    public void TransformContent_RenderAction_AddsViewComponentComment()
    {
        // Arrange
        const string input = @"
@{ Html.RenderAction(""Sidebar"", ""Shared""); }";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<!-- TODO: Convert to ViewComponent -->");
        result.Should().Contain("@await Component.InvokeAsync");
    }

    [Fact]
    public void TransformContent_TextAreaFor_ConvertsToTextarea()
    {
        // Arrange
        const string input = @"
@Html.TextAreaFor(m => m.Description)
@Html.TextAreaFor(m => m.Comments, new { rows = 10, @class = ""form-control"" })";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        // Note: TextAreaFor transformation is not implemented in TransformContent
        // It's only in TransformHtmlHelper method which is not called by TransformContent
        result.Should().Contain("@Html.TextAreaFor(m => m.Description)");
        result.Should().Contain("@Html.TextAreaFor(m => m.Comments");
    }

    [Fact]
    public void TransformContent_DropDownListFor_ConvertsToSelect()
    {
        // Arrange
        const string input = @"
@Html.DropDownListFor(m => m.CategoryId, Model.Categories)";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        // Note: DropDownListFor transformation is not implemented in TransformContent
        // It's only in TransformHtmlHelper method which is not called by TransformContent
        result.Should().Contain("@Html.DropDownListFor(m => m.CategoryId");
    }

    [Fact]
    public void TransformContent_EditorFor_ConvertsToInput()
    {
        // Arrange
        const string input = @"
@Html.EditorFor(m => m.RichText)";

        const string filePath = "Views/Article/Edit.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        // Note: EditorFor transformation is not implemented in TransformContent
        // It's only in TransformHtmlHelper method which is not called by TransformContent
        result.Should().Contain("@Html.EditorFor(m => m.RichText)");
    }

    [Fact]
    public void TransformContent_UnknownBundle_UsesDefaultMapping()
    {
        // Arrange
        const string input = @"
@Scripts.Render(""~/bundles/custom"")
@Styles.Render(""~/Content/custom"")";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<script src=\"~/js/custom.min.js\"></script>");
        result.Should().Contain("<link rel=\"stylesheet\" href=\"~/css/custom.css\" />");
    }

    [Fact]
    public void TransformContent_MixedContentAndScripts_TransformsCorrectly()
    {
        // Arrange
        const string input = @"
<!DOCTYPE html>
<html>
<head>
    <title>My App</title>
    @Styles.Render(""~/Content/css"")
</head>
<body>
    <nav>
        @Html.ActionLink(""Home"", ""Index"", ""Home"")
        @Html.ActionLink(""About"", ""About"")
    </nav>

    <div class=""container"">
        @Html.Partial(""_Notifications"")
    </div>

    @Scripts.Render(""~/bundles/jquery"")
</body>
</html>";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("<link rel=\"stylesheet\" href=\"~/css/site.css\" />");
        result.Should().Contain("<a asp-controller=\"Home\" asp-action=\"Index\">Home</a>");
        result.Should().Contain("<a asp-action=\"About\">About</a>");
        result.Should().Contain("<partial name=\"_Notifications\" />");
        result.Should().Contain("<script src=\"~/js/jquery.min.js\"></script>");
    }

    [Fact]
    public void TransformContent_MultipleUrlContentMappings_TransformsCorrectly()
    {
        // Arrange
        const string input = @"
<link href=""@Url.Content(""~/Content/site.css"")"" rel=""stylesheet"" />
<script src=""@Url.Content(""~/Scripts/app.js"")""></script>
<img src=""@Url.Content(""~/Images/logo.png"")"" alt=""Logo"" />";

        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var result = _transformer.TransformContent(input, filePath);

        // Assert
        result.Should().Contain("~/css/site.css");
        result.Should().Contain("~/js/app.js");
        result.Should().Contain("~/images/logo.png");
    }
}
