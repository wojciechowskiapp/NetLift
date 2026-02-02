using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Cli.Renderers;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text;

namespace NetLift.Cli.Commands;

/// <summary>
/// Command to migrate a solution to .NET 8+.
/// </summary>
public sealed class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    private readonly ISolutionParser _solutionParser;
    private readonly IProjectParser _projectParser;
    private readonly IPackagesConfigParser _packagesConfigParser;
    private readonly ISdkProjectConverter _sdkProjectConverter;
    private readonly IAssemblyInfoExtractor _assemblyInfoExtractor;
    private readonly IPackageReferenceConverter _packageReferenceConverter;
    private readonly IPackageMappingService _packageMappingService;
    private readonly IInteractiveService? _interactiveService;
    private readonly IDryRunService _dryRunService;
    private readonly IMigrationOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateCommand"/> class.
    /// </summary>
    public MigrateCommand(
        ISolutionParser solutionParser,
        IProjectParser projectParser,
        IPackagesConfigParser packagesConfigParser,
        ISdkProjectConverter sdkProjectConverter,
        IAssemblyInfoExtractor assemblyInfoExtractor,
        IPackageReferenceConverter packageReferenceConverter,
        IPackageMappingService packageMappingService,
        IDryRunService dryRunService,
        IMigrationOrchestrator orchestrator,
        IInteractiveService? interactiveService = null)
    {
        _solutionParser = solutionParser ?? throw new ArgumentNullException(nameof(solutionParser));
        _projectParser = projectParser ?? throw new ArgumentNullException(nameof(projectParser));
        _packagesConfigParser = packagesConfigParser ?? throw new ArgumentNullException(nameof(packagesConfigParser));
        _sdkProjectConverter = sdkProjectConverter ?? throw new ArgumentNullException(nameof(sdkProjectConverter));
        _assemblyInfoExtractor = assemblyInfoExtractor ?? throw new ArgumentNullException(nameof(assemblyInfoExtractor));
        _packageReferenceConverter = packageReferenceConverter ?? throw new ArgumentNullException(nameof(packageReferenceConverter));
        _packageMappingService = packageMappingService ?? throw new ArgumentNullException(nameof(packageMappingService));
        _dryRunService = dryRunService ?? throw new ArgumentNullException(nameof(dryRunService));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _interactiveService = interactiveService;
    }

    /// <summary>
    /// Settings for the migrate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Path to the solution file to migrate.
        /// </summary>
        [CommandArgument(0, "<SOLUTION>")]
        [Description("Path to solution file")]
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>
        /// Target framework version.
        /// </summary>
        [CommandOption("-t|--target")]
        [Description("Target framework (e.g., net8.0, net9.0)")]
        [DefaultValue("net8.0")]
        public string TargetFramework { get; set; } = "net8.0";

        /// <summary>
        /// Skip backup creation.
        /// </summary>
        [CommandOption("--no-backup")]
        [Description("Skip creating a backup before migration")]
        [DefaultValue(false)]
        public bool NoBackup { get; set; }

        /// <summary>
        /// Enable verbose output.
        /// </summary>
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Dry run mode (preview changes without applying).
        /// </summary>
        [CommandOption("--dry-run")]
        [Description("Preview changes without applying them")]
        [DefaultValue(false)]
        public bool DryRun { get; set; }

        /// <summary>
        /// Output path for dry-run report.
        /// </summary>
        [CommandOption("--dry-run-output <PATH>")]
        [Description("Write dry-run report to file")]
        public string? DryRunOutput { get; set; }

        /// <summary>
        /// Interactive mode (prompt for each project).
        /// </summary>
        [CommandOption("-i|--interactive")]
        [Description("Prompt for confirmation before migrating each project")]
        [DefaultValue(false)]
        public bool Interactive { get; set; }
    }

    /// <summary>
    /// Executes the migrate command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>Exit code (0 for success).</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate solution file
            if (!File.Exists(settings.SolutionPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Solution file not found");
                return 2;
            }

            if (!_solutionParser.IsValidSolutionFile(settings.SolutionPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Invalid solution file format");
                return 2;
            }

            // Display header
            var rule = new Rule("[bold green]NetLift Migration[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[cyan]Solution:[/] {settings.SolutionPath}");
            AnsiConsole.MarkupLine($"[cyan]Target Framework:[/] {settings.TargetFramework}");
            AnsiConsole.MarkupLine($"[cyan]Backup:[/] {(settings.NoBackup ? "[red]Disabled[/]" : "[green]Enabled[/]")}");

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[magenta]Mode:[/] Dry run (preview only)");
                _dryRunService.Reset(); // Reset dry-run service for new run
            }

            if (settings.Interactive)
            {
                AnsiConsole.MarkupLine("[magenta]Mode:[/] Interactive (will prompt for each project)");
            }

            AnsiConsole.WriteLine();

            var migrationResults = new List<ProjectMigrationResult>();

            // Run migration with progress
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    // Step 1: Parse solution
                    var parseTask = ctx.AddTask("[cyan]Parsing solution[/]");
                    var solution = await _solutionParser.ParseAsync(settings.SolutionPath);
                    parseTask.Value = 100;

                    var projectsToMigrate = solution.Projects
                        .Where(p => p.DetectedType != ProjectType.SolutionFolder)
                        .Where(p => File.Exists(p.AbsolutePath))
                        .ToList();

                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]Found {projectsToMigrate.Count} projects to migrate[/]");
                    }

                    // Step 2: Migrate each project
                    var migrateTask = ctx.AddTask($"[cyan]Migrating {projectsToMigrate.Count} projects[/]", maxValue: projectsToMigrate.Count);

                    foreach (var projectRef in projectsToMigrate)
                    {
                        migrateTask.Description = $"[cyan]Migrating {projectRef.Name}[/]";

                        // Interactive mode: prompt user
                        if (settings.Interactive && _interactiveService != null)
                        {
                            var changedFiles = GetChangedFiles(projectRef);
                            var choice = await _interactiveService.PromptChoiceAsync(
                                $"Migrate project: {projectRef.Name} (Type: {projectRef.DetectedType})",
                                changedFiles);

                            if (choice == InteractiveChoice.Abort)
                            {
                                AnsiConsole.MarkupLine("[red]Migration aborted by user[/]");
                                migrateTask.StopTask();
                                break;
                            }

                            if (choice == InteractiveChoice.Skip)
                            {
                                var skipResult = new ProjectMigrationResult
                                {
                                    ProjectName = projectRef.Name,
                                    ProjectPath = projectRef.AbsolutePath,
                                    Success = true,
                                    Warnings = new List<string> { "Skipped by user in interactive mode" }
                                };
                                migrationResults.Add(skipResult);
                                migrateTask.Increment(1);
                                continue;
                            }

                            if (choice == InteractiveChoice.Preview)
                            {
                                await ShowPreviewAsync(projectRef, settings);
                                // Re-prompt after preview
                                var retryChoice = await _interactiveService.PromptChoiceAsync(
                                    $"Migrate project: {projectRef.Name} (Type: {projectRef.DetectedType})",
                                    changedFiles);

                                if (retryChoice == InteractiveChoice.Abort)
                                {
                                    AnsiConsole.MarkupLine("[red]Migration aborted by user[/]");
                                    migrateTask.StopTask();
                                    break;
                                }

                                if (retryChoice == InteractiveChoice.Skip)
                                {
                                    var skipResult = new ProjectMigrationResult
                                    {
                                        ProjectName = projectRef.Name,
                                        ProjectPath = projectRef.AbsolutePath,
                                        Success = true,
                                        Warnings = new List<string> { "Skipped by user in interactive mode" }
                                    };
                                    migrationResults.Add(skipResult);
                                    migrateTask.Increment(1);
                                    continue;
                                }
                            }
                        }

                        var result = await MigrateProjectAsync(projectRef, settings);
                        migrationResults.Add(result);

                        migrateTask.Increment(1);
                    }

                    migrateTask.StopTask();
                });

            // If dry-run, display dry-run report
            if (settings.DryRun)
            {
                var dryRunReport = _dryRunService.GetReport();
                var renderer = new DryRunReportRenderer();
                renderer.Render(dryRunReport);

                // Write to file if requested
                if (!string.IsNullOrEmpty(settings.DryRunOutput))
                {
                    await renderer.WriteToFileAsync(dryRunReport, settings.DryRunOutput);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[green]Dry-run report saved to:[/] {settings.DryRunOutput}");
                }

                return dryRunReport.WouldSucceed ? 0 : 1;
            }

            // Display summary
            DisplaySummary(migrationResults, settings);

            // Determine exit code
            var hasErrors = migrationResults.Any(r => !r.Success);
            var hasWarnings = migrationResults.Any(r => r.Warnings.Count > 0);

            if (hasErrors)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[red]Migration completed with errors[/]");
                return 1;
            }

            if (hasWarnings)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Migration completed with warnings[/]");
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]Migration completed successfully[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Error:[/] Migration failed");
            AnsiConsole.WriteException(ex);
            return 2;
        }
    }

    private async Task<ProjectMigrationResult> MigrateProjectAsync(SolutionProject projectRef, Settings settings)
    {
        var result = new ProjectMigrationResult
        {
            ProjectName = projectRef.Name,
            ProjectPath = projectRef.AbsolutePath
        };

        try
        {
            // Check if project can be parsed
            if (!_projectParser.CanParse(projectRef.AbsolutePath))
            {
                result.Success = false;
                result.ErrorMessage = "Project is already in SDK-style format or cannot be parsed";
                return result;
            }

            // Analyze the project
            var projectInfo = await _projectParser.AnalyzeAsync(projectRef.AbsolutePath);

            // Parse packages.config if exists
            var projectDir = Path.GetDirectoryName(projectRef.AbsolutePath);
            var packagesConfigPath = !string.IsNullOrEmpty(projectDir)
                ? Path.Combine(projectDir, "packages.config")
                : null;

            List<PackageReference>? packagesFromConfig = null;
            if (packagesConfigPath != null && File.Exists(packagesConfigPath))
            {
                packagesFromConfig = _packagesConfigParser.Parse(packagesConfigPath);
                projectInfo.PackageReferences.AddRange(packagesFromConfig);
            }

            // Extract AssemblyInfo.cs data
            AssemblyInfoData? assemblyInfo = null;
            if (!string.IsNullOrEmpty(projectDir))
            {
                var assemblyInfoPath = Path.Combine(projectDir, "Properties", "AssemblyInfo.cs");
                if (File.Exists(assemblyInfoPath) && _assemblyInfoExtractor.CanExtract(assemblyInfoPath))
                {
                    assemblyInfo = await _assemblyInfoExtractor.ExtractAsync(assemblyInfoPath);
                }
            }

            // Convert to SDK-style format
            var sdkProject = _sdkProjectConverter.Convert(projectInfo, settings.TargetFramework);

            // Add assembly info properties if extracted
            if (assemblyInfo != null)
            {
                AddAssemblyInfoToProject(sdkProject, assemblyInfo);
            }

            // Convert packages if packages.config exists
            if (packagesFromConfig != null && packagesFromConfig.Count > 0)
            {
                var packagesConfig = new PackagesConfig { Packages = packagesFromConfig };
                var conversionResult = _packageReferenceConverter.Convert(packagesConfig, settings.TargetFramework);
                AddPackageReferencesToProject(sdkProject, conversionResult);

                // Add warnings from conversion
                foreach (var warning in conversionResult.Warnings)
                {
                    result.Warnings.Add($"{warning.Severity}: {warning.Message}");
                }
            }

            // Record or create backups
            if (!settings.NoBackup)
            {
                if (settings.DryRun)
                {
                    _dryRunService.RecordChange($"{projectRef.AbsolutePath}.bak", ChangeType.Backup, "Backup of project file");
                    if (packagesConfigPath != null && File.Exists(packagesConfigPath))
                    {
                        _dryRunService.RecordChange($"{packagesConfigPath}.bak", ChangeType.Backup, "Backup of packages.config");
                    }
                }
                else
                {
                    File.Copy(projectRef.AbsolutePath, $"{projectRef.AbsolutePath}.bak", overwrite: true);
                    if (packagesConfigPath != null && File.Exists(packagesConfigPath))
                    {
                        File.Copy(packagesConfigPath, $"{packagesConfigPath}.bak", overwrite: true);
                    }
                }
            }

            // Record or write project file changes
            if (settings.DryRun)
            {
                var originalContent = await File.ReadAllTextAsync(projectRef.AbsolutePath);
                var newContent = sdkProject.ToString();
                _dryRunService.RecordChange(projectRef.AbsolutePath, ChangeType.Modify, originalContent, newContent);

                // Record packages.config deletion
                if (packagesConfigPath != null && File.Exists(packagesConfigPath))
                {
                    var packagesContent = await File.ReadAllTextAsync(packagesConfigPath);
                    _dryRunService.RecordChange(packagesConfigPath, ChangeType.Delete, packagesContent, null);
                }

                // Record warnings
                foreach (var warning in result.Warnings)
                {
                    _dryRunService.RecordWarning($"{projectRef.Name}: {warning}");
                }
            }
            else
            {
                await File.WriteAllTextAsync(projectRef.AbsolutePath, sdkProject.ToString());

                // Delete packages.config after successful conversion
                if (packagesConfigPath != null && File.Exists(packagesConfigPath))
                {
                    File.Delete(packagesConfigPath);
                }
            }

            // Run code transformations via orchestrator
            // IMPORTANT: Pass the already-parsed projectInfo to avoid re-parsing
            // the converted SDK-style project file (which would have empty CompileItems)
            var migrationOptions = new MigrationOptions
            {
                DryRun = settings.DryRun,
                TransformSourceCode = true,
                TransformConfig = true,
                GenerateViewImports = true,
                TransformEntityFramework = true,
                TransformWcfServices = true
            };

            var migrationResult = await _orchestrator.MigrateProjectAsync(
                projectInfo,  // Pass pre-parsed ProjectInfo instead of path
                settings.TargetFramework,
                migrationOptions,
                CancellationToken.None);

            // Handle migration results
            if (settings.DryRun)
            {
                foreach (var change in migrationResult.Changes)
                {
                    _dryRunService.RecordChange(
                        change.FilePath,
                        change.Type,
                        change.OriginalContent,
                        change.NewContent);
                }
            }
            else
            {
                // Write actual changes
                foreach (var change in migrationResult.Changes.Where(c => c.Type != ChangeType.Delete))
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
                foreach (var change in migrationResult.Changes.Where(c => c.Type == ChangeType.Delete))
                {
                    if (File.Exists(change.FilePath))
                    {
                        File.Delete(change.FilePath);
                    }
                }
            }

            // Collect warnings from orchestrator
            foreach (var diagnostic in migrationResult.Diagnostics.Where(d => d.Level == DiagnosticLevel.Warning))
            {
                result.Warnings.Add(diagnostic.Message);
            }

            // Collect errors from orchestrator
            var errors = migrationResult.Diagnostics.Where(d => d.Level == DiagnosticLevel.Error).ToList();
            if (errors.Any())
            {
                result.Success = false;
                result.ErrorMessage = string.Join("; ", errors.Select(e => e.Message));
                return result;
            }

            // Store additional metadata from orchestrator
            result.Success = migrationResult.Success;
            result.BackupCreated = !settings.NoBackup && !settings.DryRun;
            result.FilesTransformed = migrationResult.FilesTransformed;
            result.OverallConfidence = migrationResult.OverallConfidence;
            result.ManualTasks = migrationResult.ManualTasks.ToList();

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Successfully migrated {projectRef.Name}[/]");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            if (settings.DryRun)
            {
                _dryRunService.RecordError($"{projectRef.Name}: {ex.Message}");
            }

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[red]Failed to migrate {projectRef.Name}: {ex.Message}[/]");
            }
        }

        return result;
    }

    private void AddAssemblyInfoToProject(XDocument sdkProject, AssemblyInfoData assemblyInfo)
    {
        var root = sdkProject.Root;
        if (root == null) return;

        var propertyGroup = root.Elements("PropertyGroup").FirstOrDefault();
        if (propertyGroup == null)
        {
            propertyGroup = new XElement("PropertyGroup");
            root.Add(propertyGroup);
        }

        // Add assembly info properties
        if (!string.IsNullOrWhiteSpace(assemblyInfo.Title))
            propertyGroup.Add(new XElement("AssemblyTitle", assemblyInfo.Title));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.Description))
            propertyGroup.Add(new XElement("Description", assemblyInfo.Description));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.Company))
            propertyGroup.Add(new XElement("Company", assemblyInfo.Company));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.Product))
            propertyGroup.Add(new XElement("Product", assemblyInfo.Product));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.Copyright))
            propertyGroup.Add(new XElement("Copyright", assemblyInfo.Copyright));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.AssemblyVersion))
            propertyGroup.Add(new XElement("AssemblyVersion", assemblyInfo.AssemblyVersion));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.FileVersion))
            propertyGroup.Add(new XElement("FileVersion", assemblyInfo.FileVersion));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.InformationalVersion))
            propertyGroup.Add(new XElement("Version", assemblyInfo.InformationalVersion));

        if (!string.IsNullOrWhiteSpace(assemblyInfo.NeutralLanguage))
            propertyGroup.Add(new XElement("NeutralLanguage", assemblyInfo.NeutralLanguage));
    }

    private void AddPackageReferencesToProject(XDocument sdkProject, PackageConversionResult conversionResult)
    {
        var root = sdkProject.Root;
        if (root == null) return;

        if (conversionResult.Packages.Count == 0) return;

        var itemGroup = new XElement("ItemGroup");

        foreach (var package in conversionResult.Packages)
        {
            var packageRef = new XElement("PackageReference");
            packageRef.Add(new XAttribute("Include", package.Id));
            packageRef.Add(new XAttribute("Version", package.Version));

            if (package.IsDevelopmentDependency)
            {
                packageRef.Add(new XElement("PrivateAssets", "all"));
                packageRef.Add(new XElement("IncludeAssets", "runtime; build; native; contentfiles; analyzers; buildtransitive"));
            }

            itemGroup.Add(packageRef);
        }

        root.Add(itemGroup);
    }

    private void DisplaySummary(List<ProjectMigrationResult> results, Settings settings)
    {
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Purple);

        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Files[/]");
        table.AddColumn("[bold]Confidence[/]");
        table.AddColumn("[bold]Warnings[/]");
        table.AddColumn("[bold]Backup[/]");

        foreach (var result in results)
        {
            var status = result.Success
                ? "[green]Success[/]"
                : $"[red]Failed[/]";

            var backupStatus = result.BackupCreated
                ? "[green]Yes[/]"
                : settings.DryRun ? "[yellow]Dry Run[/]" : "[dim]No[/]";

            var warningCount = result.Warnings.Count > 0
                ? $"[yellow]{result.Warnings.Count}[/]"
                : "[dim]0[/]";

            var filesTransformed = result.FilesTransformed > 0
                ? $"[cyan]{result.FilesTransformed}[/]"
                : "[dim]0[/]";

            var confidenceColor = result.OverallConfidence >= 80 ? "green" : result.OverallConfidence >= 60 ? "yellow" : "red";
            var confidenceDisplay = result.OverallConfidence > 0
                ? $"[{confidenceColor}]{result.OverallConfidence}%[/]"
                : "[dim]N/A[/]";

            table.AddRow(
                result.ProjectName,
                status,
                filesTransformed,
                confidenceDisplay,
                warningCount,
                backupStatus);
        }

        AnsiConsole.Write(table);

        // Display manual tasks
        var projectsWithTasks = results.Where(r => r.ManualTasks.Count > 0).ToList();
        if (projectsWithTasks.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold magenta]Manual Tasks Required:[/]");

            foreach (var result in projectsWithTasks)
            {
                AnsiConsole.MarkupLine($"[bold]{result.ProjectName}:[/]");
                foreach (var task in result.ManualTasks)
                {
                    AnsiConsole.MarkupLine($"  [magenta]•[/] {task}");
                }
            }
        }

        // Display warnings if verbose
        if (settings.Verbose && results.Any(r => r.Warnings.Count > 0))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Warnings:[/]");

            foreach (var result in results.Where(r => r.Warnings.Count > 0))
            {
                AnsiConsole.MarkupLine($"[bold]{result.ProjectName}:[/]");
                foreach (var warning in result.Warnings)
                {
                    AnsiConsole.MarkupLine($"  [yellow]•[/] {warning}");
                }
            }
        }

        // Display errors
        var failedProjects = results.Where(r => !r.Success).ToList();
        if (failedProjects.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold red]Errors:[/]");

            foreach (var result in failedProjects)
            {
                AnsiConsole.MarkupLine($"[bold]{result.ProjectName}:[/]");
                AnsiConsole.MarkupLine($"  [red]•[/] {result.ErrorMessage}");
            }
        }
    }

    private List<string> GetChangedFiles(SolutionProject projectRef)
    {
        var changedFiles = new List<string>
        {
            projectRef.AbsolutePath
        };

        var projectDir = Path.GetDirectoryName(projectRef.AbsolutePath);
        if (!string.IsNullOrEmpty(projectDir))
        {
            var packagesConfigPath = Path.Combine(projectDir, "packages.config");
            if (File.Exists(packagesConfigPath))
            {
                changedFiles.Add(packagesConfigPath);
            }

            var assemblyInfoPath = Path.Combine(projectDir, "Properties", "AssemblyInfo.cs");
            if (File.Exists(assemblyInfoPath))
            {
                changedFiles.Add(assemblyInfoPath);
            }
        }

        return changedFiles;
    }

    private async Task ShowPreviewAsync(SolutionProject projectRef, Settings settings)
    {
        try
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[purple]Preview: {projectRef.Name}[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            // Parse the project
            if (!_projectParser.CanParse(projectRef.AbsolutePath))
            {
                AnsiConsole.MarkupLine("[yellow]Project is already in SDK-style format or cannot be parsed[/]");
                return;
            }

            var projectInfo = await _projectParser.AnalyzeAsync(projectRef.AbsolutePath);

            // Show project info
            var infoTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Purple);

            infoTable.AddColumn("[bold]Property[/]");
            infoTable.AddColumn("[bold]Value[/]");

            infoTable.AddRow("Project Type", projectRef.DetectedType.ToString());
            infoTable.AddRow("Target Framework", settings.TargetFramework);
            infoTable.AddRow("Assembly References", projectInfo.References.Count.ToString());
            infoTable.AddRow("Package References", projectInfo.PackageReferences.Count.ToString());
            infoTable.AddRow("Project References", projectInfo.ProjectReferences.Count.ToString());

            AnsiConsole.Write(infoTable);

            // Show package changes
            var projectDir = Path.GetDirectoryName(projectRef.AbsolutePath);
            var packagesConfigPath = !string.IsNullOrEmpty(projectDir)
                ? Path.Combine(projectDir, "packages.config")
                : null;

            if (packagesConfigPath != null && File.Exists(packagesConfigPath))
            {
                var packages = _packagesConfigParser.Parse(packagesConfigPath);
                if (packages.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Package Changes:[/]");

                    var packageTable = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Purple);

                    packageTable.AddColumn("[bold]Package[/]");
                    packageTable.AddColumn("[bold]Current Version[/]");
                    packageTable.AddColumn("[bold]Action[/]");

                    foreach (var package in packages.Take(10))
                    {
                        var mapping = _packageMappingService.GetMappedPackage(package.Id, package.Version, settings.TargetFramework);
                        var actionColor = mapping.Action switch
                        {
                            MappingAction.Remove => "red",
                            MappingAction.Replace => "yellow",
                            MappingAction.Upgrade => "cyan",
                            _ => "green"
                        };

                        packageTable.AddRow(
                            package.Id,
                            package.Version,
                            $"[{actionColor}]{mapping.Action}[/]");
                    }

                    if (packages.Count > 10)
                    {
                        packageTable.AddRow("[dim]...[/]", "[dim]...[/]", $"[dim]({packages.Count - 10} more)[/]");
                    }

                    AnsiConsole.Write(packageTable);
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule() { Justification = Justify.Left });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error generating preview: {ex.Message}[/]");
        }
    }
}

/// <summary>
/// Represents the result of migrating a single project.
/// </summary>
internal class ProjectMigrationResult
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool BackupCreated { get; set; }
    public int FilesTransformed { get; set; }
    public int OverallConfidence { get; set; }
    public List<string> ManualTasks { get; set; } = new();
}
