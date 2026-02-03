using Microsoft.Extensions.DependencyInjection;
using NetLift.Analysis;
using NetLift.Analysis.Config;
using NetLift.Analysis.Interfaces;
using NetLift.Analysis.Parsers;
using NetLift.Cli.Commands;
using NetLift.Cli.Services;
using NetLift.Core.Interfaces;
using NetLift.Core.Services;
using NetLift.Transforms;
using NetLift.Transforms.Converters;
using NetLift.Transforms.Generators;
using NetLift.Transforms.Mvc.Generators;
using NetLift.Transforms.Mvc.Parsers;
using NetLift.Transforms.Mvc.Rewriters;
using NetLift.Transforms.Ef.Analyzers;
using NetLift.Transforms.Ef.Rewriters;
using NetLift.Transforms.Wcf.Parsers;
using NetLift.Transforms.Wcf.Analyzers;
using NetLift.Transforms.Wcf.Generators;
using NetLift.Transforms.Services;
using NetLift.Transforms.Modernization;
using NetLift.Transforms.Modernization.Analyzers;
using NetLift.Transforms.Modernization.Generators;
using NetLift.Transforms.Modernization.Transformers;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Interfaces.Razor;
using NetLift.Transforms.Razor.Analyzers;
using NetLift.Transforms.Razor.Transformers;
using NetLift.Validation;
using Spectre.Console.Cli;

// Setup dependency injection
var services = new ServiceCollection();

// Register parsers
services.AddSingleton<ISolutionParser, SolutionParser>();
// Use CompositeProjectParser to handle both old-format and SDK-style projects
services.AddSingleton<IProjectParser, CompositeProjectParser>();
services.AddSingleton<IPackagesConfigParser, PackagesConfigParser>();
services.AddSingleton<IWebConfigAppSettingsParser, WebConfigAppSettingsParser>();
services.AddSingleton<IWebConfigConnectionStringParser, WebConfigConnectionStringParser>();
services.AddSingleton<ISystemWebParser, SystemWebParser>();
services.AddSingleton<IServiceModelParser, ServiceModelParser>();

// Register analysis services
services.AddSingleton<IProjectTypeDetector, ProjectTypeDetector>();
services.AddSingleton<IDependencyGraphBuilder, DependencyGraphBuilder>();
services.AddSingleton<IReportBuilder, AnalysisReportBuilder>();

// Register transformation services
services.AddSingleton<ISdkProjectConverter, SdkProjectConverter>();
services.AddSingleton<IAssemblyInfoExtractor, AssemblyInfoExtractor>();
services.AddSingleton<IPackageReferenceConverter, PackageReferenceConverter>();
services.AddSingleton<IPackageMappingService, PackageMappingService>();
services.AddSingleton<ISourceFileTransformer, SourceFileTransformer>();
services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();

// Register MVC rewriters
services.AddSingleton<IMvcNamespaceRewriter, SystemWebMvcNamespaceRewriter>();
services.AddSingleton<IControllerBaseRewriter, ControllerBaseClassRewriter>();
services.AddSingleton<IControllerMethodBodyRewriter, ControllerMethodBodyRewriter>();
services.AddSingleton<IActionResultRewriter, ActionResultTypeRewriter>();
services.AddSingleton<IHttpContextRewriter, HttpContextCurrentRewriter>();
services.AddSingleton<IActionFilterTransformer, ActionFilterTransformer>();
services.AddSingleton<IAttributeRoutingTransformer, AttributeRoutingTransformer>();

// Register MVC parsers
services.AddSingleton<IRouteConfigParser, RouteConfigParser>();
services.AddSingleton<IAreaRegistrationParser, AreaRegistrationParser>();
services.AddSingleton<IBundleConfigParser, BundleConfigParser>();

// Register MVC transformers
services.AddSingleton<IAreaMigrationTransformer, AreaMigrationTransformer>();
services.AddSingleton<IAssetReferenceTransformer, AssetReferenceTransformer>();
services.AddSingleton<IRazorNamespaceTransformer, RazorNamespaceTransformer>();

// Register Razor analyzers and transformers
services.AddSingleton<IRazorViewAnalyzer, RazorViewAnalyzer>();
services.AddSingleton<IRazorViewTransformer, RazorViewTransformer>();

// Register WCF parsers, analyzers, and generators
services.AddSingleton<IWcfServiceParser, WcfServiceParser>();
services.AddSingleton<IWcfDataContractParser, WcfDataContractParser>();
services.AddSingleton<IBusinessLogicExtractor, BusinessLogicExtractor>();
services.AddSingleton<IFaultContractTransformer, FaultContractTransformer>();
services.AddSingleton<IProtoGenerator, ProtoGenerator>();
services.AddSingleton<IGrpcServiceGenerator, GrpcServiceGenerator>();
services.AddSingleton<IDuplexDetector, DuplexDetector>();
services.AddSingleton<IRestControllerGenerator, RestControllerGenerator>();
services.AddSingleton<IClientProxyGenerator, ClientProxyGenerator>();

// Register MVC generators
services.AddSingleton<IViewImportsGenerator, ViewImportsGenerator>();
services.AddSingleton<IViteConfigGenerator, ViteConfigGenerator>();
services.AddSingleton<IWebpackConfigGenerator, WebpackConfigGenerator>();
services.AddSingleton<IPackageJsonGenerator, PackageJsonGenerator>();

// Register EF analyzers and rewriters
services.AddSingleton<IDbContextDetector, DbContextDetector>();
services.AddSingleton<IDbContextConstructorRewriter, DbContextConstructorRewriter>();
services.AddSingleton<IFluentApiRelationshipRewriter, FluentApiRelationshipRewriter>();
services.AddSingleton<IManyToManyRewriter, ManyToManyRewriter>();
services.AddSingleton<IIncludeThenIncludeRewriter, IncludeThenIncludeRewriter>();
services.AddSingleton<ISqlQueryRewriter, SqlQueryRewriter>();
services.AddSingleton<ILazyLoadingConfigRewriter, LazyLoadingConfigRewriter>();
services.AddSingleton<IDatabaseInitializerRemover, DatabaseInitializerRemover>();

// Register generators
services.AddSingleton<IAppSettingsJsonGenerator, AppSettingsJsonGenerator>();
services.AddSingleton<IEnvironmentAppSettingsGenerator, EnvironmentAppSettingsGenerator>();
services.AddSingleton<IProgramCsGenerator, ProgramCsGenerator>();

// Register CQRS generators
services.AddSingleton<ICommandGenerator, CommandGenerator>();
services.AddSingleton<IQueryGenerator, QueryGenerator>();
services.AddSingleton<IHandlerGenerator, HandlerGenerator>();
services.AddSingleton<IValidatorGenerator, ValidatorGenerator>();

// Register report generators
services.AddSingleton<IHtmlReportGenerator, HtmlReportGenerator>();
services.AddSingleton<IFullHtmlReportGenerator, FullHtmlReportGenerator>();

// Register validation services
services.AddSingleton<IBuildValidator, BuildValidator>();
services.AddSingleton<ITestRunner, TestRunner>();
services.AddSingleton<IConfidenceScorer, ConfidenceScorer>();

// Register error handling services
services.AddSingleton<IRecoverySuggestionProvider, RecoverySuggestionProvider>();
services.AddSingleton<IErrorHandler, ErrorHandler>();

// Register interactive service
services.AddSingleton<IInteractiveService, InteractiveService>();

// Register dry-run service
services.AddSingleton<IDryRunService, DryRunService>();

// Register orchestrator
services.AddSingleton<IMigrationOrchestrator, MigrationOrchestrator>();

// Register modernization services
services.AddSingleton<IControllerAnalyzer, ControllerAnalyzer>();
services.AddSingleton<IServiceAnalyzer, ServiceAnalyzer>();
services.AddSingleton<ILogicExtractor, LogicExtractor>();
services.AddSingleton<IControllerTransformer, ControllerSlimmer>();
services.AddSingleton<ILoggingAnalyzer, LoggingAnalyzer>();
services.AddSingleton<IObservabilityGenerator, ObservabilityGenerator>();
services.AddSingleton<IAuthenticationAnalyzer, AuthenticationAnalyzer>();
services.AddSingleton<IAuthenticationGenerator, AuthenticationGenerator>();
services.AddSingleton<IModernizationOrchestrator, ModernizationOrchestrator>();

// Register commands
services.AddSingleton<AnalyzeCommand>();
services.AddSingleton<MigrateCommand>();
services.AddSingleton<ModernizeCommand>();
services.AddSingleton<ValidateCommand>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("netlift");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze a solution for migration readiness")
        .WithExample(new[] { "analyze", "MySolution.sln" })
        .WithExample(new[] { "analyze", "MySolution.sln", "--output", "./reports" })
        .WithExample(new[] { "analyze", "MySolution.sln", "--target", "net9.0" });

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate a solution to .NET 8+")
        .WithExample(new[] { "migrate", "MySolution.sln" })
        .WithExample(new[] { "migrate", "MySolution.sln", "--target", "net9.0" })
        .WithExample(new[] { "migrate", "MySolution.sln", "--dry-run" })
        .WithExample(new[] { "migrate", "MySolution.sln", "--interactive" });

    config.AddCommand<ModernizeCommand>("modernize")
        .WithDescription("Modernize to Clean Architecture with CQRS")
        .WithExample(new[] { "modernize", "MySolution.sln" })
        .WithExample(new[] { "modernize", "MySolution.sln", "--pattern", "cqrs" })
        .WithExample(new[] { "modernize", "MyProject.csproj", "--analyze-only" })
        .WithExample(new[] { "modernize", "MySolution.sln", "--pattern", "cqrs", "--pattern", "fluentvalidation" })
        .WithExample(new[] { "modernize", "MySolution.sln", "--dry-run", "--interactive" });

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate a migrated solution")
        .WithExample(new[] { "validate", "MySolution.sln" })
        .WithExample(new[] { "validate", "MySolution.sln", "--strict" })
        .WithExample(new[] { "validate", "MySolution.sln", "--format", "json" });

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});

return app.Run(args);

/// <summary>
/// Type registrar for Spectre.Console.Cli dependency injection.
/// </summary>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceCollection services)
    {
        _services = services;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }
}

/// <summary>
/// Type resolver for Spectre.Console.Cli dependency injection.
/// </summary>
internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        return type == null ? null : _provider.GetService(type);
    }
}
