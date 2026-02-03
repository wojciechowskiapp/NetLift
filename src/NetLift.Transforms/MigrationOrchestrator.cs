using System.Diagnostics;
using NetLift.Core.Interfaces;
using NetLift.Core.Interfaces.Razor;
using NetLift.Core.Interfaces.StaticFiles;
using NetLift.Core.Interfaces.SignalR;
using NetLift.Core.Interfaces.DependencyInjection;
using NetLift.Core.Models;
using NetLift.Core.Models.StaticFiles;

namespace NetLift.Transforms;

/// <summary>
/// Orchestrates the complete migration process for .NET Framework → .NET 8+ projects.
/// Coordinates all transformation phases and aggregates results.
/// </summary>
public class MigrationOrchestrator : IMigrationOrchestrator
{
    private readonly IProjectParser _projectParser;
    private readonly IProjectTypeDetector _projectTypeDetector;
    private readonly ISourceFileTransformer _sourceFileTransformer;
    private readonly IConfigMigrationService _configMigrationService;
    private readonly IViewImportsGenerator _viewImportsGenerator;
    private readonly ISdkProjectConverter _sdkProjectConverter;
    private readonly IWcfServiceParser _wcfServiceParser;
    private readonly IWcfDataContractParser _wcfDataContractParser;
    private readonly IDuplexDetector _duplexDetector;
    private readonly IProtoGenerator _protoGenerator;
    private readonly IGrpcServiceGenerator _grpcServiceGenerator;
    private readonly IRestControllerGenerator _restControllerGenerator;
    private readonly IDbContextDetector _dbContextDetector;

    // P2: Area and Bundle services
    private readonly IAreaRegistrationParser _areaRegistrationParser;
    private readonly IAreaMigrationTransformer _areaMigrationTransformer;
    private readonly IBundleConfigParser _bundleConfigParser;
    private readonly IViteConfigGenerator _viteConfigGenerator;
    private readonly IWebpackConfigGenerator _webpackConfigGenerator;
    private readonly IAssetReferenceTransformer _assetReferenceTransformer;
    private readonly IRazorNamespaceTransformer _razorNamespaceTransformer;
    private readonly IPackageJsonGenerator _packageJsonGenerator;

    // P3: Razor Views (HTML helpers → Tag Helpers)
    private readonly IRazorViewAnalyzer _razorViewAnalyzer;
    private readonly IRazorViewTransformer _razorViewTransformer;

    // P4: Static Files (Content → wwwroot)
    private readonly IStaticFilesAnalyzer _staticFilesAnalyzer;
    private readonly IStaticFilesMigrator _staticFilesMigrator;

    // P5: SignalR Hub transformation
    private readonly ISignalRHubAnalyzer _signalRHubAnalyzer;
    private readonly ISignalRHubTransformer _signalRHubTransformer;
    private readonly ISignalRStartupGenerator _signalRStartupGenerator;
    private readonly IGlobalHostAnalyzer _globalHostAnalyzer;

    // P6: DI Container transformation
    private readonly IDIContainerDetector _diContainerDetector;
    private readonly ILifetimeMapper _lifetimeMapper;
    private readonly IDIContainerAnalyzer _diContainerAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationOrchestrator"/> class.
    /// </summary>
    public MigrationOrchestrator(
        IProjectParser projectParser,
        IProjectTypeDetector projectTypeDetector,
        ISourceFileTransformer sourceFileTransformer,
        IConfigMigrationService configMigrationService,
        IViewImportsGenerator viewImportsGenerator,
        ISdkProjectConverter sdkProjectConverter,
        IWcfServiceParser wcfServiceParser,
        IWcfDataContractParser wcfDataContractParser,
        IDuplexDetector duplexDetector,
        IProtoGenerator protoGenerator,
        IGrpcServiceGenerator grpcServiceGenerator,
        IRestControllerGenerator restControllerGenerator,
        IDbContextDetector dbContextDetector,
        IAreaRegistrationParser areaRegistrationParser,
        IAreaMigrationTransformer areaMigrationTransformer,
        IBundleConfigParser bundleConfigParser,
        IViteConfigGenerator viteConfigGenerator,
        IWebpackConfigGenerator webpackConfigGenerator,
        IAssetReferenceTransformer assetReferenceTransformer,
        IRazorNamespaceTransformer razorNamespaceTransformer,
        IPackageJsonGenerator packageJsonGenerator,
        IRazorViewAnalyzer razorViewAnalyzer,
        IRazorViewTransformer razorViewTransformer,
        IStaticFilesAnalyzer staticFilesAnalyzer,
        IStaticFilesMigrator staticFilesMigrator,
        ISignalRHubAnalyzer signalRHubAnalyzer,
        ISignalRHubTransformer signalRHubTransformer,
        ISignalRStartupGenerator signalRStartupGenerator,
        IGlobalHostAnalyzer globalHostAnalyzer,
        IDIContainerDetector diContainerDetector,
        ILifetimeMapper lifetimeMapper,
        IDIContainerAnalyzer diContainerAnalyzer)
    {
        _projectParser = projectParser ?? throw new ArgumentNullException(nameof(projectParser));
        _projectTypeDetector = projectTypeDetector ?? throw new ArgumentNullException(nameof(projectTypeDetector));
        _sourceFileTransformer = sourceFileTransformer ?? throw new ArgumentNullException(nameof(sourceFileTransformer));
        _configMigrationService = configMigrationService ?? throw new ArgumentNullException(nameof(configMigrationService));
        _viewImportsGenerator = viewImportsGenerator ?? throw new ArgumentNullException(nameof(viewImportsGenerator));
        _sdkProjectConverter = sdkProjectConverter ?? throw new ArgumentNullException(nameof(sdkProjectConverter));
        _wcfServiceParser = wcfServiceParser ?? throw new ArgumentNullException(nameof(wcfServiceParser));
        _wcfDataContractParser = wcfDataContractParser ?? throw new ArgumentNullException(nameof(wcfDataContractParser));
        _duplexDetector = duplexDetector ?? throw new ArgumentNullException(nameof(duplexDetector));
        _protoGenerator = protoGenerator ?? throw new ArgumentNullException(nameof(protoGenerator));
        _grpcServiceGenerator = grpcServiceGenerator ?? throw new ArgumentNullException(nameof(grpcServiceGenerator));
        _restControllerGenerator = restControllerGenerator ?? throw new ArgumentNullException(nameof(restControllerGenerator));
        _dbContextDetector = dbContextDetector ?? throw new ArgumentNullException(nameof(dbContextDetector));
        _areaRegistrationParser = areaRegistrationParser ?? throw new ArgumentNullException(nameof(areaRegistrationParser));
        _areaMigrationTransformer = areaMigrationTransformer ?? throw new ArgumentNullException(nameof(areaMigrationTransformer));
        _bundleConfigParser = bundleConfigParser ?? throw new ArgumentNullException(nameof(bundleConfigParser));
        _viteConfigGenerator = viteConfigGenerator ?? throw new ArgumentNullException(nameof(viteConfigGenerator));
        _webpackConfigGenerator = webpackConfigGenerator ?? throw new ArgumentNullException(nameof(webpackConfigGenerator));
        _assetReferenceTransformer = assetReferenceTransformer ?? throw new ArgumentNullException(nameof(assetReferenceTransformer));
        _razorNamespaceTransformer = razorNamespaceTransformer ?? throw new ArgumentNullException(nameof(razorNamespaceTransformer));
        _packageJsonGenerator = packageJsonGenerator ?? throw new ArgumentNullException(nameof(packageJsonGenerator));
        _razorViewAnalyzer = razorViewAnalyzer ?? throw new ArgumentNullException(nameof(razorViewAnalyzer));
        _razorViewTransformer = razorViewTransformer ?? throw new ArgumentNullException(nameof(razorViewTransformer));
        _staticFilesAnalyzer = staticFilesAnalyzer ?? throw new ArgumentNullException(nameof(staticFilesAnalyzer));
        _staticFilesMigrator = staticFilesMigrator ?? throw new ArgumentNullException(nameof(staticFilesMigrator));
        _signalRHubAnalyzer = signalRHubAnalyzer ?? throw new ArgumentNullException(nameof(signalRHubAnalyzer));
        _signalRHubTransformer = signalRHubTransformer ?? throw new ArgumentNullException(nameof(signalRHubTransformer));
        _signalRStartupGenerator = signalRStartupGenerator ?? throw new ArgumentNullException(nameof(signalRStartupGenerator));
        _globalHostAnalyzer = globalHostAnalyzer ?? throw new ArgumentNullException(nameof(globalHostAnalyzer));
        _diContainerDetector = diContainerDetector ?? throw new ArgumentNullException(nameof(diContainerDetector));
        _lifetimeMapper = lifetimeMapper ?? throw new ArgumentNullException(nameof(lifetimeMapper));
        _diContainerAnalyzer = diContainerAnalyzer ?? throw new ArgumentNullException(nameof(diContainerAnalyzer));
    }

    /// <inheritdoc/>
    public async Task<MigrationResult> MigrateProjectAsync(
        ProjectInfo projectInfo,
        string targetFramework,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentNullException.ThrowIfNull(options);

        var projectPath = projectInfo.FilePath;
        var stopwatch = Stopwatch.StartNew();
        var changes = new List<FileChange>();
        var diagnostics = new List<MigrationDiagnostic>();
        var manualTasks = new List<string>();
        var confidenceScores = new List<int>();

        try
        {
            // Phase 1: Use pre-parsed project info (no re-parsing needed)
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Starting migration of {Path.GetFileName(projectPath)} to {targetFramework}",
                FilePath = projectPath
            });

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Using pre-parsed project info with {projectInfo.CompileItems.Count} compile items",
                FilePath = projectPath
            });

            var projectType = _projectTypeDetector.Detect(projectInfo);

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Detected project type: {projectType.PrimaryType}",
                FilePath = projectPath
            });

            var projectDir = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrEmpty(projectDir))
            {
                throw new ArgumentException($"Invalid path - cannot determine directory: {projectPath}", nameof(projectInfo));
            }

            // Phase 2: Convert project to SDK-style format (if not already converted)
            // Check if the current project file is already SDK-style by reading it
            var currentProjectContent = await File.ReadAllTextAsync(projectPath, cancellationToken);
            var isAlreadySdkStyle = currentProjectContent.Contains("<Project Sdk=", StringComparison.OrdinalIgnoreCase);

            if (!isAlreadySdkStyle && projectInfo.Format == ProjectFormat.OldStyle)
            {
                var sdkProject = _sdkProjectConverter.Convert(projectInfo, targetFramework);

                changes.Add(new FileChange
                {
                    FilePath = projectPath,
                    Type = ChangeType.Modify,
                    OriginalContent = currentProjectContent,
                    NewContent = sdkProject.ToString(),
                    Confidence = 95,
                    Description = "Converted project to SDK-style format"
                });

                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = "Converted project to SDK-style format",
                    FilePath = projectPath
                });
            }
            else if (isAlreadySdkStyle)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = "Project is already SDK-style, skipping conversion",
                    FilePath = projectPath
                });
            }

            // Phase 3: Transform C# source files
            if (options.TransformSourceCode && projectInfo.CompileItems.Any())
            {
                await TransformSourceFilesAsync(
                    projectInfo,
                    projectType,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 3.5: Migrate WCF services
            if (options.TransformWcfServices && projectType.IsWcfService.Detected)
            {
                await MigrateWcfServicesAsync(
                    projectDir,
                    projectInfo,
                    projectType,
                    options.WcfTarget,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 4: Migrate configuration files
            if (options.TransformConfig)
            {
                await MigrateConfigurationAsync(
                    projectDir,
                    targetFramework,
                    changes,
                    diagnostics,
                    cancellationToken);
            }

            // Phase 5: Generate _ViewImports.cshtml for MVC projects
            if (options.GenerateViewImports && projectType.IsMvc.Detected)
            {
                GenerateViewImportsFile(
                    projectDir,
                    projectInfo.RootNamespace ?? projectInfo.Name,
                    projectInfo,
                    changes,
                    diagnostics);
            }

            // Phase 5.5: Transform Razor view namespaces (PagedList → X.PagedList, etc.)
            if (projectType.IsMvc.Detected)
            {
                await TransformRazorNamespacesAsync(
                    projectDir,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 6: Migrate MVC Areas
            if (options.TransformAreas && projectType.IsMvc.Detected)
            {
                await MigrateAreasAsync(
                    projectDir,
                    projectInfo,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 7: Migrate BundleConfig to modern asset pipeline
            if (options.TransformBundles && projectType.IsMvc.Detected)
            {
                await MigrateBundlesAsync(
                    projectDir,
                    projectInfo,
                    options.BundleTarget,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 8: Transform Razor Views (HTML helpers → Tag Helpers)
            if (options.TransformRazorViews && projectType.IsMvc.Detected)
            {
                await TransformRazorViewsAsync(
                    projectDir,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 9: Migrate Static Files (Content/Scripts → wwwroot)
            // Console.Error.WriteLine("[DEBUG] Starting Phase 9: Static Files");
            if (options.TransformStaticFiles && projectType.IsMvc.Detected)
            {
                await MigrateStaticFilesAsync(
                    projectDir,
                    changes,
                    diagnostics,
                    confidenceScores,
                    options.DryRun,
                    cancellationToken);
            }
            // Console.Error.WriteLine("[DEBUG] Phase 9 completed");

            // Phase 10: Transform SignalR Hubs (detect from package references)
            // Console.Error.WriteLine("[DEBUG] Starting Phase 10: SignalR");
            var hasSignalR = projectInfo.PackageReferences.Any(p =>
                p.Id.Contains("SignalR", StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals("Microsoft.AspNet.SignalR", StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals("Microsoft.AspNet.SignalR.Core", StringComparison.OrdinalIgnoreCase));
            if (options.TransformSignalR && hasSignalR)
            {
                await TransformSignalRAsync(
                    projectDir,
                    projectInfo,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }

            // Phase 11: Transform DI Container registrations
            // Console.Error.WriteLine("[DEBUG] Starting Phase 11: DI");
            if (options.TransformDependencyInjection)
            {
                await TransformDependencyInjectionAsync(
                    projectDir,
                    projectInfo,
                    changes,
                    diagnostics,
                    confidenceScores,
                    cancellationToken);
            }
            // Console.Error.WriteLine("[DEBUG] Phase 11 completed");

            // Phase 12: Generate manual tasks for low-confidence transformations
            // Console.Error.WriteLine("[DEBUG] Starting Phase 12: Manual Tasks");
            foreach (var change in changes.Where(c => c.Confidence < 60))
            {
                manualTasks.Add($"Manual review required: {change.FilePath} (Confidence: {change.Confidence}%)");
            }

            // Console.Error.WriteLine("[DEBUG] Phase 12 completed");

            // Calculate overall confidence
            var overallConfidence = confidenceScores.Any()
                ? (int)confidenceScores.Average()
                : 0;

            stopwatch.Stop();
            // Console.Error.WriteLine("[DEBUG] All phases completed, returning result");

            return new MigrationResult
            {
                Success = !diagnostics.Any(d => d.Level == DiagnosticLevel.Error),
                Changes = changes.AsReadOnly(),
                Diagnostics = diagnostics.AsReadOnly(),
                OverallConfidence = overallConfidence,
                ManualTasks = manualTasks.AsReadOnly(),
                FilesTransformed = changes.Count,
                ElapsedTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Error,
                Message = $"Migration failed: {ex.Message}",
                FilePath = projectPath,
                Code = "MIG001"
            });

            return new MigrationResult
            {
                Success = false,
                Changes = changes.AsReadOnly(),
                Diagnostics = diagnostics.AsReadOnly(),
                OverallConfidence = 0,
                ManualTasks = manualTasks.AsReadOnly(),
                FilesTransformed = 0,
                ElapsedTime = stopwatch.Elapsed
            };
        }
    }

    private async Task TransformSourceFilesAsync(
        ProjectInfo projectInfo,
        ProjectTypeResult projectType,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath);
        if (string.IsNullOrEmpty(projectDir))
        {
            throw new InvalidOperationException($"Invalid project path - cannot determine directory: {projectInfo.FilePath}");
        }

        // PRE-SCAN PHASE: Detect all DbContext types across all source files
        // This enables accurate detection in controllers that reference DbContext with non-standard names
        var knownDbContextTypes = await DetectDbContextTypesAsync(projectInfo, projectDir, cancellationToken);

        if (knownDbContextTypes.Count > 0)
        {
            _sourceFileTransformer.SetKnownDbContextTypes(knownDbContextTypes);
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Detected {knownDbContextTypes.Count} DbContext type(s): {string.Join(", ", knownDbContextTypes)}",
                Code = "MIG_DBCONTEXT"
            });
        }

        // TRANSFORM PHASE: Process all source files
        foreach (var compileItem in projectInfo.CompileItems)
        {
            var sourceFilePath = Path.IsPathRooted(compileItem.Include)
                ? compileItem.Include
                : Path.Combine(projectDir, compileItem.Include);

            if (!File.Exists(sourceFilePath))
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Source file not found: {sourceFilePath}",
                    FilePath = sourceFilePath,
                    Code = "MIG002"
                });
                continue;
            }

            try
            {
                var originalContent = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
                var transformResult = await _sourceFileTransformer.TransformAsync(
                    sourceFilePath,
                    originalContent,
                    SourceFileType.Unknown, // Auto-detect
                    cancellationToken);

                // Only add change if content actually changed
                if (transformResult.Changed)
                {
                    changes.Add(new FileChange
                    {
                        FilePath = sourceFilePath,
                        Type = ChangeType.Modify,
                        OriginalContent = originalContent,
                        NewContent = transformResult.TransformedCode,
                        Confidence = transformResult.Confidence,
                        Description = $"Transformed C# source file with {transformResult.Confidence}% confidence ({string.Join(", ", transformResult.AppliedTransformers)})"
                    });

                    confidenceScores.Add(transformResult.Confidence);
                }

                diagnostics.AddRange(transformResult.Diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Error,
                    Message = $"Failed to transform {sourceFilePath}: {ex.Message}",
                    FilePath = sourceFilePath,
                    Code = "MIG003"
                });
            }
        }
    }

    /// <summary>
    /// Pre-scans all source files to detect DbContext types using inheritance analysis.
    /// This enables accurate detection in controllers that reference DbContext with non-standard names.
    /// </summary>
    private async Task<HashSet<string>> DetectDbContextTypesAsync(
        ProjectInfo projectInfo,
        string projectDir,
        CancellationToken cancellationToken)
    {
        var knownDbContextTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var compileItem in projectInfo.CompileItems)
        {
            var sourceFilePath = Path.IsPathRooted(compileItem.Include)
                ? compileItem.Include
                : Path.Combine(projectDir, compileItem.Include);

            if (!File.Exists(sourceFilePath))
            {
                continue;
            }

            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
                var detectedContexts = _dbContextDetector.Detect(sourceCode);

                foreach (var context in detectedContexts)
                {
                    knownDbContextTypes.Add(context.ClassName);
                }
            }
            catch (FileNotFoundException)
            {
                // File was deleted between enumeration and read, skip silently
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied, skip this file - errors will be reported during main transformation phase
            }
            catch (IOException)
            {
                // I/O error reading file, skip - errors will be reported during main transformation phase
            }
        }

        return knownDbContextTypes;
    }

    private async Task MigrateConfigurationAsync(
        string projectDir,
        string targetFramework,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // Find web.config with case-insensitive search for Linux compatibility
        var webConfigPath = FindWebConfigCaseInsensitive(projectDir);

        if (webConfigPath != null)
        {
            try
            {
                var configResult = await _configMigrationService.MigrateConfigAsync(
                    projectDir,
                    targetFramework,
                    cancellationToken);

                if (configResult.Success)
                {
                    changes.AddRange(configResult.GeneratedFiles);

                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Migrated web.config to {configResult.GeneratedFiles.Count} configuration file(s) with {configResult.Confidence}% confidence",
                        FilePath = webConfigPath
                    });

                    // Add diagnostic messages from config migration
                    foreach (var diagnostic in configResult.Diagnostics)
                    {
                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Info,
                            Message = diagnostic,
                            FilePath = webConfigPath
                        });
                    }
                }
                else
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Error,
                        Message = "Configuration migration failed",
                        FilePath = webConfigPath,
                        Code = "MIG004"
                    });
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Error,
                    Message = $"Failed to migrate configuration: {ex.Message}",
                    FilePath = webConfigPath,
                    Code = "MIG004"
                });
            }
        }
        else
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "No web.config found, skipping configuration migration",
                FilePath = projectDir
            });
        }
    }

    /// <summary>
    /// Finds web.config with case-insensitive search for Linux compatibility.
    /// </summary>
    private static string? FindWebConfigCaseInsensitive(string projectDirectory)
    {
        // First try exact matches
        var exactPath = Path.Combine(projectDirectory, "web.config");
        if (File.Exists(exactPath))
            return exactPath;

        exactPath = Path.Combine(projectDirectory, "Web.config");
        if (File.Exists(exactPath))
            return exactPath;

        // Fall back to case-insensitive enumeration
        try
        {
            var files = Directory.GetFiles(projectDirectory, "*", SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault(f =>
                Path.GetFileName(f).Equals("web.config", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private void GenerateViewImportsFile(
        string projectDir,
        string rootNamespace,
        ProjectInfo projectInfo,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics)
    {
        var viewsDir = Path.Combine(projectDir, "Views");

        if (!Directory.Exists(viewsDir))
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Warning,
                Message = "Views directory not found, skipping _ViewImports.cshtml generation",
                FilePath = projectDir
            });
            return;
        }

        // Detect additional namespaces needed based on package/assembly references
        DetectAndAddRequiredNamespaces(projectInfo, diagnostics);

        var viewImportsPath = Path.Combine(viewsDir, "_ViewImports.cshtml");
        var viewImportsContent = _viewImportsGenerator.Generate(rootNamespace);

        changes.Add(new FileChange
        {
            FilePath = viewImportsPath,
            Type = File.Exists(viewImportsPath) ? ChangeType.Modify : ChangeType.Create,
            OriginalContent = File.Exists(viewImportsPath) ? File.ReadAllText(viewImportsPath) : null,
            NewContent = viewImportsContent,
            Confidence = 95,
            Description = "Generated _ViewImports.cshtml for ASP.NET Core MVC"
        });

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Generated _ViewImports.cshtml",
            FilePath = viewImportsPath
        });
    }

    /// <summary>
    /// Detects required namespaces based on package/assembly references and adds them to the ViewImportsGenerator.
    /// </summary>
    private void DetectAndAddRequiredNamespaces(ProjectInfo projectInfo, List<MigrationDiagnostic> diagnostics)
    {
        // Detect PagedList usage (assembly reference or package reference)
        var hasPagedList = projectInfo.References.Any(r =>
            r.Name.Equals("PagedList", StringComparison.OrdinalIgnoreCase) ||
            r.Name.Equals("PagedList.Mvc", StringComparison.OrdinalIgnoreCase)) ||
            projectInfo.PackageReferences.Any(p =>
            p.Id.Equals("PagedList", StringComparison.OrdinalIgnoreCase) ||
            p.Id.Equals("PagedList.Mvc", StringComparison.OrdinalIgnoreCase));

        if (hasPagedList)
        {
            // PagedList → X.PagedList mapping is handled by MvcNamespaceMappings
            _viewImportsGenerator.AddNamespace("PagedList");
            _viewImportsGenerator.AddNamespace("PagedList.Mvc");
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "Detected PagedList usage, adding X.PagedList namespace to _ViewImports.cshtml"
            });
        }
    }

    private async Task MigrateWcfServicesAsync(
        string projectDir,
        ProjectInfo projectInfo,
        ProjectTypeResult projectType,
        MigrationTarget wcfTarget,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Starting WCF service migration (Target: {wcfTarget})",
            FilePath = projectDir
        });

        // Collect all WCF service contracts and data contracts
        var allServiceContracts = new List<Core.Models.Wcf.WcfServiceContract>();
        var allDataContracts = new List<Core.Models.Wcf.WcfDataContract>();

        // Parse all C# source files for WCF contracts
        foreach (var compileItem in projectInfo.CompileItems)
        {
            var sourceFilePath = Path.IsPathRooted(compileItem.Include)
                ? compileItem.Include
                : Path.Combine(projectDir, compileItem.Include);

            if (!File.Exists(sourceFilePath))
            {
                continue;
            }

            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);

                // Parse service contracts
                var serviceContracts = _wcfServiceParser.Parse(sourceCode);
                if (serviceContracts.Any())
                {
                    allServiceContracts.AddRange(serviceContracts);

                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Found {serviceContracts.Count} WCF service contract(s) in {Path.GetFileName(sourceFilePath)}",
                        FilePath = sourceFilePath
                    });
                }

                // Parse data contracts
                var dataContracts = _wcfDataContractParser.Parse(sourceCode);
                if (dataContracts.Any())
                {
                    allDataContracts.AddRange(dataContracts);

                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Found {dataContracts.Count} WCF data contract(s) in {Path.GetFileName(sourceFilePath)}",
                        FilePath = sourceFilePath
                    });
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to parse WCF contracts in {sourceFilePath}: {ex.Message}",
                    FilePath = sourceFilePath,
                    Code = "WCF001"
                });
            }
        }

        if (!allServiceContracts.Any())
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "No WCF service contracts found in project",
                FilePath = projectDir
            });
            return;
        }

        // Check for duplex patterns
        var duplexReport = _duplexDetector.Detect(allServiceContracts, null);
        if (duplexReport.HasDuplexContracts)
        {
            foreach (var warning in duplexReport.Warnings)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Duplex pattern detected in '{warning.ServiceName}': Callback contract '{warning.CallbackContractName}' - Requires manual migration",
                    FilePath = projectDir,
                    Code = "WCF002"
                });
            }

            // Add manual tasks for duplex services
            foreach (var contract in allServiceContracts.Where(c => !string.IsNullOrEmpty(c.CallbackContract)))
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Service '{contract.InterfaceName}' uses duplex pattern (callback: {contract.CallbackContract}). " +
                              "Consider migrating to SignalR, gRPC streaming, or WebSockets for bidirectional communication.",
                    FilePath = projectDir,
                    Code = "WCF003"
                });
            }
        }

        // Migrate each service contract
        var targetNamespace = projectInfo.RootNamespace ?? projectInfo.Name;

        foreach (var serviceContract in allServiceContracts)
        {
            // Skip duplex services
            if (!string.IsNullOrEmpty(serviceContract.CallbackContract))
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = $"Skipping duplex service '{serviceContract.InterfaceName}' - requires manual migration",
                    FilePath = projectDir
                });
                continue;
            }

            try
            {
                // Generate gRPC artifacts
                if (wcfTarget is MigrationTarget.Grpc or MigrationTarget.GrpcAndRest)
                {
                    await GenerateGrpcArtifactsAsync(
                        projectDir,
                        serviceContract,
                        allDataContracts,
                        targetNamespace,
                        changes,
                        diagnostics,
                        confidenceScores,
                        cancellationToken);
                }

                // Generate REST controller
                if (wcfTarget is MigrationTarget.Rest or MigrationTarget.GrpcAndRest)
                {
                    await GenerateRestControllerAsync(
                        projectDir,
                        serviceContract,
                        targetNamespace,
                        changes,
                        diagnostics,
                        confidenceScores,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Error,
                    Message = $"Failed to migrate WCF service '{serviceContract.InterfaceName}': {ex.Message}",
                    FilePath = projectDir,
                    Code = "WCF004"
                });
            }
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Completed WCF service migration. Migrated {allServiceContracts.Count(c => string.IsNullOrEmpty(c.CallbackContract))} service(s), skipped {allServiceContracts.Count(c => !string.IsNullOrEmpty(c.CallbackContract))} duplex service(s)",
            FilePath = projectDir
        });
    }

    private async Task GenerateGrpcArtifactsAsync(
        string projectDir,
        Core.Models.Wcf.WcfServiceContract serviceContract,
        IReadOnlyList<Core.Models.Wcf.WcfDataContract> dataContracts,
        string targetNamespace,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        // Generate .proto file
        var protoFile = _protoGenerator.Generate(serviceContract, dataContracts);
        var protoDir = Path.Combine(projectDir, "Protos");
        var protoPath = Path.Combine(protoDir, protoFile.FileName);

        changes.Add(new FileChange
        {
            FilePath = protoPath,
            Type = ChangeType.Create,
            OriginalContent = null,
            NewContent = protoFile.Content,
            Confidence = _protoGenerator.ConfidenceScore,
            Description = $"Generated .proto file for WCF service '{serviceContract.InterfaceName}'"
        });

        confidenceScores.Add(_protoGenerator.ConfidenceScore);

        // Add diagnostics from proto generation
        foreach (var diagnostic in _protoGenerator.Diagnostics)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = diagnostic,
                FilePath = protoPath
            });
        }

        // Generate gRPC service implementation
        var grpcService = _grpcServiceGenerator.Generate(serviceContract, targetNamespace);
        var servicesDir = Path.Combine(projectDir, "Services", "Grpc");
        var serviceFileName = $"{grpcService.ClassName}.cs";
        var servicePath = Path.Combine(servicesDir, serviceFileName);

        changes.Add(new FileChange
        {
            FilePath = servicePath,
            Type = ChangeType.Create,
            OriginalContent = null,
            NewContent = grpcService.ServiceCode,
            Confidence = _grpcServiceGenerator.ConfidenceScore,
            Description = $"Generated gRPC service implementation for '{serviceContract.InterfaceName}'"
        });

        confidenceScores.Add(_grpcServiceGenerator.ConfidenceScore);

        // Add diagnostics from gRPC generation
        foreach (var diagnostic in _grpcServiceGenerator.Diagnostics)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = diagnostic,
                FilePath = servicePath
            });
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Generated gRPC artifacts for '{serviceContract.InterfaceName}' with {_grpcServiceGenerator.ConfidenceScore}% confidence",
            FilePath = servicePath
        });

        await Task.CompletedTask;
    }

    private async Task GenerateRestControllerAsync(
        string projectDir,
        Core.Models.Wcf.WcfServiceContract serviceContract,
        string targetNamespace,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        var restController = _restControllerGenerator.Generate(serviceContract, targetNamespace);
        var controllersDir = Path.Combine(projectDir, "Controllers");
        var controllerFileName = $"{restController.ClassName}.cs";
        var controllerPath = Path.Combine(controllersDir, controllerFileName);

        changes.Add(new FileChange
        {
            FilePath = controllerPath,
            Type = ChangeType.Create,
            OriginalContent = null,
            NewContent = restController.ControllerCode,
            Confidence = _restControllerGenerator.ConfidenceScore,
            Description = $"Generated REST API controller for WCF service '{serviceContract.InterfaceName}'"
        });

        confidenceScores.Add(_restControllerGenerator.ConfidenceScore);

        // Add diagnostics from REST generation
        foreach (var diagnostic in _restControllerGenerator.Diagnostics)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = diagnostic,
                FilePath = controllerPath
            });
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Generated REST controller for '{serviceContract.InterfaceName}' with {_restControllerGenerator.ConfidenceScore}% confidence",
            FilePath = controllerPath
        });

        await Task.CompletedTask;
    }

    private async Task TransformRazorNamespacesAsync(
        string projectDir,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Transforming Razor view namespaces (PagedList → X.PagedList)",
            FilePath = projectDir
        });

        var viewsDir = Path.Combine(projectDir, "Views");
        if (!Directory.Exists(viewsDir))
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "Views directory not found, skipping Razor namespace transformation",
                FilePath = projectDir
            });
            return;
        }

        var razorFiles = Directory.GetFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);

        foreach (var razorFile in razorFiles)
        {
            try
            {
                var originalContent = await File.ReadAllTextAsync(razorFile, cancellationToken);
                var transformedContent = _razorNamespaceTransformer.TransformRazorView(originalContent);

                if (transformedContent != originalContent)
                {
                    changes.Add(new FileChange
                    {
                        FilePath = razorFile,
                        Type = ChangeType.Modify,
                        OriginalContent = originalContent,
                        NewContent = transformedContent,
                        Confidence = 95,
                        Description = "Transformed PagedList namespaces to X.PagedList"
                    });
                    confidenceScores.Add(95);

                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Transformed namespaces in {Path.GetFileName(razorFile)}",
                        FilePath = razorFile
                    });
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to transform namespaces in {razorFile}: {ex.Message}",
                    FilePath = razorFile,
                    Code = "RAZOR001"
                });
            }
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Completed Razor namespace transformation for {razorFiles.Length} view(s)",
            FilePath = projectDir
        });
    }

    private async Task MigrateAreasAsync(
        string projectDir,
        ProjectInfo projectInfo,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Scanning for MVC Areas (AreaRegistration classes)",
            FilePath = projectDir
        });

        var areaDefinitions = new List<Core.Models.Mvc.AreaDefinition>();
        var rootNamespace = projectInfo.RootNamespace ?? projectInfo.Name;

        // Scan source files for AreaRegistration classes
        foreach (var compileItem in projectInfo.CompileItems)
        {
            var sourceFilePath = Path.IsPathRooted(compileItem.Include)
                ? compileItem.Include
                : Path.Combine(projectDir, compileItem.Include);

            if (!File.Exists(sourceFilePath))
                continue;

            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
                var areas = _areaRegistrationParser.Parse(sourceCode);

                if (areas.Any())
                {
                    areaDefinitions.AddRange(areas);
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Found {areas.Count} MVC Area(s) in {Path.GetFileName(sourceFilePath)}",
                        FilePath = sourceFilePath
                    });
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to parse areas in {sourceFilePath}: {ex.Message}",
                    FilePath = sourceFilePath,
                    Code = "AREA001"
                });
            }
        }

        if (!areaDefinitions.Any())
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "No MVC Areas found in project",
                FilePath = projectDir
            });
            return;
        }

        // Migrate each area
        foreach (var area in areaDefinitions)
        {
            try
            {
                var migrationPlan = _areaMigrationTransformer.CreateMigrationPlan(area, projectDir, rootNamespace);

                // Add folder creation changes (as info only - actual folder creation happens during apply)
                foreach (var folder in migrationPlan.FoldersToCreate)
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Will create folder: {folder}",
                        FilePath = folder
                    });
                }

                // Add generated files
                foreach (var file in migrationPlan.FilesToGenerate)
                {
                    var filePath = file.Key;
                    var fileContent = file.Value;
                    changes.Add(new FileChange
                    {
                        FilePath = filePath,
                        Type = File.Exists(filePath) ? ChangeType.Modify : ChangeType.Create,
                        OriginalContent = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath, cancellationToken) : null,
                        NewContent = fileContent,
                        Confidence = 95,
                        Description = $"Generated file for Area '{area.Name}'"
                    });
                    confidenceScores.Add(95);
                }

                // Transform controllers to add [Area] attribute
                foreach (var controllerPath in migrationPlan.ControllersToUpdate)
                {
                    if (File.Exists(controllerPath))
                    {
                        var originalContent = await File.ReadAllTextAsync(controllerPath, cancellationToken);
                        var transformedContent = _areaMigrationTransformer.AddAreaAttribute(originalContent, area.Name);

                        if (transformedContent != originalContent)
                        {
                            changes.Add(new FileChange
                            {
                                FilePath = controllerPath,
                                Type = ChangeType.Modify,
                                OriginalContent = originalContent,
                                NewContent = transformedContent,
                                Confidence = 95,
                                Description = $"Added [Area(\"{area.Name}\")] attribute to controller"
                            });
                            confidenceScores.Add(95);
                        }
                    }
                }

                // Add route registration info
                if (!string.IsNullOrEmpty(migrationPlan.RouteRegistration))
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Add to Program.cs: {migrationPlan.RouteRegistration}",
                        FilePath = projectDir,
                        Code = "AREA_ROUTE"
                    });
                }

                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = $"Migrated Area '{area.Name}' with {migrationPlan.FilesToGenerate.Count} file(s) and {migrationPlan.ControllersToUpdate.Count} controller(s)",
                    FilePath = projectDir
                });
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Error,
                    Message = $"Failed to migrate Area '{area.Name}': {ex.Message}",
                    FilePath = projectDir,
                    Code = "AREA002"
                });
            }
        }
    }

    private async Task MigrateBundlesAsync(
        string projectDir,
        ProjectInfo projectInfo,
        BundleTarget bundleTarget,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Scanning for BundleConfig.cs (target: {bundleTarget})",
            FilePath = projectDir
        });

        // Look for BundleConfig.cs in App_Start folder
        var bundleConfigPath = Path.Combine(projectDir, "App_Start", "BundleConfig.cs");
        if (!File.Exists(bundleConfigPath))
        {
            // Try without App_Start
            bundleConfigPath = Path.Combine(projectDir, "BundleConfig.cs");
        }

        if (!File.Exists(bundleConfigPath))
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "No BundleConfig.cs found in project",
                FilePath = projectDir
            });
            return;
        }

        try
        {
            var bundleConfigContent = await File.ReadAllTextAsync(bundleConfigPath, cancellationToken);
            var bundles = _bundleConfigParser.Parse(bundleConfigContent);

            if (!bundles.Any())
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = "No bundles found in BundleConfig.cs",
                    FilePath = bundleConfigPath
                });
                return;
            }

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Found {bundles.Count} bundle(s) in BundleConfig.cs",
                FilePath = bundleConfigPath
            });

            // Generate build tool configuration
            string buildConfig;
            string buildConfigFileName;

            if (bundleTarget == BundleTarget.Vite)
            {
                buildConfig = _viteConfigGenerator.Generate(bundles);
                buildConfigFileName = "vite.config.js";
            }
            else
            {
                buildConfig = _webpackConfigGenerator.Generate(bundles);
                buildConfigFileName = "webpack.config.js";
            }

            var buildConfigPath = Path.Combine(projectDir, buildConfigFileName);
            changes.Add(new FileChange
            {
                FilePath = buildConfigPath,
                Type = File.Exists(buildConfigPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = File.Exists(buildConfigPath) ? await File.ReadAllTextAsync(buildConfigPath, cancellationToken) : null,
                NewContent = buildConfig,
                Confidence = 90,
                Description = $"Generated {buildConfigFileName} from BundleConfig.cs"
            });
            confidenceScores.Add(90);

            // Generate package.json
            var packageJsonPath = Path.Combine(projectDir, "package.json");
            var packageJsonContent = _packageJsonGenerator.Generate(bundleTarget == BundleTarget.Vite);

            changes.Add(new FileChange
            {
                FilePath = packageJsonPath,
                Type = File.Exists(packageJsonPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = File.Exists(packageJsonPath) ? await File.ReadAllTextAsync(packageJsonPath, cancellationToken) : null,
                NewContent = packageJsonContent,
                Confidence = 95,
                Description = "Generated package.json with build dependencies"
            });
            confidenceScores.Add(95);

            // Transform asset references in Razor views
            var viewsDir = Path.Combine(projectDir, "Views");
            if (Directory.Exists(viewsDir))
            {
                var razorFiles = Directory.GetFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);

                foreach (var razorFile in razorFiles)
                {
                    try
                    {
                        var originalContent = await File.ReadAllTextAsync(razorFile, cancellationToken);
                        var transformedContent = _assetReferenceTransformer.TransformRazorView(originalContent, bundles);

                        if (transformedContent != originalContent)
                        {
                            changes.Add(new FileChange
                            {
                                FilePath = razorFile,
                                Type = ChangeType.Modify,
                                OriginalContent = originalContent,
                                NewContent = transformedContent,
                                Confidence = 85,
                                Description = "Transformed @Styles.Render/@Scripts.Render to modern asset references"
                            });
                            confidenceScores.Add(85);
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Warning,
                            Message = $"Failed to transform asset references in {razorFile}: {ex.Message}",
                            FilePath = razorFile,
                            Code = "BUNDLE001"
                        });
                    }
                }
            }

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Migrated {bundles.Count} bundle(s) to {bundleTarget} configuration",
                FilePath = projectDir
            });

            // Add manual task for npm install
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "Run 'npm install' after migration to install build dependencies",
                FilePath = projectDir,
                Code = "BUNDLE_NPM"
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Error,
                Message = $"Failed to migrate BundleConfig: {ex.Message}",
                FilePath = bundleConfigPath,
                Code = "BUNDLE002"
            });
        }
    }

    private async Task TransformRazorViewsAsync(
        string projectDir,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Transforming Razor views (HTML helpers → Tag Helpers)",
            FilePath = projectDir
        });

        var viewsDir = Path.Combine(projectDir, "Views");
        if (!Directory.Exists(viewsDir))
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "Views directory not found, skipping Razor view transformation",
                FilePath = projectDir
            });
            return;
        }

        var razorFiles = Directory.GetFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);
        var transformedCount = 0;

        foreach (var razorFile in razorFiles)
        {
            try
            {
                // Check if there's already a pending change for this file (e.g., from namespace transformation)
                var existingChange = changes.FirstOrDefault(c => c.FilePath == razorFile && c.Type == ChangeType.Modify);
                var originalContent = existingChange?.NewContent ?? await File.ReadAllTextAsync(razorFile, cancellationToken);

                // Analyze the view for HTML helpers
                var htmlHelpers = _razorViewAnalyzer.DetectHtmlHelpers(originalContent);

                if (htmlHelpers.Count > 0)
                {
                    // Transform the view
                    var transformedContent = _razorViewTransformer.TransformContent(originalContent, razorFile);

                    if (transformedContent != originalContent)
                    {
                        // If there was an existing change, replace it with a combined change
                        if (existingChange != null)
                        {
                            changes.Remove(existingChange);
                            changes.Add(new FileChange
                            {
                                FilePath = razorFile,
                                Type = ChangeType.Modify,
                                OriginalContent = existingChange.OriginalContent,
                                NewContent = transformedContent,
                                Confidence = Math.Min(existingChange.Confidence, 90),
                                Description = $"{existingChange.Description}; transformed {htmlHelpers.Count} HTML helper(s)"
                            });
                        }
                        else
                        {
                            changes.Add(new FileChange
                            {
                                FilePath = razorFile,
                                Type = ChangeType.Modify,
                                OriginalContent = originalContent,
                                NewContent = transformedContent,
                                Confidence = 90, // High confidence for regex-based transformation
                                Description = $"Transformed {htmlHelpers.Count} HTML helper(s) to Tag Helpers"
                            });
                        }
                        confidenceScores.Add(90);
                        transformedCount++;

                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Info,
                            Message = $"Transformed {htmlHelpers.Count} HTML helper(s) in {Path.GetFileName(razorFile)}",
                            FilePath = razorFile
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to transform Razor view {razorFile}: {ex.Message}",
                    FilePath = razorFile,
                    Code = "RAZOR002"
                });
            }
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Completed Razor view transformation: {transformedCount} view(s) transformed out of {razorFiles.Length}",
            FilePath = projectDir
        });
    }

    private async Task MigrateStaticFilesAsync(
        string projectDir,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        try
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "Migrating static files (Content/Scripts → wwwroot)",
                FilePath = projectDir
            });

            // Analyze the project for static files
            var staticFilesInfo = await _staticFilesAnalyzer.AnalyzeAsync(projectDir);

            if (staticFilesInfo.Folders.Count == 0)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = "No static file folders (Content/Scripts/Images) found",
                    FilePath = projectDir
                });
                return;
            }

            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = $"Found {staticFilesInfo.Folders.Count} static file folder(s) with {staticFilesInfo.TotalFiles} file(s)",
                FilePath = projectDir
            });

            // Perform migration
            var result = await _staticFilesMigrator.MigrateAsync(staticFilesInfo, dryRun);

            if (result.Success)
            {
                // Log what folders were moved (don't add as FileChange since they're directories, not files)
                foreach (var folder in result.MovedFolders)
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Migrated {folder.Files.Count} file(s) from {folder.SourcePath} to {folder.TargetPath}",
                        FilePath = Path.Combine(projectDir, folder.TargetPath),
                        Code = "STATIC_FOLDER"
                    });
                }

                confidenceScores.Add(result.Confidence);

                // Add middleware code suggestion
                var middlewareCode = _staticFilesMigrator.GenerateStaticFilesMiddleware();
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = $"Add to Program.cs:\n{middlewareCode}",
                    FilePath = projectDir,
                    Code = "STATIC_MIDDLEWARE"
                });

                foreach (var note in result.Notes)
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = note,
                        FilePath = projectDir
                    });
                }

                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = $"Static files migration complete: {result.FilesMovedCount} files moved, {result.ReferencesUpdatedCount} references updated",
                    FilePath = projectDir
                });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Error,
                        Message = error,
                        FilePath = projectDir,
                        Code = "STATIC001"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire migration
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Warning,
                Message = $"Static files migration failed (non-critical): {ex.Message}",
                FilePath = projectDir,
                Code = "STATIC002"
            });
        }
    }

    private async Task TransformSignalRAsync(
        string projectDir,
        ProjectInfo projectInfo,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Transforming SignalR hubs (legacy → ASP.NET Core SignalR)",
            FilePath = projectDir
        });

        var transformedCount = 0;

        var allHubs = new List<Core.Models.SignalR.SignalRHubInfo>();

        // Scan source files for SignalR hubs
        foreach (var compileItem in projectInfo.CompileItems)
        {
            var sourceFilePath = Path.IsPathRooted(compileItem.Include)
                ? compileItem.Include
                : Path.Combine(projectDir, compileItem.Include);

            if (!File.Exists(sourceFilePath))
                continue;

            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);

                // Analyze for SignalR hubs
                var hubs = _signalRHubAnalyzer.AnalyzeFile(sourceCode, sourceFilePath);
                allHubs.AddRange(hubs);

                foreach (var hub in hubs)
                {
                    // Transform the hub
                    var transformResult = _signalRHubTransformer.TransformHub(sourceCode, hub);

                    if (transformResult.TransformedCode != sourceCode)
                    {
                        changes.Add(new FileChange
                        {
                            FilePath = sourceFilePath,
                            Type = ChangeType.Modify,
                            OriginalContent = sourceCode,
                            NewContent = transformResult.TransformedCode,
                            Confidence = transformResult.Confidence,
                            Description = $"Transformed SignalR hub '{hub.ClassName}' to ASP.NET Core"
                        });
                        confidenceScores.Add(transformResult.Confidence);
                        transformedCount++;

                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Info,
                            Message = $"Transformed SignalR hub '{hub.ClassName}' in {Path.GetFileName(sourceFilePath)}",
                            FilePath = sourceFilePath
                        });

                        foreach (var change in transformResult.Changes)
                        {
                            diagnostics.Add(new MigrationDiagnostic
                            {
                                Level = DiagnosticLevel.Info,
                                Message = change.Description,
                                FilePath = sourceFilePath
                            });
                        }
                    }
                }

                // Analyze for GlobalHost usage
                var globalHostUsage = _globalHostAnalyzer.AnalyzeFile(sourceCode, sourceFilePath);
                if (globalHostUsage != null && globalHostUsage.Usages.Count > 0)
                {
                    foreach (var usage in globalHostUsage.Usages)
                    {
                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Warning,
                            Message = $"GlobalHost usage detected: {usage.Pattern} - requires IHubContext<T> injection",
                            FilePath = sourceFilePath,
                            Code = "SIGNALR002"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to transform SignalR in {sourceFilePath}: {ex.Message}",
                    FilePath = sourceFilePath,
                    Code = "SIGNALR003"
                });
            }
        }

        // Generate startup code
        var serviceRegistration = _signalRStartupGenerator.GenerateServiceRegistration();
        var endpointMappings = _signalRStartupGenerator.GenerateEndpointMappings(allHubs);
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Add to Program.cs for SignalR:\n{serviceRegistration}\n\n{endpointMappings}",
            FilePath = projectDir,
            Code = "SIGNALR_STARTUP"
        });

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"SignalR transformation complete: {transformedCount} file(s) transformed",
            FilePath = projectDir
        });
    }

    private async Task TransformDependencyInjectionAsync(
        string projectDir,
        ProjectInfo projectInfo,
        List<FileChange> changes,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        CancellationToken cancellationToken)
    {
        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "Analyzing DI container registrations",
            FilePath = projectDir
        });

        // Detect which DI container is in use from package references
        var detectedFramework = _diContainerDetector.DetectFromPackages(projectInfo.PackageReferences);

        if (detectedFramework == Core.Models.DependencyInjection.DIFrameworkType.Unknown)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Info,
                Message = "No third-party DI container detected (Autofac, Unity, Ninject, StructureMap)",
                FilePath = projectDir
            });
            return;
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Detected DI container: {detectedFramework}",
            FilePath = projectDir
        });

        // Find configuration files for the detected framework
        var configFiles = await _diContainerDetector.FindConfigurationFilesAsync(projectDir, detectedFramework);

        // Analyze registrations
        foreach (var configFile in configFiles)
        {
            var configPath = Path.IsPathRooted(configFile)
                ? configFile
                : Path.Combine(projectDir, configFile);

            if (!File.Exists(configPath))
                continue;

            try
            {
                var sourceCode = await File.ReadAllTextAsync(configPath, cancellationToken);
                var registrations = await _diContainerAnalyzer.AnalyzeRegistrationsFromContentAsync(sourceCode, configPath);

                if (registrations.Count > 0)
                {
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Info,
                        Message = $"Found {registrations.Count} service registration(s) in {Path.GetFileName(configPath)}",
                        FilePath = configPath
                    });

                    // Map lifetimes
                    foreach (var reg in registrations)
                    {
                        var mappedLifetime = await _diContainerAnalyzer.MapLifetimeAsync(reg.Lifetime.ToString());

                        diagnostics.Add(new MigrationDiagnostic
                        {
                            Level = DiagnosticLevel.Info,
                            Message = $"  {reg.ServiceType} → {reg.ImplementationType} ({reg.Lifetime} → {mappedLifetime.TargetLifetime})",
                            FilePath = configPath
                        });
                    }

                    // Add a note about manual migration (full transformer not yet implemented)
                    diagnostics.Add(new MigrationDiagnostic
                    {
                        Level = DiagnosticLevel.Warning,
                        Message = $"DI container transformer not fully implemented. Found {registrations.Count} registration(s) - manual migration to MS.DI required.",
                        FilePath = configPath,
                        Code = "DI001"
                    });

                    var avgConfidence = registrations.Any()
                        ? (int)registrations.Average(r => r.ConfidenceScore)
                        : 70;
                    confidenceScores.Add(avgConfidence);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Message = $"Failed to analyze DI registrations in {configPath}: {ex.Message}",
                    FilePath = configPath,
                    Code = "DI002"
                });
            }
        }

        diagnostics.Add(new MigrationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = "DI container analysis complete",
            FilePath = projectDir
        });
    }
}
