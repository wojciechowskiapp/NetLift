namespace NetLift.Core.Models.StaticFiles;

/// <summary>
/// Contains information about static files in a project.
/// </summary>
public sealed record StaticFilesInfo
{
    /// <summary>
    /// The project path.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Static folders detected (Content, Scripts, Images, etc.).
    /// </summary>
    public IReadOnlyList<StaticFolder> Folders { get; init; } = [];

    /// <summary>
    /// Static file references found in code/views.
    /// </summary>
    public IReadOnlyList<StaticFileReference> References { get; init; } = [];

    /// <summary>
    /// Whether a Content folder exists.
    /// </summary>
    public bool HasContentFolder { get; init; }

    /// <summary>
    /// Whether a Scripts folder exists.
    /// </summary>
    public bool HasScriptsFolder { get; init; }

    /// <summary>
    /// Whether an Images folder exists.
    /// </summary>
    public bool HasImagesFolder { get; init; }

    /// <summary>
    /// Whether a fonts folder exists.
    /// </summary>
    public bool HasFontsFolder { get; init; }

    /// <summary>
    /// Whether BundleConfig.cs exists.
    /// </summary>
    public bool HasBundleConfig { get; init; }

    /// <summary>
    /// Whether wwwroot already exists.
    /// </summary>
    public bool HasWwwroot { get; init; }

    /// <summary>
    /// Total number of static files found.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Total size of static files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }
}

/// <summary>
/// Represents a static file folder.
/// </summary>
public sealed record StaticFolder
{
    /// <summary>
    /// The source folder path (relative to project).
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The target folder path under wwwroot.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// The type of static folder.
    /// </summary>
    public required StaticFolderType FolderType { get; init; }

    /// <summary>
    /// Files in this folder.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Whether the folder exists.
    /// </summary>
    public bool Exists { get; init; }
}

/// <summary>
/// Types of static file folders.
/// </summary>
public enum StaticFolderType
{
    Css,
    JavaScript,
    Images,
    Fonts,
    Other
}

/// <summary>
/// Represents a reference to a static file in code or views.
/// </summary>
public sealed record StaticFileReference
{
    /// <summary>
    /// The file containing the reference.
    /// </summary>
    public required string SourceFile { get; init; }

    /// <summary>
    /// The original path (e.g., "~/Content/site.css").
    /// </summary>
    public required string OriginalPath { get; init; }

    /// <summary>
    /// The new path (e.g., "~/css/site.css").
    /// </summary>
    public required string NewPath { get; init; }

    /// <summary>
    /// The line number of the reference.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The type of reference.
    /// </summary>
    public required StaticReferenceType ReferenceType { get; init; }

    /// <summary>
    /// The original code snippet.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// The transformed code snippet.
    /// </summary>
    public string? TransformedCode { get; init; }
}

/// <summary>
/// Types of static file references.
/// </summary>
public enum StaticReferenceType
{
    /// <summary>
    /// link href in cshtml/html
    /// </summary>
    LinkHref,

    /// <summary>
    /// script src in cshtml/html
    /// </summary>
    ScriptSrc,

    /// <summary>
    /// img src in cshtml/html
    /// </summary>
    ImageSrc,

    /// <summary>
    /// @Url.Content() in cshtml
    /// </summary>
    UrlContent,

    /// <summary>
    /// url() in CSS files
    /// </summary>
    CssUrl,

    /// <summary>
    /// Bundle path in BundleConfig.cs
    /// </summary>
    BundleConfig,

    /// <summary>
    /// Other reference type
    /// </summary>
    Other
}

/// <summary>
/// Result of static files migration.
/// </summary>
public sealed record StaticFilesMigrationResult
{
    /// <summary>
    /// Whether the migration was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Folders moved to wwwroot.
    /// </summary>
    public IReadOnlyList<StaticFolder> MovedFolders { get; init; } = [];

    /// <summary>
    /// Files moved to wwwroot.
    /// </summary>
    public int FilesMovedCount { get; init; }

    /// <summary>
    /// References updated.
    /// </summary>
    public int ReferencesUpdatedCount { get; init; }

    /// <summary>
    /// Overall confidence score.
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Warnings or notes.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    /// Errors encountered.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
