namespace NetLift.Core.Interfaces;

/// <summary>
/// Orchestrates source file transformations by detecting file type and applying appropriate rewriter chain.
/// Coordinates multiple rewriters and aggregates their confidence scores and diagnostics.
/// </summary>
public interface ISourceFileTransformer
{
    /// <summary>
    /// Transforms a source file by detecting its type and applying the appropriate rewriter chain.
    /// </summary>
    /// <param name="filePath">The path to the source file being transformed (for diagnostics).</param>
    /// <param name="sourceCode">The C# source code to transform.</param>
    /// <param name="fileType">The detected file type, or Unknown to auto-detect.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The transformation result with aggregated confidence and diagnostics.</returns>
    Task<SourceTransformResult> TransformAsync(
        string filePath,
        string sourceCode,
        SourceFileType fileType = SourceFileType.Unknown,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the source file type by analyzing the code structure using Roslyn.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <returns>The detected file type.</returns>
    SourceFileType DetectFileType(string sourceCode);
}

/// <summary>
/// Represents the type of source file for targeted transformation.
/// </summary>
public enum SourceFileType
{
    /// <summary>
    /// Unknown or non-migratable file type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// MVC controller (inherits Controller or ApiController).
    /// </summary>
    Controller = 1,

    /// <summary>
    /// Entity Framework DbContext.
    /// </summary>
    DbContext = 2,

    /// <summary>
    /// WCF service implementation (contains [ServiceContract]).
    /// </summary>
    WcfService = 3,

    /// <summary>
    /// MVC action filter (implements IActionFilter, IAuthorizationFilter, etc.).
    /// </summary>
    ActionFilter = 4,

    /// <summary>
    /// Other C# file that may need minimal transformations (namespace rewrites).
    /// </summary>
    Other = 5
}

/// <summary>
/// Represents the result of a source file transformation.
/// </summary>
public sealed record SourceTransformResult
{
    /// <summary>
    /// Gets whether the source code was modified during transformation.
    /// </summary>
    public bool Changed { get; init; }

    /// <summary>
    /// Gets the transformed source code.
    /// </summary>
    public string TransformedCode { get; init; } = "";

    /// <summary>
    /// Gets the overall confidence score (0-100) based on minimum of all applied rewriters.
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the list of rewriter names that were applied during transformation.
    /// </summary>
    public IReadOnlyList<string> AppliedTransformers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets all diagnostic messages aggregated from applied rewriters.
    /// </summary>
    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();

    /// <summary>
    /// Gets TODO comments generated for low-confidence transformations (&lt;60%).
    /// </summary>
    public IReadOnlyList<string> TodoComments { get; init; } = Array.Empty<string>();
}
