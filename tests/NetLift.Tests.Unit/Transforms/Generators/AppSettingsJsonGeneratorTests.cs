using System.Text.Json;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;

namespace NetLift.Tests.Unit.Transforms.Generators;

public class AppSettingsJsonGeneratorTests
{
    private readonly AppSettingsJsonGenerator _generator;

    public AppSettingsJsonGeneratorTests()
    {
        // Use the real parser for BuildHierarchy functionality
        var parser = new WebConfigAppSettingsParser();
        _generator = new AppSettingsJsonGenerator(parser);
    }

    [Fact]
    public void Generate_WithConnectionStrings_CreatesValidJson()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=localhost;Database=TestDb;",
                    ProviderName = "System.Data.SqlClient"
                },
                new ConnectionStringInfo
                {
                    Name = "SecondaryConnection",
                    ConnectionString = "Server=remote;Database=OtherDb;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();

        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.TryGetProperty("ConnectionStrings", out var connStringsElement).Should().BeTrue();
        connStringsElement.GetProperty("DefaultConnection").GetString()
            .Should().Be("Server=localhost;Database=TestDb;");
        connStringsElement.GetProperty("SecondaryConnection").GetString()
            .Should().Be("Server=remote;Database=OtherDb;");
    }

    [Fact]
    public void Generate_WithDebugMode_SetsDebugLogging()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings
            {
                Debug = true
            }
        };

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.TryGetProperty("Logging", out var loggingElement).Should().BeTrue();
        loggingElement.TryGetProperty("LogLevel", out var logLevelElement).Should().BeTrue();
        logLevelElement.GetProperty("Default").GetString().Should().Be("Debug");
        logLevelElement.GetProperty("Microsoft.AspNetCore").GetString().Should().Be("Debug");
    }

    [Fact]
    public void Generate_WithoutDebugMode_SetsInformationLogging()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings
            {
                Debug = false
            }
        };

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.TryGetProperty("Logging", out var loggingElement).Should().BeTrue();
        loggingElement.TryGetProperty("LogLevel", out var logLevelElement).Should().BeTrue();
        logLevelElement.GetProperty("Default").GetString().Should().Be("Information");
        logLevelElement.GetProperty("Microsoft.AspNetCore").GetString().Should().Be("Warning");
    }

    [Fact]
    public void Generate_WithHttpRuntime_ConfiguresKestrelLimits()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection
        {
            HttpRuntime = new HttpRuntimeSettings
            {
                MaxRequestLengthKb = 4096, // 4MB in KB
                ExecutionTimeoutSeconds = 110 // 1 minute 50 seconds
            }
        };

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.TryGetProperty("Kestrel", out var kestrelElement).Should().BeTrue();
        kestrelElement.TryGetProperty("Limits", out var limitsElement).Should().BeTrue();

        // MaxRequestBodySize should be in bytes (4096 KB = 4194304 bytes)
        limitsElement.GetProperty("MaxRequestBodySize").GetInt64().Should().Be(4194304);

        // RequestHeadersTimeout should be formatted as TimeSpan string
        limitsElement.GetProperty("RequestHeadersTimeout").GetString().Should().Be("00:01:50");
    }

    [Fact]
    public void Generate_WithHierarchicalAppSettings_BuildsNestedStructure()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting
                {
                    Key = "Database:Host",
                    Value = "localhost",
                    KeyPath = ["Database", "Host"]
                },
                new AppSetting
                {
                    Key = "Database:Port",
                    Value = "5432",
                    InferredType = SettingType.Integer,
                    KeyPath = ["Database", "Port"]
                },
                new AppSetting
                {
                    Key = "Features:EnableCache",
                    Value = "true",
                    InferredType = SettingType.Boolean,
                    KeyPath = ["Features", "EnableCache"]
                },
                new AppSetting
                {
                    Key = "SimpleKey",
                    Value = "SimpleValue"
                }
            ]
        };
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        // Check nested Database section
        root.TryGetProperty("Database", out var databaseElement).Should().BeTrue();
        databaseElement.GetProperty("Host").GetString().Should().Be("localhost");
        databaseElement.GetProperty("Port").GetInt64().Should().Be(5432);

        // Check nested Features section
        root.TryGetProperty("Features", out var featuresElement).Should().BeTrue();
        featuresElement.GetProperty("EnableCache").GetBoolean().Should().BeTrue();

        // Check flat key
        root.GetProperty("SimpleKey").GetString().Should().Be("SimpleValue");
    }

    [Fact]
    public void Generate_Always_AddsAllowedHosts()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.TryGetProperty("AllowedHosts", out var allowedHostsElement).Should().BeTrue();
        allowedHostsElement.GetString().Should().Be("*");
    }

    [Fact]
    public void Generate_ProducesProperlyFormattedJson()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "Default",
                    ConnectionString = "Server=localhost",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };
        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting
                {
                    Key = "AppName",
                    Value = "TestApp"
                }
            ]
        };
        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings { Debug = false }
        };

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        json.Should().Contain("  "); // Should have indentation
        json.Should().Contain("ConnectionStrings");
        json.Should().Contain("Logging");
        json.Should().Contain("AllowedHosts");

        // Should be valid JSON
        var action = () => JsonDocument.Parse(json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Generate_WithEmptySections_GeneratesMinimalValidJson()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection();

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        // Should have at least Logging and AllowedHosts
        root.TryGetProperty("Logging", out _).Should().BeTrue();
        root.TryGetProperty("AllowedHosts", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WriteToFileAsync_CreatesFile()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid()}.json");
        try
        {
            var connectionStrings = new ConnectionStringsSection
            {
                ConnectionStrings =
                [
                    new ConnectionStringInfo
                    {
                        Name = "Test",
                        ConnectionString = "Server=test",
                        ProviderName = "System.Data.SqlClient"
                    }
                ]
            };
            var appSettings = new AppSettingsSection();
            var systemWeb = new SystemWebSection();

            // Act
            await _generator.WriteToFileAsync(tempFile, connectionStrings, appSettings, systemWeb);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("ConnectionStrings");
            content.Should().Contain("Test");

            // Should be valid JSON
            var action = () => JsonDocument.Parse(content);
            action.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task WriteToFileAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"netlift-test-{Guid.NewGuid()}");
        var tempFile = Path.Combine(tempDir, "appsettings.json");

        try
        {
            Directory.Exists(tempDir).Should().BeFalse();

            var connectionStrings = new ConnectionStringsSection();
            var appSettings = new AppSettingsSection();
            var systemWeb = new SystemWebSection();

            // Act
            await _generator.WriteToFileAsync(tempFile, connectionStrings, appSettings, systemWeb);

            // Assert
            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task WriteToFileAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection();
        var appSettings = new AppSettingsSection();
        var systemWeb = new SystemWebSection();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _generator.WriteToFileAsync(null!, connectionStrings, appSettings, systemWeb));
    }

    [Fact]
    public void Generate_WithComplexScenario_GeneratesCompleteJson()
    {
        // Arrange
        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "AppDb",
                    ConnectionString = "Server=prod-sql;Database=AppDb;Integrated Security=true;",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting
                {
                    Key = "Api:BaseUrl",
                    Value = "https://api.example.com",
                    KeyPath = ["Api", "BaseUrl"]
                },
                new AppSetting
                {
                    Key = "Api:Timeout",
                    Value = "30",
                    InferredType = SettingType.Integer,
                    KeyPath = ["Api", "Timeout"]
                },
                new AppSetting
                {
                    Key = "Cache:Enabled",
                    Value = "true",
                    InferredType = SettingType.Boolean,
                    KeyPath = ["Cache", "Enabled"]
                },
                new AppSetting
                {
                    Key = "Cache:Duration",
                    Value = "3600",
                    InferredType = SettingType.Integer,
                    KeyPath = ["Cache", "Duration"]
                }
            ]
        };

        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings { Debug = false },
            HttpRuntime = new HttpRuntimeSettings
            {
                MaxRequestLengthKb = 8192,
                ExecutionTimeoutSeconds = 300
            }
        };

        // Act
        var json = _generator.Generate(connectionStrings, appSettings, systemWeb);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        // Verify ConnectionStrings
        root.GetProperty("ConnectionStrings").GetProperty("AppDb").GetString()
            .Should().Contain("prod-sql");

        // Verify hierarchical app settings
        root.GetProperty("Api").GetProperty("BaseUrl").GetString()
            .Should().Be("https://api.example.com");
        root.GetProperty("Api").GetProperty("Timeout").GetInt64().Should().Be(30);
        root.GetProperty("Cache").GetProperty("Enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("Cache").GetProperty("Duration").GetInt64().Should().Be(3600);

        // Verify Logging
        root.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Default").GetString()
            .Should().Be("Information");

        // Verify Kestrel
        root.GetProperty("Kestrel").GetProperty("Limits").GetProperty("MaxRequestBodySize").GetInt64()
            .Should().Be(8388608); // 8192 KB = 8388608 bytes
        root.GetProperty("Kestrel").GetProperty("Limits").GetProperty("RequestHeadersTimeout").GetString()
            .Should().Be("00:05:00"); // 300 seconds = 5 minutes

        // Verify AllowedHosts
        root.GetProperty("AllowedHosts").GetString().Should().Be("*");
    }
}
