using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Models.Config;
using Xunit;

namespace NetLift.Tests.Unit.Analysis.Config;

public sealed class SessionStateParserTests
{
    private readonly SessionStateParser _parser = new();

    [Fact]
    public void Parse_NoSessionStateElement_ReturnsDefaults()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <compilation targetFramework=""4.8"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Should().NotBeNull();
        result.Mode.Should().Be(SessionStateMode.InProc);
        result.TimeoutMinutes.Should().Be(20);
        result.CookieName.Should().Be("ASP.NET_SessionId");
        result.Cookieless.Should().BeFalse();
        result.RegenerateExpiredSessionId.Should().BeTrue(); // ASP.NET Framework default
    }

    [Fact]
    public void Parse_InProcMode_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""InProc"" timeout=""30"" cookieName=""MySessionCookie"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.InProc);
        result.TimeoutMinutes.Should().Be(30);
        result.CookieName.Should().Be("MySessionCookie");
    }

    [Fact]
    public void Parse_StateServerMode_ParsesConnectionString()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""StateServer""
                      stateConnectionString=""tcpip=127.0.0.1:42424""
                      timeout=""10"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.StateServer);
        result.StateConnectionString.Should().Be("tcpip=127.0.0.1:42424");
        result.TimeoutMinutes.Should().Be(10);
    }

    [Fact]
    public void Parse_SqlServerMode_ResolvesConnectionString()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <connectionStrings>
        <add name=""SessionDb""
             connectionString=""Server=localhost;Database=SessionState;Integrated Security=true;"" />
    </connectionStrings>
    <system.web>
        <sessionState mode=""SQLServer""
                      sqlConnectionStringName=""SessionDb""
                      timeout=""15"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.SQLServer);
        result.SqlConnectionString.Should().Be("Server=localhost;Database=SessionState;Integrated Security=true;");
        result.TimeoutMinutes.Should().Be(15);
    }

    [Fact]
    public void Parse_SqlServerMode_NoConnectionString_ReturnsNull()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""SQLServer""
                      sqlConnectionStringName=""NonExistent"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.SQLServer);
        result.SqlConnectionString.Should().BeNull();
    }

    [Fact]
    public void Parse_CustomMode_ParsesProviderName()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""Custom""
                      customProvider=""MyCustomProvider"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.Custom);
        result.CustomProvider.Should().Be("MyCustomProvider");
    }

    [Fact]
    public void Parse_OffMode_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""Off"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.Off);
    }

    [Theory]
    [InlineData("UseCookies", false)]
    [InlineData("UseUri", true)]
    [InlineData("AutoDetect", true)]
    [InlineData("UseDeviceProfile", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Parse_CookielessVariants_ParsesCorrectly(string cookielessValue, bool expected)
    {
        // Arrange
        var xml = XDocument.Parse($@"
<configuration>
    <system.web>
        <sessionState cookieless=""{cookielessValue}"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Cookieless.Should().Be(expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, true)] // Default
    public void Parse_RegenerateExpiredSessionId_ParsesCorrectly(string? value, bool expected)
    {
        // Arrange
        var xmlContent = value == null
            ? @"<configuration><system.web><sessionState /></system.web></configuration>"
            : $@"<configuration><system.web><sessionState regenerateExpiredSessionId=""{value}"" /></system.web></configuration>";

        var xml = XDocument.Parse(xmlContent);

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.RegenerateExpiredSessionId.Should().Be(expected);
    }

    [Fact]
    public void Parse_AllOptions_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""InProc""
                      timeout=""25""
                      cookieName=""CustomSession""
                      cookieless=""false""
                      regenerateExpiredSessionId=""false"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.InProc);
        result.TimeoutMinutes.Should().Be(25);
        result.CookieName.Should().Be("CustomSession");
        result.Cookieless.Should().BeFalse();
        result.RegenerateExpiredSessionId.Should().BeFalse();
    }

    [Fact]
    public void Parse_InvalidMode_FallsBackToInProc()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""InvalidMode"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.InProc);
    }

    [Fact]
    public void Parse_CaseInsensitiveMode_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"
<configuration>
    <system.web>
        <sessionState mode=""stateserver"" />
    </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(SessionStateMode.StateServer);
    }
}
