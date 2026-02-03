# Razor Views Migration - Implementation Plan

> **Feature:** Automated migration of MVC5 Razor views to ASP.NET Core Razor syntax

---

## Executive Summary

MVC5 Razor views use HTML helpers (@Html.ActionLink, @Html.BeginForm, etc.) that need to be transformed to ASP.NET Core Tag Helpers. This is a critical gap - every MVC application has views that need migration.

**Key Transformations:**
- `@Html.ActionLink()` → `<a asp-action="...">` Tag Helpers
- `@Html.BeginForm()` → `<form asp-action="...">` Tag Helpers
- `@Html.EditorFor()` → `<input asp-for="...">` Tag Helpers
- `@Html.ValidationMessageFor()` → `<span asp-validation-for="...">`
- `@Scripts.Render()` / `@Styles.Render()` → Direct `<script>` / `<link>` tags
- `@Url.Content("~/Content/...")` → `~/css/...` wwwroot paths
- `_ViewStart.cshtml` → `_ViewImports.cshtml` with `@addTagHelper`

---

## Architecture

### Models (NetLift.Core/Models/Razor/)

```csharp
public record RazorViewInfo
{
    public required string FilePath { get; init; }
    public required string ViewName { get; init; }
    public string? Model { get; init; }
    public string? Layout { get; init; }
    public IReadOnlyList<HtmlHelperUsage> HtmlHelpers { get; init; } = [];
    public IReadOnlyList<BundleReference> BundleReferences { get; init; } = [];
    public IReadOnlyList<ScriptReference> ScriptReferences { get; init; } = [];
    public bool UsesViewBag { get; init; }
    public bool UsesSections { get; init; }
    public IReadOnlyList<string> PartialViews { get; init; } = [];
}

public record HtmlHelperUsage
{
    public required HtmlHelperType HelperType { get; init; }
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public int ConfidenceScore { get; init; }
    public string? TransformedCode { get; init; }
}

public enum HtmlHelperType
{
    ActionLink,
    RouteLink,
    BeginForm,
    BeginRouteForm,
    AntiForgeryToken,
    EditorFor,
    EditorForModel,
    TextBoxFor,
    TextAreaFor,
    PasswordFor,
    HiddenFor,
    CheckBoxFor,
    DropDownListFor,
    ListBoxFor,
    RadioButtonFor,
    DisplayFor,
    DisplayNameFor,
    LabelFor,
    ValidationMessageFor,
    ValidationSummary,
    Partial,
    RenderPartial,
    Action,
    RenderAction,
    Raw,
    Encode,
    AttributeEncode
}

public record BundleReference
{
    public required BundleType BundleType { get; init; }
    public required string BundlePath { get; init; }
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
}

public enum BundleType
{
    Scripts,    // @Scripts.Render()
    Styles      // @Styles.Render()
}
```

### Interfaces

```csharp
public interface IRazorViewAnalyzer
{
    Task<IReadOnlyList<RazorViewInfo>> AnalyzeViewsAsync(
        string projectPath, CancellationToken ct = default);
    Task<RazorViewInfo> AnalyzeViewAsync(
        string filePath, CancellationToken ct = default);
    IReadOnlyList<HtmlHelperUsage> DetectHtmlHelpers(string content);
}

public interface IRazorViewTransformer
{
    Task<TransformResult> TransformViewAsync(
        RazorViewInfo viewInfo, CancellationToken ct = default);
    string TransformHtmlHelper(HtmlHelperUsage helper);
    string TransformBundleReference(BundleReference bundle);
}

public interface IViewImportsGenerator
{
    string GenerateViewImports(ProjectInfo project);
    string GenerateViewStart(ProjectInfo project);
}
```

---

## Transformation Rules

### 1. Html.ActionLink (Confidence: 95%)

**Before:**
```cshtml
@Html.ActionLink("Text", "Action", "Controller")
@Html.ActionLink("Text", "Action", "Controller", new { id = Model.Id }, null)
@Html.ActionLink("Text", "Action", new { id = Model.Id })
```

**After:**
```cshtml
<a asp-controller="Controller" asp-action="Action">Text</a>
<a asp-controller="Controller" asp-action="Action" asp-route-id="@Model.Id">Text</a>
<a asp-action="Action" asp-route-id="@Model.Id">Text</a>
```

### 2. Html.BeginForm (Confidence: 95%)

**Before:**
```cshtml
@using (Html.BeginForm("Action", "Controller", FormMethod.Post))
{
    <!-- form content -->
}

@using (Html.BeginForm())
{
    @Html.AntiForgeryToken()
    <!-- form content -->
}
```

**After:**
```cshtml
<form asp-controller="Controller" asp-action="Action" method="post">
    <!-- form content -->
</form>

<form method="post">
    <!-- AntiForgeryToken is automatic with asp-* tag helpers -->
    <!-- form content -->
</form>
```

### 3. Input Tag Helpers (Confidence: 90%)

**Before:**
```cshtml
@Html.TextBoxFor(m => m.Name, new { @class = "form-control" })
@Html.TextAreaFor(m => m.Description)
@Html.PasswordFor(m => m.Password)
@Html.HiddenFor(m => m.Id)
@Html.CheckBoxFor(m => m.IsActive)
@Html.DropDownListFor(m => m.CategoryId, Model.Categories)
```

**After:**
```cshtml
<input asp-for="Name" class="form-control" />
<textarea asp-for="Description"></textarea>
<input asp-for="Password" />
<input asp-for="Id" type="hidden" />
<input asp-for="IsActive" />
<select asp-for="CategoryId" asp-items="Model.Categories"></select>
```

### 4. Validation Tag Helpers (Confidence: 90%)

**Before:**
```cshtml
@Html.ValidationMessageFor(m => m.Name)
@Html.ValidationSummary(true)
@Html.ValidationSummary()
```

**After:**
```cshtml
<span asp-validation-for="Name"></span>
<div asp-validation-summary="ModelOnly"></div>
<div asp-validation-summary="All"></div>
```

### 5. Label/Display Tag Helpers (Confidence: 95%)

**Before:**
```cshtml
@Html.LabelFor(m => m.Name)
@Html.DisplayFor(m => m.Name)
@Html.DisplayNameFor(m => m.Name)
```

**After:**
```cshtml
<label asp-for="Name"></label>
@Model.Name
@Html.DisplayNameFor(m => m.Name)  // Still valid in Core
```

### 6. Editor Templates (Confidence: 80%)

**Before:**
```cshtml
@Html.EditorFor(m => m.BirthDate)
@Html.EditorForModel()
```

**After:**
```cshtml
<input asp-for="BirthDate" />
<!-- TODO: Review EditorForModel - may need partial views -->
```

### 7. Partial Views (Confidence: 85%)

**Before:**
```cshtml
@Html.Partial("_MyPartial", Model)
@{ Html.RenderPartial("_MyPartial"); }
@Html.Action("MyAction", "Controller")
@{ Html.RenderAction("MyAction"); }
```

**After:**
```cshtml
<partial name="_MyPartial" model="Model" />
@await Html.PartialAsync("_MyPartial")
<!-- TODO: Html.Action/RenderAction - use View Components in ASP.NET Core -->
@await Component.InvokeAsync("MyAction")
```

### 8. Bundle References (Confidence: 80%)

**Before:**
```cshtml
@Scripts.Render("~/bundles/jquery")
@Scripts.Render("~/bundles/jqueryval")
@Styles.Render("~/Content/css")
```

**After:**
```cshtml
<script src="~/js/jquery.min.js"></script>
<script src="~/js/jquery.validate.min.js"></script>
<link rel="stylesheet" href="~/css/site.css" />
<!-- TODO: Review bundle configuration - consider using Vite/Webpack -->
```

### 9. URL Helpers (Confidence: 90%)

**Before:**
```cshtml
<img src="@Url.Content("~/Content/images/logo.png")" />
<a href="@Url.Action("Index", "Home")">Home</a>
```

**After:**
```cshtml
<img src="~/images/logo.png" />
<a asp-controller="Home" asp-action="Index">Home</a>
```

### 10. AntiForgeryToken (Confidence: 95%)

**Before:**
```cshtml
@Html.AntiForgeryToken()
```

**After:**
```cshtml
<!-- Automatic when using form tag helpers with asp-* attributes -->
<!-- If manual token needed: -->
@Html.AntiForgeryToken()  // Still works in Core
```

---

## File Generation

### _ViewImports.cshtml

```cshtml
@using MyApp
@using MyApp.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

### _ViewStart.cshtml

```cshtml
@{
    Layout = "_Layout";
}
```

---

## Sprint Tasks

### Sprint 17: Analysis & Core Transformations (12 tasks)

| # | Task | Size | Description |
|---|------|------|-------------|
| 163 | RazorViewInfo model | S | View analysis result model |
| 164 | HtmlHelperUsage model | S | HTML helper detection model |
| 165 | IRazorViewAnalyzer interface | S | Analysis contract |
| 166 | IRazorViewTransformer interface | S | Transform contract |
| 167 | RazorViewAnalyzer - helper detection | L | Regex-based helper parsing |
| 168 | ActionLink transformer | M | @Html.ActionLink → <a asp-*> |
| 169 | BeginForm transformer | M | @Html.BeginForm → <form asp-*> |
| 170 | Input helper transformers | L | TextBoxFor, EditorFor, etc. |
| 171 | Validation helper transformers | M | ValidationMessageFor, etc. |
| 172 | ViewImportsGenerator | M | Generate _ViewImports.cshtml |
| 173 | Unit tests (50+) | XL | Helper transformation tests |
| 174 | Integration tests | M | Full view transformation |

### Sprint 18: Advanced & Polish (10 tasks)

| # | Task | Size | Description |
|---|------|------|-------------|
| 175 | Bundle reference transformer | M | @Scripts.Render removal |
| 176 | Partial view transformers | M | Html.Partial → partial tag |
| 177 | URL helper transformers | M | Url.Content, Url.Action |
| 178 | Html.Action → ViewComponent | M | Generate TODO/guidance |
| 179 | Layout file updates | M | _Layout.cshtml migration |
| 180 | Section handling | S | @section → @section (verify) |
| 181 | View discovery | M | Find all .cshtml files |
| 182 | Path reference updates | M | ~/Content → ~/css |
| 183 | E2E tests (8) | L | Full view migration |
| 184 | HTML report section | M | View migration report |

---

## Test Strategy

### Unit Tests (50+ tests)

**ActionLinkTransformerTests:**
- Transform_SimpleActionLink
- Transform_ActionLink_WithController
- Transform_ActionLink_WithRouteValues
- Transform_ActionLink_WithHtmlAttributes
- Transform_RouteLink

**BeginFormTransformerTests:**
- Transform_SimpleBeginForm
- Transform_BeginForm_WithMethod
- Transform_BeginForm_WithRouteValues
- Transform_BeginForm_PreservesContent
- Remove_AntiForgeryToken_InsideForm

**InputHelperTransformerTests:**
- Transform_TextBoxFor
- Transform_TextBoxFor_WithHtmlAttributes
- Transform_TextAreaFor
- Transform_PasswordFor
- Transform_HiddenFor
- Transform_CheckBoxFor
- Transform_DropDownListFor
- Transform_RadioButtonFor
- Transform_EditorFor

**ValidationHelperTransformerTests:**
- Transform_ValidationMessageFor
- Transform_ValidationSummary_True
- Transform_ValidationSummary_False
- Transform_ValidationSummary_WithMessage

**PartialViewTransformerTests:**
- Transform_Partial
- Transform_Partial_WithModel
- Transform_RenderPartial
- Transform_Action_GeneratesTodo
- Transform_RenderAction_GeneratesTodo

---

## Confidence Scoring

| Transformation | Confidence | Notes |
|---------------|-----------|-------|
| Html.ActionLink simple | 95% | Direct mapping |
| Html.ActionLink complex | 85% | Route values may need review |
| Html.BeginForm | 95% | Direct mapping |
| Html.TextBoxFor | 90% | HTML attributes handling |
| Html.DropDownListFor | 85% | asp-items syntax different |
| Html.EditorFor | 80% | May need template review |
| Html.ValidationMessageFor | 90% | Direct mapping |
| Html.ValidationSummary | 90% | ModelOnly vs All |
| Html.Partial | 85% | <partial> tag helper |
| Html.Action | 60% | ViewComponent migration complex |
| @Scripts.Render | 80% | Need bundle mapping |
| @Styles.Render | 80% | Need bundle mapping |

---

## Dependencies

- No external packages needed for transformation
- Generated views require `Microsoft.AspNetCore.Mvc.TagHelpers`

---

*Last updated: 2026-02-03*
