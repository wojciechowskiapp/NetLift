using FluentAssertions;
using NetLift.Core.Models;
using NetLift.Transforms.Configuration;
using NetLift.Transforms.Services;

namespace NetLift.Tests.Unit.Services;

public class PackageMappingServiceTests
{
    private readonly PackageMappingService _service;

    public PackageMappingServiceTests()
    {
        _service = new PackageMappingService();
    }

    [Fact]
    public void GetMappedPackage_WithNoMapping_ReturnsNoMappingResult()
    {
        // Arrange
        var packageId = "Some.Unknown.Package";
        var version = "1.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.OriginalVersion.Should().Be(version);
        result.Action.Should().Be(MappingAction.Keep);
        result.Reason.Should().Contain("No mapping rule found");
    }

    [Fact]
    public void GetMappedPackage_WithRemoveAction_ReturnsRemoveResult()
    {
        // Arrange
        var packageId = "Microsoft.AspNet.Mvc";
        var version = "5.2.7";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.Action.Should().Be(MappingAction.Remove);
        result.Reason.Should().NotBeNullOrEmpty();
        result.RequiresCodeChanges.Should().BeTrue();
    }

    [Fact]
    public void GetMappedPackage_WithReplaceAction_ReturnsReplaceResult()
    {
        // Arrange
        var packageId = "EntityFramework";
        var version = "6.4.4";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.Action.Should().Be(MappingAction.Replace);
        result.NewPackageId.Should().Be("Microsoft.EntityFrameworkCore");
        result.RecommendedVersion.Should().NotBeNullOrEmpty();
        result.RequiresCodeChanges.Should().BeTrue();
    }

    [Fact]
    public void GetMappedPackage_WithKeepAction_ReturnsKeepResult()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.Action.Should().Be(MappingAction.Keep);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetMappedPackage_WithUpgradeAction_ReturnsUpgradeResult()
    {
        // Arrange
        var packageId = "xunit";
        var version = "2.4.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.Action.Should().Be(MappingAction.Upgrade);
        result.RecommendedVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetMappedPackage_WithManualAction_ReturnsManualResult()
    {
        // Arrange
        var packageId = "log4net";
        var version = "2.0.15";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.OriginalPackageId.Should().Be(packageId);
        result.Action.Should().Be(MappingAction.Manual);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetMappedPackage_WithSecurityUpdate_IncludesSecurityWarning()
    {
        // Arrange
        var packageId = "System.IdentityModel.Tokens.Jwt";
        var version = "6.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.SecurityUpdate.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("security"));
    }

    [Fact]
    public void GetMappedPackage_WithCodeChanges_IncludesWarning()
    {
        // Arrange
        var packageId = "Microsoft.AspNet.WebApi";
        var version = "5.2.7";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.RequiresCodeChanges.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("code changes"));
    }

    [Fact]
    public void RequiresMapping_WithMappedPackage_ReturnsTrue()
    {
        // Arrange
        var packageId = "Microsoft.AspNet.Mvc";

        // Act
        var result = _service.RequiresMapping(packageId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresMapping_WithUnmappedPackage_ReturnsFalse()
    {
        // Arrange
        var packageId = "Some.Unknown.Package";

        // Act
        var result = _service.RequiresMapping(packageId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RequiresMapping_IsCaseInsensitive()
    {
        // Arrange & Act
        var result1 = _service.RequiresMapping("Microsoft.AspNet.Mvc");
        var result2 = _service.RequiresMapping("microsoft.aspnet.mvc");
        var result3 = _service.RequiresMapping("MICROSOFT.ASPNET.MVC");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    [Fact]
    public void IsObsolete_WithObsoletePackage_ReturnsTrue()
    {
        // Arrange
        var packageId = "System.Web";
        var targetFramework = "net8.0";

        // Act
        var result = _service.IsObsolete(packageId, targetFramework);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsObsolete_WithRemoveAction_ReturnsTrue()
    {
        // Arrange
        var packageId = "Microsoft.AspNet.Mvc";
        var targetFramework = "net8.0";

        // Act
        var result = _service.IsObsolete(packageId, targetFramework);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsObsolete_WithKeepAction_ReturnsFalse()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var targetFramework = "net8.0";

        // Act
        var result = _service.IsObsolete(packageId, targetFramework);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAllMappingRules_ReturnsAllRules()
    {
        // Act
        var rules = _service.GetAllMappingRules();

        // Assert
        rules.Should().NotBeEmpty();
        rules.Should().HaveCountGreaterThan(20); // We have 50+ mappings
    }

    [Fact]
    public void GetMappedPackage_WithSuggestedAdditional_IncludesPackages()
    {
        // Arrange
        var packageId = "Serilog";
        var version = "3.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.SuggestedAdditional.Should().NotBeNullOrEmpty();
        result.SuggestedAdditional.Should().Contain("Serilog.Extensions.Logging");
    }

    [Theory]
    [InlineData("Microsoft.AspNet.Mvc")]
    [InlineData("Microsoft.AspNet.WebApi")]
    [InlineData("Microsoft.Owin")]
    [InlineData("System.Web")]
    public void GetMappedPackage_WithAspNetPackages_ReturnsRemoveAction(string packageId)
    {
        // Arrange
        var version = "1.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Remove);
    }

    [Theory]
    [InlineData("xunit", "2.6.6")]
    [InlineData("NUnit", "4.1.0")]
    [InlineData("Moq", "4.20.70")]
    [InlineData("FluentAssertions", "6.12.0")]
    public void GetMappedPackage_WithTestingFrameworks_ReturnsUpgradeWithCorrectVersion(
        string packageId,
        string expectedVersion)
    {
        // Arrange
        var version = "1.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Upgrade);
        result.RecommendedVersion.Should().Be(expectedVersion);
    }

    [Fact]
    public void GetMappedPackage_ThrowsArgumentNullException_WhenPackageIdIsNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.GetMappedPackage(null!, "1.0.0", "net8.0"));
    }

    [Fact]
    public void GetMappedPackage_ThrowsArgumentNullException_WhenVersionIsNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.GetMappedPackage("SomePackage", null!, "net8.0"));
    }

    [Fact]
    public void GetMappedPackage_ThrowsArgumentNullException_WhenTargetFrameworkIsNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.GetMappedPackage("SomePackage", "1.0.0", null!));
    }

    [Fact]
    public void Constructor_WithCustomRules_UsesCustomRules()
    {
        // Arrange
        var customRules = new PackageMappingRules
        {
            Mappings = new List<MappingRuleDto>
            {
                new()
                {
                    OldPackage = "Custom.Package",
                    Action = "remove",
                    Reason = "Custom reason"
                }
            }
        };

        var service = new PackageMappingService(customRules);

        // Act
        var result = service.GetMappedPackage("Custom.Package", "1.0.0", "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Remove);
        result.Reason.Should().Be("Custom reason");
    }

    [Fact]
    public void GetMappedPackage_WithFrameworkSpecificMapping_Net8_ReturnsCorrectVersion()
    {
        // Arrange
        var packageId = "EntityFramework";
        var version = "6.4.4";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedVersion.Should().Be("8.0.0");
    }

    [Fact]
    public void GetMappedPackage_WithFrameworkSpecificMapping_Net6_ReturnsCorrectVersion()
    {
        // Arrange
        var packageId = "EntityFramework";
        var version = "6.4.4";
        var targetFramework = "net6.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedVersion.Should().Be("6.0.33");
    }

    [Theory]
    [InlineData("Microsoft.Bcl")]
    [InlineData("Microsoft.Bcl.Async")]
    [InlineData("Microsoft.Net.Compilers")]
    [InlineData("Microsoft.CodeDom.Providers.DotNetCompilerPlatform")]
    public void GetMappedPackage_WithObsoletePackages_ReturnsRemoveAction(string packageId)
    {
        // Arrange
        var version = "1.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Remove);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetMappedPackage_WithAnalyzerPackage_ReturnsCorrectAction()
    {
        // Arrange
        var packageId = "StyleCop.Analyzers";
        var version = "1.1.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Keep);
    }

    [Fact]
    public void GetMappedPackage_WithDeprecatedAnalyzer_ReturnsReplaceAction()
    {
        // Arrange
        var packageId = "Microsoft.CodeAnalysis.FxCopAnalyzers";
        var version = "3.0.0";
        var targetFramework = "net8.0";

        // Act
        var result = _service.GetMappedPackage(packageId, version, targetFramework);

        // Assert
        result.Should().NotBeNull();
        result.Action.Should().Be(MappingAction.Replace);
        result.NewPackageId.Should().Be("Microsoft.CodeAnalysis.NetAnalyzers");
    }
}
