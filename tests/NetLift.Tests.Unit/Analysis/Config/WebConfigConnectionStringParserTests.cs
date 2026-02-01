using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using Xunit;

namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class WebConfigConnectionStringParserTests
{
    private readonly WebConfigConnectionStringParser _parser = new();

    [Fact]
    public void Parse_WithNoConnectionStrings_ReturnsEmptySection()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().BeEmpty();
        result.HasEncryptedStrings.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithSingleConnectionString_ExtractsCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb;Integrated Security=true;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
        result.ConnectionStrings[0].ConnectionString.Should().Be("Server=localhost;Database=MyDb;Integrated Security=true;");
        result.ConnectionStrings[0].ProviderName.Should().Be("System.Data.SqlClient");
        result.ConnectionStrings[0].IsEncrypted.Should().BeFalse();
        result.HasEncryptedStrings.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithMultipleConnectionStrings_ExtractsAll()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb;"
                     providerName="System.Data.SqlClient" />
                <add name="RedisCache"
                     connectionString="localhost:6379"
                     providerName="StackExchange.Redis" />
                <add name="OracleDb"
                     connectionString="Data Source=OracleDB;User Id=myUser;Password=myPass;"
                     providerName="Oracle.ManagedDataAccess.Client" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(3);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
        result.ConnectionStrings[0].ProviderName.Should().Be("System.Data.SqlClient");
        result.ConnectionStrings[1].Name.Should().Be("RedisCache");
        result.ConnectionStrings[1].ProviderName.Should().Be("StackExchange.Redis");
        result.ConnectionStrings[2].Name.Should().Be("OracleDb");
        result.ConnectionStrings[2].ProviderName.Should().Be("Oracle.ManagedDataAccess.Client");
    }

    [Fact]
    public void Parse_WithMissingProviderName_DefaultsToSqlClient()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb;" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ProviderName.Should().Be("System.Data.SqlClient");
    }

    [Fact]
    public void Parse_WithLocalDbConnectionString_ExtractsCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="LocalDb"
                     connectionString="Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\MyDb.mdf;Integrated Security=True"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("LocalDb");
        result.ConnectionStrings[0].ConnectionString.Should().Contain("LocalDB");
        result.ConnectionStrings[0].ConnectionString.Should().Contain("|DataDirectory|");
    }

    [Fact]
    public void Parse_WithMySqlConnectionString_ExtractsCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="MySqlConnection"
                     connectionString="Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;"
                     providerName="MySql.Data.MySqlClient" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ProviderName.Should().Be("MySql.Data.MySqlClient");
    }

    [Fact]
    public void Parse_WithEncryptedConnectionStrings_DetectsEncryption()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings configProtectionProvider="RsaProtectedConfigurationProvider">
                <EncryptedData Type="http://www.w3.org/2001/04/xmlenc#Element"
                    xmlns="http://www.w3.org/2001/04/xmlenc#">
                  <EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#tripledes-cbc" />
                  <KeyInfo xmlns="http://www.w3.org/2000/09/xmldsig#">
                    <EncryptedKey xmlns="http://www.w3.org/2001/04/xmlenc#">
                      <!-- encrypted content -->
                    </EncryptedKey>
                  </KeyInfo>
                  <CipherData>
                    <CipherValue>encrypted data here</CipherValue>
                  </CipherData>
                </EncryptedData>
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.HasEncryptedStrings.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInvalidEntries_SkipsThem()
    {
        // Arrange
        var xml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="ValidConnection"
                     connectionString="Server=localhost;Database=MyDb;"
                     providerName="System.Data.SqlClient" />
                <add name=""
                     connectionString="Server=localhost;Database=MyDb2;" />
                <add connectionString="Server=localhost;Database=MyDb3;" />
                <add name="NoConnectionString" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("ValidConnection");
    }

    [Fact]
    public void ParseWithTransforms_WithNullTransform_ReturnsBaseSection()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, null);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ConnectionString.Should().Contain("Dev");
    }

    [Fact]
    public void ParseWithTransforms_WithSetAttributesTransform_UpdatesConnectionString()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=prod-server;Database=Prod;"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
        result.ConnectionStrings[0].ConnectionString.Should().Contain("prod-server");
        result.ConnectionStrings[0].ConnectionString.Should().Contain("Prod");
    }

    [Fact]
    public void ParseWithTransforms_WithSetAttributesOnProviderName_UpdatesProvider()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=MyDb;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="DefaultConnection"
                     providerName="Microsoft.Data.SqlClient"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ProviderName.Should().Be("Microsoft.Data.SqlClient");
        result.ConnectionStrings[0].ConnectionString.Should().Be("Server=localhost;Database=MyDb;");
    }

    [Fact]
    public void ParseWithTransforms_WithInsertTransform_AddsNewConnectionString()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="RedisConnection"
                     connectionString="localhost:6379"
                     providerName="StackExchange.Redis"
                     xdt:Transform="Insert" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(2);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
        result.ConnectionStrings[1].Name.Should().Be("RedisConnection");
        result.ConnectionStrings[1].ConnectionString.Should().Be("localhost:6379");
    }

    [Fact]
    public void ParseWithTransforms_WithRemoveTransform_RemovesConnectionString()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
                <add name="RedisConnection"
                     connectionString="localhost:6379"
                     providerName="StackExchange.Redis" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="RedisConnection"
                     xdt:Transform="Remove"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
    }

    [Fact]
    public void ParseWithTransforms_WithMultipleTransforms_AppliesAllInOrder()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=prod-server;Database=Prod;"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
                <add name="CacheConnection"
                     connectionString="localhost:6379"
                     providerName="StackExchange.Redis"
                     xdt:Transform="Insert" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(2);
        result.ConnectionStrings[0].ConnectionString.Should().Contain("prod-server");
        result.ConnectionStrings[1].Name.Should().Be("CacheConnection");
    }

    [Fact]
    public void ParseWithTransforms_WithNoTransformSection_ReturnsBaseSection()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <appSettings>
                <add key="SomeKey" value="SomeValue" xdt:Transform="Insert" />
              </appSettings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].ConnectionString.Should().Contain("Dev");
    }

    [Fact]
    public void ParseWithTransforms_SetAttributesOnNonExistentConnection_DoesNothing()
    {
        // Arrange
        var baseXml = XDocument.Parse("""
            <configuration>
              <connectionStrings>
                <add name="DefaultConnection"
                     connectionString="Server=localhost;Database=Dev;"
                     providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var transformXml = XDocument.Parse("""
            <configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
              <connectionStrings>
                <add name="NonExistentConnection"
                     connectionString="Server=prod-server;Database=Prod;"
                     xdt:Transform="SetAttributes"
                     xdt:Locator="Match(name)" />
              </connectionStrings>
            </configuration>
            """);

        // Act
        var result = _parser.ParseWithTransforms(baseXml, transformXml);

        // Assert
        result.ConnectionStrings.Should().HaveCount(1);
        result.ConnectionStrings[0].Name.Should().Be("DefaultConnection");
    }
}
