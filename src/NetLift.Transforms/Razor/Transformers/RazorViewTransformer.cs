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

        // Transform @helper directives (not supported in ASP.NET Core Razor)
        result = TransformHelperDirectives(result);

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

    /// <summary>
    /// Transforms @helper directives to local functions or partial views.
    /// @helper is not supported in ASP.NET Core Razor and must be converted.
    /// </summary>
    private static string TransformHelperDirectives(string content)
    {
        // Pattern to match @helper Name(params) { body }
        // This is a simplified pattern - @helper can have complex nested braces
        var helperPattern = new Regex(
            @"@helper\s+(\w+)\s*\(([^)]*)\)\s*\{",
            RegexOptions.Multiline);

        var result = content;
        var match = helperPattern.Match(result);

        while (match.Success)
        {
            var helperName = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            var startIndex = match.Index;
            var bodyStartIndex = match.Index + match.Length;

            // Find the matching closing brace
            var braceCount = 1;
            var bodyEndIndex = bodyStartIndex;
            for (var i = bodyStartIndex; i < result.Length && braceCount > 0; i++)
            {
                if (result[i] == '{') braceCount++;
                else if (result[i] == '}') braceCount--;
                if (braceCount == 0) bodyEndIndex = i;
            }

            var helperBody = result.Substring(bodyStartIndex, bodyEndIndex - bodyStartIndex);
            var fullHelper = result.Substring(startIndex, bodyEndIndex - startIndex + 1);

            // Convert to local function in @functions block
            var localFunction = GenerateLocalFunction(helperName, parameters, helperBody);

            // Replace the @helper with a comment and reference to @functions
            var replacement = $@"@* TODO: @helper '{helperName}' converted to local function. Move to @functions block or create a partial view. *@
@* Original: @helper {helperName}({parameters}) *@
@{{
    // Local function (move to @functions block at top of file)
    {localFunction}
}}";

            result = result.Substring(0, startIndex) + replacement + result.Substring(bodyEndIndex + 1);

            // Find next match
            match = helperPattern.Match(result, startIndex + replacement.Length);
        }

        return result;
    }

    /// <summary>
    /// Generates a placeholder for a @helper that needs manual conversion.
    /// @helper is complex and risky to auto-convert - safer to leave as TODO.
    /// </summary>
    private static string GenerateLocalFunction(string name, string parameters, string body)
    {
        var cleanBody = body.Trim();
        // Escape body for display in comment (limit length, escape newlines)
        var displayBody = cleanBody.Replace("\n", " ").Replace("\r", "");
        if (displayBody.Length > 200)
        {
            displayBody = displayBody.Substring(0, 200) + "...";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"    // TODO: Manually convert @helper '{name}' to a local function or partial view");
        sb.AppendLine($"    // Original: @helper {name}({parameters}) {{ {displayBody} }}");
        sb.AppendLine($"    // Option 1: Convert to local function in @functions block");
        sb.AppendLine($"    // Option 2: Create a partial view '__{name}.cshtml' and use <partial name=\"__{name}\" />");
        sb.AppendLine($"    // Option 3: Create a Tag Helper or View Component for reusability");
        sb.AppendLine($"    string {name}({parameters})");
        sb.AppendLine("    {");
        sb.AppendLine($"        throw new NotImplementedException(\"@helper '{name}' requires manual conversion\");");
        sb.AppendLine("    }");

        return sb.ToString();
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
            HtmlHelperType.RadioButtonFor => TransformRadioButtonFor(helper.OriginalCode),
            HtmlHelperType.DropDownListFor => TransformDropDownListFor(helper.OriginalCode),
            HtmlHelperType.ListBoxFor => TransformListBoxFor(helper.OriginalCode),
            HtmlHelperType.EditorFor => TransformEditorFor(helper.OriginalCode),
            HtmlHelperType.DisplayFor => TransformDisplayFor(helper.OriginalCode),
            HtmlHelperType.DisplayNameFor => TransformDisplayNameFor(helper.OriginalCode),
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
        // TextBoxFor - support nested properties with ([\w.]+)
        content = Regex.Replace(content,
            @"@Html\.TextBoxFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\"{attrs} />";
            });

        // PasswordFor
        content = Regex.Replace(content,
            @"@Html\.PasswordFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\" type=\"password\"{attrs} />";
            });

        // HiddenFor - support nested properties
        content = Regex.Replace(content,
            @"@Html\.HiddenFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*\)",
            "<input asp-for=\"$1\" type=\"hidden\" />");

        // CheckBoxFor
        content = Regex.Replace(content,
            @"@Html\.CheckBoxFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\"{attrs} />";
            });

        // RadioButtonFor - @Html.RadioButtonFor(m => m.Gender, "Male") → <input asp-for="Gender" type="radio" value="Male" />
        content = Regex.Replace(content,
            @"@Html\.RadioButtonFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*""?([^"")]+)""?\s*(?:,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim('"');
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<input asp-for=\"{propName}\" type=\"radio\" value=\"{value}\"{attrs} />";
            });

        // ListBoxFor (multi-select) - @Html.ListBoxFor(m => m.SelectedIds, Model.Items)
        content = Regex.Replace(content,
            @"@Html\.ListBoxFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*([^,)]+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var items = match.Groups[2].Value.Trim();
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<select asp-for=\"{propName}\" asp-items=\"{items}\" multiple=\"multiple\"{attrs}></select>";
            });

        // EditorFor with nested htmlAttributes - @Html.EditorFor(m => m.Name, new { htmlAttributes = new { @class = "form-control" } })
        content = Regex.Replace(content,
            @"@Html\.EditorFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*new\s*\{\s*htmlAttributes\s*=\s*new\s*\{([^}]*)\}\s*\}\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input asp-for=\"{propName}\"{attrs} />";
            });

        // Basic EditorFor - @Html.EditorFor(m => m.Name) → <input asp-for="Name" />
        content = Regex.Replace(content,
            @"@Html\.EditorFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*\)",
            "<input asp-for=\"$1\" />");

        // ========== NON-LAMBDA HELPERS ==========

        // Html.TextBox("Name") - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.TextBox\s*\(\s*""(\w+)""(?:\s*,\s*([^,)]+))?(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var value = match.Groups[2].Success ? $" value=\"{match.Groups[2].Value.Trim()}\"" : "";
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<input type=\"text\" name=\"{name}\" id=\"{name}\"{value}{attrs} />";
            });

        // Html.Password("Name") - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.Password\s*\(\s*""(\w+)""(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<input type=\"password\" name=\"{name}\" id=\"{name}\"{attrs} />";
            });

        // Html.Hidden("Name") or Html.Hidden("Name", value) - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.Hidden\s*\(\s*""(\w+)""(?:\s*,\s*([^)]+))?\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var value = match.Groups[2].Success ? $" value=\"{match.Groups[2].Value.Trim()}\"" : "";
                return $"<input type=\"hidden\" name=\"{name}\" id=\"{name}\"{value} />";
            });

        // Html.CheckBox("Name") - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.CheckBox\s*\(\s*""(\w+)""(?:\s*,\s*(true|false))?(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var isChecked = match.Groups[2].Success && match.Groups[2].Value == "true" ? " checked=\"checked\"" : "";
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<input type=\"checkbox\" name=\"{name}\" id=\"{name}\"{isChecked}{attrs} />";
            });

        return content;
    }

    private static string TransformFormHelpers(string content)
    {
        // Track how many @using form blocks we've opened (for matching closing braces)
        int formBlocksOpened = 0;

        // @using (Html.BeginForm("Action", "Controller", FormMethod.Get/Post, new { ... })) {
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*""(\w+)""\s*,\s*""(\w+)""\s*,\s*FormMethod\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)\s*\)\s*\{",
            match =>
            {
                formBlocksOpened++;
                var action = match.Groups[1].Value;
                var controller = match.Groups[2].Value;
                var method = match.Groups[3].Value.ToLower();
                var attrs = ParseHtmlAttributes(match.Groups[4].Value);
                return $"<form asp-controller=\"{controller}\" asp-action=\"{action}\" method=\"{method}\"{attrs}>";
            });

        // @using (Html.BeginForm("Action", "Controller")) { - without FormMethod
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*""(\w+)""\s*,\s*""(\w+)""\s*\)\s*\)\s*\{",
            match =>
            {
                formBlocksOpened++;
                var action = match.Groups[1].Value;
                var controller = match.Groups[2].Value;
                return $"<form asp-controller=\"{controller}\" asp-action=\"{action}\" method=\"post\">";
            });

        // Simple @using (Html.BeginForm()) {
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*\)\s*\)\s*\{",
            match =>
            {
                formBlocksOpened++;
                return "<form method=\"post\">";
            });

        // Also handle variants without opening brace on same line (brace on next line or Razor implicit)
        // @using (Html.BeginForm("Action", "Controller", FormMethod.Get/Post, new { ... }))
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*""(\w+)""\s*,\s*""(\w+)""\s*,\s*FormMethod\.(\w+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)\s*\)(?!\s*\{)",
            match =>
            {
                formBlocksOpened++;
                var action = match.Groups[1].Value;
                var controller = match.Groups[2].Value;
                var method = match.Groups[3].Value.ToLower();
                var attrs = ParseHtmlAttributes(match.Groups[4].Value);
                return $"<form asp-controller=\"{controller}\" asp-action=\"{action}\" method=\"{method}\"{attrs}>";
            });

        // @using (Html.BeginForm("Action", "Controller")) - without FormMethod, no brace
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*""(\w+)""\s*,\s*""(\w+)""\s*\)\s*\)(?!\s*\{)",
            match =>
            {
                formBlocksOpened++;
                var action = match.Groups[1].Value;
                var controller = match.Groups[2].Value;
                return $"<form asp-controller=\"{controller}\" asp-action=\"{action}\" method=\"post\">";
            });

        // Simple @using (Html.BeginForm()) - no brace
        content = Regex.Replace(content,
            @"@using\s*\(\s*Html\.BeginForm\s*\(\s*\)\s*\)(?!\s*\{)",
            match =>
            {
                formBlocksOpened++;
                return "<form method=\"post\">";
            });

        // Now handle the closing braces for form blocks
        // Replace standalone closing braces } with </form> for each form block we opened
        content = TransformFormClosingBraces(content, formBlocksOpened);

        return content;
    }

    private static string TransformFormClosingBraces(string content, int formBlocksToClose)
    {
        if (formBlocksToClose <= 0)
        {
            return content;
        }

        var lines = content.Split('\n').ToList();
        var formPattern = new Regex(@"<form\s");
        var closingBracePattern = new Regex(@"^\s*\}\s*$");
        var razorBlockPattern = new Regex(@"@(if|foreach|for|while|switch|section|functions|code)\s*[\({]|@\{");

        // Find all form tag positions
        var formPositions = new List<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (formPattern.IsMatch(lines[i]))
            {
                formPositions.Add(i);
            }
        }

        // For each form, find its matching closing brace (working forward)
        var closingBracePositions = new List<int>();
        foreach (var formPos in formPositions)
        {
            int depth = 0;
            bool foundFormContent = false;

            for (int i = formPos + 1; i < lines.Count; i++)
            {
                var line = lines[i];

                // Track Razor block depth
                if (razorBlockPattern.IsMatch(line))
                {
                    depth++;
                }

                // Found a closing brace
                if (closingBracePattern.IsMatch(line))
                {
                    if (depth > 0)
                    {
                        // This brace closes a nested Razor block
                        depth--;
                    }
                    else if (foundFormContent)
                    {
                        // This brace closes the form
                        closingBracePositions.Add(i);
                        break;
                    }
                }

                // Any actual content after form tag means we're in the form body
                if (!string.IsNullOrWhiteSpace(line) && !closingBracePattern.IsMatch(line))
                {
                    foundFormContent = true;
                }
            }
        }

        // Replace closing braces with </form> (process in reverse to maintain line indices)
        foreach (var pos in closingBracePositions.OrderByDescending(p => p))
        {
            lines[pos] = lines[pos].Replace("}", "</form>");
        }

        return string.Join("\n", lines);
    }

    private static string TransformActionLinks(string content)
    {
        // @Html.ActionLink("Text", "Action", "Controller", new { area = "Admin" }, null) - with area
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*,\s*""(\w+)""\s*,\s*new\s*\{\s*area\s*=\s*""([^""]*)""\s*\}\s*,\s*null\s*\)",
            match =>
            {
                var text = match.Groups[1].Value;
                var action = match.Groups[2].Value;
                var controller = match.Groups[3].Value;
                var area = match.Groups[4].Value;
                var areaAttr = string.IsNullOrEmpty(area) ? "" : $" asp-area=\"{area}\"";
                return $"<a asp-controller=\"{controller}\" asp-action=\"{action}\"{areaAttr}>{text}</a>";
            });

        // @Html.ActionLink("Text", "Action", "Controller", new { area = "", id = x }, new { @class = "..." })
        content = Regex.Replace(content,
            @"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""(\w+)""\s*,\s*""(\w+)""\s*,\s*new\s*\{([^}]*)\}\s*,\s*(?:null|new\s*\{([^}]*)\})\s*\)",
            match =>
            {
                var text = match.Groups[1].Value;
                var action = match.Groups[2].Value;
                var controller = match.Groups[3].Value;
                var routeValuesRaw = match.Groups[4].Value;
                var htmlAttrsRaw = match.Groups[5].Value;

                var routeAttrs = new List<string>();
                var areaAttr = "";

                // Parse route values
                var routeMatches = Regex.Matches(routeValuesRaw, @"(\w+)\s*=\s*""?([^"",}]+)""?");
                foreach (Match rm in routeMatches)
                {
                    var key = rm.Groups[1].Value;
                    var value = rm.Groups[2].Value.Trim('"');
                    if (key == "area")
                    {
                        if (!string.IsNullOrEmpty(value))
                            areaAttr = $" asp-area=\"{value}\"";
                    }
                    else
                    {
                        routeAttrs.Add($"asp-route-{key}=\"{value}\"");
                    }
                }

                var attrs = ParseHtmlAttributes(htmlAttrsRaw);
                var routeStr = routeAttrs.Count > 0 ? " " + string.Join(" ", routeAttrs) : "";
                return $"<a asp-controller=\"{controller}\" asp-action=\"{action}\"{areaAttr}{routeStr}{attrs}>{text}</a>";
            });

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
        // ValidationMessageFor - support nested properties with ([\w.]+)
        // Pattern: @Html.ValidationMessageFor(m => m.Name, "", new { @class = "text-danger" })
        content = Regex.Replace(content,
            @"@Html\.ValidationMessageFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)(?:\s*,\s*""[^""]*"")?(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<span asp-validation-for=\"{propName}\"{attrs}></span>";
            });

        // ValidationSummary(true, "Message", new { @class = "..." }) - full parameters
        content = Regex.Replace(content,
            @"@Html\.ValidationSummary\s*\(\s*true\s*,\s*""([^""]*)""\s*,\s*new\s*\{([^}]*)\}\s*\)",
            match =>
            {
                var message = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                var messageContent = string.IsNullOrEmpty(message) ? "" : message;
                return $"<div asp-validation-summary=\"ModelOnly\"{attrs}>{messageContent}</div>";
            });

        // ValidationSummary(false, "Message", new { @class = "..." }) - All with message
        content = Regex.Replace(content,
            @"@Html\.ValidationSummary\s*\(\s*false\s*,\s*""([^""]*)""\s*,\s*new\s*\{([^}]*)\}\s*\)",
            match =>
            {
                var message = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                var messageContent = string.IsNullOrEmpty(message) ? "" : message;
                return $"<div asp-validation-summary=\"All\"{attrs}>{messageContent}</div>";
            });

        // ValidationSummary(true, "Message") - with message only
        content = Regex.Replace(content,
            @"@Html\.ValidationSummary\s*\(\s*true\s*,\s*""([^""]+)""\s*\)",
            "<div asp-validation-summary=\"ModelOnly\">$1</div>");

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
        // LabelFor with custom text and htmlAttributes - @Html.LabelFor(m => m.Id, "Custom Text", htmlAttributes: new { @class = "..." })
        content = Regex.Replace(content,
            @"@Html\.LabelFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*""([^""]+)""\s*,\s*(?:htmlAttributes:\s*)?new\s*\{([^}]*)\}\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var customText = match.Groups[2].Value;
                var attrs = ParseHtmlAttributes(match.Groups[3].Value);
                return $"<label asp-for=\"{propName}\"{attrs}>{customText}</label>";
            });

        // LabelFor with custom text only - @Html.LabelFor(m => m.Id, "Custom Text")
        content = Regex.Replace(content,
            @"@Html\.LabelFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*""([^""]+)""\s*\)",
            "<label asp-for=\"$1\">$2</label>");

        // LabelFor with htmlAttributes using named parameter - @Html.LabelFor(m => m.Name, htmlAttributes: new { ... })
        content = Regex.Replace(content,
            @"@Html\.LabelFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*htmlAttributes:\s*new\s*\{([^}]*)\}\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<label asp-for=\"{propName}\"{attrs}></label>";
            });

        // LabelFor - basic with optional htmlAttributes
        content = Regex.Replace(content,
            @"@Html\.LabelFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)(?:\s*,\s*new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var propName = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<label asp-for=\"{propName}\"{attrs}></label>";
            });

        // Html.Label("Name") - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.Label\s*\(\s*""([^""]+)""\s*\)",
            "<label>$1</label>");

        // DisplayFor - @Html.DisplayFor(m => m.Name) → <span>@Model.Name</span>
        content = Regex.Replace(content,
            @"@Html\.DisplayFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*\)",
            "<span>@Model.$1</span>");

        // DisplayFor with modelItem (in foreach) - @Html.DisplayFor(modelItem => item.Property)
        content = Regex.Replace(content,
            @"@Html\.DisplayFor\s*\(\s*modelItem\s*=>\s*(\w+)\.([\w.]+)\s*\)",
            "<span>@$1.$2</span>");

        // DisplayNameFor - preserves the helper as it works in Core
        content = Regex.Replace(content,
            @"@Html\.DisplayNameFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*\)",
            "@Html.DisplayNameFor(model => model.$1)");

        // EditorForModel - needs manual review
        content = Regex.Replace(content,
            @"@Html\.EditorForModel\s*\(\s*\)",
            "<!-- TODO: EditorForModel() - implement custom editor template or expand to individual fields -->");

        // Html.DropDownList("Name", selectList) - non-lambda version
        content = Regex.Replace(content,
            @"@Html\.DropDownList\s*\(\s*""(\w+)""\s*,\s*null(?:\s*,\s*(?:htmlAttributes:\s*)?new\s*\{([^}]*)\})?\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var attrs = ParseHtmlAttributes(match.Groups[2].Value);
                return $"<select name=\"{name}\" asp-items=\"ViewBag.{name}\"{attrs}></select>";
            });

        // Html.DropDownList("Name", String.Empty) - with optionLabel
        content = Regex.Replace(content,
            @"@Html\.DropDownList\s*\(\s*""(\w+)""\s*,\s*String\.Empty\s*\)",
            match =>
            {
                var name = match.Groups[1].Value;
                return $"<select name=\"{name}\" asp-items=\"ViewBag.{name}\"><option value=\"\">Select...</option></select>";
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
        // Support nested properties
        var match = Regex.Match(code, @"Html\.TextAreaFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)");
        if (match.Success)
        {
            return $"<textarea asp-for=\"{match.Groups[1].Value}\"></textarea>";
        }
        return code;
    }
    private static string TransformDropDownListFor(string code)
    {
        // Support nested properties
        var match = Regex.Match(code, @"Html\.DropDownListFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*([^,)]+)");
        if (match.Success)
        {
            return $"<select asp-for=\"{match.Groups[1].Value}\" asp-items=\"{match.Groups[2].Value.Trim()}\"></select>";
        }
        return code;
    }

    private static string TransformListBoxFor(string code)
    {
        // @Html.ListBoxFor(m => m.SelectedIds, Model.Items) → <select asp-for="SelectedIds" asp-items="Model.Items" multiple="multiple"></select>
        var match = Regex.Match(code, @"Html\.ListBoxFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*([^,)]+)");
        if (match.Success)
        {
            return $"<select asp-for=\"{match.Groups[1].Value}\" asp-items=\"{match.Groups[2].Value.Trim()}\" multiple=\"multiple\"></select>";
        }
        return code;
    }

    private static string TransformRadioButtonFor(string code)
    {
        // @Html.RadioButtonFor(m => m.Gender, "Male") → <input asp-for="Gender" type="radio" value="Male" />
        var match = Regex.Match(code, @"Html\.RadioButtonFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)\s*,\s*""?([^"")]+)""?");
        if (match.Success)
        {
            var propName = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim('"');
            return $"<input asp-for=\"{propName}\" type=\"radio\" value=\"{value}\" />";
        }
        return code;
    }

    private static string TransformEditorFor(string code)
    {
        // Support nested properties
        var match = Regex.Match(code, @"Html\.EditorFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)");
        if (match.Success)
        {
            return $"<input asp-for=\"{match.Groups[1].Value}\" />";
        }
        return code;
    }

    private static string TransformDisplayFor(string code)
    {
        // @Html.DisplayFor(m => m.Name) → <span>@Model.Name</span>
        var match = Regex.Match(code, @"Html\.DisplayFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)");
        if (match.Success)
        {
            return $"<span>@Model.{match.Groups[1].Value}</span>";
        }
        return code;
    }

    private static string TransformDisplayNameFor(string code)
    {
        // @Html.DisplayNameFor(m => m.Name) → @Html.DisplayNameFor(model => model.Name)
        // Note: DisplayNameFor works in ASP.NET Core, just standardize the lambda parameter
        var match = Regex.Match(code, @"Html\.DisplayNameFor\s*\(\s*\w+\s*=>\s*\w+\.([\w.]+)");
        if (match.Success)
        {
            return $"@Html.DisplayNameFor(model => model.{match.Groups[1].Value})";
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
