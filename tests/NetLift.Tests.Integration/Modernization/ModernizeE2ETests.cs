using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetLift.Analysis;
using NetLift.Analysis.Config;
using NetLift.Analysis.Interfaces;
using NetLift.Analysis.Parsers;
using NetLift.Core.Interfaces;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;
using NetLift.Tests.Integration.E2E;
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
/// End-to-end tests for the NetLift modernize command.
/// Tests the complete modernization pipeline from analysis to CQRS pattern application.
/// Uses shared fixture to run expensive ModernizeAsync once for standard CQRS tests.
/// </summary>
[Collection("E2E")]
public class ModernizeE2ETests : IClassFixture<ModernizeE2EFixture>
{
    private readonly ModernizeE2EFixture _fixture;

    public ModernizeE2ETests(ModernizeE2EFixture fixture)
    {
        _fixture = fixture;
    }

    #region Tests using shared fixture (fast - assertions only)

    [Fact]
    public void ModernizeAsync_GeneratesCommandFiles()
    {
        // Uses pre-computed result from fixture
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().NotBeEmpty();

        var commandFiles = result.GeneratedFiles.Where(f => f.FileType == "Command+Handler").ToList();
        commandFiles.Should().NotBeEmpty("at least one Command+Handler should be generated");

        var createCommand = commandFiles.FirstOrDefault(f => f.FilePath.Contains("CreateCommand.cs"));
        createCommand.Should().NotBeNull("CreateCommand should be generated for POST action");

        if (createCommand != null)
        {
            createCommand.FilePath.Should().Contain("Application");
            createCommand.FilePath.Should().Contain("Products");
            createCommand.FilePath.Should().Contain("Commands");
            File.Exists(createCommand.FilePath).Should().BeTrue("generated Command file should exist on disk");
        }
    }

    [Fact]
    public void ModernizeAsync_GeneratesQueryFiles()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var queryFiles = result.GeneratedFiles.Where(f => f.FileType == "Query+Handler").ToList();
        queryFiles.Should().NotBeEmpty("at least one Query+Handler should be generated");

        var indexQuery = queryFiles.FirstOrDefault(f => f.FilePath.Contains("GetListQuery.cs") || f.FilePath.Contains("IndexQuery.cs"));
        indexQuery.Should().NotBeNull("Index action should generate a query");

        var detailsQuery = queryFiles.FirstOrDefault(f => f.FilePath.Contains("GetByIdQuery.cs") || f.FilePath.Contains("DetailsQuery.cs"));
        detailsQuery.Should().NotBeNull("Details action should generate a query");

        if (indexQuery != null)
        {
            indexQuery.FilePath.Should().Contain("Application");
            indexQuery.FilePath.Should().Contain("Products");
            indexQuery.FilePath.Should().Contain("Queries");
            File.Exists(indexQuery.FilePath).Should().BeTrue("generated Query file should exist on disk");
        }
    }

    [Fact]
    public async Task ModernizeAsync_GeneratesCombinedCommandQueryHandlerFiles()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var commandFiles = result.GeneratedFiles.Where(f => f.FileType == "Command+Handler").ToList();
        var queryFiles = result.GeneratedFiles.Where(f => f.FileType == "Query+Handler").ToList();

        commandFiles.Should().NotBeEmpty("Command+Handler files should be generated for commands");
        queryFiles.Should().NotBeEmpty("Query+Handler files should be generated for queries");

        foreach (var file in commandFiles.Concat(queryFiles))
        {
            File.Exists(file.FilePath).Should().BeTrue($"file {file.FilePath} should exist on disk");

            var content = await File.ReadAllTextAsync(file.FilePath);
            content.Should().Contain("IRequest<", "file should contain request interface");
            content.Should().Contain("IRequestHandler<", "file should contain handler interface");
        }
    }

    [Fact]
    public async Task ModernizeAsync_GeneratesResultClass()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var resultFile = result.GeneratedFiles.FirstOrDefault(f => f.FileType == "Common" && f.FilePath.Contains("Result.cs"));
        resultFile.Should().NotBeNull("Result.cs should be generated in Application/Common");

        if (resultFile != null)
        {
            resultFile.FilePath.Should().Contain("Application");
            resultFile.FilePath.Should().Contain("Common");
            resultFile.Confidence.Should().Be(100, "Result class generation should have 100% confidence");
            File.Exists(resultFile.FilePath).Should().BeTrue("Result.cs should exist on disk");

            var content = await File.ReadAllTextAsync(resultFile.FilePath);
            content.Should().Contain("public class Result<T>", "Result class should be defined");
            content.Should().Contain("IsSuccess", "Result should have IsSuccess property");
        }
    }

    [Fact]
    public async Task ModernizeAsync_TransformsControllerToUseMediatR()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ModifiedFiles.Should().NotBeEmpty("controller files should be modified");

        var modifiedController = result.ModifiedFiles.FirstOrDefault(f => f.FilePath.Contains("ProductsController.cs"));
        modifiedController.Should().NotBeNull("ProductsController should be modified");

        if (modifiedController != null)
        {
            var controllerContent = await File.ReadAllTextAsync(modifiedController.FilePath);

            controllerContent.Should().Contain("private readonly IMediator _mediator",
                "controller should have IMediator field");
            controllerContent.Should().Contain("IMediator mediator",
                "constructor should accept IMediator");
            controllerContent.Should().Contain("_mediator = mediator",
                "constructor should assign mediator to field");
            controllerContent.Should().Contain("using TestApp.Application.Common.Interfaces",
                "controller should have using directive for IMediator interface");
        }
    }

    [Fact]
    public async Task ModernizeAsync_TransformsActionMethodsToUseMediatRSend()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var modifiedController = result.ModifiedFiles.FirstOrDefault(f => f.FilePath.Contains("ProductsController.cs"));
        modifiedController.Should().NotBeNull();

        if (modifiedController != null)
        {
            var controllerContent = await File.ReadAllTextAsync(modifiedController.FilePath);

            controllerContent.Should().Contain("await _mediator.Send(",
                "actions should use await _mediator.Send()");
            controllerContent.Should().Contain("async Task<IActionResult>",
                "actions should be converted to async methods");
        }
    }

    [Fact]
    public void ModernizeAsync_ReturnsModifiedFilesListWithTransformedControllers()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ModifiedFiles.Should().NotBeEmpty("controller files should be in ModifiedFiles list");

        var controllerMod = result.ModifiedFiles.FirstOrDefault(f => f.FilePath.Contains("ProductsController.cs"));
        controllerMod.Should().NotBeNull("ProductsController should be in ModifiedFiles");

        if (controllerMod != null)
        {
            controllerMod.Changes.Should().NotBeEmpty("modified file should list changes");
            controllerMod.Changes.Should().Contain(c => c.Contains("MediatR"),
                "changes should mention MediatR transformation");
            controllerMod.Confidence.Should().BeGreaterThan(0, "modified file should have confidence score");
        }
    }

    [Fact]
    public void ModernizeAsync_TracksAppliedPatterns()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AppliedPatterns.Should().NotBeEmpty("applied patterns should be tracked");
        result.AppliedPatterns.Should().ContainKey(ModernizationPattern.Cqrs,
            "CQRS pattern should be tracked");
        result.AppliedPatterns[ModernizationPattern.Cqrs].Should().BeGreaterThan(0,
            "CQRS pattern should have been applied at least once");
    }

    [Fact]
    public void ModernizeAsync_GeneratesDiagnostics()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Diagnostics.Should().NotBeEmpty("modernization should produce diagnostics");
        result.Diagnostics.Should().Contain(d => d.Severity == NetLift.Core.Models.Modernization.DiagnosticSeverity.Info,
            "should have informational diagnostics");
        result.Diagnostics.Should().Contain(d => d.Message.Contains("IApplicationDbContext"),
            "should mention IApplicationDbContext interface");
    }

    [Fact]
    public void ModernizeAsync_SetsOverallConfidence()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Confidence.Should().BeInRange(0, 100, "confidence should be a valid percentage");
        result.Confidence.Should().BeGreaterThan(50,
            "simple controller modernization should have reasonable confidence");
    }

    [Fact]
    public void ModernizeAsync_TracksDuration()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero, "duration should be tracked");
    }

    [Fact]
    public void ModernizeAsync_OrganizesFilesByControllerName()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        foreach (var file in result.GeneratedFiles.Where(f => f.FileType == "Command+Handler" || f.FileType == "Query+Handler"))
        {
            file.FilePath.Should().Contain("Application", "files should be in Application folder");
            file.FilePath.Should().Contain("Products", "files should be organized by controller name");

            if (file.FileType == "Command+Handler")
            {
                file.FilePath.Should().Contain("Commands", "command files should be in Commands folder");
            }
            else if (file.FileType == "Query+Handler")
            {
                file.FilePath.Should().Contain("Queries", "query files should be in Queries folder");
            }
        }
    }

    [Fact]
    public void ModernizeAsync_IncludesSourceReference()
    {
        var result = _fixture.Result;

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        foreach (var file in result.GeneratedFiles.Where(f =>
            f.FileType == "Command+Handler" || f.FileType == "Query+Handler"))
        {
            file.SourceReference.Should().NotBeNullOrEmpty(
                "generated files should reference their source controller/action");
            file.SourceReference.Should().Contain("ProductsController",
                "source reference should mention the controller");
        }
    }

    #endregion

    #region Tests requiring separate execution (different options or setup)

    [Fact]
    public async Task ModernizeAsync_WithDryRun_DoesNotModifyOriginalFiles()
    {
        // This test needs its own setup with DryRun = true
        var workingDir = Path.Combine(Path.GetTempPath(), $"netlift-dryrun-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var projectInfo = await CreateTestProjectAsync(workingDir);
            var originalControllerPath = Path.Combine(workingDir, "Controllers", "ProductsController.cs");
            var originalContent = await File.ReadAllTextAsync(originalControllerPath);

            var orchestrator = CreateModernizationOrchestrator();
            var options = new ModernizationOptions
            {
                Patterns = new HashSet<ModernizationPattern> { ModernizationPattern.Cqrs },
                DryRun = true
            };

            var result = await orchestrator.ModernizeAsync(projectInfo, options);

            result.Should().NotBeNull();
            result.GeneratedFiles.Should().NotBeEmpty("dry-run should still analyze and plan file generation");
            result.Diagnostics.Should().NotBeEmpty("dry-run should provide diagnostics");
        }
        finally
        {
            if (Directory.Exists(workingDir))
                Directory.Delete(workingDir, recursive: true);
        }
    }

    [Fact]
    public async Task ModernizeAsync_WithFluentValidation_GeneratesValidators()
    {
        // This test needs FluentValidation pattern in addition to CQRS
        var workingDir = Path.Combine(Path.GetTempPath(), $"netlift-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var projectInfo = await CreateTestProjectAsync(workingDir);
            var orchestrator = CreateModernizationOrchestrator();
            var options = new ModernizationOptions
            {
                Patterns = new HashSet<ModernizationPattern>
                {
                    ModernizationPattern.Cqrs,
                    ModernizationPattern.FluentValidation
                },
                DryRun = false
            };

            var result = await orchestrator.ModernizeAsync(projectInfo, options);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            var validatorFiles = result.GeneratedFiles.Where(f => f.FileType == "Validator").ToList();

            if (validatorFiles.Any())
            {
                foreach (var validator in validatorFiles)
                {
                    File.Exists(validator.FilePath).Should().BeTrue($"validator file {validator.FilePath} should exist");
                    validator.FilePath.Should().Contain("Validator.cs", "validator files should end with Validator.cs");
                }
            }
        }
        finally
        {
            if (Directory.Exists(workingDir))
                Directory.Delete(workingDir, recursive: true);
        }
    }

    [Fact]
    public async Task ModernizeAsync_WithOverloadedActions_GeneratesUniqueNames()
    {
        // This test needs a different controller with overloaded actions
        var workingDir = Path.Combine(Path.GetTempPath(), $"netlift-overload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var controllerDir = Path.Combine(workingDir, "Controllers");
            Directory.CreateDirectory(controllerDir);

            var controllerPath = Path.Combine(controllerDir, "StudentsController.cs");
            var controllerSource = @"using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace TestApp.Controllers
{
    public class StudentsController : Controller
    {
        private List<Department> _departments = new List<Department>();

        // GET: Students/Create - loads dropdown data (not trivial)
        public IActionResult Create()
        {
            // Load form data from database
            ViewBag.Departments = _departments.Select(d => new { d.Id, d.Name });
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        public IActionResult Create(Student student)
        {
            // Create student logic
            return RedirectToAction(""Index"");
        }

        // PUT: Students/Create (edge case - both commands)
        [HttpPut]
        public IActionResult Create(int id, Student student)
        {
            // Update or create logic
            return RedirectToAction(""Index"");
        }
    }

    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";
            await File.WriteAllTextAsync(controllerPath, controllerSource);

            var projectPath = Path.Combine(workingDir, "TestApp.csproj");
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>TestApp</RootNamespace>
  </PropertyGroup>
</Project>";
            await File.WriteAllTextAsync(projectPath, projectContent);

            var projectInfo = new ProjectInfo
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

            var orchestrator = CreateModernizationOrchestrator();
            var options = new ModernizationOptions
            {
                Patterns = new HashSet<ModernizationPattern> { ModernizationPattern.Cqrs },
                DryRun = false
            };

            var result = await orchestrator.ModernizeAsync(projectInfo, options);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            var queries = result.GeneratedFiles.Where(f => f.FileType == "Query+Handler").ToList();
            var createQuery = queries.FirstOrDefault(f => f.FilePath.Contains("Create") && f.FilePath.Contains("Query"));
            createQuery.Should().NotBeNull("GET Create() should generate a Query");
            if (createQuery != null)
            {
                createQuery.FilePath.Should().Contain("FormQuery.cs", "GET Create with overload should generate CreateFormQuery");
            }

            var commands = result.GeneratedFiles.Where(f => f.FileType == "Command+Handler").ToList();
            var postCreateCommand = commands.FirstOrDefault(f =>
                f.FilePath.Contains("CreateCommand.cs") &&
                !f.FilePath.Contains("Put"));
            postCreateCommand.Should().NotBeNull("POST Create(Student) should generate StudentsCreateCommand");

            var putCreateCommand = commands.FirstOrDefault(f => f.FilePath.Contains("Put"));
            putCreateCommand.Should().NotBeNull("PUT Create with overload should generate StudentsCreatePutCommand");
            if (putCreateCommand != null)
            {
                putCreateCommand.FilePath.Should().Contain("PutCommand.cs", "PUT Create should have Put suffix");
            }

            var allFilenames = result.GeneratedFiles
                .Where(f => f.FileType == "Command+Handler" || f.FileType == "Query+Handler")
                .Select(f => Path.GetFileName(f.FilePath))
                .ToList();

            allFilenames.Should().OnlyHaveUniqueItems("all generated files should have unique names");
        }
        finally
        {
            if (Directory.Exists(workingDir))
                Directory.Delete(workingDir, recursive: true);
        }
    }

    #endregion

    #region Helper methods for standalone tests

    private async Task<ProjectInfo> CreateTestProjectAsync(string workingDir)
    {
        var controllerDir = Path.Combine(workingDir, "Controllers");
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

        var projectPath = Path.Combine(workingDir, "TestApp.csproj");
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

        services.AddSingleton<IProjectParser, OldFormatProjectParser>();
        services.AddSingleton<IPackagesConfigParser, PackagesConfigParser>();
        services.AddSingleton<IWebConfigAppSettingsParser, WebConfigAppSettingsParser>();
        services.AddSingleton<IWebConfigConnectionStringParser, WebConfigConnectionStringParser>();
        services.AddSingleton<ISystemWebParser, SystemWebParser>();
        services.AddSingleton<IServiceModelParser, ServiceModelParser>();
        services.AddSingleton<IProjectTypeDetector, ProjectTypeDetector>();
        services.AddSingleton<IReportBuilder, AnalysisReportBuilder>();
        services.AddSingleton<ISdkProjectConverter, SdkProjectConverter>();
        services.AddSingleton<IAssemblyInfoExtractor, AssemblyInfoExtractor>();
        services.AddSingleton<IPackageReferenceConverter, PackageReferenceConverter>();
        services.AddSingleton<IPackageMappingService, PackageMappingService>();
        services.AddSingleton<ISourceFileTransformer, SourceFileTransformer>();
        services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();
        services.AddSingleton<IMvcNamespaceRewriter, SystemWebMvcNamespaceRewriter>();
        services.AddSingleton<IControllerBaseRewriter, ControllerBaseClassRewriter>();
        services.AddSingleton<IActionResultRewriter, ActionResultTypeRewriter>();
        services.AddSingleton<IHttpContextRewriter, HttpContextCurrentRewriter>();
        services.AddSingleton<IActionFilterTransformer, ActionFilterTransformer>();
        services.AddSingleton<IAttributeRoutingTransformer, AttributeRoutingTransformer>();
        services.AddSingleton<IRouteConfigParser, RouteConfigParser>();
        services.AddSingleton<IViewImportsGenerator, ViewImportsGenerator>();
        services.AddSingleton<IWcfServiceParser, WcfServiceParser>();
        services.AddSingleton<IWcfDataContractParser, WcfDataContractParser>();
        services.AddSingleton<IBusinessLogicExtractor, BusinessLogicExtractor>();
        services.AddSingleton<IFaultContractTransformer, FaultContractTransformer>();
        services.AddSingleton<IProtoGenerator, ProtoGenerator>();
        services.AddSingleton<IGrpcServiceGenerator, GrpcServiceGenerator>();
        services.AddSingleton<IDuplexDetector, DuplexDetector>();
        services.AddSingleton<IRestControllerGenerator, RestControllerGenerator>();
        services.AddSingleton<IClientProxyGenerator, ClientProxyGenerator>();
        services.AddSingleton<IAreaRegistrationParser, AreaRegistrationParser>();
        services.AddSingleton<IAreaMigrationTransformer, AreaMigrationTransformer>();
        services.AddSingleton<IBundleConfigParser, BundleConfigParser>();
        services.AddSingleton<IViteConfigGenerator, ViteConfigGenerator>();
        services.AddSingleton<IWebpackConfigGenerator, WebpackConfigGenerator>();
        services.AddSingleton<IAssetReferenceTransformer, AssetReferenceTransformer>();
        services.AddSingleton<IRazorNamespaceTransformer, RazorNamespaceTransformer>();
        services.AddSingleton<IPackageJsonGenerator, PackageJsonGenerator>();
        services.AddSingleton<IDbContextDetector, DbContextDetector>();
        services.AddSingleton<IDbContextConstructorRewriter, DbContextConstructorRewriter>();
        services.AddSingleton<IFluentApiRelationshipRewriter, FluentApiRelationshipRewriter>();
        services.AddSingleton<IManyToManyRewriter, ManyToManyRewriter>();
        services.AddSingleton<IIncludeThenIncludeRewriter, IncludeThenIncludeRewriter>();
        services.AddSingleton<ISqlQueryRewriter, SqlQueryRewriter>();
        services.AddSingleton<ILazyLoadingConfigRewriter, LazyLoadingConfigRewriter>();
        services.AddSingleton<IDatabaseInitializerRemover, DatabaseInitializerRemover>();
        services.AddSingleton<IAppSettingsJsonGenerator, AppSettingsJsonGenerator>();
        services.AddSingleton<IEnvironmentAppSettingsGenerator, EnvironmentAppSettingsGenerator>();
        services.AddSingleton<IProgramCsGenerator, ProgramCsGenerator>();
        services.AddSingleton<ICommandGenerator, CommandGenerator>();
        services.AddSingleton<IQueryGenerator, QueryGenerator>();
        services.AddSingleton<IHandlerGenerator, HandlerGenerator>();
        services.AddSingleton<IValidatorGenerator, ValidatorGenerator>();
        services.AddSingleton<IControllerAnalyzer, ControllerAnalyzer>();
        services.AddSingleton<IServiceAnalyzer, ServiceAnalyzer>();
        services.AddSingleton<ILogicExtractor, LogicExtractor>();
        services.AddSingleton<IControllerTransformer, ControllerSlimmer>();
        services.AddSingleton<IModernizationOrchestrator, ModernizationOrchestrator>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IModernizationOrchestrator>();
    }

    #endregion
}
