namespace NetLift.Core.Models.SignalR;

/// <summary>
/// Result of SignalR modernization transformation.
/// </summary>
public record SignalRModernizationResult
{
    /// <summary>
    /// Whether the transformation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Transformed Hub files.
    /// </summary>
    public IReadOnlyList<TransformedSignalRFile> TransformedFiles { get; init; } = [];

    /// <summary>
    /// Generated startup/Program.cs code for SignalR configuration.
    /// </summary>
    public string? GeneratedStartupCode { get; init; }

    /// <summary>
    /// Packages to add.
    /// </summary>
    public IReadOnlyList<string> PackagesToAdd { get; init; } = [];

    /// <summary>
    /// Packages to remove.
    /// </summary>
    public IReadOnlyList<string> PackagesToRemove { get; init; } = [];

    /// <summary>
    /// Warnings and issues found during transformation.
    /// </summary>
    public IReadOnlyList<SignalRWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Overall confidence score (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// A transformed SignalR file.
/// </summary>
public record TransformedSignalRFile
{
    /// <summary>
    /// The original file path.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The file type (Hub, Service with GlobalHost, etc.).
    /// </summary>
    public required SignalRFileType FileType { get; init; }

    /// <summary>
    /// The transformed code.
    /// </summary>
    public required string TransformedCode { get; init; }

    /// <summary>
    /// List of changes made.
    /// </summary>
    public IReadOnlyList<SignalRChange> Changes { get; init; } = [];

    /// <summary>
    /// Confidence score for this file (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Types of SignalR-related files.
/// </summary>
public enum SignalRFileType
{
    /// <summary>
    /// A SignalR Hub class.
    /// </summary>
    Hub,

    /// <summary>
    /// A service/controller using GlobalHost.
    /// </summary>
    ServiceWithGlobalHost,

    /// <summary>
    /// Startup/OWIN configuration.
    /// </summary>
    StartupConfiguration,

    /// <summary>
    /// JavaScript/TypeScript client code.
    /// </summary>
    ClientScript
}

/// <summary>
/// A specific change made during SignalR transformation.
/// </summary>
public record SignalRChange
{
    /// <summary>
    /// Description of the change.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The line number affected.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The original code.
    /// </summary>
    public string? OriginalCode { get; init; }

    /// <summary>
    /// The new code.
    /// </summary>
    public string? NewCode { get; init; }

    /// <summary>
    /// Change type.
    /// </summary>
    public required SignalRChangeType ChangeType { get; init; }
}

/// <summary>
/// Types of SignalR transformation changes.
/// </summary>
public enum SignalRChangeType
{
    /// <summary>
    /// Lifecycle method transformation.
    /// </summary>
    LifecycleMethod,

    /// <summary>
    /// Client invocation transformation.
    /// </summary>
    ClientInvocation,

    /// <summary>
    /// Groups operation transformation.
    /// </summary>
    GroupsOperation,

    /// <summary>
    /// GlobalHost to IHubContext transformation.
    /// </summary>
    GlobalHostToHubContext,

    /// <summary>
    /// Using statement update.
    /// </summary>
    UsingStatement,

    /// <summary>
    /// Method made async.
    /// </summary>
    AsyncTransformation,

    /// <summary>
    /// Startup configuration change.
    /// </summary>
    StartupConfiguration,

    /// <summary>
    /// Method or feature removed with TODO.
    /// </summary>
    RemovedWithTodo
}

/// <summary>
/// A warning generated during SignalR transformation.
/// </summary>
public record SignalRWarning
{
    /// <summary>
    /// The warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The file path affected.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// The line number affected.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Severity of the warning.
    /// </summary>
    public required SignalRWarningSeverity Severity { get; init; }
}

/// <summary>
/// Severity levels for SignalR warnings.
/// </summary>
public enum SignalRWarningSeverity
{
    /// <summary>
    /// Informational - no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - review recommended.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - manual intervention required.
    /// </summary>
    Error
}
