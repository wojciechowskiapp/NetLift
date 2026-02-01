using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Generators;

/// <summary>
/// Unit tests for <see cref="EnvironmentAppSettingsGenerator"/>.
/// </summary>
public class EnvironmentAppSettingsGeneratorTests
{
    private readonly EnvironmentAppSettingsGenerator _generator;
    private readonly WebConfigAppSettingsParser _appSettingsParser;

    public EnvironmentAppSettingsGeneratorTests()
    {
        _appSettingsParser = new WebConfigAppSettingsParser();
        _generator = new EnvironmentAppSettingsGenerator(_appSettingsParser);
    }

    [Fact]
    public void GenerateDevelopment_ShouldSetDebugLogLevel()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("Logging", out var logging).Should().BeTrue();
        logging.TryGetProperty("LogLevel", out var logLevel).Should().BeTrue();
        logLevel.TryGetProperty("Default", out var defaultLevel).Should().BeTrue();
        defaultLevel.GetString().Should().Be("Debug");
    }

    [Fact]
    public void GenerateDevelopment_ShouldEnableDetailedErrors()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("DetailedErrors", out var detailedErrors).Should().BeTrue();
        detailedErrors.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void GenerateDevelopment_ShouldIncludeEfCoreLogging()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("Logging", out var logging).Should().BeTrue();
        logging.TryGetProperty("LogLevel", out var logLevel).Should().BeTrue();
        logLevel.TryGetProperty("Microsoft.EntityFrameworkCore", out var efCoreLevel).Should().BeTrue();
        efCoreLevel.GetString().Should().Be("Information");
    }

    [Fact]
    public void GenerateDevelopment_ShouldUseActualConnectionStrings()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=localhost;Database=DevDb;Trusted_Connection=True;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("DefaultConnection", out var defaultConn).Should().BeTrue();
        defaultConn.GetString().Should().Be("Server=localhost;Database=DevDb;Trusted_Connection=True;");
    }

    [Fact]
    public void GenerateProduction_ShouldSetWarningLogLevel()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("Logging", out var logging).Should().BeTrue();
        logging.TryGetProperty("LogLevel", out var logLevel).Should().BeTrue();
        logLevel.TryGetProperty("Default", out var defaultLevel).Should().BeTrue();
        defaultLevel.GetString().Should().Be("Warning");
    }

    [Fact]
    public void GenerateProduction_ShouldUseEnvironmentVariablePlaceholders()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=localhost;Database=DevDb;Trusted_Connection=True;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("DefaultConnection", out var defaultConn).Should().BeTrue();
        defaultConn.GetString().Should().Be("${CONNECTION_STRING_DEFAULTCONNECTION}");
    }

    [Fact]
    public void GenerateProduction_ShouldIncludeKestrelHttpsEndpoint()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("Kestrel", out var kestrel).Should().BeTrue();
        kestrel.TryGetProperty("Endpoints", out var endpoints).Should().BeTrue();
        endpoints.TryGetProperty("Https", out var https).Should().BeTrue();
        https.TryGetProperty("Url", out var url).Should().BeTrue();
        url.GetString().Should().Be("https://*:443");
    }

    [Fact]
    public void GenerateProduction_ShouldIncludeApplicationInsightsPlaceholder()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ApplicationInsights", out var appInsights).Should().BeTrue();
        appInsights.TryGetProperty("InstrumentationKey", out var key).Should().BeTrue();
        key.GetString().Should().Be("${APPLICATIONINSIGHTS_INSTRUMENTATIONKEY}");
    }

    [Fact]
    public void GenerateDevelopment_ShouldApplyDebugTransform()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=prod;Database=ProdDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        var debugTransform = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <connectionStrings>
    <add name=""DefaultConnection""
         connectionString=""Server=localhost;Database=DevDb;Integrated Security=True;""
         providerName=""System.Data.SqlClient""
         xdt:Transform=""SetAttributes""
         xdt:Locator=""Match(name)"" />
  </connectionStrings>
</configuration>");

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb, debugTransform);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("DefaultConnection", out var defaultConn).Should().BeTrue();
        defaultConn.GetString().Should().Be("Server=localhost;Database=DevDb;Integrated Security=True;");
    }

    [Fact]
    public void GenerateProduction_ShouldApplyReleaseTransform()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=dev;Database=DevDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        var releaseTransform = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <connectionStrings>
    <add name=""DefaultConnection""
         connectionString=""Server=prod;Database=ProdDb;Integrated Security=True;""
         providerName=""System.Data.SqlClient""
         xdt:Transform=""SetAttributes""
         xdt:Locator=""Match(name)"" />
  </connectionStrings>
</configuration>");

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb, releaseTransform);
        var document = JsonDocument.Parse(json);

        // Assert - Should still use placeholder even with transform (transform is applied first)
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("DefaultConnection", out var defaultConn).Should().BeTrue();
        defaultConn.GetString().Should().Be("${CONNECTION_STRING_DEFAULTCONNECTION}");
    }

    [Fact]
    public void GenerateDevelopment_ShouldHandleInsertTransform()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings = []
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        var debugTransform = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <connectionStrings>
    <add name=""NewConnection""
         connectionString=""Server=localhost;Database=NewDb;Integrated Security=True;""
         providerName=""System.Data.SqlClient""
         xdt:Transform=""Insert"" />
  </connectionStrings>
</configuration>");

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb, debugTransform);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("NewConnection", out var newConn).Should().BeTrue();
        newConn.GetString().Should().Be("Server=localhost;Database=NewDb;Integrated Security=True;");
    }

    [Fact]
    public void GenerateDevelopment_ShouldHandleRemoveTransform()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "ToBeRemoved",
                    ConnectionString = "Server=localhost;Database=OldDb;",
                    ProviderName = "System.Data.SqlClient"
                },
                new ConnectionStringInfo
                {
                    Name = "KeepThis",
                    ConnectionString = "Server=localhost;Database=KeepDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        var debugTransform = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <connectionStrings>
    <add name=""ToBeRemoved"" xdt:Transform=""Remove"" xdt:Locator=""Match(name)"" />
  </connectionStrings>
</configuration>");

        // Act
        var json = _generator.GenerateDevelopment(connectionStrings, appSettings, systemWeb, debugTransform);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("ToBeRemoved", out _).Should().BeFalse();
        connStrings.TryGetProperty("KeepThis", out var keepThis).Should().BeTrue();
        keepThis.GetString().Should().Be("Server=localhost;Database=KeepDb;");
    }

    [Fact]
    public void GenerateProduction_ShouldSanitizeConnectionStringNamesWithSpecialChars()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "My-Custom.Connection_String",
                    ConnectionString = "Server=localhost;Database=TestDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.GenerateProduction(connectionStrings, appSettings, systemWeb);
        var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("My-Custom.Connection_String", out var customConn).Should().BeTrue();
        customConn.GetString().Should().Be("${CONNECTION_STRING_MY_CUSTOM_CONNECTION_STRING}");
    }

    [Fact]
    public async Task WriteEnvironmentFilesAsync_ShouldCreateBothFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=localhost;Database=TestDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        try
        {
            // Act
            await _generator.WriteEnvironmentFilesAsync(
                tempDir,
                connectionStrings,
                appSettings,
                systemWeb);

            // Assert
            var devPath = Path.Combine(tempDir, "appsettings.Development.json");
            var prodPath = Path.Combine(tempDir, "appsettings.Production.json");

            File.Exists(devPath).Should().BeTrue();
            File.Exists(prodPath).Should().BeTrue();

            var devContent = await File.ReadAllTextAsync(devPath);
            var prodContent = await File.ReadAllTextAsync(prodPath);

            devContent.Should().Contain("\"DetailedErrors\": true");
            prodContent.Should().Contain("${CONNECTION_STRING_DEFAULTCONNECTION}");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task WriteEnvironmentFilesAsync_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested", "path");
        var connectionStrings = new ConnectionStringsSection { ConnectionStrings = [] };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        try
        {
            // Act
            await _generator.WriteEnvironmentFilesAsync(
                tempDir,
                connectionStrings,
                appSettings,
                systemWeb);

            // Assert
            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "appsettings.Development.json")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "appsettings.Production.json")).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            var rootTemp = Path.Combine(Path.GetTempPath(), Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(tempDir))!));
            if (Directory.Exists(rootTemp))
            {
                Directory.Delete(rootTemp, true);
            }
        }
    }

    [Fact]
    public void WriteEnvironmentFilesAsync_ShouldThrowWhenOutputDirectoryIsNull()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection { ConnectionStrings = [] };
        var appSettings = new AppSettingsSection { Settings = [] };
        var systemWeb = new SystemWebSection();

        // Act & Assert
        var act = async () => await _generator.WriteEnvironmentFilesAsync(
            null!,
            connectionStrings,
            appSettings,
            systemWeb);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Output directory cannot be null or whitespace.*");
    }

    [Fact]
    public void Constructor_ShouldThrowWhenAppSettingsParserIsNull()
    {
        // Act & Assert
        var act = () => new EnvironmentAppSettingsGenerator(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("appSettingsParser");
    }
}
