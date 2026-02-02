using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Transforms.Converters;

namespace NetLift.Tests.Unit.Transforms;

public class PackageReferenceConverterTests
{
    private readonly IPackageReferenceConverter _converter;

    public PackageReferenceConverterTests()
    {
        _converter = new PackageReferenceConverter();
    }

    [Fact]
    public void Convert_NullPackagesConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _converter.Convert(null!, "net8.0");
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("packagesConfig");
    }

    [Fact]
    public void Convert_NullTargetFramework_ThrowsArgumentException()
    {
        // Arrange
        var packagesConfig = new PackagesConfig();

        // Act & Assert
        var act = () => _converter.Convert(packagesConfig, null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetFramework");
    }

    [Fact]
    public void Convert_EmptyTargetFramework_ThrowsArgumentException()
    {
        // Arrange
        var packagesConfig = new PackagesConfig();

        // Act & Assert
        var act = () => _converter.Convert(packagesConfig, string.Empty);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetFramework");
    }

    [Fact]
    public void Convert_EmptyPackagesConfig_ReturnsEmptyResult()
    {
        // Arrange
        var packagesConfig = new PackagesConfig();

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Should().NotBeNull();
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Replacements.Should().BeEmpty();
    }

    [Fact]
    public void Convert_KeepsRegularPackages()
    {
        // Arrange - Use packages that are not in ObsoletePackages or PackageReplacements
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Serilog", Version = "3.1.1" },
                new() { Id = "AutoMapper", Version = "12.0.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(2);
        result.Packages.Should().Contain(p => p.Id == "Serilog" && p.Version == "3.1.1");
        result.Packages.Should().Contain(p => p.Id == "AutoMapper" && p.Version == "12.0.0");
        result.RemovedPackages.Should().BeEmpty();
        result.Replacements.Should().BeEmpty();
    }

    [Fact]
    public void Convert_RemovesObsoletePackages()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.Bcl", Version = "1.1.10" },
                new() { Id = "Microsoft.Bcl.Async", Version = "1.0.168" },
                new() { Id = "Microsoft.CodeDom.Providers.DotNetCompilerPlatform", Version = "2.0.1" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(3);
        result.RemovedPackages.Should().Contain(p => p.Id == "Microsoft.Bcl");
        result.RemovedPackages.Should().Contain(p => p.Id == "Microsoft.Bcl.Async");
        result.RemovedPackages.Should().Contain(p => p.Id == "Microsoft.CodeDom.Providers.DotNetCompilerPlatform");
        result.Warnings.Should().HaveCount(3);
        result.Warnings.Should().AllSatisfy(w => w.Severity.Should().Be(WarningSeverity.Info));
    }

    [Fact]
    public void Convert_RemovesAspNetMvcPackages()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.AspNet.Mvc", Version = "5.2.7" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert: ASP.NET Core MVC is included with Microsoft.NET.Sdk.Web, so the package is removed
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
        result.RemovedPackages[0].Id.Should().Be("Microsoft.AspNet.Mvc");
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Severity.Should().Be(WarningSeverity.Info);
    }

    [Fact]
    public void Convert_RemovesAspNetWebApiPackages()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.AspNet.WebApi", Version = "5.2.7" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert: WebAPI is included with Microsoft.NET.Sdk.Web, so the package is removed
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
        result.RemovedPackages[0].Id.Should().Be("Microsoft.AspNet.WebApi");
    }

    [Fact]
    public void Convert_RemovesSystemRuntimeForModernDotNet()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.Runtime", Version = "4.3.0" },
                new() { Id = "System.Collections", Version = "4.3.0" },
                new() { Id = "System.Linq", Version = "4.3.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(3);
        result.Warnings.Should().HaveCount(3);
        result.Warnings.Should().AllSatisfy(w =>
            w.Message.Should().Contain("now part of net8.0 framework"));
    }

    [Fact]
    public void Convert_KeepsSystemPackagesForNetFramework()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.Runtime", Version = "4.3.0" }
            }
        };

        // Act - Using .NET Framework target
        var result = _converter.Convert(packagesConfig, "net48");

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("System.Runtime");
        result.RemovedPackages.Should().BeEmpty();
    }

    [Fact]
    public void Convert_DeduplicatesPackages_KeepsHighestVersion()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Newtonsoft.Json", Version = "12.0.3" },
                new() { Id = "Newtonsoft.Json", Version = "13.0.3" },
                new() { Id = "Newtonsoft.Json", Version = "11.0.2" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("Newtonsoft.Json");
        result.Packages[0].Version.Should().Be("13.0.3");
    }

    [Fact]
    public void Convert_DeduplicatesCaseInsensitive()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Newtonsoft.Json", Version = "12.0.3" },
                new() { Id = "NEWTONSOFT.JSON", Version = "13.0.3" },
                new() { Id = "newtonsoft.json", Version = "11.0.2" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Version.Should().Be("13.0.3");
    }

    [Fact]
    public void Convert_HandlesPreReleaseVersions()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "TestPackage", Version = "1.0.0-beta1" },
                new() { Id = "TestPackage", Version = "1.0.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Convert_PreservesDevelopmentDependencyFlag()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "TestPackage", Version = "1.0.0", IsDevelopmentDependency = true }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].IsDevelopmentDependency.Should().BeTrue();
    }

    [Fact]
    public void Convert_ReplacementPreservesDevelopmentDependencyFlag()
    {
        // Arrange - Use ApplicationInsights which actually gets replaced (not removed)
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.ApplicationInsights", Version = "2.20.0", IsDevelopmentDependency = true }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert: ApplicationInsights is replaced with AspNetCore version
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("Microsoft.ApplicationInsights.AspNetCore");
        result.Packages[0].IsDevelopmentDependency.Should().BeTrue();
    }

    [Fact]
    public void Convert_SortsPackagesAlphabetically()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Zebra", Version = "1.0.0" },
                new() { Id = "Apple", Version = "1.0.0" },
                new() { Id = "Mango", Version = "1.0.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(3);
        result.Packages[0].Id.Should().Be("Apple");
        result.Packages[1].Id.Should().Be("Mango");
        result.Packages[2].Id.Should().Be("Zebra");
    }

    [Fact]
    public void Convert_ComplexScenario_MixedActions()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Newtonsoft.Json", Version = "12.0.3" }, // Replaced (upgraded)
                new() { Id = "Microsoft.AspNet.Mvc", Version = "5.2.7" }, // Remove (included with Sdk.Web)
                new() { Id = "Microsoft.Bcl.Async", Version = "1.0.168" }, // Remove (obsolete)
                new() { Id = "System.Runtime", Version = "4.3.0" }, // Remove (framework)
                new() { Id = "EntityFramework", Version = "6.4.4" }, // Remove (replaced by EF Core)
                new() { Id = "Serilog", Version = "3.1.1" } // Keep as-is
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().HaveCount(2);
        result.Packages.Should().Contain(p => p.Id == "Newtonsoft.Json" && p.Version == "13.0.3"); // Upgraded
        result.Packages.Should().Contain(p => p.Id == "Serilog"); // Kept as-is

        result.RemovedPackages.Should().HaveCount(4);
        result.RemovedPackages.Should().Contain(p => p.Id == "Microsoft.AspNet.Mvc");
        result.RemovedPackages.Should().Contain(p => p.Id == "Microsoft.Bcl.Async");
        result.RemovedPackages.Should().Contain(p => p.Id == "System.Runtime");
        result.RemovedPackages.Should().Contain(p => p.Id == "EntityFramework");

        result.Replacements.Should().HaveCount(1);
        result.Replacements[0].OldPackage.Id.Should().Be("Newtonsoft.Json");

        result.Warnings.Should().HaveCount(5); // 4 removed + 1 replaced
    }

    [Fact]
    public void IsAnalyzerPackage_RecognizesKnownAnalyzers()
    {
        // Assert
        PackageReferenceConverter.IsAnalyzerPackage("StyleCop.Analyzers").Should().BeTrue();
        PackageReferenceConverter.IsAnalyzerPackage("Microsoft.CodeAnalysis").Should().BeTrue();
        PackageReferenceConverter.IsAnalyzerPackage("SonarAnalyzer.CSharp").Should().BeTrue();
    }

    [Fact]
    public void IsAnalyzerPackage_RecognizesByNamingPattern()
    {
        // Assert
        PackageReferenceConverter.IsAnalyzerPackage("MyCustom.Analyzers").Should().BeTrue();
        PackageReferenceConverter.IsAnalyzerPackage("SomePackage.Analyzer").Should().BeTrue();
        PackageReferenceConverter.IsAnalyzerPackage("MyCodeAnalysis.Tool").Should().BeTrue();
    }

    [Fact]
    public void IsAnalyzerPackage_ReturnsFalseForRegularPackages()
    {
        // Assert
        PackageReferenceConverter.IsAnalyzerPackage("Newtonsoft.Json").Should().BeFalse();
        PackageReferenceConverter.IsAnalyzerPackage("EntityFramework").Should().BeFalse();
        PackageReferenceConverter.IsAnalyzerPackage("Serilog").Should().BeFalse();
    }

    [Fact]
    public void IsAnalyzerPackage_IsCaseInsensitive()
    {
        // Assert
        PackageReferenceConverter.IsAnalyzerPackage("STYLECOP.ANALYZERS").Should().BeTrue();
        PackageReferenceConverter.IsAnalyzerPackage("stylecop.analyzers").Should().BeTrue();
    }

    [Fact]
    public void Convert_WorksWithNetCore31()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.Runtime", Version = "4.3.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "netcoreapp3.1");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
    }

    [Fact]
    public void Convert_WorksWithNet60()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.Collections", Version = "4.3.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net6.0");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
    }

    [Fact]
    public void Convert_WorksWithNet70()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.Linq", Version = "4.3.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net7.0");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
    }

    [Fact]
    public void Convert_WarningContainsPackageId()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.Bcl", Version = "1.1.10" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].PackageId.Should().Be("Microsoft.Bcl");
    }

    [Fact]
    public void Convert_RemovesSystemValueTuple()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "System.ValueTuple", Version = "4.5.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
        result.RemovedPackages[0].Id.Should().Be("System.ValueTuple");
    }

    [Fact]
    public void Convert_RemovesAspNetWebPages()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.AspNet.WebPages", Version = "3.2.7" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert: Razor pages are included with Microsoft.NET.Sdk.Web, so package is removed
        result.Packages.Should().BeEmpty();
        result.RemovedPackages.Should().HaveCount(1);
        result.RemovedPackages[0].Id.Should().Be("Microsoft.AspNet.WebPages");
    }

    [Fact]
    public void Convert_ReplacesApplicationInsights()
    {
        // Arrange
        var packagesConfig = new PackagesConfig
        {
            Packages = new List<PackageReference>
            {
                new() { Id = "Microsoft.ApplicationInsights", Version = "2.20.0" }
            }
        };

        // Act
        var result = _converter.Convert(packagesConfig, "net8.0");

        // Assert: ApplicationInsights is replaced with ASP.NET Core version
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("Microsoft.ApplicationInsights.AspNetCore");
        result.Packages[0].Version.Should().Be("2.22.0");
        result.Replacements.Should().HaveCount(1);
    }
}
