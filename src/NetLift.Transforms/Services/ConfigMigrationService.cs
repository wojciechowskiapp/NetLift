using System.Xml.Linq;
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
            // Find web.config
            var webConfigPath = Path.Combine(projectDirectory, "web.config");
            if (!File.Exists(webConfigPath))
            {
                // Try case-insensitive search
                var files = Directory.GetFiles(projectDirectory, "web.config", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
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
                webConfigPath = files[0];
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

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsJsonPath,
                Type = File.Exists(appSettingsJsonPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = File.Exists(appSettingsJsonPath) ? await File.ReadAllTextAsync(appSettingsJsonPath, cancellationToken) : null,
                NewContent = appSettingsJsonContent,
                Confidence = confidence,
                Description = "Generated appsettings.json from web.config"
            });

            // Generate appsettings.Development.json
            var appSettingsDevPath = Path.Combine(projectDirectory, "appsettings.Development.json");
            var appSettingsDevContent = _environmentAppSettingsGenerator.GenerateDevelopment(
                connectionStrings, appSettings, systemWeb, debugTransform);

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsDevPath,
                Type = File.Exists(appSettingsDevPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = File.Exists(appSettingsDevPath) ? await File.ReadAllTextAsync(appSettingsDevPath, cancellationToken) : null,
                NewContent = appSettingsDevContent,
                Confidence = confidence,
                Description = "Generated appsettings.Development.json from web.config and Web.Debug.config"
            });

            // Generate appsettings.Production.json
            var appSettingsProdPath = Path.Combine(projectDirectory, "appsettings.Production.json");
            var appSettingsProdContent = _environmentAppSettingsGenerator.GenerateProduction(
                connectionStrings, appSettings, systemWeb, releaseTransform);

            generatedFiles.Add(new FileChange
            {
                FilePath = appSettingsProdPath,
                Type = File.Exists(appSettingsProdPath) ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = File.Exists(appSettingsProdPath) ? await File.ReadAllTextAsync(appSettingsProdPath, cancellationToken) : null,
                NewContent = appSettingsProdContent,
                Confidence = confidence,
                Description = "Generated appsettings.Production.json from web.config and Web.Release.config"
            });

            // Generate Program.cs
            var programCsPath = Path.Combine(projectDirectory, "Program.cs");
            var programGenerationOptions = DetermineProgramGenerationOptions(systemWeb);
            var programCsContent = _programCsGenerator.Generate(systemWeb, appSettings, programGenerationOptions);

            // Check if Program.cs already exists
            var programCsExists = File.Exists(programCsPath);
            var programCsConfidence = programCsExists ? 70 : 95; // Lower confidence if overwriting existing Program.cs

            if (programCsExists)
            {
                diagnostics.Add("INFO: Program.cs already exists and will be overwritten. Review the generated file carefully.");
            }

            generatedFiles.Add(new FileChange
            {
                FilePath = programCsPath,
                Type = programCsExists ? ChangeType.Modify : ChangeType.Create,
                OriginalContent = programCsExists ? await File.ReadAllTextAsync(programCsPath, cancellationToken) : null,
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
        catch
        {
            return null;
        }
    }

    private static ProgramGenerationOptions DetermineProgramGenerationOptions(SystemWebSection systemWeb)
    {
        // Determine what features to enable in Program.cs based on web.config settings
        // This is a basic heuristic and can be enhanced
        return new ProgramGenerationOptions
        {
            IncludeSwagger = true,
            IncludeAuthentication = false, // Will be set by authentication migration
            IncludeSession = false, // Will be set by session migration
            IncludeHealthChecks = true
        };
    }
}
