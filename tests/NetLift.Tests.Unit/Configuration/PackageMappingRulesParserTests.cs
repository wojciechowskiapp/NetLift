using FluentAssertions;
using NetLift.Transforms.Configuration;

namespace NetLift.Tests.Unit.Configuration;

public class PackageMappingRulesParserTests
{
    private readonly PackageMappingRulesParser _parser;

    public PackageMappingRulesParserTests()
    {
        _parser = new PackageMappingRulesParser();
    }

    [Fact]
    public void ParseRules_WithValidYaml_ReturnsRules()
    {
        // Arrange
        var yaml = @"
version: '1.0'
description: 'Test rules'
settings:
  default_action: keep
  preserve_version: true
  warn_on_major_upgrade: true
mappings:
  - old_package: TestPackage
    action: remove
    reason: Test reason
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Version.Should().Be("1.0");
        rules.Description.Should().Be("Test rules");
        rules.Mappings.Should().HaveCount(1);
        rules.Mappings[0].OldPackage.Should().Be("TestPackage");
        rules.Mappings[0].Action.Should().Be("remove");
    }

    [Fact]
    public void ParseRules_WithMissingOldPackage_ThrowsInvalidOperationException()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - action: remove
";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _parser.ParseRules(yaml));
        exception.Message.Should().Contain("old_package");
    }

    [Fact]
    public void ParseRules_WithMissingAction_ThrowsInvalidOperationException()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: TestPackage
";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _parser.ParseRules(yaml));
        exception.Message.Should().Contain("action");
    }

    [Fact]
    public void ParseRules_WithReplaceActionAndNoNewPackage_ThrowsInvalidOperationException()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: TestPackage
    action: replace
";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _parser.ParseRules(yaml));
        exception.Message.Should().Contain("new_package");
    }

    [Fact]
    public void ParseRules_WithInvalidYaml_ThrowsInvalidOperationException()
    {
        // Arrange
        var yaml = "invalid: yaml: content:";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _parser.ParseRules(yaml));
    }

    [Fact]
    public void ParseRules_WithVersionMapping_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: TestPackage
    action: replace
    new_package: NewPackage
    version_mapping:
      net8.0: '8.0.0'
      net6.0: '6.0.0'
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Mappings[0].VersionMapping.Should().ContainKey("net8.0");
        rules.Mappings[0].VersionMapping!["net8.0"].Should().Be("8.0.0");
        rules.Mappings[0].VersionMapping.Should().ContainKey("net6.0");
        rules.Mappings[0].VersionMapping!["net6.0"].Should().Be("6.0.0");
    }

    [Fact]
    public void ParseRules_WithFrameworkCompatibility_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: TestPackage
    action: replace
    new_package: NewPackage
    framework_compatibility:
      net8.0:
        action: replace
        version: '8.0.0'
      net48:
        action: keep
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Mappings[0].FrameworkCompatibility.Should().ContainKey("net8.0");
        rules.Mappings[0].FrameworkCompatibility!["net8.0"].Action.Should().Be("replace");
        rules.Mappings[0].FrameworkCompatibility!["net8.0"].Version.Should().Be("8.0.0");
        rules.Mappings[0].FrameworkCompatibility.Should().ContainKey("net48");
        rules.Mappings[0].FrameworkCompatibility!["net48"].Action.Should().Be("keep");
    }

    [Fact]
    public void ParseRules_WithAnalyzers_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
analyzers:
  - package: TestAnalyzer
    action: keep
    latest_version: '1.0.0'
    private_assets: all
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Analyzers.Should().HaveCount(1);
        rules.Analyzers[0].Package.Should().Be("TestAnalyzer");
        rules.Analyzers[0].Action.Should().Be("keep");
        rules.Analyzers[0].LatestVersion.Should().Be("1.0.0");
        rules.Analyzers[0].PrivateAssets.Should().Be("all");
    }

    [Fact]
    public void ParseRules_WithObsoletePackages_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
obsolete_packages:
  - package: ObsoletePackage
    reason: No longer needed
    action: remove
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.ObsoletePackages.Should().HaveCount(1);
        rules.ObsoletePackages[0].Package.Should().Be("ObsoletePackage");
        rules.ObsoletePackages[0].Reason.Should().Be("No longer needed");
        rules.ObsoletePackages[0].Action.Should().Be("remove");
    }

    [Fact]
    public void ParseRules_WithAspNetMigrations_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
aspnet_migrations:
  - old_package: Microsoft.AspNet.Mvc
    action: remove
    reason: Built into framework
    requires_code_changes: true
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.AspnetMigrations.Should().HaveCount(1);
        rules.AspnetMigrations[0].OldPackage.Should().Be("Microsoft.AspNet.Mvc");
        rules.AspnetMigrations[0].Action.Should().Be("remove");
        rules.AspnetMigrations[0].RequiresCodeChanges.Should().BeTrue();
    }

    [Fact]
    public void ParseRules_WithComplexYaml_ParsesAllCategories()
    {
        // Arrange
        var yaml = @"
version: '1.0'
description: 'Comprehensive test'
mappings:
  - old_package: Package1
    action: keep
aspnet_migrations:
  - old_package: Package2
    action: remove
ef_migrations:
  - old_package: Package3
    action: replace
    new_package: Package3.New
testing_migrations:
  - old_package: Package4
    action: upgrade
logging_migrations:
  - old_package: Package5
    action: manual
security_migrations:
  - old_package: Package6
    action: upgrade
    security_update: true
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Mappings.Should().HaveCount(1);
        rules.AspnetMigrations.Should().HaveCount(1);
        rules.EfMigrations.Should().HaveCount(1);
        rules.TestingMigrations.Should().HaveCount(1);
        rules.LoggingMigrations.Should().HaveCount(1);
        rules.SecurityMigrations.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadRulesAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = "non-existent-file.yml";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _parser.LoadRulesAsync(nonExistentPath));
    }

    [Fact]
    public void ParseRules_WithSuggestedAdditional_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: Serilog
    action: keep
    suggested_additional:
      - Serilog.Extensions.Logging
      - Serilog.Sinks.Console
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Mappings[0].SuggestedAdditional.Should().HaveCount(2);
        rules.Mappings[0].SuggestedAdditional.Should().Contain("Serilog.Extensions.Logging");
        rules.Mappings[0].SuggestedAdditional.Should().Contain("Serilog.Sinks.Console");
    }

    [Fact]
    public void ParseRules_WithMigrationGuide_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
mappings:
  - old_package: TestPackage
    action: replace
    new_package: NewPackage
    migration_guide: 'https://example.com/guide'
    notes: 'Important migration notes'
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Mappings[0].MigrationGuide.Should().Be("https://example.com/guide");
        rules.Mappings[0].Notes.Should().Be("Important migration notes");
    }

    [Fact]
    public void ParseRules_WithSettings_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
settings:
  default_action: remove
  preserve_version: false
  warn_on_major_upgrade: false
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.Settings.DefaultAction.Should().Be("remove");
        rules.Settings.PreserveVersion.Should().BeFalse();
        rules.Settings.WarnOnMajorUpgrade.Should().BeFalse();
    }

    [Fact]
    public void ParseRules_WithFrameworkPackages_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
framework_packages:
  net8.0:
    - System.Net.Http
    - System.Memory
  net6.0:
    - System.Net.Http
";

        // Act
        var rules = _parser.ParseRules(yaml);

        // Assert
        rules.Should().NotBeNull();
        rules.FrameworkPackages.Should().ContainKey("net8.0");
        rules.FrameworkPackages["net8.0"].Should().Contain("System.Net.Http");
        rules.FrameworkPackages["net8.0"].Should().Contain("System.Memory");
        rules.FrameworkPackages.Should().ContainKey("net6.0");
        rules.FrameworkPackages["net6.0"].Should().Contain("System.Net.Http");
    }

    [Fact]
    public void MappingRuleDto_ToDomainModel_ConvertsCorrectly()
    {
        // Arrange
        var dto = new MappingRuleDto
        {
            OldPackage = "TestPackage",
            Action = "replace",
            NewPackage = "NewPackage",
            VersionMapping = new Dictionary<string, string> { ["net8.0"] = "8.0.0" },
            Reason = "Test reason",
            RequiresCodeChanges = true,
            SecurityUpdate = true
        };

        // Act
        var domainModel = dto.ToDomainModel();

        // Assert
        domainModel.Should().NotBeNull();
        domainModel.OldPackageId.Should().Be("TestPackage");
        domainModel.Action.Should().Be(NetLift.Core.Models.MappingAction.Replace);
        domainModel.NewPackageId.Should().Be("NewPackage");
        domainModel.RequiresCodeChanges.Should().BeTrue();
        domainModel.SecurityUpdate.Should().BeTrue();
    }

    [Theory]
    [InlineData("keep", NetLift.Core.Models.MappingAction.Keep)]
    [InlineData("replace", NetLift.Core.Models.MappingAction.Replace)]
    [InlineData("remove", NetLift.Core.Models.MappingAction.Remove)]
    [InlineData("upgrade", NetLift.Core.Models.MappingAction.Upgrade)]
    [InlineData("manual", NetLift.Core.Models.MappingAction.Manual)]
    public void MappingRuleDto_ToDomainModel_ParsesActionCorrectly(
        string actionString,
        NetLift.Core.Models.MappingAction expectedAction)
    {
        // Arrange
        var dto = new MappingRuleDto
        {
            OldPackage = "TestPackage",
            Action = actionString
        };

        // Act
        var domainModel = dto.ToDomainModel();

        // Assert
        domainModel.Action.Should().Be(expectedAction);
    }
}
