using FluentAssertions;
using NetLift.Analysis.Parsers;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Parsers;

public class PackagesConfigParserTests
{
    private readonly IPackagesConfigParser _parser;
    private readonly string _fixturesPath;

    public PackagesConfigParserTests()
    {
        _parser = new PackagesConfigParser();
        _fixturesPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "test-fixtures", "mvc5-basic", "Mvc5Basic"));
    }

    [Fact]
    public void Parse_ValidPackagesConfig_ReturnsAllPackages()
    {
        // Arrange
        var packagesConfigPath = Path.Combine(_fixturesPath, "packages.config");

        // Act
        var result = _parser.Parse(packagesConfigPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(19);
    }

    [Fact]
    public void Parse_ValidPackagesConfig_ExtractsPackageProperties()
    {
        // Arrange
        var packagesConfigPath = Path.Combine(_fixturesPath, "packages.config");

        // Act
        var result = _parser.Parse(packagesConfigPath);

        // Assert
        var mvcPackage = result.FirstOrDefault(p => p.Id == "Microsoft.AspNet.Mvc");
        mvcPackage.Should().NotBeNull();
        mvcPackage!.Id.Should().Be("Microsoft.AspNet.Mvc");
        mvcPackage.Version.Should().Be("5.2.7");
        mvcPackage.TargetFramework.Should().Be("net48");
        mvcPackage.IsDevelopmentDependency.Should().BeFalse();
    }

    [Fact]
    public void Parse_ValidPackagesConfig_ExtractsMultiplePackages()
    {
        // Arrange
        var packagesConfigPath = Path.Combine(_fixturesPath, "packages.config");

        // Act
        var result = _parser.Parse(packagesConfigPath);

        // Assert
        result.Should().Contain(p => p.Id == "EntityFramework");
        result.Should().Contain(p => p.Id == "Newtonsoft.Json");
        result.Should().Contain(p => p.Id == "Microsoft.AspNet.Razor");
        result.Should().Contain(p => p.Id == "Antlr");
    }

    [Fact]
    public void Parse_ValidPackagesConfig_ExtractsCorrectVersions()
    {
        // Arrange
        var packagesConfigPath = Path.Combine(_fixturesPath, "packages.config");

        // Act
        var result = _parser.Parse(packagesConfigPath);

        // Assert
        var efPackage = result.FirstOrDefault(p => p.Id == "EntityFramework");
        efPackage.Should().NotBeNull();
        efPackage!.Version.Should().Be("6.4.4");

        var jsonPackage = result.FirstOrDefault(p => p.Id == "Newtonsoft.Json");
        jsonPackage.Should().NotBeNull();
        jsonPackage!.Version.Should().Be("12.0.2");
    }

    [Fact]
    public void Parse_MissingFile_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixturesPath, "nonexistent-packages.config");

        // Act
        var result = _parser.Parse(nonExistentPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullFilePath_ReturnsEmptyList()
    {
        // Act
        var result = _parser.Parse(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyFilePath_ReturnsEmptyList()
    {
        // Act
        var result = _parser.Parse(string.Empty);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceFilePath_ReturnsEmptyList()
    {
        // Act
        var result = _parser.Parse("   ");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyPackagesConfig_ReturnsEmptyList()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
</packages>");

            // Act
            var result = _parser.Parse(tempFile);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_PackageWithDevelopmentDependency_ExtractsFlag()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""TestPackage"" version=""1.0.0"" targetFramework=""net48"" developmentDependency=""true"" />
</packages>");

            // Act
            var result = _parser.Parse(tempFile);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsDevelopmentDependency.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_PackageWithoutDevelopmentDependency_DefaultsToFalse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""TestPackage"" version=""1.0.0"" targetFramework=""net48"" />
</packages>");

            // Act
            var result = _parser.Parse(tempFile);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsDevelopmentDependency.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_MalformedXml_ReturnsEmptyList()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""TestPackage"" version=""1.0.0""");

            // Act
            var result = _parser.Parse(tempFile);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_PackageWithMissingTargetFramework_ExtractsOtherProperties()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""TestPackage"" version=""1.0.0"" />
</packages>");

            // Act
            var result = _parser.Parse(tempFile);

            // Assert
            result.Should().HaveCount(1);
            result[0].Id.Should().Be("TestPackage");
            result[0].Version.Should().Be("1.0.0");
            result[0].TargetFramework.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_PackageReference_HasDefaultCompatibilityUnknown()
    {
        // Arrange
        var packagesConfigPath = Path.Combine(_fixturesPath, "packages.config");

        // Act
        var result = _parser.Parse(packagesConfigPath);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(p => p.Compatibility.Should().Be(PackageCompatibility.Unknown));
    }
}
