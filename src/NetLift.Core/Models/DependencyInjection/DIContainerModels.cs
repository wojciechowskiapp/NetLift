namespace NetLift.Core.Models.DependencyInjection;

/// <summary>
/// Types of DI frameworks that can be detected.
/// </summary>
public enum DIFrameworkType
{
    Unknown,
    Autofac,
    Unity,
    Ninject,
    StructureMap,
    /// <summary>
    /// Multiple frameworks detected in the same project.
    /// </summary>
    Mixed
}

/// <summary>
/// Complexity level of the DI configuration.
/// </summary>
public enum DIComplexityLevel
{
    /// <summary>
    /// Basic registrations only.
    /// </summary>
    Simple,

    /// <summary>
    /// Named services, factories.
    /// </summary>
    Moderate,

    /// <summary>
    /// Interceptors, property injection, assembly scanning.
    /// </summary>
    Complex,

    /// <summary>
    /// Multi-tenant, child containers.
    /// </summary>
    VeryComplex
}

/// <summary>
/// Method used for service registration.
/// </summary>
public enum RegistrationMethod
{
    /// <summary>
    /// RegisterType&lt;T&gt;() / AddScoped&lt;T&gt;()
    /// </summary>
    Type,

    /// <summary>
    /// RegisterInstance() / AddSingleton(instance)
    /// </summary>
    Instance,

    /// <summary>
    /// Register(c => ...) / AddScoped(sp => ...)
    /// </summary>
    Factory,

    /// <summary>
    /// RegisterGeneric() / AddScoped(typeof(...))
    /// </summary>
    Generic,

    /// <summary>
    /// RegisterAssemblyTypes() / Scrutor
    /// </summary>
    AssemblyScanning
}

/// <summary>
/// Interceptor patterns used in legacy DI frameworks.
/// </summary>
public enum InterceptorPattern
{
    AutofacInterceptor,
    UnityInterceptionBehavior,
    NinjectInterceptor,
    StructureMapDecorator
}

/// <summary>
/// Severity level for transformation notes.
/// </summary>
public enum NoteSeverity
{
    Info,
    Warning,
    Todo,
    ManualAction
}

/// <summary>
/// Service lifetime scopes (aligned with Microsoft.Extensions.DependencyInjection).
/// </summary>
public enum ServiceLifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2
}

/// <summary>
/// Root model representing detected DI configuration.
/// </summary>
public sealed record DIContainerInfo
{
    /// <summary>
    /// The DI framework type detected.
    /// </summary>
    public DIFrameworkType Framework { get; init; }

    /// <summary>
    /// Configuration files where DI setup is defined.
    /// </summary>
    public IReadOnlyList<string> ConfigurationFiles { get; init; } = [];

    /// <summary>
    /// All service registrations found.
    /// </summary>
    public IReadOnlyList<ServiceRegistrationInfo> Registrations { get; init; } = [];

    /// <summary>
    /// Autofac/Ninject modules detected.
    /// </summary>
    public IReadOnlyList<ModuleInfo> Modules { get; init; } = [];

    /// <summary>
    /// Complexity level of the DI configuration.
    /// </summary>
    public DIComplexityLevel Complexity { get; init; }

    /// <summary>
    /// Overall confidence score for migration (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }

    /// <summary>
    /// Patterns detected (e.g., "Property Injection", "Assembly Scanning").
    /// </summary>
    public IReadOnlyList<string> DetectedPatterns { get; init; } = [];

    /// <summary>
    /// Whether property injection is used.
    /// </summary>
    public bool HasPropertyInjection { get; init; }

    /// <summary>
    /// Whether interceptors/decorators are used.
    /// </summary>
    public bool HasInterceptors { get; init; }

    /// <summary>
    /// Whether assembly scanning is used.
    /// </summary>
    public bool HasAssemblyScanning { get; init; }

    /// <summary>
    /// Whether Scrutor package is required for migration.
    /// </summary>
    public bool RequiresScrutor { get; init; }
}

/// <summary>
/// Individual service registration details.
/// </summary>
public sealed record ServiceRegistrationInfo
{
    /// <summary>
    /// The service type (interface or class).
    /// </summary>
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>
    /// The implementation type.
    /// </summary>
    public string ImplementationType { get; init; } = string.Empty;

    /// <summary>
    /// The service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// The registration method used.
    /// </summary>
    public RegistrationMethod Method { get; init; }

    /// <summary>
    /// Named/keyed service key (if applicable).
    /// </summary>
    public string? NamedKey { get; init; }

    /// <summary>
    /// Factory registration details (if Method is Factory).
    /// </summary>
    public FactoryRegistrationInfo? Factory { get; init; }

    /// <summary>
    /// Property injection details (if applicable).
    /// </summary>
    public PropertyInjectionInfo? PropertyInjection { get; init; }

    /// <summary>
    /// Interceptor details (if applicable).
    /// </summary>
    public InterceptorInfo? Interceptor { get; init; }

    /// <summary>
    /// Original source code of the registration.
    /// </summary>
    public string SourceCode { get; init; } = string.Empty;

    /// <summary>
    /// Source file path.
    /// </summary>
    public string SourceFile { get; init; } = string.Empty;

    /// <summary>
    /// Source line number.
    /// </summary>
    public int SourceLine { get; init; }

    /// <summary>
    /// Confidence score for this registration (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }

    /// <summary>
    /// Notes or warnings about this registration.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// Maps legacy lifetime scopes to Microsoft.Extensions.DependencyInjection lifetimes.
/// </summary>
public sealed record LifetimeMapping
{
    /// <summary>
    /// The source lifetime from the legacy framework.
    /// </summary>
    public string SourceLifetime { get; init; } = string.Empty;

    /// <summary>
    /// The framework this mapping applies to.
    /// </summary>
    public DIFrameworkType Framework { get; init; }

    /// <summary>
    /// The target Microsoft.Extensions.DependencyInjection lifetime.
    /// </summary>
    public ServiceLifetime TargetLifetime { get; init; }

    /// <summary>
    /// Confidence score for this mapping (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }

    /// <summary>
    /// Additional notes about the mapping.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Autofac/Ninject module representation.
/// </summary>
public sealed record ModuleInfo
{
    /// <summary>
    /// The module name.
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// The fully qualified type name of the module.
    /// </summary>
    public string ModuleTypeName { get; init; } = string.Empty;

    /// <summary>
    /// File path where the module is defined.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Registrations defined in this module.
    /// </summary>
    public IReadOnlyList<ServiceRegistrationInfo> Registrations { get; init; } = [];

    /// <summary>
    /// Module dependencies (other modules that must load first).
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>
    /// Order in which the module is registered.
    /// </summary>
    public int RegistrationOrder { get; init; }
}

/// <summary>
/// Factory delegate patterns used in service registration.
/// </summary>
public sealed record FactoryRegistrationInfo
{
    /// <summary>
    /// The factory expression (lambda or delegate).
    /// </summary>
    public string FactoryExpression { get; init; } = string.Empty;

    /// <summary>
    /// Dependencies resolved within the factory.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>
    /// Whether the factory is simple (can be auto-migrated).
    /// </summary>
    public bool IsSimple { get; init; }

    /// <summary>
    /// Confidence score for this factory transformation (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }

    /// <summary>
    /// The transformed factory expression for ASP.NET Core.
    /// </summary>
    public string? TransformedExpression { get; init; }
}

/// <summary>
/// Property injection details for a service.
/// </summary>
public sealed record PropertyInjectionInfo
{
    /// <summary>
    /// The target type that uses property injection.
    /// </summary>
    public string TargetType { get; init; } = string.Empty;

    /// <summary>
    /// Properties that are injected.
    /// </summary>
    public IReadOnlyList<PropertyDependency> Properties { get; init; } = [];

    /// <summary>
    /// Whether properties are auto-wired (all properties of certain types).
    /// </summary>
    public bool IsAutoWired { get; init; }

    /// <summary>
    /// Whether the property injection can be converted to constructor injection.
    /// </summary>
    public bool CanConvertToConstructor { get; init; }

    /// <summary>
    /// Suggested constructor signature if conversion is possible.
    /// </summary>
    public string? SuggestedConstructor { get; init; }

    /// <summary>
    /// Confidence score for this transformation (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }
}

/// <summary>
/// A property dependency for property injection.
/// </summary>
public sealed record PropertyDependency
{
    /// <summary>
    /// The property name.
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// The property type.
    /// </summary>
    public string PropertyType { get; init; } = string.Empty;

    /// <summary>
    /// Whether the property is required (non-nullable).
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Whether the property has a setter.
    /// </summary>
    public bool HasSetter { get; init; }
}

/// <summary>
/// Interceptor/decorator patterns detected.
/// </summary>
public sealed record InterceptorInfo
{
    /// <summary>
    /// The interceptor type name.
    /// </summary>
    public string InterceptorType { get; init; } = string.Empty;

    /// <summary>
    /// The interceptor pattern used.
    /// </summary>
    public InterceptorPattern Pattern { get; init; }

    /// <summary>
    /// Whether the interceptor can be auto-migrated.
    /// </summary>
    public bool CanAutoMigrate { get; init; }

    /// <summary>
    /// Suggested migration approach (e.g., "Use Scrutor Decorator pattern").
    /// </summary>
    public string? MigrationApproach { get; init; }

    /// <summary>
    /// Confidence score for this transformation (0-100).
    /// </summary>
    public int ConfidenceScore { get; init; }
}

/// <summary>
/// Result of DI transformation to ASP.NET Core.
/// </summary>
public sealed record DITransformResult
{
    /// <summary>
    /// The generated Program.cs or Startup.cs code.
    /// </summary>
    public string GeneratedCode { get; init; } = string.Empty;

    /// <summary>
    /// Files to create (e.g., ServiceCollectionExtensions.cs).
    /// </summary>
    public IReadOnlyList<string> FilesToCreate { get; init; } = [];

    /// <summary>
    /// Files to modify (e.g., existing classes that need constructor changes).
    /// </summary>
    public IReadOnlyList<string> FilesToModify { get; init; } = [];

    /// <summary>
    /// NuGet packages to add (e.g., Scrutor).
    /// </summary>
    public IReadOnlyList<string> PackagesToAdd { get; init; } = [];

    /// <summary>
    /// NuGet packages to remove (e.g., Autofac, Unity).
    /// </summary>
    public IReadOnlyList<string> PackagesToRemove { get; init; } = [];

    /// <summary>
    /// Transformation notes, warnings, and manual actions.
    /// </summary>
    public IReadOnlyList<TransformationNote> Notes { get; init; } = [];

    /// <summary>
    /// Overall confidence score for the transformation (0-100).
    /// </summary>
    public int OverallConfidence { get; init; }
}

/// <summary>
/// A note or warning about a transformation step.
/// </summary>
public sealed record TransformationNote
{
    /// <summary>
    /// The severity level.
    /// </summary>
    public NoteSeverity Severity { get; init; }

    /// <summary>
    /// The message text.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Location in code (file:line or type name).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Recommended action to take.
    /// </summary>
    public string? Recommendation { get; init; }
}

/// <summary>
/// Options for DI container transformation.
/// </summary>
public sealed record DITransformOptions
{
    /// <summary>
    /// Whether to perform a dry run (no file changes).
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Whether to group registrations by namespace.
    /// </summary>
    public bool GroupByNamespace { get; init; }

    /// <summary>
    /// Whether to generate comments in the output.
    /// </summary>
    public bool GenerateComments { get; init; } = true;

    /// <summary>
    /// Target .NET version (8 or higher for keyed services).
    /// </summary>
    public int TargetDotNetVersion { get; init; } = 8;
}
