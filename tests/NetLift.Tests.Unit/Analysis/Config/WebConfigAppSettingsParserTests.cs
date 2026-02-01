using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Tests.Unit.Analysis.Config;

public class WebConfigAppSettingsParserTests
{
    private readonly IWebConfigAppSettingsParser _parser;

    public WebConfigAppSettingsParserTests()
    {
        _parser = new WebConfigAppSettingsParser();
    }

    [Fact]
    public void Parse_NullDocument_ReturnsEmptySection()
    {
        // Act
        var result = _parser.Parse(null!);

        // Assert
        result.Should().NotBeNull();
        result.Settings.Should().BeEmpty();
        result.ExternalFile.Should().BeNull();
        result.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public void Parse_NoAppSettingsSection_ReturnsEmptySection()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <compilation debug=""true"" />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Should().NotBeNull();
        result.Settings.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyAppSettings_ReturnsEmptySettings()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().BeEmpty();
    }

    [Fact]
    public void Parse_FlatKeyValuePairs_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""AppName"" value=""MyApp"" />
    <add key=""Version"" value=""1.0.0"" />
    <add key=""Environment"" value=""Production"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(3);
        result.Settings.Should().Contain(s => s.Key == "AppName" && s.Value == "MyApp");
        result.Settings.Should().Contain(s => s.Key == "Version" && s.Value == "1.0.0");
        result.Settings.Should().Contain(s => s.Key == "Environment" && s.Value == "Production");
    }

    [Fact]
    public void Parse_NestedKeysWithColon_ExtractsKeyPath()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Database:ConnectionString"" value=""Server=localhost"" />
    <add key=""Database:Timeout"" value=""30"" />
    <add key=""Logging:Level"" value=""Information"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(3);

        var dbConn = result.Settings.First(s => s.Key == "Database:ConnectionString");
        dbConn.KeyPath.Should().NotBeNull();
        dbConn.KeyPath.Should().Equal("Database", "ConnectionString");

        var dbTimeout = result.Settings.First(s => s.Key == "Database:Timeout");
        dbTimeout.KeyPath.Should().NotBeNull();
        dbTimeout.KeyPath.Should().Equal("Database", "Timeout");

        var logLevel = result.Settings.First(s => s.Key == "Logging:Level");
        logLevel.KeyPath.Should().NotBeNull();
        logLevel.KeyPath.Should().Equal("Logging", "Level");
    }

    [Fact]
    public void Parse_NestedKeysWithDot_ExtractsKeyPath()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Database.ConnectionString"" value=""Server=localhost"" />
    <add key=""Database.Timeout"" value=""30"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(2);

        var dbConn = result.Settings.First(s => s.Key == "Database.ConnectionString");
        dbConn.KeyPath.Should().NotBeNull();
        dbConn.KeyPath.Should().Equal("Database", "ConnectionString");
    }

    [Fact]
    public void Parse_TypeInference_String()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""AppName"" value=""MyApplication"" />
    <add key=""ConnectionString"" value=""Server=localhost;Database=MyDb"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().AllSatisfy(s => s.InferredType.Should().Be(SettingType.String));
    }

    [Fact]
    public void Parse_TypeInference_Boolean()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""EnableFeature"" value=""true"" />
    <add key=""DebugMode"" value=""false"" />
    <add key=""IsProduction"" value=""True"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().AllSatisfy(s => s.InferredType.Should().Be(SettingType.Boolean));
    }

    [Fact]
    public void Parse_TypeInference_Integer()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""MaxConnections"" value=""100"" />
    <add key=""Timeout"" value=""30"" />
    <add key=""RetryCount"" value=""3"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().AllSatisfy(s => s.InferredType.Should().Be(SettingType.Integer));
    }

    [Fact]
    public void Parse_TypeInference_Double()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Threshold"" value=""99.5"" />
    <add key=""Rate"" value=""1.25"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().AllSatisfy(s => s.InferredType.Should().Be(SettingType.Double));
    }

    [Fact]
    public void Parse_TypeInference_Json()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Config"" value=""{ &quot;key&quot;: &quot;value&quot; }"" />
    <add key=""Items"" value=""[1, 2, 3]"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().AllSatisfy(s => s.InferredType.Should().Be(SettingType.Json));
    }

    [Fact]
    public void Parse_ExternalFile_ExtractsFileAttribute()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings file=""appsettings.config"">
    <add key=""Setting1"" value=""Value1"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ExternalFile.Should().Be("appsettings.config");
        result.Settings.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_EncryptedSection_DetectsEncryption()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings configProtectionProvider=""RsaProtectedConfigurationProvider"">
    <EncryptedData>
      <CipherData>
        <CipherValue>encrypted-data-here</CipherValue>
      </CipherData>
    </EncryptedData>
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void Parse_MissingKeyAttribute_IgnoresEntry()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add value=""SomeValue"" />
    <add key=""ValidKey"" value=""ValidValue"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Key.Should().Be("ValidKey");
    }

    [Fact]
    public void Parse_EmptyKeyAttribute_IgnoresEntry()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key="""" value=""SomeValue"" />
    <add key=""ValidKey"" value=""ValidValue"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Key.Should().Be("ValidKey");
    }

    [Fact]
    public void ParseWithTransforms_NullTransform_ReturnsBaseConfig()
    {
        // Arrange
        var baseXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Setting1"" value=""Value1"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.ParseWithTransforms(baseXml, null);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Key.Should().Be("Setting1");
        result.Settings[0].Value.Should().Be("Value1");
    }

    [Fact]
    public void ParseWithTransforms_ReplaceTransform_ReplacesValue()
    {
        // Arrange
        var baseXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Environment"" value=""Development"" />
  </appSettings>
</configuration>");

        var transformXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <appSettings>
    <add key=""Environment"" value=""Production"" xdt:Transform=""Replace"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Value.Should().Be("Production");
    }

    [Fact]
    public void ParseWithTransforms_SetAttributesTransform_UpdatesValue()
    {
        // Arrange
        var baseXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""ConnectionString"" value=""Server=localhost"" />
  </appSettings>
</configuration>");

        var transformXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <appSettings>
    <add key=""ConnectionString"" value=""Server=production"" xdt:Transform=""SetAttributes"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Value.Should().Be("Server=production");
    }

    [Fact]
    public void ParseWithTransforms_RemoveTransform_RemovesEntry()
    {
        // Arrange
        var baseXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Setting1"" value=""Value1"" />
    <add key=""Setting2"" value=""Value2"" />
  </appSettings>
</configuration>");

        var transformXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <appSettings>
    <add key=""Setting1"" xdt:Transform=""Remove"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.Settings.Should().HaveCount(1);
        result.Settings[0].Key.Should().Be("Setting2");
    }

    [Fact]
    public void ParseWithTransforms_InsertTransform_AddsNewEntry()
    {
        // Arrange
        var baseXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""Setting1"" value=""Value1"" />
  </appSettings>
</configuration>");

        var transformXml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <appSettings>
    <add key=""Setting2"" value=""Value2"" xdt:Transform=""Insert"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.Settings.Should().HaveCount(2);
        result.Settings.Should().Contain(s => s.Key == "Setting1");
        result.Settings.Should().Contain(s => s.Key == "Setting2");
    }

    [Fact]
    public void BuildHierarchy_FlatKeys_ReturnsFlatDictionary()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new() { Key = "AppName", Value = "MyApp", InferredType = SettingType.String },
                new() { Key = "MaxConnections", Value = "100", InferredType = SettingType.Integer }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result.Should().HaveCount(2);
        result["AppName"].Should().Be("MyApp");
        result["MaxConnections"].Should().Be(100L);
    }

    [Fact]
    public void BuildHierarchy_NestedKeys_CreatesHierarchy()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new()
                {
                    Key = "Database:ConnectionString",
                    Value = "Server=localhost",
                    InferredType = SettingType.String,
                    KeyPath = new[] { "Database", "ConnectionString" }
                },
                new()
                {
                    Key = "Database:Timeout",
                    Value = "30",
                    InferredType = SettingType.Integer,
                    KeyPath = new[] { "Database", "Timeout" }
                }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("Database");
        var database = result["Database"] as Dictionary<string, object>;
        database.Should().NotBeNull();
        database!["ConnectionString"].Should().Be("Server=localhost");
        database["Timeout"].Should().Be(30L);
    }

    [Fact]
    public void BuildHierarchy_MixedFlatAndNested_CreatesCorrectStructure()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new() { Key = "AppName", Value = "MyApp", InferredType = SettingType.String },
                new()
                {
                    Key = "Database:ConnectionString",
                    Value = "Server=localhost",
                    InferredType = SettingType.String,
                    KeyPath = new[] { "Database", "ConnectionString" }
                },
                new() { Key = "Version", Value = "1.0.0", InferredType = SettingType.String }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result.Should().HaveCount(3);
        result["AppName"].Should().Be("MyApp");
        result["Version"].Should().Be("1.0.0");

        var database = result["Database"] as Dictionary<string, object>;
        database.Should().NotBeNull();
        database!["ConnectionString"].Should().Be("Server=localhost");
    }

    [Fact]
    public void BuildHierarchy_BooleanValues_ConvertedCorrectly()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new() { Key = "EnableFeature", Value = "true", InferredType = SettingType.Boolean },
                new() { Key = "DebugMode", Value = "false", InferredType = SettingType.Boolean }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result["EnableFeature"].Should().Be(true);
        result["DebugMode"].Should().Be(false);
    }

    [Fact]
    public void BuildHierarchy_DoubleValues_ConvertedCorrectly()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new() { Key = "Threshold", Value = "99.5", InferredType = SettingType.Double },
                new() { Key = "Rate", Value = "1.25", InferredType = SettingType.Double }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result["Threshold"].Should().Be(99.5);
        result["Rate"].Should().Be(1.25);
    }

    [Fact]
    public void BuildHierarchy_JsonValues_KeptAsString()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new() { Key = "Config", Value = @"{ ""key"": ""value"" }", InferredType = SettingType.Json }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result["Config"].Should().Be(@"{ ""key"": ""value"" }");
    }

    [Fact]
    public void BuildHierarchy_DeepNesting_CreatesMultipleLevels()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = new List<AppSetting>
            {
                new()
                {
                    Key = "App:Database:Primary:ConnectionString",
                    Value = "Server=localhost",
                    InferredType = SettingType.String,
                    KeyPath = new[] { "App", "Database", "Primary", "ConnectionString" }
                }
            }
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result.Should().ContainKey("App");
        var app = result["App"] as Dictionary<string, object>;
        app.Should().NotBeNull();

        var database = app!["Database"] as Dictionary<string, object>;
        database.Should().NotBeNull();

        var primary = database!["Primary"] as Dictionary<string, object>;
        primary.Should().NotBeNull();

        primary!["ConnectionString"].Should().Be("Server=localhost");
    }

    [Fact]
    public void BuildHierarchy_EmptySection_ReturnsEmptyDictionary()
    {
        // Arrange
        var section = new AppSettingsSection
        {
            Settings = Array.Empty<AppSetting>()
        };

        // Act
        var result = _parser.BuildHierarchy(section);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ComplexRealWorldExample_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
    <add key=""AppName"" value=""MyApplication"" />
    <add key=""EnableLogging"" value=""true"" />
    <add key=""MaxRetries"" value=""3"" />
    <add key=""Timeout"" value=""30.5"" />
    <add key=""Database:ConnectionString"" value=""Server=localhost;Database=MyDb"" />
    <add key=""Database:CommandTimeout"" value=""60"" />
    <add key=""Logging:Level"" value=""Information"" />
    <add key=""Logging:EnableConsole"" value=""false"" />
  </appSettings>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Settings.Should().HaveCount(8);

        // Verify flat keys
        result.Settings.Should().Contain(s => s.Key == "AppName" && s.InferredType == SettingType.String);
        result.Settings.Should().Contain(s => s.Key == "EnableLogging" && s.InferredType == SettingType.Boolean);
        result.Settings.Should().Contain(s => s.Key == "MaxRetries" && s.InferredType == SettingType.Integer);
        result.Settings.Should().Contain(s => s.Key == "Timeout" && s.InferredType == SettingType.Double);

        // Verify nested keys
        var dbConn = result.Settings.First(s => s.Key == "Database:ConnectionString");
        dbConn.KeyPath.Should().Equal("Database", "ConnectionString");

        var logLevel = result.Settings.First(s => s.Key == "Logging:Level");
        logLevel.KeyPath.Should().Equal("Logging", "Level");
    }
}
