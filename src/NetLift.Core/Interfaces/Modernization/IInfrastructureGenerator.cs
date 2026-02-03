namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates common CQRS infrastructure code for modernized projects.
/// Creates pipeline behaviors, Result pattern, and supporting interfaces.
/// </summary>
public interface IInfrastructureGenerator
{
    /// <summary>
    /// Generates all common infrastructure files for CQRS pattern.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code</param>
    /// <param name="options">Infrastructure generation options</param>
    /// <returns>Dictionary of file paths to generated content</returns>
    Dictionary<string, string> GenerateAll(string rootNamespace, InfrastructureOptions options);

    /// <summary>
    /// Generates the ValidationBehavior pipeline behavior.
    /// Wires FluentValidation to MediatR pipeline for automatic request validation.
    /// </summary>
    string GenerateValidationBehavior(string rootNamespace);

    /// <summary>
    /// Generates the LoggingBehavior pipeline behavior.
    /// Adds structured logging with correlation IDs and performance tracking.
    /// </summary>
    string GenerateLoggingBehavior(string rootNamespace);

    /// <summary>
    /// Generates the TransactionBehavior pipeline behavior.
    /// Implements Unit of Work pattern with automatic transaction management.
    /// </summary>
    string GenerateTransactionBehavior(string rootNamespace);

    /// <summary>
    /// Generates the PerformanceBehavior pipeline behavior.
    /// Detects slow requests and logs warnings for optimization.
    /// </summary>
    string GeneratePerformanceBehavior(string rootNamespace, int slowRequestThresholdMs = 500);

    /// <summary>
    /// Generates the UnhandledExceptionBehavior pipeline behavior.
    /// Provides global error handling and logging for all requests.
    /// </summary>
    string GenerateUnhandledExceptionBehavior(string rootNamespace);

    /// <summary>
    /// Generates the enhanced Result pattern classes.
    /// Includes Result, Result&lt;T&gt;, Error, and ValidationError types.
    /// </summary>
    string GenerateResultPattern(string rootNamespace);

    /// <summary>
    /// Generates the PagedList model for pagination support.
    /// </summary>
    string GeneratePagedList(string rootNamespace);

    /// <summary>
    /// Generates QueryableExtensions with ToPagedListAsync and other helpers.
    /// </summary>
    string GenerateQueryableExtensions(string rootNamespace);

    /// <summary>
    /// Generates MediatR replacement interfaces (IRequest, IRequestHandler, IPipelineBehavior).
    /// </summary>
    string GenerateMediatRInterfaces(string rootNamespace);

    /// <summary>
    /// Generates ICurrentUserService interface for audit trails.
    /// </summary>
    string GenerateCurrentUserService(string rootNamespace);

    /// <summary>
    /// Generates IDateTime interface for testable date/time.
    /// </summary>
    string GenerateDateTimeService(string rootNamespace);

    /// <summary>
    /// Generates DependencyInjection.cs for registering all CQRS services.
    /// </summary>
    string GenerateDependencyInjection(string rootNamespace, InfrastructureOptions options);
}

/// <summary>
/// Options for infrastructure code generation.
/// </summary>
public record InfrastructureOptions
{
    /// <summary>
    /// Whether to include ValidationBehavior in the pipeline.
    /// </summary>
    public bool IncludeValidationBehavior { get; init; } = true;

    /// <summary>
    /// Whether to include LoggingBehavior in the pipeline.
    /// </summary>
    public bool IncludeLoggingBehavior { get; init; } = true;

    /// <summary>
    /// Whether to include TransactionBehavior in the pipeline.
    /// </summary>
    public bool IncludeTransactionBehavior { get; init; } = true;

    /// <summary>
    /// Whether to include PerformanceBehavior in the pipeline.
    /// </summary>
    public bool IncludePerformanceBehavior { get; init; } = true;

    /// <summary>
    /// Whether to include UnhandledExceptionBehavior in the pipeline.
    /// </summary>
    public bool IncludeUnhandledExceptionBehavior { get; init; } = true;

    /// <summary>
    /// Threshold in milliseconds for slow request warnings.
    /// </summary>
    public int SlowRequestThresholdMs { get; init; } = 500;

    /// <summary>
    /// Whether to use real MediatR package or generate lightweight replacement.
    /// </summary>
    public bool UseMediatR { get; init; } = true;

    /// <summary>
    /// Whether to generate AutoMapper integration.
    /// Note: AutoMapper 13+ has a commercial license for companies with revenue > $1M.
    /// Default is false to avoid licensing concerns. Use Mapster or manual mapping instead.
    /// </summary>
    public bool IncludeAutoMapper { get; init; } = false;

    /// <summary>
    /// Whether to generate caching infrastructure.
    /// </summary>
    public bool IncludeCaching { get; init; } = false;
}
