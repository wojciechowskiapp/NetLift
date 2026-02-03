using System.Text.RegularExpressions;
using NetLift.Core.Interfaces.Razor;
using NetLift.Core.Models.Razor;

namespace NetLift.Transforms.Razor.Analyzers;

/// <summary>
/// Analyzes Razor view files to identify HTML helpers and patterns that need transformation.
/// </summary>
public partial class RazorViewAnalyzer : IRazorViewAnalyzer
{
    // HTML Helper patterns
    [GeneratedRegex(@"@Html\.(\w+)\(([^)]*(?:\([^)]*\)[^)]*)*)\)", RegexOptions.Compiled)]
    private static partial Regex HtmlHelperRegex();

    [GeneratedRegex(@"@Html\.ActionLink\s*\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*(?:,\s*""([^""]*)"")?\s*(?:,\s*new\s*\{([^}]*)\})?\s*(?:,\s*null)?\s*\)", RegexOptions.Compiled)]
    private static partial Regex ActionLinkRegex();

    [GeneratedRegex(@"@using\s*\(\s*Html\.BeginForm\s*\(([^)]*)\)\s*\)", RegexOptions.Compiled)]
    private static partial Regex BeginFormRegex();

    // Support nested properties like m => m.Address.City using ([\w.]+) instead of (\w+)
    [GeneratedRegex(@"@Html\.(TextBoxFor|TextAreaFor|PasswordFor|HiddenFor|CheckBoxFor|DropDownListFor|RadioButtonFor|ListBoxFor|EditorFor)\s*\(\s*(\w+)\s*=>\s*\2\.([\w.]+)(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled)]
    private static partial Regex InputForHelperRegex();

    // Support nested properties and additional parameters
    [GeneratedRegex(@"@Html\.(ValidationMessageFor|LabelFor|DisplayFor|DisplayNameFor)\s*\(\s*(\w+)\s*=>\s*\2\.([\w.]+)(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled)]
    private static partial Regex SimpleForHelperRegex();

    [GeneratedRegex(@"@Html\.ValidationSummary\s*\(\s*(true|false)?\s*\)", RegexOptions.Compiled)]
    private static partial Regex ValidationSummaryRegex();

    // Bundle patterns
    [GeneratedRegex(@"@(Scripts|Styles)\.Render\s*\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled)]
    private static partial Regex BundleRenderRegex();

    // URL Helper patterns
    [GeneratedRegex(@"@Url\.Content\s*\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled)]
    private static partial Regex UrlContentRegex();

    [GeneratedRegex(@"@Url\.Action\s*\(\s*""([^""]+)""(?:\s*,\s*""([^""]+)"")?\s*\)", RegexOptions.Compiled)]
    private static partial Regex UrlActionRegex();

    // Partial view patterns
    [GeneratedRegex(@"@Html\.(Partial|RenderPartial)\s*\(\s*""([^""]+)""(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled)]
    private static partial Regex PartialRegex();

    [GeneratedRegex(@"@(?:Html\.)?(?:Action|RenderAction)\s*\(\s*""([^""]+)""(?:\s*,\s*""([^""]+)"")?\s*\)", RegexOptions.Compiled)]
    private static partial Regex ActionRegex();

    // Model and Layout patterns
    [GeneratedRegex(@"@model\s+([^\r\n]+)", RegexOptions.Compiled)]
    private static partial Regex ModelRegex();

    [GeneratedRegex(@"Layout\s*=\s*""([^""]+)""\s*;", RegexOptions.Compiled)]
    private static partial Regex LayoutRegex();

    [GeneratedRegex(@"@section\s+(\w+)", RegexOptions.Compiled)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"ViewBag\.", RegexOptions.Compiled)]
    private static partial Regex ViewBagRegex();

    /// <inheritdoc />
    public async Task<RazorViewInfo> AnalyzeViewAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return AnalyzeView(content, filePath);
    }

    /// <inheritdoc />
    public RazorViewInfo AnalyzeView(string content, string filePath)
    {
        var viewName = Path.GetFileNameWithoutExtension(filePath);
        var isLayout = viewName.StartsWith("_Layout", StringComparison.OrdinalIgnoreCase);

        var modelMatch = ModelRegex().Match(content);
        var layoutMatch = LayoutRegex().Match(content);

        var htmlHelpers = DetectHtmlHelpers(content);
        var bundleReferences = DetectBundleReferences(content);
        var urlHelpers = DetectUrlHelpers(content);
        var partialViews = DetectPartialViews(content);
        var sections = DetectSections(content);
        var usesViewBag = ViewBagRegex().IsMatch(content);

        var confidence = CalculateConfidence(htmlHelpers, bundleReferences, partialViews);

        return new RazorViewInfo
        {
            FilePath = filePath,
            ViewName = viewName,
            ModelType = modelMatch.Success ? modelMatch.Groups[1].Value.Trim() : null,
            Layout = layoutMatch.Success ? layoutMatch.Groups[1].Value : null,
            HtmlHelpers = htmlHelpers,
            BundleReferences = bundleReferences,
            UrlHelpers = urlHelpers,
            PartialViews = partialViews,
            Sections = sections,
            IsLayout = isLayout,
            UsesViewBag = usesViewBag,
            Confidence = confidence
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RazorViewInfo>> AnalyzeProjectViewsAsync(string projectPath)
    {
        var views = new List<RazorViewInfo>();
        var viewsPath = Path.Combine(projectPath, "Views");

        if (!Directory.Exists(viewsPath))
        {
            return views;
        }

        var cshtmlFiles = Directory.GetFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories);

        foreach (var file in cshtmlFiles)
        {
            try
            {
                var viewInfo = await AnalyzeViewAsync(file);
                views.Add(viewInfo);
            }
            catch (IOException)
            {
                // Skip files that can't be read
            }
        }

        return views;
    }

    /// <inheritdoc />
    public IReadOnlyList<HtmlHelperUsage> DetectHtmlHelpers(string content)
    {
        var helpers = new List<HtmlHelperUsage>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // ActionLink
            foreach (Match match in ActionLinkRegex().Matches(line))
            {
                helpers.Add(CreateHtmlHelperUsage(HtmlHelperType.ActionLink, match.Value, lineNumber, match));
            }

            // BeginForm
            foreach (Match match in BeginFormRegex().Matches(line))
            {
                helpers.Add(CreateHtmlHelperUsage(HtmlHelperType.BeginForm, match.Value, lineNumber, match));
            }

            // Input helpers (TextBoxFor, EditorFor, etc.)
            foreach (Match match in InputForHelperRegex().Matches(line))
            {
                var helperType = ParseHelperType(match.Groups[1].Value);
                helpers.Add(CreateHtmlHelperUsage(helperType, match.Value, lineNumber, match));
            }

            // Simple helpers (ValidationMessageFor, LabelFor, etc.)
            foreach (Match match in SimpleForHelperRegex().Matches(line))
            {
                var helperType = ParseHelperType(match.Groups[1].Value);
                helpers.Add(CreateHtmlHelperUsage(helperType, match.Value, lineNumber, match));
            }

            // ValidationSummary
            foreach (Match match in ValidationSummaryRegex().Matches(line))
            {
                helpers.Add(CreateHtmlHelperUsage(HtmlHelperType.ValidationSummary, match.Value, lineNumber, match));
            }

            // Generic @Html.* calls not caught by specific patterns
            foreach (Match match in HtmlHelperRegex().Matches(line))
            {
                if (!helpers.Any(h => h.OriginalCode == match.Value))
                {
                    var helperType = ParseHelperType(match.Groups[1].Value);
                    helpers.Add(CreateHtmlHelperUsage(helperType, match.Value, lineNumber, match));
                }
            }
        }

        return helpers;
    }

    /// <inheritdoc />
    public IReadOnlyList<BundleReference> DetectBundleReferences(string content)
    {
        var bundles = new List<BundleReference>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            foreach (Match match in BundleRenderRegex().Matches(line))
            {
                var bundleType = match.Groups[1].Value == "Scripts"
                    ? BundleReferenceType.Scripts
                    : BundleReferenceType.Styles;

                bundles.Add(new BundleReference
                {
                    BundleType = bundleType,
                    BundlePath = match.Groups[2].Value,
                    OriginalCode = match.Value,
                    LineNumber = lineNumber
                });
            }
        }

        return bundles;
    }

    private IReadOnlyList<UrlHelperUsage> DetectUrlHelpers(string content)
    {
        var helpers = new List<UrlHelperUsage>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            foreach (Match match in UrlContentRegex().Matches(line))
            {
                helpers.Add(new UrlHelperUsage
                {
                    HelperType = UrlHelperType.Content,
                    OriginalCode = match.Value,
                    LineNumber = lineNumber
                });
            }

            foreach (Match match in UrlActionRegex().Matches(line))
            {
                helpers.Add(new UrlHelperUsage
                {
                    HelperType = UrlHelperType.Action,
                    OriginalCode = match.Value,
                    LineNumber = lineNumber
                });
            }
        }

        return helpers;
    }

    private IReadOnlyList<PartialViewReference> DetectPartialViews(string content)
    {
        var partials = new List<PartialViewReference>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            foreach (Match match in PartialRegex().Matches(line))
            {
                var refType = match.Groups[1].Value == "Partial"
                    ? PartialReferenceType.Partial
                    : PartialReferenceType.RenderPartial;

                partials.Add(new PartialViewReference
                {
                    ReferenceType = refType,
                    PartialName = match.Groups[2].Value,
                    Model = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null,
                    OriginalCode = match.Value,
                    LineNumber = lineNumber,
                    Confidence = 85
                });
            }

            foreach (Match match in ActionRegex().Matches(line))
            {
                var isRender = match.Value.Contains("RenderAction");
                partials.Add(new PartialViewReference
                {
                    ReferenceType = isRender ? PartialReferenceType.RenderAction : PartialReferenceType.Action,
                    PartialName = match.Groups[1].Value,
                    OriginalCode = match.Value,
                    LineNumber = lineNumber,
                    Confidence = 60 // Lower confidence - needs ViewComponent
                });
            }
        }

        return partials;
    }

    private static IReadOnlyList<string> DetectSections(string content)
    {
        return SectionRegex().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    private static HtmlHelperUsage CreateHtmlHelperUsage(HtmlHelperType type, string code, int lineNumber, Match match)
    {
        var confidence = GetHelperConfidence(type);
        return new HtmlHelperUsage
        {
            HelperType = type,
            OriginalCode = code,
            LineNumber = lineNumber,
            Arguments = ExtractArguments(match),
            Confidence = confidence
        };
    }

    private static IReadOnlyList<string> ExtractArguments(Match match)
    {
        var args = new List<string>();
        for (int i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
            {
                args.Add(match.Groups[i].Value.Trim());
            }
        }
        return args;
    }

    private static HtmlHelperType ParseHelperType(string name)
    {
        return name switch
        {
            "ActionLink" => HtmlHelperType.ActionLink,
            "RouteLink" => HtmlHelperType.RouteLink,
            "BeginForm" => HtmlHelperType.BeginForm,
            "TextBox" => HtmlHelperType.TextBox,
            "TextBoxFor" => HtmlHelperType.TextBoxFor,
            "TextArea" => HtmlHelperType.TextArea,
            "TextAreaFor" => HtmlHelperType.TextAreaFor,
            "Password" => HtmlHelperType.Password,
            "PasswordFor" => HtmlHelperType.PasswordFor,
            "Hidden" => HtmlHelperType.Hidden,
            "HiddenFor" => HtmlHelperType.HiddenFor,
            "CheckBox" => HtmlHelperType.CheckBox,
            "CheckBoxFor" => HtmlHelperType.CheckBoxFor,
            "RadioButton" => HtmlHelperType.RadioButton,
            "RadioButtonFor" => HtmlHelperType.RadioButtonFor,
            "DropDownList" => HtmlHelperType.DropDownList,
            "DropDownListFor" => HtmlHelperType.DropDownListFor,
            "ListBox" => HtmlHelperType.ListBox,
            "ListBoxFor" => HtmlHelperType.ListBoxFor,
            "Editor" => HtmlHelperType.Editor,
            "EditorFor" => HtmlHelperType.EditorFor,
            "EditorForModel" => HtmlHelperType.EditorForModel,
            "Display" => HtmlHelperType.Display,
            "DisplayFor" => HtmlHelperType.DisplayFor,
            "DisplayForModel" => HtmlHelperType.DisplayForModel,
            "DisplayName" => HtmlHelperType.DisplayName,
            "DisplayNameFor" => HtmlHelperType.DisplayNameFor,
            "Label" => HtmlHelperType.Label,
            "LabelFor" => HtmlHelperType.LabelFor,
            "ValidationMessage" => HtmlHelperType.ValidationMessage,
            "ValidationMessageFor" => HtmlHelperType.ValidationMessageFor,
            "ValidationSummary" => HtmlHelperType.ValidationSummary,
            "Partial" => HtmlHelperType.Partial,
            "RenderPartial" => HtmlHelperType.RenderPartial,
            "Action" => HtmlHelperType.Action,
            "RenderAction" => HtmlHelperType.RenderAction,
            "Raw" => HtmlHelperType.Raw,
            "Encode" => HtmlHelperType.Encode,
            "AntiForgeryToken" => HtmlHelperType.AntiForgeryToken,
            "Id" => HtmlHelperType.Id,
            "IdFor" => HtmlHelperType.IdFor,
            "Name" => HtmlHelperType.Name,
            "NameFor" => HtmlHelperType.NameFor,
            _ => HtmlHelperType.Raw
        };
    }

    private static int GetHelperConfidence(HtmlHelperType type)
    {
        return type switch
        {
            HtmlHelperType.ActionLink => 95,
            HtmlHelperType.BeginForm => 95,
            HtmlHelperType.TextBoxFor => 90,
            HtmlHelperType.TextAreaFor => 90,
            HtmlHelperType.PasswordFor => 90,
            HtmlHelperType.HiddenFor => 95,
            HtmlHelperType.CheckBoxFor => 90,
            HtmlHelperType.DropDownListFor => 85,
            HtmlHelperType.LabelFor => 95,
            HtmlHelperType.ValidationMessageFor => 90,
            HtmlHelperType.ValidationSummary => 90,
            HtmlHelperType.EditorFor => 80,
            HtmlHelperType.EditorForModel => 70,
            HtmlHelperType.Partial => 85,
            HtmlHelperType.RenderPartial => 85,
            HtmlHelperType.Action => 60, // Needs ViewComponent
            HtmlHelperType.RenderAction => 60,
            HtmlHelperType.AntiForgeryToken => 95,
            _ => 75
        };
    }

    private static int CalculateConfidence(
        IReadOnlyList<HtmlHelperUsage> helpers,
        IReadOnlyList<BundleReference> bundles,
        IReadOnlyList<PartialViewReference> partials)
    {
        if (helpers.Count == 0 && bundles.Count == 0 && partials.Count == 0)
        {
            return 100; // Nothing to transform
        }

        var totalConfidence = 0;
        var count = 0;

        foreach (var helper in helpers)
        {
            totalConfidence += helper.Confidence;
            count++;
        }

        foreach (var bundle in bundles)
        {
            totalConfidence += 80; // Bundles are medium confidence
            count++;
        }

        foreach (var partial in partials)
        {
            totalConfidence += partial.Confidence;
            count++;
        }

        return count > 0 ? totalConfidence / count : 100;
    }
}
