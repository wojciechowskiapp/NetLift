using FluentAssertions;
using NetLift.Core.Models.Config;
using NetLift.Transforms.Generators;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Generators;

public sealed class SessionCodeGeneratorTests
{
    private readonly SessionCodeGenerator _generator = new();

    [Fact]
    public void GenerateServicesCode_OffMode_ReturnsComment()
    {
        // Arrange
        var session = new SessionStateSettings { Mode = SessionStateMode.Off };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("// Session state disabled");
        result.Should().NotContain("AddSession");
    }

    [Fact]
    public void GenerateServicesCode_InProcMode_GeneratesMemoryCache()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc,
            TimeoutMinutes = 30,
            CookieName = "ASP.NET_SessionId"
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("AddDistributedMemoryCache");
        result.Should().Contain("AddSession");
        result.Should().Contain("TimeSpan.FromMinutes(30)");
        result.Should().Contain(".AspNetCore.Session"); // Mapped cookie name
        result.Should().Contain("HttpOnly = true");
        result.Should().Contain("SecurePolicy = CookieSecurePolicy.Always");
        result.Should().Contain("SameSite = SameSiteMode.Lax");
    }

    [Fact]
    public void GenerateServicesCode_InProcMode_CustomCookieName_PreservesCookieName()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc,
            CookieName = "MyCustomSession"
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("Cookie.Name = \"MyCustomSession\"");
        result.Should().NotContain(".AspNetCore.Session");
    }

    [Fact]
    public void GenerateServicesCode_StateServerMode_GeneratesRedisCache()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.StateServer,
            StateConnectionString = "tcpip=127.0.0.1:42424",
            TimeoutMinutes = 20
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("AddStackExchangeRedisCache");
        result.Should().Contain("dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis");
        result.Should().Contain("TODO: Convert StateServer connection");
        result.Should().Contain("tcpip=127.0.0.1:42424");
        result.Should().Contain("GetConnectionString(\"RedisCache\")");
        result.Should().Contain("InstanceName = \"Session_\"");
        result.Should().Contain("AddSession");
        result.Should().Contain("TimeSpan.FromMinutes(20)");
    }

    [Fact]
    public void GenerateServicesCode_StateServerMode_NoConnectionString_GeneratesTodo()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.StateServer
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("TODO: Configure Redis connection string");
        result.Should().NotContain("Convert StateServer connection");
    }

    [Fact]
    public void GenerateServicesCode_SqlServerMode_GeneratesSqlCache()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.SQLServer,
            SqlConnectionString = "Server=localhost;Database=SessionState;",
            TimeoutMinutes = 15
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("AddDistributedSqlServerCache");
        result.Should().Contain("dotnet add package Microsoft.Extensions.Caching.SqlServer");
        result.Should().Contain("dotnet sql-cache create");
        result.Should().Contain("Server=localhost;Database=SessionState;");
        result.Should().Contain("GetConnectionString(\"SessionCache\")");
        result.Should().Contain("SchemaName = \"dbo\"");
        result.Should().Contain("TableName = \"SessionCache\"");
        result.Should().Contain("AddSession");
        result.Should().Contain("TimeSpan.FromMinutes(15)");
    }

    [Fact]
    public void GenerateServicesCode_SqlServerMode_NoConnectionString_GeneratesTodo()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.SQLServer
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("TODO: Configure SQL Server connection string");
        result.Should().NotContain("Original connection string:");
    }

    [Fact]
    public void GenerateServicesCode_CustomMode_GeneratesWarning()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.Custom,
            CustomProvider = "MyApp.CustomSessionProvider"
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("WARNING: Custom session state provider detected");
        result.Should().Contain("MyApp.CustomSessionProvider");
        result.Should().Contain("TODO: Migrate custom provider to IDistributedCache");
        result.Should().Contain("Falling back to in-memory cache");
        result.Should().Contain("AddDistributedMemoryCache");
        result.Should().Contain("AddSession");
    }

    [Fact]
    public void GenerateServicesCode_CustomMode_NoProvider_GeneratesWarning()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.Custom
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("WARNING: Custom session state provider detected");
        result.Should().NotContain("Original provider:");
    }

    [Fact]
    public void GenerateServicesCode_CookielessEnabled_GeneratesWarning()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc,
            Cookieless = true
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("WARNING: Cookieless sessions not supported");
        result.Should().Contain("Consider alternative authentication/state management");
    }

    [Fact]
    public void GenerateMiddlewareCode_ReturnsUseSession()
    {
        // Act
        var result = _generator.GenerateMiddlewareCode();

        // Assert
        result.Should().Contain("app.UseSession();");
        result.Should().Contain("after UseRouting()");
        result.Should().Contain("before UseEndpoints()");
    }

    [Fact]
    public void GenerateServicesCode_DefaultTimeout_Uses20Minutes()
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc
            // TimeoutMinutes defaults to 20
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("TimeSpan.FromMinutes(20)");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void GenerateServicesCode_CustomTimeout_UsesCorrectValue(int timeout)
    {
        // Arrange
        var session = new SessionStateSettings
        {
            Mode = SessionStateMode.InProc,
            TimeoutMinutes = timeout
        };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain($"TimeSpan.FromMinutes({timeout})");
    }

    [Fact]
    public void GenerateServicesCode_SecuritySettings_AlwaysIncluded()
    {
        // Arrange
        var session = new SessionStateSettings { Mode = SessionStateMode.InProc };

        // Act
        var result = _generator.GenerateServicesCode(session);

        // Assert
        result.Should().Contain("HttpOnly = true");
        result.Should().Contain("Security best practice");
        result.Should().Contain("SecurePolicy = CookieSecurePolicy.Always");
        result.Should().Contain("HTTPS only");
        result.Should().Contain("SameSite = SameSiteMode.Lax");
        result.Should().Contain("CSRF protection");
    }
}
