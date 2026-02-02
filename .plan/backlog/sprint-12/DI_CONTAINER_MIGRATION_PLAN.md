# DI Container Migration - Implementation Plan

> **Feature:** Automated migration from legacy DI containers (Autofac, Unity, Ninject, StructureMap) to Microsoft.Extensions.DependencyInjection

---

## Executive Summary

This plan details the implementation of DI container migration capability in NetLift. The feature will automatically detect legacy DI frameworks, analyze service registrations, and generate equivalent Microsoft.Extensions.DependencyInjection code for ASP.NET Core applications.

**Target Frameworks:**
- Autofac → MEDI
- Unity → MEDI
- Ninject → MEDI
- StructureMap → MEDI

**Key Capabilities:**
1. Automatic detection of DI framework via packages and code patterns
2. Parsing of service registrations with Roslyn
3. Lifetime scope mapping (Singleton/Scoped/Transient)
4. Factory delegate transformation
5. Property injection detection and conversion to constructor injection
6. Named/keyed service handling (.NET 8+ keyed services)
7. Interceptor and decorator detection (with manual migration guidance)
8. Assembly scanning with Scrutor library integration
9. Confidence scoring for all transformations

---

## Architecture Overview

### Models (NetLift.Core/Models/DependencyInjection/)

#### 1. DIContainerInfo
Root model representing detected DI configuration.

```csharp
public class DIContainerInfo
{
    public DIFrameworkType Framework { get; set; }
    public List<string> ConfigurationFiles { get; set; }
    public List<ServiceRegistrationInfo> Registrations { get; set; }
    public List<ModuleInfo> Modules { get; set; } // Autofac modules
    public DIComplexityLevel Complexity { get; set; }
    public int ConfidenceScore { get; set; }
    public List<string> DetectedPatterns { get; set; }
    public bool HasPropertyInjection { get; set; }
    public bool HasInterceptors { get; set; }
    public bool HasAssemblyScanning { get; set; }
    public bool RequiresScrutor { get; set; }
}

public enum DIFrameworkType
{
    Unknown,
    Autofac,
    Unity,
    Ninject,
    StructureMap,
    Mixed // Multiple frameworks detected
}

public enum DIComplexityLevel
{
    Simple,      // Basic registrations only
    Moderate,    // Named services, factories
    Complex,     // Interceptors, property injection, assembly scanning
    VeryComplex  // Multi-tenant, child containers
}
```

#### 2. ServiceRegistrationInfo
Individual service registration details.

```csharp
public class ServiceRegistrationInfo
{
    public string ServiceType { get; set; }
    public string ImplementationType { get; set; }
    public ServiceLifetime Lifetime { get; set; }
    public RegistrationMethod Method { get; set; }
    public string NamedKey { get; set; }
    public FactoryRegistrationInfo Factory { get; set; }
    public PropertyInjectionInfo PropertyInjection { get; set; }
    public InterceptorInfo Interceptor { get; set; }
    public string SourceCode { get; set; }
    public string SourceFile { get; set; }
    public int SourceLine { get; set; }
    public int ConfidenceScore { get; set; }
    public List<string> Notes { get; set; }
}

public enum RegistrationMethod
{
    Type,           // RegisterType<T>() / AddScoped<T>()
    Instance,       // RegisterInstance() / AddSingleton(instance)
    Factory,        // Register(c => ...) / AddScoped(sp => ...)
    Generic,        // RegisterGeneric() / AddScoped(typeof(...))
    AssemblyScanning // RegisterAssemblyTypes() / Scrutor
}
```

#### 3. LifetimeMapping
Maps legacy lifetime scopes to MEDI ServiceLifetime.

```csharp
public class LifetimeMapping
{
    public string SourceLifetime { get; set; }
    public DIFrameworkType Framework { get; set; }
    public ServiceLifetime TargetLifetime { get; set; }
    public int ConfidenceScore { get; set; }
    public string Notes { get; set; }
}
```

#### 4. ModuleInfo
Autofac module representation.

```csharp
public class ModuleInfo
{
    public string ModuleName { get; set; }
    public string ModuleTypeName { get; set; }
    public string FilePath { get; set; }
    public List<ServiceRegistrationInfo> Registrations { get; set; }
    public List<string> Dependencies { get; set; } // Other modules this depends on
    public int RegistrationOrder { get; set; }
}
```

#### 5. FactoryRegistrationInfo
Factory delegate patterns.

```csharp
public class FactoryRegistrationInfo
{
    public string FactoryExpression { get; set; }
    public List<string> Dependencies { get; set; } // Types resolved in factory
    public bool IsSimple { get; set; } // Simple: new T(dep), Complex: conditional logic
    public int ConfidenceScore { get; set; }
    public string TransformedExpression { get; set; } // MEDI equivalent
}
```

#### 6. PropertyInjectionInfo
Property injection details.

```csharp
public class PropertyInjectionInfo
{
    public string TargetType { get; set; }
    public List<PropertyDependency> Properties { get; set; }
    public bool IsAutoWired { get; set; }
    public bool CanConvertToConstructor { get; set; }
    public string SuggestedConstructor { get; set; }
    public int ConfidenceScore { get; set; }
}

public class PropertyDependency
{
    public string PropertyName { get; set; }
    public string PropertyType { get; set; }
    public bool IsRequired { get; set; }
    public bool HasSetter { get; set; }
}
```

#### 7. InterceptorInfo
Interceptor/decorator patterns.

```csharp
public class InterceptorInfo
{
    public string InterceptorType { get; set; }
    public InterceptorPattern Pattern { get; set; }
    public bool CanAutoMigrate { get; set; }
    public string MigrationApproach { get; set; } // "Scrutor.Decorate", "Castle.DynamicProxy", "Manual"
    public int ConfidenceScore { get; set; }
}

public enum InterceptorPattern
{
    AutofacInterceptor,
    UnityInterceptionBehavior,
    NinjectInterceptor,
    StructureMapDecorator
}
```

#### 8. DITransformResult
Result of DI transformation.

```csharp
public class DITransformResult
{
    public string GeneratedCode { get; set; }
    public List<string> FilesToCreate { get; set; }
    public List<string> FilesToModify { get; set; }
    public List<string> PackagesToAdd { get; set; }
    public List<string> PackagesToRemove { get; set; }
    public List<TransformationNote> Notes { get; set; }
    public int OverallConfidence { get; set; }
}

public class TransformationNote
{
    public NoteSeverity Severity { get; set; }
    public string Message { get; set; }
    public string Location { get; set; }
    public string Recommendation { get; set; }
}

public enum NoteSeverity
{
    Info,
    Warning,
    Todo,
    ManualAction
}
```

---

### Interfaces (NetLift.Core/Interfaces/DependencyInjection/)

#### 1. IDIContainerDetector
Detects which DI framework is being used.

```csharp
public interface IDIContainerDetector
{
    Task<DIContainerInfo> DetectAsync(SolutionInfo solution);
    Task<List<DIFrameworkType>> GetUsedFrameworksAsync(ProjectInfo project);
    Task<List<string>> FindConfigurationFilesAsync(ProjectInfo project, DIFrameworkType framework);
}
```

#### 2. IDIContainerAnalyzer
Base analyzer interface.

```csharp
public interface IDIContainerAnalyzer
{
    DIFrameworkType SupportedFramework { get; }
    Task<List<ServiceRegistrationInfo>> AnalyzeRegistrationsAsync(string filePath);
    Task<LifetimeMapping> MapLifetimeAsync(string sourceLifetime);
    int CalculateConfidence(ServiceRegistrationInfo registration);
}
```

#### 3. IAutofacAnalyzer : IDIContainerAnalyzer
Autofac-specific analyzer.

```csharp
public interface IAutofacAnalyzer : IDIContainerAnalyzer
{
    Task<List<ModuleInfo>> ParseModulesAsync(ProjectInfo project);
    Task<List<ServiceRegistrationInfo>> ParseContainerBuilderAsync(SyntaxNode node);
    Task<List<ServiceRegistrationInfo>> ParseModuleAsync(string filePath);
}
```

#### 4. IUnityAnalyzer : IDIContainerAnalyzer
Unity-specific analyzer.

```csharp
public interface IUnityAnalyzer : IDIContainerAnalyzer
{
    Task<List<ServiceRegistrationInfo>> ParseUnityConfigAsync(string filePath);
    Task<PropertyInjectionInfo> ParseInjectionPropertyAsync(SyntaxNode node);
}
```

#### 5. INinjectAnalyzer : IDIContainerAnalyzer
Ninject-specific analyzer.

```csharp
public interface INinjectAnalyzer : IDIContainerAnalyzer
{
    Task<List<ServiceRegistrationInfo>> ParseBindingsAsync(SyntaxNode node);
    Task<List<ModuleInfo>> ParseNinjectModulesAsync(ProjectInfo project);
}
```

#### 6. IStructureMapAnalyzer : IDIContainerAnalyzer
StructureMap-specific analyzer.

```csharp
public interface IStructureMapAnalyzer : IDIContainerAnalyzer
{
    Task<List<ServiceRegistrationInfo>> ParseRegistryAsync(string filePath);
    Task<List<ServiceRegistrationInfo>> ParseScanAsync(SyntaxNode node);
}
```

#### 7. IDIContainerTransformer
Transforms DI registrations to MEDI.

```csharp
public interface IDIContainerTransformer
{
    Task<DITransformResult> TransformAsync(DIContainerInfo containerInfo, TransformOptions options);
    string GenerateServiceCollectionCode(List<ServiceRegistrationInfo> registrations);
    string GenerateExtensionMethod(ModuleInfo module);
    string GenerateProgramCsIntegration(DIContainerInfo containerInfo);
}
```

#### 8. ILifetimeMapper
Maps framework-specific lifetimes to ServiceLifetime.

```csharp
public interface ILifetimeMapper
{
    LifetimeMapping MapLifetime(string sourceLifetime, DIFrameworkType framework);
    ServiceLifetime GetServiceLifetime(LifetimeMapping mapping);
    Task LoadMappingsFromYamlAsync();
}
```

#### 9. IPropertyInjectionAnalyzer
Analyzes and transforms property injection.

```csharp
public interface IPropertyInjectionAnalyzer
{
    Task<PropertyInjectionInfo> AnalyzeAsync(TypeDeclarationSyntax typeDeclaration);
    bool CanConvertToConstructorInjection(PropertyInjectionInfo info);
    string GenerateConstructor(PropertyInjectionInfo info);
    TransformResult TransformToConstructorInjection(TypeDeclarationSyntax typeDeclaration);
}
```

#### 10. IInterceptorTransformer
Handles interceptor migration.

```csharp
public interface IInterceptorTransformer
{
    Task<InterceptorInfo> AnalyzeInterceptorAsync(ServiceRegistrationInfo registration);
    TransformationNote GenerateMigrationGuidance(InterceptorInfo interceptor);
    bool CanUseScrutor(InterceptorInfo interceptor);
    string GenerateScrutorDecorator(InterceptorInfo interceptor);
}
```

---

## Detection Patterns

### Package Detection

**Autofac:**
- Autofac
- Autofac.Mvc5
- Autofac.WebApi2
- Autofac.Integration.Mvc
- Autofac.Integration.WebApi

**Unity:**
- Unity
- Unity.Container
- Unity.Mvc5
- Unity.AspNet.WebApi
- Unity.WebAPI

**Ninject:**
- Ninject
- Ninject.Web.Mvc
- Ninject.Web.WebApi
- Ninject.MVC5

**StructureMap:**
- StructureMap
- StructureMap.MVC5
- StructureMap.WebApi2

### Code Pattern Detection (Roslyn)

**Autofac:**
```csharp
// Using statements
using Autofac;
using Autofac.Core;

// Type references
ContainerBuilder builder = new ContainerBuilder();
IContainer container = builder.Build();

// Inheritance patterns
public class MyModule : Autofac.Module { }

// Method invocations
builder.RegisterType<ServiceImpl>().As<IService>();
builder.RegisterModule<MyModule>();
```

**Unity:**
```csharp
// Using statements
using Unity;
using Microsoft.Practices.Unity;

// Type references
IUnityContainer container = new UnityContainer();

// Method invocations
container.RegisterType<IService, ServiceImpl>();
container.Resolve<IService>();
```

**Ninject:**
```csharp
// Using statements
using Ninject;

// Type references
IKernel kernel = new StandardKernel();

// Inheritance patterns
public class MyModule : NinjectModule { }

// Method invocations
kernel.Bind<IService>().To<ServiceImpl>();
```

**StructureMap:**
```csharp
// Using statements
using StructureMap;

// Type references
IContainer container = new Container();

// Inheritance patterns
public class MyRegistry : Registry { }

// Method invocations
For<IService>().Use<ServiceImpl>();
```

### File Pattern Detection

- **Autofac:** `*Module.cs` files, classes inheriting from `Autofac.Module`
- **Unity:** `UnityConfig.cs`, `UnityWebApiActivator.cs`
- **Ninject:** `NinjectWebCommon.cs`, `App_Start/NinjectConfig.cs`
- **StructureMap:** `StructuremapMvc.cs`, `*Registry.cs` files

---

## Lifetime Mapping

### YAML Configuration File
`src/NetLift.Transforms/Configuration/di-lifetime-mappings.yml`

```yaml
autofac:
  - source: "SingleInstance"
    target: "Singleton"
    confidence: 100
  - source: "InstancePerLifetimeScope"
    target: "Scoped"
    confidence: 100
  - source: "InstancePerDependency"
    target: "Transient"
    confidence: 100
  - source: "InstancePerRequest"
    target: "Scoped"
    confidence: 95
    notes: "HTTP request scope maps to scoped lifetime"

unity:
  - source: "ContainerControlledLifetimeManager"
    target: "Singleton"
    confidence: 100
  - source: "HierarchicalLifetimeManager"
    target: "Scoped"
    confidence: 100
  - source: "TransientLifetimeManager"
    target: "Transient"
    confidence: 100
  - source: "PerRequestLifetimeManager"
    target: "Scoped"
    confidence: 95
    notes: "HTTP request scope maps to scoped lifetime"

ninject:
  - source: "InSingletonScope"
    target: "Singleton"
    confidence: 100
  - source: "InRequestScope"
    target: "Scoped"
    confidence: 100
  - source: "InTransientScope"
    target: "Transient"
    confidence: 100
  - source: "InThreadScope"
    target: "Scoped"
    confidence: 80
    notes: "Thread scope has no direct equivalent, using scoped"

structuremap:
  - source: "Singleton"
    target: "Singleton"
    confidence: 100
  - source: "ContainerScoped"
    target: "Scoped"
    confidence: 100
  - source: "AlwaysUnique"
    target: "Transient"
    confidence: 100
  - source: "Unique"
    target: "Transient"
    confidence: 100
  - source: "HttpContextScoped"
    target: "Scoped"
    confidence: 95
```

---

## Transformation Examples

### 1. Simple Registration (Confidence: 95-100%)

**Autofac:**
```csharp
builder.RegisterType<ServiceImpl>().As<IService>().InstancePerLifetimeScope();
```

**MEDI:**
```csharp
services.AddScoped<IService, ServiceImpl>();
```

### 2. Generic Registration (Confidence: 95-100%)

**Autofac:**
```csharp
builder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>));
```

**MEDI:**
```csharp
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

### 3. Instance Registration (Confidence: 95-100%)

**Unity:**
```csharp
var config = new AppConfig();
container.RegisterInstance<IConfig>(config);
```

**MEDI:**
```csharp
var config = new AppConfig();
services.AddSingleton<IConfig>(config);
```

### 4. Factory Registration (Confidence: 85-95%)

**Autofac:**
```csharp
builder.Register(c => new Service(c.Resolve<IDep>(), "config"))
       .As<IService>()
       .InstancePerLifetimeScope();
```

**MEDI:**
```csharp
services.AddScoped<IService>(sp =>
    new Service(sp.GetRequiredService<IDep>(), "config"));
```

### 5. Named Registration - .NET 8+ (Confidence: 90%)

**Autofac:**
```csharp
builder.RegisterType<CacheService>().Named<ICache>("redis");
builder.RegisterType<MemoryCache>().Named<ICache>("memory");
```

**MEDI (.NET 8+):**
```csharp
services.AddKeyedScoped<ICache, CacheService>("redis");
services.AddKeyedScoped<ICache, MemoryCache>("memory");
```

### 6. Named Registration - Pre-.NET 8 (Confidence: 70%)

**MEDI (Pre-.NET 8 - Factory Pattern):**
```csharp
// Generate factory
services.AddScoped<Func<string, ICache>>(sp => key =>
{
    return key switch
    {
        "redis" => sp.GetRequiredService<CacheService>(),
        "memory" => sp.GetRequiredService<MemoryCache>(),
        _ => throw new ArgumentException($"Unknown cache key: {key}")
    };
});

// TODO: Update consumers to use Func<string, ICache> instead of Named<ICache>
```

### 7. Property Injection → Constructor Injection (Confidence: 70-80%)

**Autofac (Property Injection):**
```csharp
builder.RegisterType<MyService>().PropertiesAutowired();

public class MyService
{
    public ILogger Logger { get; set; }
    public ICache Cache { get; set; }
}
```

**MEDI (Constructor Injection):**
```csharp
services.AddScoped<MyService>();

public class MyService
{
    private readonly ILogger _logger;
    private readonly ICache _cache;

    // Generated constructor
    public MyService(ILogger logger, ICache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
}
```

### 8. Assembly Scanning (Confidence: 75% with Scrutor)

**Autofac:**
```csharp
builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
       .Where(t => t.Name.EndsWith("Service"))
       .AsImplementedInterfaces()
       .InstancePerLifetimeScope();
```

**MEDI with Scrutor:**
```csharp
services.Scan(scan => scan
    .FromExecutingAssembly()
    .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Service")))
    .AsImplementedInterfaces()
    .WithScopedLifetime());

// Add Scrutor package reference
```

### 9. Decorator Pattern (Confidence: 70% with Scrutor)

**StructureMap:**
```csharp
For<IService>().DecorateAllWith<CachingServiceDecorator>();
```

**MEDI with Scrutor:**
```csharp
services.AddScoped<IService, ServiceImpl>();
services.Decorate<IService, CachingServiceDecorator>();

// Add Scrutor package reference
```

### 10. Interceptor (Confidence: 50% - Manual)

**Autofac:**
```csharp
builder.RegisterType<MyService>()
       .As<IService>()
       .EnableInterfaceInterceptors()
       .InterceptedBy(typeof(LoggingInterceptor));
```

**MEDI (Manual):**
```csharp
// TODO: Interceptor migration requires manual intervention
// Option 1: Use Castle.DynamicProxy
// services.AddScoped<IService>(sp =>
//     ProxyGenerator.CreateInterfaceProxyWithTarget<IService>(
//         new MyService(), new LoggingInterceptor()));
//
// Option 2: Use Decorator pattern
// services.Decorate<IService, LoggingServiceDecorator>();
//
// Option 3: Use DispatchProxy (built-in but limited)
```

### 11. Module Consolidation

**Autofac Module:**
```csharp
public class DataModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SqlRepository>().As<IRepository>();
        builder.RegisterType<UnitOfWork>().As<IUnitOfWork>();
    }
}

// Registration
builder.RegisterModule<DataModule>();
```

**MEDI Extension Method:**
```csharp
public static class DataServiceExtensions
{
    /// <summary>
    /// Registers data access services.
    /// Migrated from Autofac DataModule.
    /// </summary>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddScoped<IRepository, SqlRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}

// Registration in Program.cs
builder.Services.AddDataServices();
```

---

## Confidence Scoring Algorithm

```csharp
public int CalculateConfidence(ServiceRegistrationInfo registration)
{
    int confidence = 100;

    // Lifetime mapping
    if (registration.Lifetime == ServiceLifetime.Scoped &&
        registration.SourceLifetime.Contains("Request"))
    {
        confidence -= 5; // Request scope maps well but not perfect
    }

    // Registration method
    switch (registration.Method)
    {
        case RegistrationMethod.Type:
            // Direct mapping, no reduction
            break;
        case RegistrationMethod.Generic:
            // Direct mapping, no reduction
            break;
        case RegistrationMethod.Instance:
            // Direct mapping, no reduction
            break;
        case RegistrationMethod.Factory:
            confidence -= (registration.Factory.IsSimple ? 10 : 25);
            break;
        case RegistrationMethod.AssemblyScanning:
            confidence -= 25; // Requires Scrutor
            break;
    }

    // Named registrations
    if (!string.IsNullOrEmpty(registration.NamedKey))
    {
        bool isDotNet8Plus = DetectTargetFramework() >= 8;
        confidence -= isDotNet8Plus ? 10 : 30;
    }

    // Property injection
    if (registration.PropertyInjection != null)
    {
        confidence -= registration.PropertyInjection.CanConvertToConstructor ? 20 : 40;
    }

    // Interceptors
    if (registration.Interceptor != null)
    {
        confidence -= 50; // Requires significant manual work
    }

    return Math.Max(confidence, 0);
}
```

---

## Test Strategy

### Unit Tests (150+ tests total)

#### Detection Tests (30 tests)
- `AutofacDetectorTests.cs` (8 tests)
  - Detect_Autofac_FromPackageReference
  - Detect_Autofac_FromModule
  - Detect_Autofac_FromContainerBuilder
  - Detect_Multiple_Modules
  - Detect_Autofac_MvcIntegration
  - Detect_Autofac_WebApiIntegration
  - Detect_ConfigurationFiles
  - NoDetection_WhenNoAutofac

- `UnityDetectorTests.cs` (6 tests)
  - Detect_Unity_FromPackageReference
  - Detect_Unity_FromUnityConfig
  - Detect_Unity_FromWebApiConfig
  - Detect_Unity_InGlobalAsax
  - Detect_ConfigurationFiles
  - NoDetection_WhenNoUnity

- `NinjectDetectorTests.cs` (8 tests)
  - Detect_Ninject_FromPackageReference
  - Detect_Ninject_FromNinjectWebCommon
  - Detect_Ninject_FromModule
  - Detect_Ninject_InOwinStartup
  - Detect_Multiple_Modules
  - Detect_ConfigurationFiles
  - NoDetection_WhenNoNinject
  - Detect_WebActivator_Integration

- `StructureMapDetectorTests.cs` (8 tests)
  - Detect_StructureMap_FromPackageReference
  - Detect_StructureMap_FromRegistry
  - Detect_StructureMap_FromGlobalAsax
  - Detect_Multiple_Registries
  - Detect_ConfigurationFiles
  - NoDetection_WhenNoStructureMap
  - Detect_StructureMap_InOwinStartup
  - Detect_NestedRegistry

#### Analyzer Tests (60 tests)
- `AutofacAnalyzerTests.cs` (20 tests)
  - Parse_RegisterType_Singleton
  - Parse_RegisterType_InstancePerLifetimeScope
  - Parse_RegisterType_InstancePerDependency
  - Parse_RegisterType_InstancePerRequest
  - Parse_RegisterGeneric
  - Parse_RegisterInstance
  - Parse_Factory_Simple
  - Parse_Factory_WithMultipleDependencies
  - Parse_Named_Registration
  - Parse_Keyed_Registration
  - Parse_PropertyInjection_Autowired
  - Parse_Interceptor_EnableInterfaceInterceptors
  - Parse_Module_Load
  - Parse_Module_With_Dependencies
  - Parse_ContainerBuilder_Multiple_Registrations
  - Parse_AsImplementedInterfaces
  - Parse_SingleInstance
  - Parse_ExternallyOwned
  - Parse_OnActivated_Hook
  - Parse_WithParameter

- `UnityAnalyzerTests.cs` (12 tests)
  - Parse_RegisterType_ContainerControlled
  - Parse_RegisterType_Hierarchical
  - Parse_RegisterType_Transient
  - Parse_RegisterType_PerRequest
  - Parse_Named_Registration
  - Parse_InjectionProperty
  - Parse_InjectionConstructor
  - Parse_InjectionFactory
  - Parse_RegisterInstance
  - Parse_Interception_Behavior
  - Parse_Multiple_Registrations
  - Parse_RegisterTypes_Convention

- `NinjectAnalyzerTests.cs` (14 tests)
  - Parse_Bind_InSingletonScope
  - Parse_Bind_InRequestScope
  - Parse_Bind_InTransientScope
  - Parse_Bind_InThreadScope
  - Parse_Named_Binding
  - Parse_Property_Injection_Attribute
  - Parse_Constructor_Injection
  - Parse_Conditional_Binding_When
  - Parse_Conditional_Binding_WhenInjectedInto
  - Parse_ToConstant
  - Parse_ToMethod
  - Parse_Intercept_With
  - Parse_NinjectModule
  - Parse_Rebind

- `StructureMapAnalyzerTests.cs` (14 tests)
  - Parse_ForUse_Singleton
  - Parse_ForUse_ContainerScoped
  - Parse_ForUse_AlwaysUnique
  - Parse_ForUse_Unique
  - Parse_ForUse_HttpContextScoped
  - Parse_Named_Registration
  - Parse_Decorator_DecorateAllWith
  - Parse_Scan_AssemblyScanning
  - Parse_Registry_Multiple_Registrations
  - Parse_Use_Lambda
  - Parse_Add_Named
  - Parse_SetAllProperties
  - Parse_FillAllPropertiesOfType
  - Parse_Conditional_If

#### Lifetime Mapper Tests (12 tests)
- `LifetimeMapperTests.cs`
  - Map_Autofac_SingleInstance_To_Singleton
  - Map_Autofac_InstancePerLifetimeScope_To_Scoped
  - Map_Autofac_InstancePerDependency_To_Transient
  - Map_Unity_ContainerControlled_To_Singleton
  - Map_Unity_Hierarchical_To_Scoped
  - Map_Unity_Transient_To_Transient
  - Map_Ninject_InSingletonScope_To_Singleton
  - Map_Ninject_InRequestScope_To_Scoped
  - Map_StructureMap_Singleton_To_Singleton
  - Map_StructureMap_ContainerScoped_To_Scoped
  - LoadMappings_FromYaml
  - UnknownLifetime_ReturnsDefaultMapping

#### Transformer Tests (30 tests)
- `DIContainerTransformerTests.cs`
  - Transform_Simple_Registration
  - Transform_Generic_Registration
  - Transform_Instance_Registration
  - Transform_Factory_Simple
  - Transform_Factory_Complex
  - Transform_Named_Registration_DotNet8
  - Transform_Named_Registration_PreDotNet8
  - Transform_Multiple_Registrations_Grouped
  - Transform_Module_To_ExtensionMethod
  - Transform_Multiple_Modules_With_Dependencies
  - Generate_ProgramCs_Integration
  - Generate_ServiceCollection_Code
  - Add_Scrutor_When_Assembly_Scanning
  - Add_TODO_For_Interceptors
  - Preserve_Comments
  - Handle_Circular_Dependencies
  - Generate_XML_Documentation
  - Calculate_Confidence_Scores
  - Group_By_Namespace
  - Generate_Separate_Extensions_Per_Module
  - Handle_Conditional_Registrations
  - Transform_Decorator_To_Scrutor
  - Transform_Child_Container_To_Scope
  - Transform_Generic_With_Constraints
  - Generate_Factory_For_Named_Services_PreDotNet8
  - Handle_Service_Overrides
  - Transform_Assembly_Scanning_With_Filter
  - Transform_OpenGeneric_With_Constraints
  - Handle_Multi_Interface_Registration
  - Generate_DI_Validation

#### Property Injection Tests (12 tests)
- `PropertyInjectionAnalyzerTests.cs`
  - Detect_Property_With_DependencyAttribute
  - Detect_Property_With_InjectAttribute
  - Detect_AutoWired_Properties
  - CanConvert_To_Constructor_True
  - CanConvert_To_Constructor_False_ReadOnlyProperty
  - CanConvert_To_Constructor_False_Computed
  - Generate_Constructor_From_Properties
  - Transform_Property_To_Constructor
  - Preserve_Existing_Constructor
  - Handle_Property_With_DefaultValue
  - Detect_Required_Vs_Optional_Properties
  - Generate_TODO_For_Complex_Property_Injection

#### Interceptor Tests (6 tests)
- `InterceptorTransformerTests.cs`
  - Detect_Autofac_Interceptor
  - Detect_Unity_InterceptionBehavior
  - Detect_Ninject_Interceptor
  - Detect_StructureMap_Decorator
  - Generate_TODO_With_Scrutor_Option
  - Generate_TODO_With_Castle_Option

### Integration Tests (8 tests)

- `DIContainerMigrationIntegrationTests.cs`
  - Analyze_Autofac_Project_EndToEnd
  - Analyze_Unity_Project_EndToEnd
  - Analyze_Ninject_Project_EndToEnd
  - Analyze_StructureMap_Project_EndToEnd
  - Transform_Autofac_To_MEDI_EndToEnd
  - Transform_And_Build_Migrated_Project
  - Verify_Generated_Code_Has_Same_Registrations
  - Full_Migration_With_Confidence_Report

### E2E Tests (8 tests)

- `DIContainerE2ETests.cs`
  - E2E_Autofac_Simple_Project
  - E2E_Autofac_Complex_With_Modules
  - E2E_Unity_MVC5_Project
  - E2E_Ninject_WebApi_Project
  - E2E_StructureMap_With_Registry
  - E2E_Mixed_DI_Frameworks
  - E2E_Autofac_With_Property_Injection
  - E2E_Autofac_With_Assembly_Scanning

### Test Fixtures

Create test fixtures under `tests/fixtures/`:

1. **autofac-basic/**
   - Simple Autofac setup with ContainerBuilder
   - 5-10 service registrations
   - Mix of Singleton/Scoped/Transient
   - packages.config with Autofac

2. **autofac-modules/**
   - Multiple Autofac modules
   - Module dependencies
   - Named registrations
   - Factory registrations

3. **autofac-complex/**
   - Property injection
   - Interceptors
   - Assembly scanning
   - Decorators

4. **unity-basic/**
   - UnityConfig.cs
   - Container registrations in Global.asax
   - Mix of lifetime managers

5. **ninject-basic/**
   - NinjectWebCommon.cs
   - Kernel bindings
   - Named bindings

6. **structuremap-basic/**
   - StructuremapMvc.cs
   - Registry pattern
   - For/Use syntax

7. **mixed-di/** (edge case)
   - Project using both Autofac and Unity
   - Should generate warning

---

## Sprint Breakdown

### Sprint 12: DI Container Migration Foundation (12 tasks)

| # | Task | Estimate | Priority |
|---|------|----------|----------|
| 107 | Create DIContainerInfo model | S | P0 |
| 108 | Create ServiceRegistrationInfo model | S | P0 |
| 109 | Create LifetimeMapping model | S | P0 |
| 110 | Create ModuleInfo model | S | P0 |
| 111 | Create FactoryRegistrationInfo model | S | P0 |
| 112 | Create PropertyInjectionInfo model | S | P0 |
| 113 | Create InterceptorInfo model | S | P0 |
| 114 | Create DITransformResult model | S | P0 |
| 115 | Create IDIContainerDetector interface | S | P0 |
| 116 | Create IDIContainerAnalyzer interface | S | P0 |
| 117 | Create IDIContainerTransformer interface | S | P0 |
| 118 | Create di-lifetime-mappings.yml | M | P0 |

**Sprint 12 Goal:** Foundation models, interfaces, and YAML configuration

---

### Sprint 13: Detection & Autofac Analysis (12 tasks)

| # | Task | Estimate | Priority |
|---|------|----------|----------|
| 119 | Implement DIContainerDetector | M | P0 |
| 120 | Implement LifetimeMapper with YAML loader | M | P0 |
| 121 | Create IAutofacAnalyzer interface | S | P0 |
| 122 | Implement AutofacAnalyzer - basic registrations | L | P0 |
| 123 | Implement AutofacAnalyzer - modules | M | P0 |
| 124 | Implement AutofacAnalyzer - named registrations | M | P0 |
| 125 | Implement AutofacAnalyzer - factory registrations | M | P0 |
| 126 | Implement AutofacAnalyzer - property injection | M | P0 |
| 127 | Create autofac-basic test fixture | M | P0 |
| 128 | Create autofac-modules test fixture | M | P0 |
| 129 | Add 30 AutofacAnalyzer unit tests | L | P0 |
| 130 | Add 3 Autofac integration tests | M | P0 |

**Sprint 13 Goal:** Full Autofac detection and analysis with comprehensive tests

---

### Sprint 14: Unity, Ninject, StructureMap Analysis (12 tasks)

| # | Task | Estimate | Priority |
|---|------|----------|----------|
| 131 | Create IUnityAnalyzer interface | S | P0 |
| 132 | Implement UnityAnalyzer | L | P0 |
| 133 | Create INinjectAnalyzer interface | S | P0 |
| 134 | Implement NinjectAnalyzer | L | P0 |
| 135 | Create IStructureMapAnalyzer interface | S | P0 |
| 136 | Implement StructureMapAnalyzer | L | P0 |
| 137 | Create unity-basic test fixture | M | P0 |
| 138 | Create ninject-basic test fixture | M | P0 |
| 139 | Create structuremap-basic test fixture | M | P0 |
| 140 | Add 40 Unity/Ninject/StructureMap unit tests | XL | P0 |
| 141 | Add 6 integration tests (2 per framework) | M | P0 |
| 142 | Test mixed DI frameworks detection | S | P1 |

**Sprint 14 Goal:** Complete analysis for all 4 DI frameworks

---

### Sprint 15: Transformation & Generation (10 tasks)

| # | Task | Estimate | Priority |
|---|------|----------|----------|
| 143 | Implement DIContainerTransformer - simple registrations | L | P0 |
| 144 | Implement DIContainerTransformer - factory registrations | M | P0 |
| 145 | Implement DIContainerTransformer - named registrations | M | P0 |
| 146 | Generate extension methods from modules | M | P0 |
| 147 | Generate Program.cs integration code | M | P0 |
| 148 | Implement PropertyInjectionAnalyzer | M | P0 |
| 149 | Implement PropertyInjectionTransformer | M | P0 |
| 150 | Add 30 transformer unit tests | L | P0 |
| 151 | Add 3 transformation integration tests | M | P0 |
| 152 | Verify generated code compiles | M | P0 |

**Sprint 15 Goal:** Full transformation pipeline from legacy DI to MEDI

---

### Sprint 16: Advanced Scenarios & Polish (10 tasks)

| # | Task | Estimate | Priority |
|---|------|----------|----------|
| 153 | Implement InterceptorTransformer with guidance | M | P0 |
| 154 | Handle assembly scanning with Scrutor | M | P0 |
| 155 | Handle child containers/scopes | M | P1 |
| 156 | Handle conditional registrations | M | P1 |
| 157 | Generate DI validation code | M | P1 |
| 158 | Create autofac-complex test fixture | M | P0 |
| 159 | Add 12 advanced scenario tests | L | P0 |
| 160 | Add 8 E2E tests | L | P0 |
| 161 | Generate HTML report section for DI migration | M | P0 |
| 162 | Update orchestrator to include DI phase | M | P0 |

**Sprint 16 Goal:** Handle edge cases, generate comprehensive reports

---

## Git Workflow Integration

DI Container migration will follow the same branch-per-phase strategy:

```
main
└── netlift/01-sdk-conversion
    └── netlift/02-configuration
        └── netlift/03-mvc-controllers
            └── netlift/04-di-container  ← New phase
```

**Commit structure:**
1. "Detect Autofac DI configuration"
2. "Remove legacy DI packages (Autofac, Autofac.Mvc5)"
3. "Generate MEDI service registrations"
4. "Transform property injection to constructor injection"
5. "Generate extension methods from Autofac modules"
6. "Add TODO comments for interceptor migration"
7. "Update Program.cs with service registrations"

---

## Dependencies

### NuGet Packages to Add (if needed)
- **Scrutor** (3.3.0+): For assembly scanning and decoration
  - Add when: Assembly scanning or decorator patterns detected
  - Confidence boost: +15% for assembly scanning

### NuGet Packages to Remove
- Autofac
- Autofac.Mvc5
- Autofac.WebApi2
- Autofac.Integration.*
- Unity
- Unity.Container
- Unity.Mvc5
- Unity.AspNet.WebApi
- Ninject
- Ninject.Web.Mvc
- Ninject.Web.WebApi
- StructureMap
- StructureMap.MVC5
- StructureMap.WebApi2

---

## Confidence Scoring Summary

| Transformation Type | Confidence Range | Action |
|---------------------|------------------|--------|
| Simple type registration | 95-100% | Auto-apply |
| Generic registration | 95-100% | Auto-apply |
| Instance registration | 95-100% | Auto-apply |
| Factory - simple | 85-95% | Auto-apply with INFO |
| Factory - complex | 70-85% | Apply with WARNING |
| Named - .NET 8+ | 90-95% | Auto-apply |
| Named - Pre-.NET 8 | 65-75% | Apply with TODO |
| Property injection - convertible | 70-80% | Apply with TODO |
| Property injection - complex | 50-70% | Generate TODO |
| Assembly scanning with Scrutor | 75-85% | Apply with INFO |
| Assembly scanning manual | 50-60% | Generate TODO |
| Decorators with Scrutor | 70-80% | Apply with TODO |
| Interceptors | 40-50% | Generate manual task |
| Multi-tenant | 30-40% | Generate manual task |

---

## Generated Code Quality Standards

All generated code must:
1. Follow C# naming conventions
2. Include XML documentation comments
3. Use `IServiceCollection` extension methods for grouping
4. Add TODO comments for manual steps
5. Reference confidence scores in comments
6. Include migration notes as comments
7. Use proper error handling (ArgumentNullException checks)
8. Be formatted with consistent indentation
9. Use modern C# features (file-scoped namespaces, target-typed new)
10. Include links to Microsoft docs for MEDI patterns

**Example generated extension method:**
```csharp
namespace MyApp.Infrastructure.DependencyInjection;

/// <summary>
/// Service registration for data access layer.
/// Migrated from Autofac DataModule with 95% confidence.
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Registers data access services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // IRepository<T> → Repository<T> (Scoped)
        // Original: builder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>)).InstancePerLifetimeScope()
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // IUnitOfWork → UnitOfWork (Scoped)
        // Original: builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope()
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
```

---

## HTML Report Section

Add new section to migration report: "Dependency Injection Migration"

**Include:**
- Detected DI framework(s)
- Number of registrations migrated
- Confidence score distribution (chart)
- List of registrations with confidence < 80%
- Manual action items
- Property injection transformations performed
- Interceptors requiring manual work
- Package changes (removed/added)

---

## CLI Integration

Update `MigrateCommand` to include DI migration phase:

```bash
# Analyze DI only
netlift migrate MySolution.sln --phase di-container --analyze-only

# Migrate with DI
netlift migrate MySolution.sln --include-di

# Migrate DI only (if already on .NET 8)
netlift migrate MySolution.sln --phase di-container --dry-run

# Skip DI migration
netlift migrate MySolution.sln --skip-di
```

---

## Success Metrics

Sprint completion will be measured by:
1. **Test Coverage:** 150+ tests, all passing
2. **Confidence Accuracy:** Generated code with stated confidence actually compiles/works
3. **E2E Success:** All 4 framework fixtures migrate successfully
4. **Code Quality:** Generated code passes analyzer rules
5. **Documentation:** All public APIs have XML docs

---

## Future Enhancements (Post-MVP)

1. **Multi-tenant container migration** - Advanced Autofac feature
2. **Module dependency analysis** - Visualize module dependencies
3. **DI validation** - Generate validation code to ensure all services resolve
4. **Performance comparison** - Benchmark before/after DI performance
5. **Custom DI frameworks** - Support for other frameworks (Castle Windsor, Simple Injector)
6. **DI anti-pattern detection** - Service locator, fat registrations

---

## Notes

- **Do not auto-migrate interceptors** - Too complex, low confidence, generate TODO
- **Prefer constructor injection** - Always convert property injection when possible
- **Group registrations logically** - By namespace or module
- **Document all transformations** - Comments in generated code
- **Test with real projects** - eShopModernizing has Autofac, use as validation

---

*Last updated: 2026-02-02*
