using System.Xml.Linq;
using NetLift.Analysis.Config;
using NetLift.Core.Models.Config;

namespace NetLift.Tests.Unit.Analysis.Config;

public class SystemWebParserTests
{
    private readonly SystemWebParser _parser;

    public SystemWebParserTests()
    {
        _parser = new SystemWebParser();
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptySection()
    {
        // Arrange
        var doc = new XDocument(new XElement("configuration"));

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Compilation);
        Assert.Null(result.HttpRuntime);
        Assert.Null(result.CustomErrors);
    }

    [Fact]
    public void Parse_NullDocument_ReturnsEmptySection()
    {
        // Act
        var result = _parser.Parse(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Compilation);
        Assert.Null(result.HttpRuntime);
        Assert.Null(result.CustomErrors);
    }

    [Fact]
    public void Parse_CompilationSettings_ExtractsCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.8"" optimizeCompilations=""true"">
            <assemblies>
                <add assembly=""System.Web.Mvc, Version=5.2.7.0"" />
                <add assembly=""System.Web.Abstractions"" />
            </assemblies>
        </compilation>
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.Compilation);
        Assert.True(result.Compilation.Debug);
        Assert.Equal("4.8", result.Compilation.TargetFramework);
        Assert.True(result.Compilation.OptimizeCompilations);
        Assert.Equal(2, result.Compilation.Assemblies.Count);
        Assert.Contains("System.Web.Mvc, Version=5.2.7.0", result.Compilation.Assemblies);
        Assert.Contains("System.Web.Abstractions", result.Compilation.Assemblies);
    }

    [Fact]
    public void Parse_CompilationSettings_DebugFalse_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <compilation debug=""false"" targetFramework=""4.7.2"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.Compilation);
        Assert.False(result.Compilation.Debug);
        Assert.Equal("4.7.2", result.Compilation.TargetFramework);
        Assert.False(result.Compilation.OptimizeCompilations);
    }

    [Fact]
    public void Parse_CompilationSettings_NoAssemblies_ReturnsEmptyList()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.8"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.Compilation);
        Assert.Empty(result.Compilation.Assemblies);
    }

    [Fact]
    public void Parse_HttpRuntimeSettings_ExtractsCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <httpRuntime targetFramework=""4.8"" maxRequestLength=""51200"" executionTimeout=""3600"" enableVersionHeader=""false"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.HttpRuntime);
        Assert.Equal("4.8", result.HttpRuntime.TargetFramework);
        Assert.Equal(51200, result.HttpRuntime.MaxRequestLengthKb);
        Assert.Equal(3600, result.HttpRuntime.ExecutionTimeoutSeconds);
        Assert.False(result.HttpRuntime.EnableVersionHeader);
    }

    [Fact]
    public void Parse_HttpRuntimeSettings_MinimalConfiguration_UsesDefaults()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <httpRuntime targetFramework=""4.8"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.HttpRuntime);
        Assert.Equal("4.8", result.HttpRuntime.TargetFramework);
        Assert.Null(result.HttpRuntime.MaxRequestLengthKb);
        Assert.Null(result.HttpRuntime.ExecutionTimeoutSeconds);
        Assert.True(result.HttpRuntime.EnableVersionHeader); // Default is true
    }

    [Fact]
    public void Parse_CustomErrors_ModeOff_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""Off"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.Off, result.CustomErrors.Mode);
        Assert.Null(result.CustomErrors.DefaultRedirect);
        Assert.Empty(result.CustomErrors.ErrorPages);
    }

    [Fact]
    public void Parse_CustomErrors_ModeOn_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""On"" defaultRedirect=""~/Error"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.On, result.CustomErrors.Mode);
        Assert.Equal("~/Error", result.CustomErrors.DefaultRedirect);
    }

    [Fact]
    public void Parse_CustomErrors_ModeRemoteOnly_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""RemoteOnly"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.RemoteOnly, result.CustomErrors.Mode);
    }

    [Fact]
    public void Parse_CustomErrors_NoModeAttribute_DefaultsToRemoteOnly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.RemoteOnly, result.CustomErrors.Mode);
    }

    [Fact]
    public void Parse_CustomErrors_WithErrorPages_ExtractsCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""On"" defaultRedirect=""~/Error"">
            <error statusCode=""404"" redirect=""~/Error/NotFound"" />
            <error statusCode=""500"" redirect=""~/Error/ServerError"" />
            <error statusCode=""403"" redirect=""~/Error/Forbidden"" />
        </customErrors>
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.On, result.CustomErrors.Mode);
        Assert.Equal("~/Error", result.CustomErrors.DefaultRedirect);
        Assert.Equal(3, result.CustomErrors.ErrorPages.Count);

        var error404 = result.CustomErrors.ErrorPages.First(e => e.StatusCode == 404);
        Assert.Equal("~/Error/NotFound", error404.Redirect);

        var error500 = result.CustomErrors.ErrorPages.First(e => e.StatusCode == 500);
        Assert.Equal("~/Error/ServerError", error500.Redirect);

        var error403 = result.CustomErrors.ErrorPages.First(e => e.StatusCode == 403);
        Assert.Equal("~/Error/Forbidden", error403.Redirect);
    }

    [Fact]
    public void Parse_CustomErrors_InvalidErrorPage_SkipsIt()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""On"">
            <error statusCode=""404"" redirect=""~/Error/NotFound"" />
            <error statusCode=""invalid"" redirect=""~/Error/Invalid"" />
            <error statusCode=""500"" />
        </customErrors>
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Single(result.CustomErrors.ErrorPages); // Only the valid 404 entry
        Assert.Equal(404, result.CustomErrors.ErrorPages[0].StatusCode);
    }

    [Fact]
    public void Parse_AllSections_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <compilation debug=""false"" targetFramework=""4.8"">
            <assemblies>
                <add assembly=""System.Web.Mvc, Version=5.2.7.0"" />
            </assemblies>
        </compilation>
        <httpRuntime targetFramework=""4.8"" maxRequestLength=""10240"" />
        <customErrors mode=""RemoteOnly"" defaultRedirect=""~/Error"">
            <error statusCode=""404"" redirect=""~/Error/NotFound"" />
        </customErrors>
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.Compilation);
        Assert.False(result.Compilation.Debug);
        Assert.Equal("4.8", result.Compilation.TargetFramework);
        Assert.Single(result.Compilation.Assemblies);

        Assert.NotNull(result.HttpRuntime);
        Assert.Equal("4.8", result.HttpRuntime.TargetFramework);
        Assert.Equal(10240, result.HttpRuntime.MaxRequestLengthKb);

        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.RemoteOnly, result.CustomErrors.Mode);
        Assert.Equal("~/Error", result.CustomErrors.DefaultRedirect);
        Assert.Single(result.CustomErrors.ErrorPages);
    }

    [Fact]
    public void Parse_MissingSystemWebSection_ReturnsEmptySection()
    {
        // Arrange
        var xml = @"
<configuration>
    <appSettings>
        <add key=""test"" value=""value"" />
    </appSettings>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Compilation);
        Assert.Null(result.HttpRuntime);
        Assert.Null(result.CustomErrors);
    }

    [Fact]
    public void Parse_EmptySystemWebSection_ReturnsEmptySection()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Compilation);
        Assert.Null(result.HttpRuntime);
        Assert.Null(result.CustomErrors);
    }

    [Fact]
    public void Parse_CompilationSettings_CaseInsensitiveBooleans_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <compilation debug=""True"" optimizeCompilations=""FALSE"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.Compilation);
        Assert.True(result.Compilation.Debug);
        Assert.False(result.Compilation.OptimizeCompilations);
    }

    [Fact]
    public void Parse_CustomErrors_CaseInsensitiveMode_ParsesCorrectly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""OFF"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.Off, result.CustomErrors.Mode);
    }

    [Fact]
    public void Parse_CustomErrors_InvalidMode_DefaultsToRemoteOnly()
    {
        // Arrange
        var xml = @"
<configuration>
    <system.web>
        <customErrors mode=""invalid"" />
    </system.web>
</configuration>";
        var doc = XDocument.Parse(xml);

        // Act
        var result = _parser.Parse(doc);

        // Assert
        Assert.NotNull(result.CustomErrors);
        Assert.Equal(CustomErrorMode.RemoteOnly, result.CustomErrors.Mode);
    }
}
