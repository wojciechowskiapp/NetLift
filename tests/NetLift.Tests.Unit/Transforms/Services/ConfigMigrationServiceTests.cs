using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Models;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;
using NetLift.Transforms.Services;

namespace NetLift.Tests.Unit.Transforms.Services;

public sealed class ConfigMigrationServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ConfigMigrationService _service;

    public ConfigMigrationServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"netlift_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        var appSettingsParser = new WebConfigAppSettingsParser();
        var connectionStringParser = new WebConfigConnectionStringParser();
        var systemWebParser = new SystemWebParser();
        var appSettingsJsonGenerator = new AppSettingsJsonGenerator(appSettingsParser);
        var environmentAppSettingsGenerator = new EnvironmentAppSettingsGenerator(appSettingsParser);
        var programCsGenerator = new ProgramCsGenerator();

        _service = new ConfigMigrationService(
            appSettingsParser,
            connectionStringParser,
            systemWebParser,
            appSettingsJsonGenerator,
            environmentAppSettingsGenerator,
            programCsGenerator);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateConfigAsync_WithoutWebConfig_ReturnsSuccessWithNoDiagnostics()
    {
        // Arrange
        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().BeEmpty();
        result.Diagnostics.Should().Contain(d => d.Contains("web.config not found"));
        result.Confidence.Should().Be(100);
    }

    [Fact]
    public async Task MigrateConfigAsync_WithBasicWebConfig_GeneratesAllConfigFiles()
    {
        // Arrange
        var webConfig = CreateBasicWebConfig();
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, webConfig);
        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(4);

        // Check appsettings.json
        result.GeneratedFiles.Should().Contain(f => f.FilePath.EndsWith("appsettings.json"));
        var appSettingsFile = result.GeneratedFiles.First(f => f.FilePath.EndsWith("appsettings.json"));
        appSettingsFile.Type.Should().Be(ChangeType.Create);
        appSettingsFile.NewContent.Should().NotBeNullOrEmpty();

        // Check appsettings.Development.json
        result.GeneratedFiles.Should().Contain(f => f.FilePath.EndsWith("appsettings.Development.json"));

        // Check appsettings.Production.json
        result.GeneratedFiles.Should().Contain(f => f.FilePath.EndsWith("appsettings.Production.json"));

        // Check Program.cs
        result.GeneratedFiles.Should().Contain(f => f.FilePath.EndsWith("Program.cs"));
        var programCsFile = result.GeneratedFiles.First(f => f.FilePath.EndsWith("Program.cs"));
        programCsFile.Type.Should().Be(ChangeType.Create);
        programCsFile.NewContent.Should().Contain("var builder = WebApplication.CreateBuilder(args);");
        programCsFile.Confidence.Should().Be(95);
    }

    [Fact]
    public async Task MigrateConfigAsync_WithExistingProgramCs_ModifiesWithLowerConfidence()
    {
        // Arrange
        var webConfig = CreateBasicWebConfig();
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, webConfig);

        var existingProgramCs = "// Existing Program.cs\nConsole.WriteLine(\"Hello\");";
        var programCsPath = Path.Combine(_tempDirectory, "Program.cs");
        await File.WriteAllTextAsync(programCsPath, existingProgramCs);

        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();

        var programCsFile = result.GeneratedFiles.First(f => f.FilePath.EndsWith("Program.cs"));
        programCsFile.Type.Should().Be(ChangeType.Modify);
        programCsFile.OriginalContent.Should().Be(existingProgramCs);
        programCsFile.Confidence.Should().Be(70);

        result.Diagnostics.Should().Contain(d => d.Contains("Program.cs already exists"));
    }

    [Fact]
    public async Task MigrateConfigAsync_WithConnectionStrings_IncludesInAppSettings()
    {
        // Arrange
        var webConfig = CreateWebConfigWithConnectionStrings();
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, webConfig);
        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();

        var appSettingsFile = result.GeneratedFiles.First(f => f.FilePath.EndsWith("appsettings.json"));
        appSettingsFile.NewContent.Should().Contain("ConnectionStrings");
        appSettingsFile.NewContent.Should().Contain("DefaultConnection");
    }

    [Fact]
    public async Task MigrateConfigAsync_WithAppSettings_IncludesInAppSettings()
    {
        // Arrange
        var webConfig = CreateWebConfigWithAppSettings();
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, webConfig);
        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();

        var appSettingsFile = result.GeneratedFiles.First(f => f.FilePath.EndsWith("appsettings.json"));
        appSettingsFile.NewContent.Should().Contain("MyAppSetting");
        appSettingsFile.NewContent.Should().Contain("MyValue");
    }

    [Fact]
    public async Task MigrateConfigAsync_WithTransformFiles_UsesThemForEnvironmentSettings()
    {
        // Arrange
        var webConfig = CreateBasicWebConfig();
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, webConfig);

        var debugTransform = CreateDebugTransform();
        var debugTransformPath = Path.Combine(_tempDirectory, "Web.Debug.config");
        await File.WriteAllTextAsync(debugTransformPath, debugTransform);

        var releaseTransform = CreateReleaseTransform();
        var releaseTransformPath = Path.Combine(_tempDirectory, "Web.Release.config");
        await File.WriteAllTextAsync(releaseTransformPath, releaseTransform);

        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(4);
    }

    [Fact]
    public async Task MigrateConfigAsync_WithInvalidWebConfig_ReturnsFailure()
    {
        // Arrange
        var invalidWebConfig = "This is not valid XML";
        var webConfigPath = Path.Combine(_tempDirectory, "web.config");
        await File.WriteAllTextAsync(webConfigPath, invalidWebConfig);
        var targetFramework = "net8.0";

        // Act
        var result = await _service.MigrateConfigAsync(_tempDirectory, targetFramework);

        // Assert
        result.Success.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.Diagnostics.Should().Contain(d => d.Contains("Failed to parse web.config"));
    }

    [Fact]
    public void MigrateConfigAsync_WithNullProjectDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.MigrateConfigAsync(null!, "net8.0");
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void MigrateConfigAsync_WithNullTargetFramework_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.MigrateConfigAsync(_tempDirectory, null!);
        act.Should().ThrowAsync<ArgumentException>();
    }

    private static string CreateBasicWebConfig()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <system.web>
    <compilation debug=""true"" targetFramework=""4.8"" />
    <httpRuntime targetFramework=""4.8"" />
  </system.web>
</configuration>";
    }

    private static string CreateWebConfigWithConnectionStrings()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <connectionStrings>
    <add name=""DefaultConnection""
         connectionString=""Server=localhost;Database=MyDb;Trusted_Connection=True;""
         providerName=""System.Data.SqlClient"" />
  </connectionStrings>
  <system.web>
    <compilation debug=""true"" targetFramework=""4.8"" />
  </system.web>
</configuration>";
    }

    private static string CreateWebConfigWithAppSettings()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""MyAppSetting"" value=""MyValue"" />
    <add key=""AnotherSetting"" value=""123"" />
  </appSettings>
  <system.web>
    <compilation debug=""true"" targetFramework=""4.8"" />
  </system.web>
</configuration>";
    }

    private static string CreateDebugTransform()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <system.web>
    <compilation xdt:Transform=""RemoveAttributes(debug)"" />
  </system.web>
</configuration>";
    }

    private static string CreateReleaseTransform()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <system.web>
    <compilation xdt:Transform=""RemoveAttributes(debug)"" />
  </system.web>
</configuration>";
    }
}
