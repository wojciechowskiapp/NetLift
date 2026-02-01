using FluentAssertions;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;

namespace NetLift.Tests.Unit.Transforms.Generators;

public sealed class ProgramCsGeneratorTests
{
    private readonly ProgramCsGenerator _generator = new();

    [Fact]
    public void Generate_IncludesWebApplicationBuilder()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("var builder = WebApplication.CreateBuilder(args);");
        result.Should().Contain("var app = builder.Build();");
        result.Should().Contain("app.Run();");
    }

    [Fact]
    public void Generate_IncludesConfigurationSetup()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("builder.Configuration");
        result.Should().Contain("AddJsonFile(\"appsettings.json\"");
        result.Should().Contain("AddJsonFile($\"appsettings.{builder.Environment.EnvironmentName}.json\"");
        result.Should().Contain("AddEnvironmentVariables()");
    }

    [Fact]
    public void Generate_ConfiguresKestrelFromHttpRuntime()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            HttpRuntime = new HttpRuntimeSettings
            {
                MaxRequestLengthKb = 8192,
                ExecutionTimeoutSeconds = 120
            }
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("builder.WebHost.ConfigureKestrel");
        result.Should().Contain("options.Limits.MaxRequestBodySize = 8388608L"); // 8192KB in bytes
        result.Should().Contain("options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(120)");
    }

    [Fact]
    public void Generate_IncludesSwaggerWhenEnabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeSwagger = true };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("builder.Services.AddEndpointsApiExplorer()");
        result.Should().Contain("builder.Services.AddSwaggerGen()");
        result.Should().Contain("app.UseSwagger()");
        result.Should().Contain("app.UseSwaggerUI()");
    }

    [Fact]
    public void Generate_ExcludesSwaggerWhenDisabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeSwagger = false };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().NotContain("AddSwaggerGen");
        result.Should().NotContain("UseSwagger");
    }

    [Fact]
    public void Generate_ConfiguresExceptionHandlerFromCustomErrors_Off()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            CustomErrors = new CustomErrorSettings
            {
                Mode = CustomErrorMode.Off
            }
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("app.UseDeveloperExceptionPage()");
        result.Should().NotContain("app.UseExceptionHandler");
    }

    [Fact]
    public void Generate_ConfiguresExceptionHandlerFromCustomErrors_On()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            CustomErrors = new CustomErrorSettings
            {
                Mode = CustomErrorMode.On,
                DefaultRedirect = "/CustomError"
            }
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("app.UseExceptionHandler(\"/CustomError\")");
        result.Should().NotContain("UseDeveloperExceptionPage");
    }

    [Fact]
    public void Generate_ConfiguresExceptionHandlerFromCustomErrors_RemoteOnly()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            CustomErrors = new CustomErrorSettings
            {
                Mode = CustomErrorMode.RemoteOnly,
                DefaultRedirect = "/Error"
            }
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("if (app.Environment.IsDevelopment())");
        result.Should().Contain("app.UseDeveloperExceptionPage()");
        result.Should().Contain("app.UseExceptionHandler(\"/Error\")");
        result.Should().Contain("app.UseHsts()");
    }

    [Fact]
    public void Generate_UsesDefaultErrorPathWhenNotSpecified()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            CustomErrors = new CustomErrorSettings
            {
                Mode = CustomErrorMode.On
            }
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("app.UseExceptionHandler(\"/Error\")");
    }

    [Fact]
    public void Generate_GeneratesOptionsBindingForHierarchicalSettings()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection
        {
            Settings = new[]
            {
                new AppSetting
                {
                    Key = "Azure:StorageAccountName",
                    Value = "mystorageaccount",
                    KeyPath = new[] { "Azure", "StorageAccountName" }
                },
                new AppSetting
                {
                    Key = "Azure:StorageAccountKey",
                    Value = "mykey",
                    KeyPath = new[] { "Azure", "StorageAccountKey" }
                },
                new AppSetting
                {
                    Key = "Email:SmtpServer",
                    Value = "smtp.example.com",
                    KeyPath = new[] { "Email", "SmtpServer" }
                },
                new AppSetting
                {
                    Key = "SimpleKey",
                    Value = "SimpleValue"
                }
            }
        };
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("Configure options binding for hierarchical settings");
        result.Should().Contain("Configure<AzureOptions>(builder.Configuration.GetSection(\"Azure\"))");
        result.Should().Contain("Configure<EmailOptions>(builder.Configuration.GetSection(\"Email\"))");
        result.Should().NotContain("SimpleKeyOptions");
    }

    [Fact]
    public void Generate_IncludesHealthChecksWhenEnabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeHealthChecks = true };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("using Microsoft.AspNetCore.Diagnostics.HealthChecks;");
        result.Should().Contain("builder.Services.AddHealthChecks()");
        result.Should().Contain("app.MapHealthChecks(\"/health\")");
    }

    [Fact]
    public void Generate_ExcludesHealthChecksWhenDisabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeHealthChecks = false };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().NotContain("AddHealthChecks");
        result.Should().NotContain("MapHealthChecks");
    }

    [Fact]
    public void Generate_IncludesAuthenticationWhenEnabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeAuthentication = true };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("builder.Services.AddAuthentication()");
        result.Should().Contain("builder.Services.AddAuthorization()");
        result.Should().Contain("app.UseAuthentication()");
    }

    [Fact]
    public void Generate_ExcludesAuthenticationWhenDisabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeAuthentication = false };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().NotContain("AddAuthentication");
        result.Should().Contain("app.UseAuthorization()"); // Authorization is always included
    }

    [Fact]
    public void Generate_IncludesSessionWhenEnabled()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions { IncludeSession = true };

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("builder.Services.AddDistributedMemoryCache()");
        result.Should().Contain("builder.Services.AddSession");
        result.Should().Contain("app.UseSession()");
    }

    [Fact]
    public void Generate_IncludesStandardMiddleware()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().Contain("app.UseHttpsRedirection()");
        result.Should().Contain("app.UseStaticFiles()");
        result.Should().Contain("app.UseRouting()");
        result.Should().Contain("app.UseAuthorization()");
        result.Should().Contain("app.MapControllers()");
    }

    [Fact]
    public async Task WriteToFileAsync_CreatesFileWithCorrectContent()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();
        var tempFile = Path.Combine(Path.GetTempPath(), $"Program_{Guid.NewGuid()}.cs");

        try
        {
            // Act
            await _generator.WriteToFileAsync(tempFile, systemWeb, appSettings, options);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("var builder = WebApplication.CreateBuilder(args);");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Generate_DoesNotConfigureKestrelWhenHttpRuntimeIsNull()
    {
        // Arrange
        var systemWeb = new SystemWebSection
        {
            HttpRuntime = null
        };
        var appSettings = new AppSettingsSection();
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().NotContain("ConfigureKestrel");
    }

    [Fact]
    public void Generate_DoesNotGenerateOptionsBindingWhenNoHierarchicalSettings()
    {
        // Arrange
        var systemWeb = new SystemWebSection();
        var appSettings = new AppSettingsSection
        {
            Settings = new[]
            {
                new AppSetting
                {
                    Key = "SimpleKey1",
                    Value = "Value1"
                },
                new AppSetting
                {
                    Key = "SimpleKey2",
                    Value = "Value2"
                }
            }
        };
        var options = new ProgramGenerationOptions();

        // Act
        var result = _generator.Generate(systemWeb, appSettings, options);

        // Assert
        result.Should().NotContain("Configure options binding");
        result.Should().NotContain("Configure<");
    }
}
