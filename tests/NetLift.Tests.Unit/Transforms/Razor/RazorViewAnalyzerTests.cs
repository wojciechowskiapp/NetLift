using FluentAssertions;
using NetLift.Core.Models.Razor;
using NetLift.Transforms.Razor.Analyzers;

namespace NetLift.Tests.Unit.Transforms.Razor;

public sealed class RazorViewAnalyzerTests
{
    private readonly RazorViewAnalyzer _analyzer = new();

    [Fact]
    public void DetectHtmlHelpers_ActionLink_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.ActionLink(""Home"", ""Index"", ""Home"")
    @Html.ActionLink(""About"", ""About"")
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);

        var firstHelper = helpers[0];
        firstHelper.HelperType.Should().Be(HtmlHelperType.ActionLink);
        firstHelper.OriginalCode.Should().Contain("ActionLink");
        firstHelper.OriginalCode.Should().Contain("Home");
        firstHelper.LineNumber.Should().Be(3);
        firstHelper.Confidence.Should().Be(95);

        var secondHelper = helpers[1];
        secondHelper.HelperType.Should().Be(HtmlHelperType.ActionLink);
        secondHelper.LineNumber.Should().Be(4);
    }

    [Fact]
    public void DetectHtmlHelpers_BeginForm_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
@using (Html.BeginForm(""Create"", ""Products""))
{
    <input type=""submit"" value=""Save"" />
}";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().ContainSingle();

        var helper = helpers[0];
        helper.HelperType.Should().Be(HtmlHelperType.BeginForm);
        helper.OriginalCode.Should().Contain("BeginForm");
        helper.OriginalCode.Should().Contain("Create");
        helper.OriginalCode.Should().Contain("Products");
        helper.LineNumber.Should().Be(2);
        helper.Confidence.Should().Be(95);
    }

    [Fact]
    public void DetectHtmlHelpers_TextBoxFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.TextBoxFor(m => m.Name)
    @Html.TextBoxFor(m => m.Email, new { @class = ""form-control"" })
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);

        var firstHelper = helpers[0];
        firstHelper.HelperType.Should().Be(HtmlHelperType.TextBoxFor);
        firstHelper.OriginalCode.Should().Contain("TextBoxFor");
        firstHelper.OriginalCode.Should().Contain("m.Name");
        firstHelper.LineNumber.Should().Be(3);
        firstHelper.Confidence.Should().Be(90);

        var secondHelper = helpers[1];
        secondHelper.HelperType.Should().Be(HtmlHelperType.TextBoxFor);
        secondHelper.OriginalCode.Should().Contain("m.Email");
        secondHelper.LineNumber.Should().Be(4);
    }

    [Fact]
    public void DetectHtmlHelpers_ValidationMessageFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div class=""form-group"">
    @Html.LabelFor(m => m.Name)
    @Html.TextBoxFor(m => m.Name)
    @Html.ValidationMessageFor(m => m.Name)
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(3);

        var validationHelper = helpers.FirstOrDefault(h => h.HelperType == HtmlHelperType.ValidationMessageFor);
        validationHelper.Should().NotBeNull();
        validationHelper!.OriginalCode.Should().Contain("ValidationMessageFor");
        validationHelper.OriginalCode.Should().Contain("m.Name");
        validationHelper.LineNumber.Should().Be(5);
        validationHelper.Confidence.Should().Be(90);
    }

    [Fact]
    public void DetectHtmlHelpers_ValidationSummary_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
@Html.ValidationSummary(true)
<form>
    @Html.ValidationSummary(false)
    <input type=""submit"" />
</form>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);

        var firstSummary = helpers[0];
        firstSummary.HelperType.Should().Be(HtmlHelperType.ValidationSummary);
        firstSummary.OriginalCode.Should().Contain("ValidationSummary");
        firstSummary.OriginalCode.Should().Contain("true");
        firstSummary.LineNumber.Should().Be(2);
        firstSummary.Confidence.Should().Be(90);

        var secondSummary = helpers[1];
        secondSummary.HelperType.Should().Be(HtmlHelperType.ValidationSummary);
        secondSummary.OriginalCode.Should().Contain("false");
        secondSummary.LineNumber.Should().Be(4);
    }

    [Fact]
    public void DetectHtmlHelpers_LabelFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div class=""form-group"">
    @Html.LabelFor(m => m.ProductName)
    @Html.LabelFor(m => m.Price, new { @class = ""control-label"" })
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);

        var firstLabel = helpers[0];
        firstLabel.HelperType.Should().Be(HtmlHelperType.LabelFor);
        firstLabel.OriginalCode.Should().Contain("LabelFor");
        firstLabel.OriginalCode.Should().Contain("m.ProductName");
        firstLabel.LineNumber.Should().Be(3);
        firstLabel.Confidence.Should().Be(95);

        var secondLabel = helpers[1];
        secondLabel.HelperType.Should().Be(HtmlHelperType.LabelFor);
        secondLabel.OriginalCode.Should().Contain("m.Price");
        secondLabel.LineNumber.Should().Be(4);
    }

    [Fact]
    public void DetectHtmlHelpers_MultipleHelpers_DetectsAll()
    {
        // Arrange
        const string content = @"
@model MyApp.Models.ProductViewModel

<h2>Product Form</h2>

@using (Html.BeginForm(""Create"", ""Product""))
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

    <button type=""submit"">Save</button>
}

<div>
    @Html.ActionLink(""Back to List"", ""Index"")
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCountGreaterOrEqualTo(10);

        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.BeginForm);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.AntiForgeryToken);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.ValidationSummary);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.LabelFor);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.TextBoxFor);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.ValidationMessageFor);
        helpers.Should().Contain(h => h.HelperType == HtmlHelperType.ActionLink);
    }

    [Fact]
    public void DetectBundleReferences_Scripts_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
@Scripts.Render(""~/bundles/jquery"")
@Scripts.Render(""~/bundles/bootstrap"")
@Scripts.Render(""~/bundles/modernizr"")";

        // Act
        var bundles = _analyzer.DetectBundleReferences(content);

        // Assert
        bundles.Should().HaveCount(3);

        var jqueryBundle = bundles[0];
        jqueryBundle.BundleType.Should().Be(BundleReferenceType.Scripts);
        jqueryBundle.BundlePath.Should().Be("~/bundles/jquery");
        jqueryBundle.OriginalCode.Should().Contain("Scripts.Render");
        jqueryBundle.LineNumber.Should().Be(2);

        var bootstrapBundle = bundles[1];
        bootstrapBundle.BundleType.Should().Be(BundleReferenceType.Scripts);
        bootstrapBundle.BundlePath.Should().Be("~/bundles/bootstrap");
        bootstrapBundle.LineNumber.Should().Be(3);

        var modernizrBundle = bundles[2];
        modernizrBundle.BundleType.Should().Be(BundleReferenceType.Scripts);
        modernizrBundle.BundlePath.Should().Be("~/bundles/modernizr");
        modernizrBundle.LineNumber.Should().Be(4);
    }

    [Fact]
    public void DetectBundleReferences_Styles_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<!DOCTYPE html>
<html>
<head>
    @Styles.Render(""~/Content/css"")
    @Styles.Render(""~/Content/bootstrap"")
</head>
<body>
    <h1>Welcome</h1>
</body>
</html>";

        // Act
        var bundles = _analyzer.DetectBundleReferences(content);

        // Assert
        bundles.Should().HaveCount(2);

        var cssBundle = bundles[0];
        cssBundle.BundleType.Should().Be(BundleReferenceType.Styles);
        cssBundle.BundlePath.Should().Be("~/Content/css");
        cssBundle.OriginalCode.Should().Contain("Styles.Render");
        cssBundle.LineNumber.Should().Be(5);

        var bootstrapBundle = bundles[1];
        bootstrapBundle.BundleType.Should().Be(BundleReferenceType.Styles);
        bootstrapBundle.BundlePath.Should().Be("~/Content/bootstrap");
        bootstrapBundle.LineNumber.Should().Be(6);
    }

    [Fact]
    public void AnalyzeView_ExtractsModelType()
    {
        // Arrange
        const string content = @"
@model MyApp.ViewModels.ProductViewModel

<h2>Product Details</h2>
<p>@Model.Name</p>";
        const string filePath = "Views/Product/Details.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.ModelType.Should().Be("MyApp.ViewModels.ProductViewModel");
        viewInfo.ViewName.Should().Be("Details");
        viewInfo.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void AnalyzeView_ExtractsLayout()
    {
        // Arrange
        const string content = @"
@{
    Layout = ""~/Views/Shared/_Layout.cshtml"";
}

<h2>My Page</h2>";
        const string filePath = "Views/Home/Index.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.Layout.Should().Be("~/Views/Shared/_Layout.cshtml");
        viewInfo.ViewName.Should().Be("Index");
    }

    [Fact]
    public void AnalyzeView_DetectsViewBag()
    {
        // Arrange
        const string content = @"
<h2>@ViewBag.Title</h2>
<p>@ViewBag.Message</p>
<span>@ViewBag.Count</span>";
        const string filePath = "Views/Home/About.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.UsesViewBag.Should().BeTrue();
        viewInfo.ViewName.Should().Be("About");
    }

    [Fact]
    public void AnalyzeView_CalculatesConfidence()
    {
        // Arrange
        const string contentHighConfidence = @"
@model MyApp.Models.Product

<div>
    @Html.ActionLink(""Home"", ""Index"")
    @Html.LabelFor(m => m.Name)
    @Html.TextBoxFor(m => m.Name)
</div>
@Scripts.Render(""~/bundles/jquery"")";

        const string contentLowConfidence = @"
@Html.Action(""Menu"", ""Shared"")
@Html.RenderAction(""Sidebar"")";

        const string filePath = "Views/Product/Edit.cshtml";

        // Act
        var highConfidenceView = _analyzer.AnalyzeView(contentHighConfidence, filePath);
        var lowConfidenceView = _analyzer.AnalyzeView(contentLowConfidence, filePath);

        // Assert
        highConfidenceView.Confidence.Should().BeGreaterThan(80);
        lowConfidenceView.Confidence.Should().BeLessThan(80);
    }

    [Fact]
    public void AnalyzeView_EmptyContent_ReturnsValidInfo()
    {
        // Arrange
        const string content = "";
        const string filePath = "Views/Home/Empty.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.Should().NotBeNull();
        viewInfo.ViewName.Should().Be("Empty");
        viewInfo.FilePath.Should().Be(filePath);
        viewInfo.HtmlHelpers.Should().BeEmpty();
        viewInfo.BundleReferences.Should().BeEmpty();
        viewInfo.ModelType.Should().BeNull();
        viewInfo.Layout.Should().BeNull();
        viewInfo.UsesViewBag.Should().BeFalse();
        viewInfo.Confidence.Should().Be(100); // Nothing to transform
    }

    [Fact]
    public void AnalyzeView_LayoutFile_IsDetected()
    {
        // Arrange
        const string content = @"
<!DOCTYPE html>
<html>
<head>
    <title>@ViewBag.Title</title>
    @Styles.Render(""~/Content/css"")
</head>
<body>
    @RenderBody()
    @Scripts.Render(""~/bundles/jquery"")
</body>
</html>";
        const string filePath = "Views/Shared/_Layout.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.IsLayout.Should().BeTrue();
        viewInfo.ViewName.Should().Be("_Layout");
    }

    [Fact]
    public void DetectHtmlHelpers_NoHelpers_ReturnsEmpty()
    {
        // Arrange
        const string content = @"
<div>
    <h1>Static Content</h1>
    <p>No HTML helpers here</p>
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().BeEmpty();
    }

    [Fact]
    public void DetectBundleReferences_NoBundles_ReturnsEmpty()
    {
        // Arrange
        const string content = @"
<link rel=""stylesheet"" href=""~/css/site.css"" />
<script src=""~/js/site.js""></script>";

        // Act
        var bundles = _analyzer.DetectBundleReferences(content);

        // Assert
        bundles.Should().BeEmpty();
    }

    [Fact]
    public void DetectHtmlHelpers_HiddenFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<form>
    @Html.HiddenFor(m => m.Id)
    @Html.HiddenFor(m => m.RowVersion)
</form>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);
        helpers.Should().AllSatisfy(h =>
        {
            h.HelperType.Should().Be(HtmlHelperType.HiddenFor);
            h.Confidence.Should().Be(95);
        });
    }

    [Fact]
    public void DetectHtmlHelpers_CheckBoxFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.CheckBoxFor(m => m.IsActive)
    @Html.CheckBoxFor(m => m.AcceptTerms, new { @class = ""checkbox"" })
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);
        helpers.Should().AllSatisfy(h =>
        {
            h.HelperType.Should().Be(HtmlHelperType.CheckBoxFor);
            h.Confidence.Should().Be(90);
        });
    }

    [Fact]
    public void DetectHtmlHelpers_DropDownListFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.DropDownListFor(m => m.CategoryId, Model.Categories)
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().ContainSingle();
        var helper = helpers[0];
        helper.HelperType.Should().Be(HtmlHelperType.DropDownListFor);
        helper.Confidence.Should().Be(85);
    }

    [Fact]
    public void DetectHtmlHelpers_EditorFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.EditorFor(m => m.Description)
    @Html.EditorFor(m => m.RichText, ""RichTextEditor"")
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);
        helpers.Should().AllSatisfy(h =>
        {
            h.HelperType.Should().Be(HtmlHelperType.EditorFor);
            h.Confidence.Should().Be(80);
        });
    }

    [Fact]
    public void AnalyzeView_WithSections_DetectsSections()
    {
        // Arrange
        const string content = @"
@model MyApp.Models.Product

<h2>Product</h2>

@section Scripts {
    <script src=""~/js/product.js""></script>
}

@section Styles {
    <link rel=""stylesheet"" href=""~/css/product.css"" />
}";
        const string filePath = "Views/Product/Details.cshtml";

        // Act
        var viewInfo = _analyzer.AnalyzeView(content, filePath);

        // Assert
        viewInfo.Sections.Should().HaveCount(2);
        viewInfo.Sections.Should().Contain("Scripts");
        viewInfo.Sections.Should().Contain("Styles");
    }

    [Fact]
    public void DetectHtmlHelpers_PasswordFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.PasswordFor(m => m.Password)
    @Html.PasswordFor(m => m.ConfirmPassword, new { @class = ""form-control"" })
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);
        helpers.Should().AllSatisfy(h =>
        {
            h.HelperType.Should().Be(HtmlHelperType.PasswordFor);
            h.Confidence.Should().Be(90);
        });
    }

    [Fact]
    public void DetectHtmlHelpers_RadioButtonFor_DetectsCorrectly()
    {
        // Arrange
        const string content = @"
<div>
    @Html.RadioButtonFor(m => m.Gender, ""Male"")
    @Html.RadioButtonFor(m => m.Gender, ""Female"")
</div>";

        // Act
        var helpers = _analyzer.DetectHtmlHelpers(content);

        // Assert
        helpers.Should().HaveCount(2);
        helpers.Should().AllSatisfy(h => h.HelperType.Should().Be(HtmlHelperType.RadioButtonFor));
    }
}
