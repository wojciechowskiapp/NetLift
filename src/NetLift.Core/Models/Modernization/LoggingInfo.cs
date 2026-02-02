namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents detected logging configuration and usage in a .NET Framework project.
/// </summary>
public sealed record LoggingInfo
{
    /// <summary>
    /// Gets the logging framework detected in the project.
    /// </summary>
    public required LoggingFramework Framework { get; init; }

    /// <summary>
    /// Gets the path to the logging configuration file, if applicable.
    /// </summary>
    public string? ConfigurationFilePath { get; init; }

    /// <summary>
    /// Gets the raw XML configuration content for the logging framework.
    /// </summary>
    public string? ConfigurationXml { get; init; }

    /// <summary>
    /// Gets the custom logger type name, if a custom logger wrapper is detected.
    /// </summary>
    public string? CustomLoggerType { get; init; }

    /// <summary>
    /// Gets the list of detected logger usages throughout the project.
    /// </summary>
    public IReadOnlyList<LoggerUsage> LoggerUsages { get; init; } = [];

    /// <summary>
    /// Gets the confidence score for this logging detection (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets whether this logging configuration requires migration to modern patterns.
    /// </summary>
    public bool RequiresMigration { get; init; }

    /// <summary>
    /// Gets warnings about the logging configuration or migration.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the NuGet package references for the logging framework.
    /// </summary>
    public IReadOnlyList<string> PackageReferences { get; init; } = [];
}

/// <summary>
/// Represents the type of logging framework detected.
/// </summary>
public enum LoggingFramework
{
    /// <summary>
    /// No logging framework detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Apache log4net logging framework.
    /// </summary>
    Log4Net,

    /// <summary>
    /// NLog logging framework.
    /// </summary>
    NLog,

    /// <summary>
    /// Microsoft Enterprise Library logging application block.
    /// </summary>
    EnterpriseLibrary,

    /// <summary>
    /// Direct console or debug output (Console.WriteLine, Debug.WriteLine).
    /// </summary>
    Console,

    /// <summary>
    /// Custom logger wrapper or implementation.
    /// </summary>
    Custom,

    /// <summary>
    /// Multiple logging frameworks detected in the same project.
    /// </summary>
    Mixed
}

/// <summary>
/// Represents a detected usage of a logger in source code.
/// </summary>
public sealed record LoggerUsage
{
    /// <summary>
    /// Gets the file path where the logger usage was found.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the line number where the logger usage was found.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Gets the method or member where the logger usage was found.
    /// </summary>
    public required string MemberName { get; init; }

    /// <summary>
    /// Gets the type of logger usage (field declaration, method call, etc.).
    /// </summary>
    public required LoggerUsageType UsageType { get; init; }

    /// <summary>
    /// Gets the logger type or pattern detected.
    /// </summary>
    public required string LoggerType { get; init; }

    /// <summary>
    /// Gets the source code snippet of the logger usage.
    /// </summary>
    public string? SourceSnippet { get; init; }
}

/// <summary>
/// Represents the type of logger usage detected in source code.
/// </summary>
public enum LoggerUsageType
{
    /// <summary>
    /// Logger field declaration.
    /// </summary>
    FieldDeclaration,

    /// <summary>
    /// Logger property declaration.
    /// </summary>
    PropertyDeclaration,

    /// <summary>
    /// Logger initialization or factory call.
    /// </summary>
    Initialization,

    /// <summary>
    /// Logger method call (Info, Debug, Error, etc.).
    /// </summary>
    MethodCall,

    /// <summary>
    /// Console.WriteLine or similar direct output.
    /// </summary>
    ConsoleOutput,

    /// <summary>
    /// Debug.WriteLine or Trace.WriteLine.
    /// </summary>
    DebugOutput
}
