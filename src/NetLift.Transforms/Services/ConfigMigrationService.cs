using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Core.Models.Config;

namespace NetLift.Transforms.Services;

/// <summary>
/// Service for migrating web.config to appsettings.json and Program.cs.
/// Coordinates config parsers and generators to transform legacy ASP.NET configuration
/// into modern ASP.NET Core configuration format.
/// </summary>
public sealed class ConfigMigrationService : IConfigMigrationService
{
    private readonly IWebConfigAppSettingsParser _appSettingsParser;
    private readonly IWebConfigConnectionStringParser _connectionStringParser;
    private readonly ISystemWebParser _systemWebParser;
    private readonly IAppSettingsJsonGenerator _appSettingsJsonGenerator;
    private readonly IEnvironmentAppSettingsGenerator _environmentAppSettingsGenerator;
    private readonly IProgramCsGenerator _programCsGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigMigrationService"/> class.
    /// </summary>
    public ConfigMigrationService(
        IWebConfigAppSettingsParser appSettingsParser,
        IWebConfigConnectionStringParser connectionStringParser,
        ISystemWebParser systemWebParser,
        IAppSettingsJsonGenerator appSettingsJsonGenerator,
        IEnvironmentAppSettingsGenerator environmentAppSettingsGenerator,
        IProgramCsGenerator programCsGenerator)
    {
        _appSettingsParser = appSettingsParser ?? throw new ArgumentNullException(nameof(appSettingsParser));
        _connectionStringParser = connectionStringParser ?? throw new ArgumentNullException(nameof(connectionStringParser));
        _systemWebParser = systemWebParser ?? throw new ArgumentNullException(nameof(systemWebParser));
        _appSettingsJsonGenerator = appSettingsJsonGenerator ?? throw new ArgumentNullException(nameof(appSettingsJsonGenerator));
        _environmentAppSettingsGenerator = environmentAppSettingsGenerator ?? throw new ArgumentNullException(nameof(environmentAppSettingsGenerator));
        _programCsGenerator = programCsGenerator ?? throw new ArgumentNullException(nameof(programCsGenerator));
    }

    /// <inheritdoc />
    public async Task<ConfigMigrationResult> MigrateConfigAsync(
        string projectDirectory,
        string targetFramework,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

        var diagnostics = new List<string>();
        var generatedFiles = new List<FileChange>();
        var confidence = 100;

        try
        {
            // Find web.config (case-insensitive for Linux compatibility)
            var webConfigPath = FindWebConfigCaseInsensitive(projectDirectory);
            if (webConfigPath == null)
            {
                diagnostics.Add("web.config not found in project directory. Configuration migration skipped.");
                return new ConfigMigrationResult
                {
                    Success = true,
                    GeneratedFiles = generatedFiles,
                    Diagnostics = diagnostics,
                    Confidence = 100
                };
            }

            // Load and parse web.config
            XDocument webConfig;
            try
            {
                webConfig = await LoadWebConfigAsync(webConfigPath, cancellationToken);
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Failed to parse web.config: {ex.Message}");
                return new ConfigMigrationResult
                {
                    Success = false,
                    GeneratedFiles = generatedFiles,
                    Diagnostics = diagnostics,
                    Confidence = 0
                };
            }

            // Parse web.config sections
            var appSettings = _appSettingsParser.Parse(webConfig);
            var connectionStrings = _connectionStringParser.Parse(webConfig);
            var systemWeb = _systemWebParser.Parse(webConfig);

            // Check for encrypted sections
            if (appSettings.IsEncrypted || connectionStrings.HasEncryptedStrings)
            {
                diagnostics.Add("WARNING: Encrypted configuration sections detected. Manual decryption required.");
                confidence = Math.Min(confidence, 50);
            }

            // Check for external config files
            if (!string.IsNullOrEmpty(appSettings.ExternalFile))
            {
                diagnostics.Add($"INFO: External appSettings file detected: {appSettings.ExternalFile}. Consider merging manually.");
                confidence = Math.Min(confidence, 85);
            }

            // Load optional transform files
            XDocument? debugTransform = await TryLoadTransformAsync(projectDirectory, "Web.Debug.config", cancellationToken);
            XDocument? releaseTransform = await TryLoadTransformAsync(projectDirectory, "Web.Release.config", cancellationToken);

            // Generate appsettings.json
            var appSettingsJsonPath = Path.Combine(projectDirectory, "appsettings.json");
            var appSettingsJsonContent = _appSettingsJsonGenerator.Generate(connectionStrings, appSettings, systemWeb);

            string? originalAppSettingsContent = null;
            if (File.Exists(appSettingsJsonPath))
            {
                try
                {
                    originalAppSettingsContent = await File.ReadAllTextAsync(appSettingsJsonPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    diagnostics.Add($"WARNING: File {appSettingsJsonPath} was deleted during migration.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    diagnostics.Add($"ERROR: Access denied reading {appSettingsJsonPath}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    diagnostics.Add($"ERROR: I/O error reading {appSettingsJsonPath}: {ex.Message}");
                }
            }

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsJsonPath,
                Type = File.Exists(appSettingsJsonPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = originalAppSettingsContent,
                NewContent = appSettingsJsonContent,
                Confidence = confidence,
                Description = "Generated appsettings.json from web.config"
            });

            // Generate appsettings.Development.json
            var appSettingsDevPath = Path.Combine(projectDirectory, "appsettings.Development.json");
            var appSettingsDevContent = _environmentAppSettingsGenerator.GenerateDevelopment(
                connectionStrings, appSettings, systemWeb, debugTransform);

            string? originalDevContent = null;
            if (File.Exists(appSettingsDevPath))
            {
                try
                {
                    originalDevContent = await File.ReadAllTextAsync(appSettingsDevPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    diagnostics.Add($"WARNING: File {appSettingsDevPath} was deleted during migration.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    diagnostics.Add($"ERROR: Access denied reading {appSettingsDevPath}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    diagnostics.Add($"ERROR: I/O error reading {appSettingsDevPath}: {ex.Message}");
                }
            }

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsDevPath,
                Type = File.Exists(appSettingsDevPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = originalDevContent,
                NewContent = appSettingsDevContent,
                Confidence = confidence,
                Description = "Generated appsettings.Development.json from web.config and Web.Debug.config"
            });

            // Generate appsettings.Production.json
            var appSettingsProdPath = Path.Combine(projectDirectory, "appsettings.Production.json");
            var appSettingsProdContent = _environmentAppSettingsGenerator.GenerateProduction(
                connectionStrings, appSettings, systemWeb, releaseTransform);

            string? originalProdContent = null;
            if (File.Exists(appSettingsProdPath))
            {
                try
                {
                    originalProdContent = await File.ReadAllTextAsync(appSettingsProdPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    diagnostics.Add($"WARNING: File {appSettingsProdPath} was deleted during migration.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    diagnostics.Add($"ERROR: Access denied reading {appSettingsProdPath}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    diagnostics.Add($"ERROR: I/O error reading {appSettingsProdPath}: {ex.Message}");
                }
            }

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsProdPath,
                Type = File.Exists(appSettingsProdPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = originalProdContent,
                NewContent = appSettingsProdContent,
                Confidence = confidence,
                Description = "Generated appsettings.Production.json from web.config and Web.Release.config"
            });

            // Generate Program.cs
            var programCsPath = Path.Combine(projectDirectory, "Program.cs");
            var programGenerationOptions = DetermineProgramGenerationOptions(systemWeb, projectDirectory);
            var programCsContent = _programCsGenerator.Generate(systemWeb, appSettings, programGenerationOptions);

            // Check if Program.cs already exists
            var programCsExists = File.Exists(programCsPath);
            var programCsConfidence = programCsExists ? 70 : 95; // Lower confidence if overwriting existing Program.cs

            if (programCsExists)
            {
                diagnostics.Add("INFO: Program.cs already exists and will be overwritten. Review the generated file carefully.");
            }

            string? originalProgramContent = null;
            if (programCsExists)
            {
                try
                {
                    originalProgramContent = await File.ReadAllTextAsync(programCsPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    diagnostics.Add($"WARNING: File {programCsPath} was deleted during migration.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    diagnostics.Add($"ERROR: Access denied reading {programCsPath}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    diagnostics.Add($"ERROR: I/O error reading {programCsPath}: {ex.Message}");
                }
            }

            generatedFiles.Add(new FileChange
            {
                FilePath = programCsPath,
                Type = programCsExists ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = originalProgramContent,
                NewContent = programCsContent,
                Confidence = programCsConfidence,
                Description = programCsExists
                    ? "Regenerated Program.cs from web.config (existing file will be overwritten)"
                    : "Generated Program.cs from web.config"
            });

            // Add info message about web.config
            diagnostics.Add("INFO: web.config can be backed up and removed after migration. ASP.NET Core does not use web.config for application configuration.");

            // Calculate overall confidence
            var overallConfidence = generatedFiles.Any()
                ? (int)generatedFiles.Average(f => f.Confidence)
                : confidence;

            return new ConfigMigrationResult
            {
                Success = true,
                GeneratedFiles = generatedFiles,
                Diagnostics = diagnostics,
                Confidence = overallConfidence
            };
        }
        catch (Exception ex)
        {
            diagnostics.Add($"ERROR: Configuration migration failed: {ex.Message}");
            return new ConfigMigrationResult
            {
                Success = false,
                GeneratedFiles = generatedFiles,
                Diagnostics = diagnostics,
                Confidence = 0
            };
        }
    }

    private static async Task<XDocument> LoadWebConfigAsync(string path, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return XDocument.Parse(content);
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
        catch (UnauthorizedAccessException)
        {
            // Access denied to directory
            return null;
        }
        catch (IOException)
        {
            // I/O error enumerating files
            return null;
        }
    }

    private static async Task<XDocument?> TryLoadTransformAsync(
        string projectDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            return XDocument.Parse(content);
        }
        catch (FileNotFoundException)
        {
            // Transform file not found
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied to transform file
            return null;
        }
        catch (IOException)
        {
            // I/O error reading transform file
            return null;
        }
        catch (System.Xml.XmlException)
        {
            // Invalid XML in transform file
            return null;
        }
    }

    private static ProgramGenerationOptions DetermineProgramGenerationOptions(SystemWebSection systemWeb, string projectDirectory)
    {
        // Determine what features to enable in Program.cs based on web.config settings
        // This is a basic heuristic and can be enhanced

        // Detect if this is an MVC app with Razor views by checking for Views folder or .cshtml files
        var isMvcWithViews = DetectMvcWithViews(projectDirectory);

        // Try to find DbContext class name
        var dbContextName = DetectDbContextName(projectDirectory);

        return new ProgramGenerationOptions
        {
            IncludeSwagger = true,
            IncludeAuthentication = false, // Will be set by authentication migration
            IncludeSession = false, // Will be set by session migration
            IncludeHealthChecks = true,
            IsMvcWithViews = isMvcWithViews,
            DbContextName = dbContextName
        };
    }

    private static bool DetectMvcWithViews(string projectDirectory)
    {
        // Check for Views folder
        var viewsPath = Path.Combine(projectDirectory, "Views");
        if (Directory.Exists(viewsPath))
        {
            return true;
        }

        // Check for any .cshtml files in the project
        try
        {
            var cshtmlFiles = Directory.GetFiles(projectDirectory, "*.cshtml", SearchOption.AllDirectories);
            return cshtmlFiles.Length > 0;
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied to directory
            return false;
        }
        catch (IOException)
        {
            // I/O error enumerating files
            return false;
        }
    }

    private static string? DetectDbContextName(string projectDirectory)
    {
        // Try to find a class that inherits from DbContext by scanning C# files using Roslyn
        try
        {
            var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
            foreach (var csFile in csFiles)
            {
                // Skip generated files, obj folder, etc.
                if (csFile.Contains("\\obj\\") || csFile.Contains("/obj/") ||
                    csFile.Contains("\\bin\\") || csFile.Contains("/bin/"))
                {
                    continue;
                }

                try
                {
                    var content = File.ReadAllText(csFile);
                    var dbContextClasses = FindDbContextClasses(content);

                    var dbContextName = dbContextClasses.FirstOrDefault();
                    if (dbContextName != null)
                    {
                        return dbContextName;
                    }
                }
                catch (FileNotFoundException)
                {
                    // File was deleted, skip
                }
                catch (UnauthorizedAccessException)
                {
                    // Access denied, skip this file
                }
                catch (IOException)
                {
                    // I/O error, skip this file
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Directory access denied, can't scan for DbContext
        }
        catch (IOException)
        {
            // I/O error enumerating files
        }

        return null;
    }

    /// <summary>
    /// Finds all classes that inherit from DbContext using Roslyn syntax parsing.
    /// Handles partial classes, attributes, generic base types, multiple inheritance, and comments.
    /// </summary>
    private static IEnumerable<string> FindDbContextClasses(string csharpCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(csharpCode);
            var root = tree.GetRoot();

            var dbContextClasses = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => InheritsFromDbContext(c))
                .Select(c => c.Identifier.Text);

            return dbContextClasses;
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Determines if a class declaration inherits from DbContext.
    /// Supports various formats: DbContext, Namespace.DbContext, DbContext&lt;T&gt;
    /// </summary>
    private static bool InheritsFromDbContext(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
            return false;

        return classDecl.BaseList.Types
            .Any(t =>
            {
                var typeName = t.Type.ToString();
                return typeName == "DbContext" ||
                       typeName.EndsWith(".DbContext") ||
                       typeName.StartsWith("DbContext<");
            });
    }
}
