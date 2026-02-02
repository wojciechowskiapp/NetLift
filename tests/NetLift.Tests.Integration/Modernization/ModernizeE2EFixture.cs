using Microsoft.Extensions.DependencyInjection;
using NetLift.Analysis;
using NetLift.Analysis.Config;
using NetLift.Analysis.Interfaces;
using NetLift.Analysis.Parsers;
using NetLift.Core.Interfaces;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms;
using NetLift.Transforms.Converters;
using NetLift.Transforms.Ef.Analyzers;
using NetLift.Transforms.Ef.Rewriters;
using NetLift.Transforms.Generators;
using NetLift.Transforms.Modernization;
using NetLift.Transforms.Modernization.Analyzers;
using NetLift.Transforms.Modernization.Generators;
using NetLift.Transforms.Modernization.Transformers;
using NetLift.Transforms.Mvc.Generators;
using NetLift.Transforms.Mvc.Parsers;
using NetLift.Transforms.Mvc.Rewriters;
using NetLift.Transforms.Services;
using NetLift.Transforms.Wcf.Analyzers;
using NetLift.Transforms.Wcf.Generators;
using NetLift.Transforms.Wcf.Parsers;

namespace NetLift.Tests.Integration.Modernization;

/// <summary>
/// Shared fixture for ModernizeE2E tests. Runs the expensive modernization operation once
/// and shares the result across all tests that use the standard CQRS pattern setup.
/// </summary>
public class ModernizeE2EFixture : IAsyncLifetime
{
    public string WorkingDirectory { get; private set; } = "";
    public ModernizationResult Result { get; private set; } = null!;
    public ProjectInfo ProjectInfo { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        WorkingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"netlift-modernize-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(WorkingDirectory);

        ProjectInfo = await CreateTestProjectAsync();
        var orchestrator = CreateModernizationOrchestrator();
        var options = new ModernizationOptions
        {
            Patterns = new HashSet<ModernizationPattern> { ModernizationPattern.Cqrs },
            DryRun = false
        };

        Result = await orchestrator.ModernizeAsync(ProjectInfo, options);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(WorkingDirectory))
        {
            try
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
        return Task.CompletedTask;
    }

    private async Task<ProjectInfo> CreateTestProjectAsync()
    {
        var controllerDir = Path.Combine(WorkingDirectory, "Controllers");
        Directory.CreateDirectory(controllerDir);

        var controllerPath = Path.Combine(controllerDir, "ProductsController.cs");
        var controllerSource = @"using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers
{
    public class ProductsController : Controller
    {
        public IActionResult Index()
        {
            var products = new List<string> { ""Product1"", ""Product2"" };
            return View(products);
        }

        public IActionResult Details(int id)
        {
            var product = $""Product {id}"";
            return View(product);
        }

        [HttpPost]
        public IActionResult Create(string name)
        {
            // Create product logic
            return RedirectToAction(""Index"");
        }
    }
}";
        await File.WriteAllTextAsync(controllerPath, controllerSource);

        var projectPath = Path.Combine(WorkingDirectory, "TestApp.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>TestApp</RootNamespace>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, projectContent);

        return new ProjectInfo
        {
            FilePath = projectPath,
            AssemblyName = "TestApp",
            RootNamespace = "TestApp",
            TargetFramework = new NetLift.Core.Models.TargetFramework
            {
                Moniker = "net8.0",
                Type = FrameworkType.Core
            },
            CompileItems = new List<CompileItem>
            {
                new CompileItem { Include = controllerPath }
            }
        };
    }

    private IModernizationOrchestrator CreateModernizationOrchestrator()
    {
        var services = new ServiceCollection();

        // Register analysis services
        services.AddSingleton<IProjectParser, OldFormatProjectParser>();
        services.AddSingleton<IPackagesConfigParser, PackagesConfigParser>();
        services.AddSingleton<IWebConfigAppSettingsParser, WebConfigAppSettingsParser>();
        services.AddSingleton<IWebConfigConnectionStringParser, WebConfigConnectionStringParser>();
        services.AddSingleton<ISystemWebParser, SystemWebParser>();
        services.AddSingleton<IServiceModelParser, ServiceModelParser>();
        services.AddSingleton<IProjectTypeDetector, ProjectTypeDetector>();
        services.AddSingleton<IReportBuilder, AnalysisReportBuilder>();

        // Register transformation services
        services.AddSingleton<ISdkProjectConverter, SdkProjectConverter>();
        services.AddSingleton<IAssemblyInfoExtractor, AssemblyInfoExtractor>();
        services.AddSingleton<IPackageReferenceConverter, PackageReferenceConverter>();
        services.AddSingleton<IPackageMappingService, PackageMappingService>();
        services.AddSingleton<ISourceFileTransformer, SourceFileTransformer>();
        services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();

        // Register MVC services
        services.AddSingleton<IMvcNamespaceRewriter, SystemWebMvcNamespaceRewriter>();
        services.AddSingleton<IControllerBaseRewriter, ControllerBaseClassRewriter>();
        services.AddSingleton<IActionResultRewriter, ActionResultTypeRewriter>();
        services.AddSingleton<IHttpContextRewriter, HttpContextCurrentRewriter>();
        services.AddSingleton<IActionFilterTransformer, ActionFilterTransformer>();
        services.AddSingleton<IAttributeRoutingTransformer, AttributeRoutingTransformer>();
        services.AddSingleton<IRouteConfigParser, RouteConfigParser>();
        services.AddSingleton<IViewImportsGenerator, ViewImportsGenerator>();

        // Register WCF services
        services.AddSingleton<IWcfServiceParser, WcfServiceParser>();
        services.AddSingleton<IWcfDataContractParser, WcfDataContractParser>();
        services.AddSingleton<IBusinessLogicExtractor, BusinessLogicExtractor>();
        services.AddSingleton<IFaultContractTransformer, FaultContractTransformer>();
        services.AddSingleton<IProtoGenerator, ProtoGenerator>();
        services.AddSingleton<IGrpcServiceGenerator, GrpcServiceGenerator>();
        services.AddSingleton<IDuplexDetector, DuplexDetector>();
        services.AddSingleton<IRestControllerGenerator, RestControllerGenerator>();
        services.AddSingleton<IClientProxyGenerator, ClientProxyGenerator>();

        // Register P2 services
        services.AddSingleton<IAreaRegistrationParser, AreaRegistrationParser>();
        services.AddSingleton<IAreaMigrationTransformer, AreaMigrationTransformer>();
        services.AddSingleton<IBundleConfigParser, BundleConfigParser>();
        services.AddSingleton<IViteConfigGenerator, ViteConfigGenerator>();
        services.AddSingleton<IWebpackConfigGenerator, WebpackConfigGenerator>();
        services.AddSingleton<IAssetReferenceTransformer, AssetReferenceTransformer>();
        services.AddSingleton<IRazorNamespaceTransformer, RazorNamespaceTransformer>();
        services.AddSingleton<IPackageJsonGenerator, PackageJsonGenerator>();

        // Register EF services
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

        // Register modernization services
        services.AddSingleton<IControllerAnalyzer, ControllerAnalyzer>();
        services.AddSingleton<IServiceAnalyzer, ServiceAnalyzer>();
        services.AddSingleton<ILogicExtractor, LogicExtractor>();
        services.AddSingleton<IControllerTransformer, ControllerSlimmer>();
        services.AddSingleton<IModernizationOrchestrator, ModernizationOrchestrator>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IModernizationOrchestrator>();
    }
}
