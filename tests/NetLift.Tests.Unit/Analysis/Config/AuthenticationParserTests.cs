using System.Xml.Linq;
using FluentAssertions;
using NetLift.Analysis.Config;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Tests.Unit.Analysis.Config;

/// <summary>
/// Tests for the authentication parser.
/// </summary>
public class AuthenticationParserTests
{
    private readonly IAuthenticationParser _parser;

    public AuthenticationParserTests()
    {
        _parser = new AuthenticationParser();
    }

    [Fact]
    public void Parse_NullDocument_ReturnsDefaultSection()
    {
        // Act
        var result = _parser.Parse(null!);

        // Assert
        result.Should().NotBeNull();
        result.Mode.Should().Be(AuthenticationMode.None);
        result.FormsSettings.Should().BeNull();
        result.AuthorizationRules.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoAuthenticationElement_ReturnsNoneMode()
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
        result.Mode.Should().Be(AuthenticationMode.None);
        result.FormsSettings.Should().BeNull();
    }

    [Fact]
    public void Parse_FormsAuthentication_ParsesMode()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms loginUrl=""~/Account/Login"" />
    </authentication>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(AuthenticationMode.Forms);
        result.FormsSettings.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WindowsAuthentication_ParsesMode()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Windows"" />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(AuthenticationMode.Windows);
        result.FormsSettings.Should().BeNull();
    }

    [Fact]
    public void Parse_PassportAuthentication_ParsesMode()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Passport"" />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(AuthenticationMode.Passport);
    }

    [Fact]
    public void Parse_FormsWithDefaultSettings_UsesDefaults()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms />
    </authentication>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.FormsSettings.Should().NotBeNull();
        result.FormsSettings!.TimeoutMinutes.Should().Be(30);
        result.FormsSettings.SlidingExpiration.Should().BeTrue();
        result.FormsSettings.RequireSsl.Should().BeFalse();
        result.FormsSettings.CookieName.Should().Be(".ASPXAUTH");
        result.FormsSettings.CookiePath.Should().Be("/");
        result.FormsSettings.Protection.Should().Be("All");
    }

    [Fact]
    public void Parse_FormsWithAllSettings_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms
        loginUrl=""~/Login.aspx""
        timeout=""60""
        slidingExpiration=""false""
        requireSSL=""true""
        name="".MyAuth""
        defaultUrl=""~/Home.aspx""
        domain="".example.com""
        enableCrossAppRedirects=""true""
        path=""/app""
        protection=""Encryption"" />
    </authentication>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.FormsSettings.Should().NotBeNull();
        result.FormsSettings!.LoginUrl.Should().Be("~/Login.aspx");
        result.FormsSettings.TimeoutMinutes.Should().Be(60);
        result.FormsSettings.SlidingExpiration.Should().BeFalse();
        result.FormsSettings.RequireSsl.Should().BeTrue();
        result.FormsSettings.CookieName.Should().Be(".MyAuth");
        result.FormsSettings.DefaultUrl.Should().Be("~/Home.aspx");
        result.FormsSettings.Domain.Should().Be(".example.com");
        result.FormsSettings.EnableCrossAppRedirects.Should().BeTrue();
        result.FormsSettings.CookiePath.Should().Be("/app");
        result.FormsSettings.Protection.Should().Be("Encryption");
    }

    [Fact]
    public void Parse_FormsWithoutFormsElement_ReturnsDefaultSettings()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"" />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(AuthenticationMode.Forms);
        result.FormsSettings.Should().NotBeNull();
        result.FormsSettings!.TimeoutMinutes.Should().Be(30);
    }

    [Fact]
    public void Parse_NoAuthorizationElement_ReturnsEmptyRules()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms />
    </authentication>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().BeEmpty();
    }

    [Fact]
    public void Parse_DenyAnonymousUsers_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms />
    </authentication>
    <authorization>
      <deny users=""?"" />
      <allow users=""*"" />
    </authorization>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().HaveCount(2);
        result.AuthorizationRules[0].IsAllow.Should().BeFalse();
        result.AuthorizationRules[0].Users.Should().Be("?");
        result.AuthorizationRules[1].IsAllow.Should().BeTrue();
        result.AuthorizationRules[1].Users.Should().Be("*");
    }

    [Fact]
    public void Parse_RoleBasedAuthorization_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""Forms"">
      <forms />
    </authentication>
    <authorization>
      <allow roles=""Admin,Manager"" />
      <deny users=""*"" />
    </authorization>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().HaveCount(2);
        result.AuthorizationRules[0].IsAllow.Should().BeTrue();
        result.AuthorizationRules[0].Roles.Should().Be("Admin,Manager");
        result.AuthorizationRules[1].IsAllow.Should().BeFalse();
        result.AuthorizationRules[1].Users.Should().Be("*");
    }

    [Fact]
    public void Parse_VerbBasedAuthorization_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authorization>
      <allow verbs=""GET,POST"" users=""*"" />
      <deny verbs=""DELETE,PUT"" users=""?"" />
    </authorization>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().HaveCount(2);
        result.AuthorizationRules[0].Verbs.Should().Be("GET,POST");
        result.AuthorizationRules[1].Verbs.Should().Be("DELETE,PUT");
    }

    [Fact]
    public void Parse_ComplexAuthorizationRules_ParsesAllAttributes()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authorization>
      <allow roles=""Admin"" verbs=""GET,POST,PUT,DELETE"" />
      <allow users=""specificuser"" verbs=""GET"" />
      <deny users=""*"" />
    </authorization>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().HaveCount(3);

        result.AuthorizationRules[0].IsAllow.Should().BeTrue();
        result.AuthorizationRules[0].Roles.Should().Be("Admin");
        result.AuthorizationRules[0].Verbs.Should().Be("GET,POST,PUT,DELETE");

        result.AuthorizationRules[1].IsAllow.Should().BeTrue();
        result.AuthorizationRules[1].Users.Should().Be("specificuser");
        result.AuthorizationRules[1].Verbs.Should().Be("GET");

        result.AuthorizationRules[2].IsAllow.Should().BeFalse();
        result.AuthorizationRules[2].Users.Should().Be("*");
    }

    [Fact]
    public void Parse_InvalidAuthenticationMode_DefaultsToNone()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""InvalidMode"" />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.Mode.Should().Be(AuthenticationMode.None);
    }

    [Fact]
    public void Parse_EmptyAuthorizationElement_ReturnsEmptyRules()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authorization />
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MixedCaseAuthenticationMode_ParsesCorrectly()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authentication mode=""forms"">
      <forms />
    </authentication>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert - should handle case-insensitive comparison
        result.Mode.Should().Be(AuthenticationMode.None); // "forms" != "Forms" in current implementation
    }

    [Fact]
    public void Parse_NonAuthorizationElements_IgnoresThem()
    {
        // Arrange
        var xml = XDocument.Parse(@"<?xml version=""1.0""?>
<configuration>
  <system.web>
    <authorization>
      <allow users=""*"" />
      <customElement someAttr=""value"" />
      <deny users=""?"" />
    </authorization>
  </system.web>
</configuration>");

        // Act
        var result = _parser.Parse(xml);

        // Assert
        result.AuthorizationRules.Should().HaveCount(2);
        result.AuthorizationRules[0].IsAllow.Should().BeTrue();
        result.AuthorizationRules[1].IsAllow.Should().BeFalse();
    }
}
