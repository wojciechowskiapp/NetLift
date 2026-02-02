using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetLift.Analysis;
using NetLift.Analysis.Config;
using NetLift.Analysis.Interfaces;
using NetLift.Analysis.Parsers;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Transforms;
using NetLift.Transforms.Converters;
using NetLift.Transforms.Ef.Analyzers;
using NetLift.Transforms.Ef.Rewriters;
using NetLift.Transforms.Generators;
using NetLift.Transforms.Mvc.Generators;
using NetLift.Transforms.Mvc.Parsers;
using NetLift.Transforms.Mvc.Rewriters;
using NetLift.Transforms.Services;
using NetLift.Transforms.Wcf.Analyzers;
using NetLift.Transforms.Wcf.Generators;
using NetLift.Transforms.Wcf.Parsers;
using NetLift.Transforms.Modernization;
using NetLift.Transforms.Modernization.Analyzers;
using NetLift.Transforms.Modernization.Generators;
using NetLift.Core.Interfaces.Modernization;

namespace NetLift.Tests.Integration.E2E;

/// <summary>
/// End-to-end tests for complete migration of mvc5-basic fixture.
/// Tests the full migration pipeline from .NET Framework to .NET 8+.
/// </summary>
[Collection("E2E")]
public class MigrationE2ETests : E2ETestBase
{
    private async Task ApplyChangesAsync(IReadOnlyList<FileChange> changes)
    {
        // Write file changes to disk (simulating what MigrateCommand does)
        foreach (var change in changes.Where(c => c.Type != ChangeType.Delete))
        {
            if (change.NewContent != null)
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(change.FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(change.FilePath, change.NewContent);
            }
        }

        // Handle deletions
        foreach (var change in changes.Where(c => c.Type == ChangeType.Delete))
        {
            if (File.Exists(change.FilePath))
            {
                File.Delete(change.FilePath);
            }
        }
    }

    private IProjectParser CreateProjectParser()
    {
        return new OldFormatProjectParser();
    }

    private async Task<ProjectInfo> ParseProjectAsync(string projectPath)
    {
        var parser = CreateProjectParser();
        return await parser.AnalyzeAsync(projectPath);
    }

    private IMigrationOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();

        // Register parsers
        services.AddSingleton<IProjectParser, OldFormatProjectParser>();
        services.AddSingleton<IPackagesConfigParser, PackagesConfigParser>();
        services.AddSingleton<IWebConfigAppSettingsParser, WebConfigAppSettingsParser>();
        services.AddSingleton<IWebConfigConnectionStringParser, WebConfigConnectionStringParser>();
        services.AddSingleton<ISystemWebParser, SystemWebParser>();
        services.AddSingleton<IServiceModelParser, ServiceModelParser>();

        // Register analysis services
        services.AddSingleton<IProjectTypeDetector, ProjectTypeDetector>();
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

        // Register MVC generators
        services.AddSingleton<IViewImportsGenerator, ViewImportsGenerator>();

        // Register P2: Area and Bundle services
        services.AddSingleton<IAreaRegistrationParser, AreaRegistrationParser>();
        services.AddSingleton<IAreaMigrationTransformer, AreaMigrationTransformer>();
        services.AddSingleton<IBundleConfigParser, BundleConfigParser>();
        services.AddSingleton<IViteConfigGenerator, ViteConfigGenerator>();
        services.AddSingleton<IWebpackConfigGenerator, WebpackConfigGenerator>();
        services.AddSingleton<IAssetReferenceTransformer, AssetReferenceTransformer>();
        services.AddSingleton<IRazorNamespaceTransformer, RazorNamespaceTransformer>();
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

        // Register orchestrator
        services.AddSingleton<IMigrationOrchestrator, MigrationOrchestrator>();

        // Register modernization services
        services.AddSingleton<IControllerAnalyzer, ControllerAnalyzer>();
        services.AddSingleton<IModernizationOrchestrator, ModernizationOrchestrator>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IMigrationOrchestrator>();
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_ProducesValidSdkProject()
    {
        // Arrange: Copy fixture to working directory
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Verify original project exists
        File.Exists(projectPath).Should().BeTrue("the mvc5-basic fixture project should exist");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions { DryRun = false },
            CancellationToken.None);

        // Assert: Migration should succeed
        result.Success.Should().BeTrue("migration should complete without errors");
        result.OverallConfidence.Should().BeGreaterThan(60, "migration should have reasonable confidence");
        result.Changes.Should().NotBeEmpty("migration should produce file changes");

        // Apply changes to disk (orchestrator only calculates, doesn't write)
        await ApplyChangesAsync(result.Changes);

        // Verify SDK-style project was created
        var projectContent = await File.ReadAllTextAsync(projectPath);
        projectContent.Should().Contain("<Project Sdk=\"Microsoft.NET.Sdk.Web\">",
            "project should be converted to SDK-style format");
        projectContent.Should().Contain("<TargetFramework>net8.0</TargetFramework>",
            "target framework should be set to net8.0");
        projectContent.Should().NotContain("<ProjectTypeGuids>",
            "old-style project type GUIDs should be removed");

        // Verify appsettings.json was generated
        var appSettingsPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "appsettings.json");
        File.Exists(appSettingsPath).Should().BeTrue("appsettings.json should be generated");

        // Verify Program.cs was generated
        var programPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Program.cs");
        File.Exists(programPath).Should().BeTrue("Program.cs should be generated for ASP.NET Core");
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_TransformsControllerCode()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions { DryRun = false },
            CancellationToken.None);

        // Assert: Controller transformations
        result.Success.Should().BeTrue();

        // Apply changes to disk
        await ApplyChangesAsync(result.Changes);

        var controllerPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Controllers", "HomeController.cs");
        if (File.Exists(controllerPath))
        {
            var controllerContent = await File.ReadAllTextAsync(controllerPath);

            // Should have ASP.NET Core namespace
            controllerContent.Should().Contain("Microsoft.AspNetCore.Mvc",
                "controller should use ASP.NET Core namespaces");

            // Should NOT have old namespace
            controllerContent.Should().NotContain("System.Web.Mvc",
                "old System.Web.Mvc namespace should be removed");
        }
    }

    [Fact]
    public async Task Migrate_DryRunMode_DoesNotModifyFiles()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");
        var originalContent = await File.ReadAllTextAsync(projectPath);
        var originalTimestamp = File.GetLastWriteTimeUtc(projectPath);

        // Act: Parse project first, then run migration in dry-run mode
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions { DryRun = true },
            CancellationToken.None);

        // Assert: Migration should analyze but not modify
        result.Success.Should().BeTrue("dry-run should complete successfully");
        result.Changes.Should().NotBeEmpty("dry-run should calculate changes");

        // File should not have changed
        var afterContent = await File.ReadAllTextAsync(projectPath);
        afterContent.Should().Be(originalContent, "dry-run should not modify files");

        var afterTimestamp = File.GetLastWriteTimeUtc(projectPath);
        afterTimestamp.Should().Be(originalTimestamp, "dry-run should not touch files");
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_GeneratesConfigurationFiles()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions
            {
                DryRun = false,
                TransformConfig = true
            },
            CancellationToken.None);

        // Assert: Configuration files should be generated
        result.Success.Should().BeTrue();

        // Apply changes to disk
        await ApplyChangesAsync(result.Changes);

        var projectDir = Path.GetDirectoryName(projectPath)!;

        // Verify appsettings.json
        var appSettingsPath = Path.Combine(projectDir, "appsettings.json");
        File.Exists(appSettingsPath).Should().BeTrue("appsettings.json should be generated");

        var appSettingsContent = await File.ReadAllTextAsync(appSettingsPath);
        appSettingsContent.Should().Contain("\"ConnectionStrings\":", "connection strings should be migrated");
        appSettingsContent.Should().NotBeEmpty();

        // Verify environment-specific files
        var devSettingsPath = Path.Combine(projectDir, "appsettings.Development.json");
        var prodSettingsPath = Path.Combine(projectDir, "appsettings.Production.json");

        if (File.Exists(devSettingsPath))
        {
            var devContent = await File.ReadAllTextAsync(devSettingsPath);
            devContent.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_GeneratesViewImports()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions
            {
                DryRun = false,
                GenerateViewImports = true
            },
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Apply changes to disk
        await ApplyChangesAsync(result.Changes);

        var viewImportsPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Views", "_ViewImports.cshtml");
        File.Exists(viewImportsPath).Should().BeTrue("_ViewImports.cshtml should be generated for MVC projects");

        var viewImportsContent = await File.ReadAllTextAsync(viewImportsPath);
        viewImportsContent.Should().Contain("@using", "_ViewImports should contain using directives");
        viewImportsContent.Should().Contain("@addTagHelper", "_ViewImports should register tag helpers");
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_ProducesComprehensiveDiagnostics()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions { DryRun = false },
            CancellationToken.None);

        // Assert: Diagnostics should provide detailed information
        result.Diagnostics.Should().NotBeEmpty("migration should produce diagnostic messages");

        // Should have informational messages
        result.Diagnostics.Should().Contain(d => d.Level == DiagnosticLevel.Info,
            "migration should provide informational messages");

        // Should report project type detection
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Detected project type"),
            "migration should report detected project type");

        // Should track progress
        result.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero,
            "migration should track execution time");

        result.FilesTransformed.Should().BeGreaterThan(0,
            "migration should report number of transformed files");
    }

    [Fact]
    public async Task Migrate_WithSourceCodeTransformationDisabled_SkipsControllerTransformation()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then disable source code transformation
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions
            {
                DryRun = false,
                TransformSourceCode = false
            },
            CancellationToken.None);

        // Assert: Project file should be migrated but source code should be untouched
        result.Success.Should().BeTrue();

        // Apply changes to disk
        await ApplyChangesAsync(result.Changes);

        // Project file should still be converted
        var projectContent = await File.ReadAllTextAsync(projectPath);
        projectContent.Should().Contain("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");

        // Controller file should not be modified
        var controllerPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Controllers", "HomeController.cs");
        if (File.Exists(controllerPath))
        {
            var controllerContent = await File.ReadAllTextAsync(controllerPath);
            // Original file should still have old namespace
            controllerContent.Should().Contain("System.Web.Mvc",
                "source code transformation was disabled, old namespaces should remain");
        }
    }

    [Fact]
    public async Task Migrate_Mvc5BasicFixture_TracksConfidenceScores()
    {
        // Arrange
        CopyFixtureToWorkingDirectory();
        var projectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");

        // Act: Parse project first, then migrate with ProjectInfo
        var projectInfo = await ParseProjectAsync(projectPath);
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.MigrateProjectAsync(
            projectInfo,
            "net8.0",
            new MigrationOptions { DryRun = false },
            CancellationToken.None);

        // Assert: Confidence tracking
        result.OverallConfidence.Should().BeInRange(0, 100,
            "confidence score should be valid percentage");

        // Each file change should have a confidence score
        foreach (var change in result.Changes)
        {
            change.Confidence.Should().BeInRange(0, 100,
                $"file change {change.FilePath} should have valid confidence score");
        }

        // High confidence changes should not generate manual tasks
        var highConfidenceChanges = result.Changes.Where(c => c.Confidence >= 80).ToList();
        if (highConfidenceChanges.Any())
        {
            // Manual tasks should primarily be for low confidence items
            result.ManualTasks.Count.Should().BeLessOrEqualTo(result.Changes.Count(c => c.Confidence < 60));
        }
    }
}
