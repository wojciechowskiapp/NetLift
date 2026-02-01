using NetLift.Analysis.Config;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;

namespace NetLift.Tests.Unit.Transforms.Generators;

/// <summary>
/// This is an example showing typical generated appsettings.json output.
/// Run this test and check the output to see what gets generated.
/// </summary>
public class AppSettingsJsonGenerator_Example
{
    [Fact]
    public void Example_CompleteWebConfigMigration()
    {
        // Arrange - Simulating a typical ASP.NET MVC 5 web.config
        var parser = new WebConfigAppSettingsParser();
        var generator = new AppSettingsJsonGenerator(parser);

        var connectionStrings = new ConnectionStringsSection
        {
            ConnectionStrings =
            [
                new ConnectionStringInfo
                {
                    Name = "DefaultConnection",
                    ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=MyApp;Trusted_Connection=True;MultipleActiveResultSets=true",
                    ProviderName = "System.Data.SqlClient"
                }
            ]
        };

        var appSettings = new AppSettingsSection
        {
            Settings =
            [
                new AppSetting { Key = "Environment", Value = "Production" },
                new AppSetting { Key = "Api:BaseUrl", Value = "https://api.myapp.com", KeyPath = ["Api", "BaseUrl"] },
                new AppSetting { Key = "Api:Timeout", Value = "30", InferredType = SettingType.Integer, KeyPath = ["Api", "Timeout"] },
                new AppSetting { Key = "Api:RetryCount", Value = "3", InferredType = SettingType.Integer, KeyPath = ["Api", "RetryCount"] },
                new AppSetting { Key = "Cache:Enabled", Value = "true", InferredType = SettingType.Boolean, KeyPath = ["Cache", "Enabled"] },
                new AppSetting { Key = "Cache:AbsoluteExpiration", Value = "3600", InferredType = SettingType.Integer, KeyPath = ["Cache", "AbsoluteExpiration"] },
                new AppSetting { Key = "EmailSettings:SmtpServer", Value = "smtp.gmail.com", KeyPath = ["EmailSettings", "SmtpServer"] },
                new AppSetting { Key = "EmailSettings:Port", Value = "587", InferredType = SettingType.Integer, KeyPath = ["EmailSettings", "Port"] },
                new AppSetting { Key = "EmailSettings:UseSsl", Value = "true", InferredType = SettingType.Boolean, KeyPath = ["EmailSettings", "UseSsl"] }
            ]
        };

        var systemWeb = new SystemWebSection
        {
            Compilation = new CompilationSettings
            {
                Debug = false,
                TargetFramework = "4.8"
            },
            HttpRuntime = new HttpRuntimeSettings
            {
                MaxRequestLengthKb = 4096,
                ExecutionTimeoutSeconds = 110
            }
        };

        // Act
        var json = generator.Generate(connectionStrings, appSettings, systemWeb);

        // Output for documentation
        System.Console.WriteLine("Generated appsettings.json:");
        System.Console.WriteLine(json);

        /* Expected output:
        {
          "ConnectionStrings": {
            "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyApp;Trusted_Connection=True;MultipleActiveResultSets=true"
          },
          "Environment": "Production",
          "Api": {
            "BaseUrl": "https://api.myapp.com",
            "Timeout": 30,
            "RetryCount": 3
          },
          "Cache": {
            "Enabled": true,
            "AbsoluteExpiration": 3600
          },
          "EmailSettings": {
            "SmtpServer": "smtp.gmail.com",
            "Port": 587,
            "UseSsl": true
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "Kestrel": {
            "Limits": {
              "MaxRequestBodySize": 4194304,
              "RequestHeadersTimeout": "00:01:50"
            }
          },
          "AllowedHosts": "*"
        }
        */
    }
}
