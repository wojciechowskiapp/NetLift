namespace NetLift.Core.Models.Razor;

/// <summary>
/// Contains information about an analyzed Razor view file.
/// </summary>
public sealed record RazorViewInfo
{
    /// <summary>
    /// The file path of the .cshtml file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The view name (without extension).
    /// </summary>
    public required string ViewName { get; init; }

    /// <summary>
    /// The @model directive type, if specified.
    /// </summary>
    public string? ModelType { get; init; }

    /// <summary>
    /// The layout file path, if specified.
    /// </summary>
    public string? Layout { get; init; }

    /// <summary>
    /// HTML helper usages found in the view.
    /// </summary>
    public IReadOnlyList<HtmlHelperUsage> HtmlHelpers { get; init; } = [];

    /// <summary>
    /// Bundle references (@Scripts.Render, @Styles.Render).
    /// </summary>
    public IReadOnlyList<BundleReference> BundleReferences { get; init; } = [];

    /// <summary>
    /// URL helper usages (Url.Content, Url.Action).
    /// </summary>
    public IReadOnlyList<UrlHelperUsage> UrlHelpers { get; init; } = [];

    /// <summary>
    /// Partial view references.
    /// </summary>
    public IReadOnlyList<PartialViewReference> PartialViews { get; init; } = [];

    /// <summary>
    /// @section definitions.
    /// </summary>
    public IReadOnlyList<string> Sections { get; init; } = [];

    /// <summary>
    /// Whether this is a layout file (_Layout.cshtml).
    /// </summary>
    public bool IsLayout { get; init; }

    /// <summary>
    /// Whether the view uses ViewBag.
    /// </summary>
    public bool UsesViewBag { get; init; }

    /// <summary>
    /// Overall confidence score for migration (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Represents usage of an HTML helper method in a Razor view.
/// </summary>
public sealed record HtmlHelperUsage
{
    /// <summary>
    /// The type of HTML helper.
    /// </summary>
    public required HtmlHelperType HelperType { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The line number in the view.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Arguments passed to the helper.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// HTML attributes specified (for input helpers).
    /// </summary>
    public IReadOnlyDictionary<string, string> HtmlAttributes { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The transformed Tag Helper code.
    /// </summary>
    public string? TransformedCode { get; init; }

    /// <summary>
    /// Confidence score for this transformation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Notes or warnings about the transformation.
    /// </summary>
    public string? TransformationNote { get; init; }
}

/// <summary>
/// Types of HTML helpers that need transformation.
/// </summary>
public enum HtmlHelperType
{
    ActionLink,
    RouteLink,
    BeginForm,
    BeginRouteForm,
    EndForm,
    AntiForgeryToken,
    TextBox,
    TextBoxFor,
    TextArea,
    TextAreaFor,
    Password,
    PasswordFor,
    Hidden,
    HiddenFor,
    CheckBox,
    CheckBoxFor,
    RadioButton,
    RadioButtonFor,
    DropDownList,
    DropDownListFor,
    ListBox,
    ListBoxFor,
    Editor,
    EditorFor,
    EditorForModel,
    Display,
    DisplayFor,
    DisplayForModel,
    DisplayName,
    DisplayNameFor,
    Label,
    LabelFor,
    ValidationMessage,
    ValidationMessageFor,
    ValidationSummary,
    Partial,
    RenderPartial,
    Action,
    RenderAction,
    Raw,
    Encode,
    AttributeEncode,
    Id,
    IdFor,
    Name,
    NameFor,
    Value,
    ValueFor
}

/// <summary>
/// Represents a bundle reference (@Scripts.Render or @Styles.Render).
/// </summary>
public sealed record BundleReference
{
    /// <summary>
    /// The type of bundle.
    /// </summary>
    public required BundleReferenceType BundleType { get; init; }

    /// <summary>
    /// The bundle virtual path (e.g., "~/bundles/jquery").
    /// </summary>
    public required string BundlePath { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The line number in the view.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The transformed script/link tags.
    /// </summary>
    public string? TransformedCode { get; init; }
}

/// <summary>
/// Types of bundle references.
/// </summary>
public enum BundleReferenceType
{
    /// <summary>
    /// @Scripts.Render()
    /// </summary>
    Scripts,

    /// <summary>
    /// @Styles.Render()
    /// </summary>
    Styles
}

/// <summary>
/// Represents usage of URL helpers.
/// </summary>
public sealed record UrlHelperUsage
{
    /// <summary>
    /// The type of URL helper.
    /// </summary>
    public required UrlHelperType HelperType { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The line number in the view.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The transformed code.
    /// </summary>
    public string? TransformedCode { get; init; }
}

/// <summary>
/// Types of URL helpers.
/// </summary>
public enum UrlHelperType
{
    Content,
    Action,
    RouteUrl
}

/// <summary>
/// Represents a partial view reference.
/// </summary>
public sealed record PartialViewReference
{
    /// <summary>
    /// The type of partial reference.
    /// </summary>
    public required PartialReferenceType ReferenceType { get; init; }

    /// <summary>
    /// The partial view name.
    /// </summary>
    public required string PartialName { get; init; }

    /// <summary>
    /// The model passed to the partial.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The line number in the view.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The transformed code.
    /// </summary>
    public string? TransformedCode { get; init; }

    /// <summary>
    /// Confidence score for this transformation.
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Types of partial view references.
/// </summary>
public enum PartialReferenceType
{
    Partial,
    RenderPartial,
    Action,
    RenderAction
}

/// <summary>
/// Result of transforming a Razor view.
/// </summary>
public sealed record RazorViewTransformResult
{
    /// <summary>
    /// The original view info.
    /// </summary>
    public required RazorViewInfo OriginalView { get; init; }

    /// <summary>
    /// The transformed content.
    /// </summary>
    public required string TransformedContent { get; init; }

    /// <summary>
    /// Number of transformations applied.
    /// </summary>
    public int TransformationCount { get; init; }

    /// <summary>
    /// Overall confidence score.
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Warnings or notes about the transformation.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    /// Whether the transformation was successful.
    /// </summary>
    public bool Success { get; init; }
}
