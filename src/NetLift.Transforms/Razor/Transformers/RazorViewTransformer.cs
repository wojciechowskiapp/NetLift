using System.Text.RegularExpressions;
using NetLift.Core.Interfaces.Razor;
using NetLift.Core.Models.Razor;

namespace NetLift.Transforms.Razor.Transformers;

/// <summary>
/// Transforms MVC5 Razor views to ASP.NET Core Tag Helper syntax.
/// </summary>
public partial class RazorViewTransformer : IRazorViewTransformer
{
    private readonly IRazorViewAnalyzer _analyzer;

    // Bundle path mappings (common conventions)
    private static readonly Dictionary<string, string> BundleMappings = new()
    {
        { "~/bundles/jquery", "~/js/jquery.min.js" },
        { "~/bundles/jqueryval", "~/js/jquery.validate.min.js" },
        { "~/bundles/modernizr", "~/js/modernizr.min.js" },
        { "~/bundles/bootstrap", "~/js/bootstrap.bundle.min.js" },
        { "~/Content/css", "~/css/site.css" },
        { "~/Content/bootstrap", "~/css/bootstrap.min.css" }
    };

    public RazorViewTransformer(IRazorViewAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <inheritdoc />
    public async Task<RazorViewTransformResult> TransformViewAsync(RazorViewInfo viewInfo)
    {
        try
        {
            var content = await File.ReadAllTextAsync(viewInfo.FilePath);
            var transformedContent = TransformContent(content, viewInfo.FilePath);

            var transformationCount = CountTransformations(viewInfo);

            return new RazorViewTransformResult
            {
                OriginalView = viewInfo,
                TransformedContent = transformedContent,
                TransformationCount = transformationCount,
                Confidence = viewInfo.Confidence,
                Success = true,
                Notes = GenerateNotes(viewInfo)
            };
        }
        catch (Exception ex)
        {
            return new RazorViewTransformResult
            {
                OriginalView = viewInfo,
                TransformedContent = string.Empty,
                TransformationCount = 0,
                Confidence = 0,
                Success = false,
                Notes = [$"Error: {ex.Message}"]
            };
        }
    }

    /// <inheritdoc />
    public string TransformContent(string content, string filePath)
    {
        var result = content;

        // Transform input helpers (TextBoxFor, EditorFor, etc.)
        result = TransformInputHelpers(result);

        // Transform form helpers
        result = TransformFormHelpers(result);

        // Transform action links
        result = TransformActionLinks(result);

        // Transform validation helpers
        result = TransformValidationHelpers(result);

        // Transform label helpers
        result = TransformLabelHelpers(result);

        // Transform partial views
        result = TransformPartialViews(result);

        // Transform bundle references
        result = TransformBundleReferences(result);

        // Transform URL helpers
        result = TransformUrlHelpers(result);

        return result;
    }

    /// <inheritdoc />
    public string TransformHtmlHelper(HtmlHelperUsage helper)
    {
        return helper.HelperType switch
        {
            HtmlHelperType.ActionLink => TransformActionLink(helper.OriginalCode),
            HtmlHelperType.BeginForm => TransformBeginForm(helper.OriginalCode),
            HtmlHelperType.TextBoxFor => TransformInputFor(helper.OriginalCode, "text"),
            HtmlHelperType.TextAreaFor => TransformTextAreaFor(helper.OriginalCode),
            HtmlHelperType.PasswordFor => TransformInputFor(helper.OriginalCode, "password"),
            HtmlHelperType.HiddenFor => TransformInputFor(helper.OriginalCode, "hidden"),
            HtmlHelperType.CheckBoxFor => TransformInputFor(helper.OriginalCode, "checkbox"),
            HtmlHelperType.DropDownListFor => TransformDropDownListFor(helper.OriginalCode),
            HtmlHelperType.EditorFor => TransformEditorFor(helper.OriginalCode),
            HtmlHelperType.LabelFor => TransformLabelFor(helper.OriginalCode),
            HtmlHelperType.ValidationMessageFor => TransformValidationMessageFor(helper.OriginalCode),
            HtmlHelperType.ValidationSummary => TransformValidationSummary(helper.OriginalCode),
            HtmlHelperType.Partial => TransformPartial(helper.OriginalCode),
            HtmlHelperType.RenderPartial => TransformRenderPartial(helper.OriginalCode),
            HtmlHelperType.Action => TransformAction(helper.OriginalCode),
            HtmlHelperType.RenderAction => TransformRenderAction(helper.OriginalCode),
            HtmlHelperType.AntiForgeryToken => "<!-- AntiForgeryToken is automatic with asp-* form tag helpers -->",
            _ => helper.OriginalCode
        };
    }

    /// <inheritdoc />
    public string TransformBundleReference(BundleReference bundle)
    {
        var mappedPath = MapBundlePath(bundle.BundlePath);

        return bundle.BundleType switch
        {
            BundleReferenceType.Scripts => $"<script src=\"{mappedPath}\"></script>",
            BundleReferenceType.Styles => $"<link rel=\"stylesheet\" href=\"{mappedPath}\" />",
            _ => bundle.OriginalCode
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RazorViewTransformResult>> TransformProjectViewsAsync(string projectPath, bool dryRun = false)
    {
        var results = new List<RazorViewTransformResult>();
        var views = await _analyzer.AnalyzeProjectViewsAsync(projectPath);

        foreach (var view in views)
        {
            var result = await TransformViewAsync(view);
            results.Add(result);

            if (!dryRun && result.Success)
            {
                await File.WriteAllTextAsync(view.FilePath, result.TransformedContent);
            }
        }

        return results;
    }

    #region Private transformation methods

    private static string TransformInputHelpers(string content)
    {
        // TextBoxFor
        content = Regex.Replace(content,
            @"@Html\.TextBoxFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\"{attrs} />";
            });

        // PasswordFor
        content = Regex.Replace(content,
            @"@Html\.PasswordFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\" type=\"password\"{attrs} />";
            });

        // HiddenFor
        content = Regex.Replace(content,
            @"@Html\.HiddenFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)\s*\)",
            "<input asp-for=\"$1\" type=\"hidden\" />");

        // CheckBoxFor
        content = Regex.Replace(content,
            @"@Html\.CheckBoxFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\"{attrs} />";
            });

        return content;
    }

    private static string TransformFormHelpers(string content)
    {
        // @using (Html.BeginForm("Action", "Controller", FormMethod.Post))
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*""(\w+)""\s*,\s*""(\w+)""(?:\s*,\s*FormMethod\.(\w+))?\s*\)\s*\)",
            match =>
            {
                var action = match.Groups[1].Value;
                var controller = match.Groups[2].Value;
                var method = match.Groups[3].Success ? match.Groups[3].Value.ToLower() : "post";
                return $"<form asp-controller=\"{controller}\" asp-action=\"{action}\" method=\"{method}\">";
            });

        // Simple @using (Html.BeginForm())
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*\)\s*\)",
            "<form method=\"post\">");

        // Close form (replace closing brace after BeginForm)
        // This is tricky - we need to match the closing } that corresponds to the form
        // For simplicity, we'll add a note that manual review may be needed

        return content;
    }

    private static string TransformActionLinks(string content)
    {
        // @Html.ActionLink("Text", "Action", "Controller")
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*,\s*""(\w+)""\s*\)",
            "<a asp-controller=\"$3\" asp-action=\"$2\">$1</a>");

        // @Html.ActionLink("Text", "Action", "Controller", new { id = x }, null)
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*,\s*""(\w+)""\s*,\s*new\s*\{\s*(\w+)\s*=\s*([^}]+)\s*\}\s*,\s*null\s*\)",
            "<a asp-controller=\"$3\" asp-action=\"$2\" asp-route-$4=\"$5\">$1</a>");

        // @Html.ActionLink("Text", "Action", new { id = x })
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*,\s*new\s*\{\s*(\w+)\s*=\s*([^}]+)\s*\}\s*\)",
            "<a asp-action=\"$2\" asp-route-$3=\"$4\">$1</a>");

        // @Html.ActionLink("Text", "Action")
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*\)",
            "<a asp-action=\"$2\">$1</a>");

        return content;
    }

    private static string TransformValidationHelpers(string content)
    {
        // ValidationMessageFor
        content = Regex.Replace(content,
            @"@Html\.ValidationMessageFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)(?:\s*,\s*""([^""]*)"")?(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var message = match.Groups[2].Success ? $" data-valmsg-for=\"{match.Groups[2].Value}\"" : "";
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<span asp-validation-for=\"{propName}\"{message}{attrs}></span>";
            });

        // ValidationSummary(true) - ModelOnly
        content = Regex.Replace(content,
            @"@Html\.ValidationSummary\s*\(\s*true\s*\)",
            "<div asp-validation-summary=\"ModelOnly\"></div>");

        // ValidationSummary(false) or ValidationSummary() - All
        content = Regex.Replace(content,
            @"@Html\.ValidationSummary\s*\(\s*(?:false)?\s*\)",
            "<div asp-validation-summary=\"All\"></div>");

        return content;
    }

    private static string TransformLabelHelpers(string content)
    {
        // LabelFor
        content = Regex.Replace(content,
            @"@Html\.LabelFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<label asp-for=\"{propName}\"{attrs}></label>";
            });

        return content;
    }

    private static string TransformPartialViews(string content)
    {
        // @Html.Partial("_PartialName", Model)
        content = Regex.Replace(content,
            @"@Html\.Partial\s*\(\s*""([^""]+)""\s*,\s*([^)]+)\s*\)",
            "<partial name=\"$1\" model=\"$2\" />");

        // @Html.Partial("_PartialName")
        content = Regex.Replace(content,
            @"@Html\.Partial\s*\(\s*""([^""]+)""\s*\)",
            "<partial name=\"$1\" />");

        // @{ Html.RenderPartial("_PartialName", Model); }
        content = Regex.Replace(content,
            @"@\{\s*Html\.RenderPartial\s*\(\s*""([^""]+)""(?:\s*,\s*([^)]+))?\s*\);\s*\}",
            match =>
            {
                var name = match.Groups[1].Value;
                var model = match.Groups[2].Success ? $" model=\"{match.Groups[2].Value.Trim()}\"" : "";
                return $"<partial name=\"{name}\"{model} />";
            });

        // Html.Action/RenderAction - these need ViewComponents
        content = Regex.Replace(content,
            @"@Html\.Action\s*\(\s*""(\w+)""(?:\s*,\s*""(\w+)"")?\s*\)",
            "<!-- TODO: Convert to ViewComponent -->\n@await Component.InvokeAsync(\"$1\")");

        content = Regex.Replace(content,
            @"@\{\s*Html\.RenderAction\s*\(\s*""(\w+)""(?:\s*,\s*""(\w+)"")?\s*\);\s*\}",
            "<!-- TODO: Convert to ViewComponent -->\n@await Component.InvokeAsync(\"$1\")");

        return content;
    }

    private static string TransformBundleReferences(string content)
    {
        // @Scripts.Render("~/bundles/...")
        content = Regex.Replace(content,
            @"@Scripts\.Render\s*\(\s*""([^""]+)""\s*\)",
            match =>
            {
                var bundlePath = match.Groups[1].Value;
                var mappedPath = MapBundlePath(bundlePath);
                return $"<script src=\"{mappedPath}\"></script>";
            });

        // @Styles.Render("~/Content/...")
        content = Regex.Replace(content,
            @"@Styles\.Render\s*\(\s*""([^""]+)""\s*\)",
            match =>
            {
                var bundlePath = match.Groups[1].Value;
                var mappedPath = MapBundlePath(bundlePath);
                return $"<link rel=\"stylesheet\" href=\"{mappedPath}\" />";
            });

        return content;
    }

    private static string TransformUrlHelpers(string content)
    {
        // @Url.Content("~/Content/...") - map to wwwroot paths
        content = Regex.Replace(content,
            @"@Url\.Content\s*\(\s*""~/Content/([^""]+)""\s*\)",
            "~/css/$1");

        content = Regex.Replace(content,
            @"@Url\.Content\s*\(\s*""~/Scripts/([^""]+)""\s*\)",
            "~/js/$1");

        content = Regex.Replace(content,
            @"@Url\.Content\s*\(\s*""~/Images/([^""]+)""\s*\)",
            "~/images/$1");

        return content;
    }

    private static string TransformActionLink(string code) => code;
    private static string TransformBeginForm(string code) => code;
    private static string TransformInputFor(string code, string type) => code;
    private static string TransformTextAreaFor(string code)
    {
        var match = Regex.Match(code, @"Html\.TextAreaFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)");
        if (match.Success)
        {
            return $"<textarea asp-for=\"{match.Groups[1].Value}\"></textarea>";
        }
        return code;
    }
    private static string TransformDropDownListFor(string code)
    {
        var match = Regex.Match(code, @"Html\.DropDownListFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)\s*,\s*([^,)]+)");
        if (match.Success)
        {
            return $"<select asp-for=\"{match.Groups[1].Value}\" asp-items=\"{match.Groups[2].Value.Trim()}\"></select>";
        }
        return code;
    }
    private static string TransformEditorFor(string code)
    {
        var match = Regex.Match(code, @"Html\.EditorFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)");
        if (match.Success)
        {
            return $"<input asp-for=\"{match.Groups[1].Value}\" />";
        }
        return code;
    }
    private static string TransformLabelFor(string code) => code;
    private static string TransformValidationMessageFor(string code) => code;
    private static string TransformValidationSummary(string code) => code;
    private static string TransformPartial(string code) => code;
    private static string TransformRenderPartial(string code) => code;
    private static string TransformAction(string code) => "<!-- TODO: Convert to ViewComponent -->\n@await Component.InvokeAsync(\"...\")";
    private static string TransformRenderAction(string code) => "<!-- TODO: Convert to ViewComponent -->\n@await Component.InvokeAsync(\"...\")";

    private static string MapBundlePath(string bundlePath)
    {
        if (BundleMappings.TryGetValue(bundlePath, out var mapped))
        {
            return mapped;
        }

        // Default mapping logic
        if (bundlePath.Contains("bundles"))
        {
            var name = bundlePath.Split('/').Last();
            return $"~/js/{name}.min.js";
        }
        if (bundlePath.Contains("Content"))
        {
            var name = bundlePath.Replace("~/Content/", "");
            return $"~/css/{name}.css";
        }

        return bundlePath.Replace("~/Content/", "~/css/").Replace("~/Scripts/", "~/js/");
    }

    private static string ParseHtmlAttributes(string attrString)
    {
        if (string.IsNullOrWhiteSpace(attrString))
        {
            return "";
        }

        var attrs = new List<string>();
        var matches = Regex.Matches(attrString, @"@?(\w+)\s*=\s*""?([^"",}]+)""?");

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.TrimStart('@');
            var value = match.Groups[2].Value.Trim();
            attrs.Add($"{name}=\"{value}\"");
        }

        return attrs.Count > 0 ? " " + string.Join(" ", attrs) : "";
    }

    private static int CountTransformations(RazorViewInfo viewInfo)
    {
        return viewInfo.HtmlHelpers.Count +
               viewInfo.BundleReferences.Count +
               viewInfo.UrlHelpers.Count +
               viewInfo.PartialViews.Count;
    }

    private static IReadOnlyList<string> GenerateNotes(RazorViewInfo viewInfo)
    {
        var notes = new List<string>();

        if (viewInfo.PartialViews.Any(p => p.ReferenceType is PartialReferenceType.Action or PartialReferenceType.RenderAction))
        {
            notes.Add("Contains Html.Action/RenderAction - needs manual conversion to ViewComponents");
        }

        if (viewInfo.BundleReferences.Count > 0)
        {
            notes.Add("Bundle references converted to direct script/link tags - review and adjust paths");
        }

        if (viewInfo.UsesViewBag)
        {
            notes.Add("Uses ViewBag - consider converting to strongly-typed ViewModels");
        }

        return notes;
    }

    #endregion
}
