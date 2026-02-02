using FluentAssertions;
using NetLift.Analysis.Parsers;
using NetLift.Core.Models;
using NetLift.Tests.Unit.TestHelpers;

namespace NetLift.Tests.Unit.Analysis.Parsers;

public class OldFormatProjectParserTests
{
    private readonly OldFormatProjectParser _parser;
    private readonly string _mvc5BasicProjectPath;

    public OldFormatProjectParserTests()
    {
        _parser = new OldFormatProjectParser();
        _mvc5BasicProjectPath = TestFixtureHelper.GetFixturePath("mvc5-basic", "Mvc5Basic/Mvc5Basic.csproj");
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidOldStyleProject_ShouldParseSuccessfully()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.Should().NotBeNull();
        result.Format.Should().Be(ProjectFormat.OldStyle);
        result.Name.Should().Be("Mvc5Basic");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractTargetFramework()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.TargetFramework.Should().NotBeNull();
        result.TargetFramework!.OriginalVersion.Should().Be("v4.8");
        result.TargetFramework.Type.Should().Be(FrameworkType.Framework);
        result.TargetFramework.Moniker.Should().Be("net48");
        result.TargetFramework.Version.Should().NotBeNull();
        result.TargetFramework.Version!.Major.Should().Be(4);
        result.TargetFramework.Version!.Minor.Should().Be(8);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractAssemblyName()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.AssemblyName.Should().Be("Mvc5Basic");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractRootNamespace()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.RootNamespace.Should().Be("Mvc5Basic");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractOutputType()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.OutputType.Should().Be("Library");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractProjectGuid()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.ProjectGuid.Should().Be("{A8B4D6F2-3C9E-4A1B-8D5F-2E7C4B9A1F3D}");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractProjectTypeGuids()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.ProjectTypeGuids.Should().HaveCount(2);
        result.ProjectTypeGuids.Should().Contain("{349c5851-65df-11da-9384-00065b846f21}");
        result.ProjectTypeGuids.Should().Contain("{fae04ec0-301f-11d3-bf4b-00c04f79efbc}");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractSystemReferences()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.References.Should().NotBeEmpty();

        var systemReference = result.References.FirstOrDefault(r => r.Name == "System");
        systemReference.Should().NotBeNull();
        systemReference!.IsNuGetPackage.Should().BeFalse();
        systemReference.HintPath.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractNuGetReferences()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        var mvcReference = result.References.FirstOrDefault(r => r.Name == "System.Web.Mvc");
        mvcReference.Should().NotBeNull();
        mvcReference!.Version.Should().Be("5.2.7.0");
        mvcReference.Culture.Should().Be("neutral");
        mvcReference.PublicKeyToken.Should().Be("31bf3856ad364e35");
        mvcReference.ProcessorArchitecture.Should().Be("MSIL");
        mvcReference.IsNuGetPackage.Should().BeTrue();
        mvcReference.HintPath.Should().Contain("Microsoft.AspNet.Mvc.5.2.7");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractPrivateReferences()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        var privateReference = result.References.FirstOrDefault(r => r.IsPrivate == true);
        privateReference.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractMultipleNuGetPackages()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        var nugetReferences = result.References.Where(r => r.IsNuGetPackage).ToList();
        nugetReferences.Should().NotBeEmpty();

        var expectedPackages = new[]
        {
            "Microsoft.Web.Infrastructure",
            "System.Web.Helpers",
            "System.Web.Mvc",
            "System.Web.Optimization",
            "System.Web.Razor",
            "System.Web.WebPages",
            "Newtonsoft.Json",
            "EntityFramework"
        };

        foreach (var expectedPackage in expectedPackages)
        {
            result.References.Should().Contain(r => r.Name.Contains(expectedPackage) && r.IsNuGetPackage,
                $"Expected to find NuGet reference for {expectedPackage}");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractCompileItems()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.CompileItems.Should().NotBeEmpty();

        var homeController = result.CompileItems.FirstOrDefault(c => c.Include.Contains("HomeController.cs"));
        homeController.Should().NotBeNull();
        homeController!.Include.Should().Be("Controllers\\HomeController.cs");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExcludeGlobalAsaxCs()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert: Global.asax.cs is excluded because it's incompatible with ASP.NET Core
        // (replaced by Program.cs in modern ASP.NET Core apps)
        var globalAsaxCs = result.CompileItems.FirstOrDefault(c => c.Include.Contains("Global.asax.cs"));
        globalAsaxCs.Should().BeNull("Global.asax.cs should be excluded from migration");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractContentItems()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.ContentItems.Should().NotBeEmpty();

        var webConfig = result.ContentItems.FirstOrDefault(c => c.Include.Contains("Web.config"));
        webConfig.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractViewFiles()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        var viewFiles = result.ContentItems.Where(c => c.Include.EndsWith(".cshtml")).ToList();
        viewFiles.Should().NotBeEmpty();

        var expectedViews = new[]
        {
            "Views\\_ViewStart.cshtml",
            "Views\\Shared\\_Layout.cshtml",
            "Views\\Home\\Index.cshtml"
        };

        foreach (var expectedView in expectedViews)
        {
            result.ContentItems.Should().Contain(c => c.Include == expectedView,
                $"Expected to find view file {expectedView}");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractProperties()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.Properties.Should().NotBeEmpty();
        result.Properties.Should().ContainKey("TargetFrameworkVersion");
        result.Properties.Should().ContainKey("AssemblyName");
        result.Properties.Should().ContainKey("RootNamespace");
        result.Properties.Should().ContainKey("OutputType");
        result.Properties.Should().ContainKey("MvcBuildViews");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = "C:\\NonExistent\\Project.csproj";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _parser.AnalyzeAsync(nonExistentPath));
    }

    [Fact]
    public void CanParse_WithOldStyleProject_ShouldReturnTrue()
    {
        // Act
        var result = _parser.CanParse(_mvc5BasicProjectPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentPath = "C:\\NonExistent\\Project.csproj";

        // Act
        var result = _parser.CanParse(nonExistentPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldSetCorrectFilePath()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.FilePath.Should().Be(Path.GetFullPath(_mvc5BasicProjectPath));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldExtractAllReferenceTypes()
    {
        // Act
        var result = await _parser.AnalyzeAsync(_mvc5BasicProjectPath);

        // Assert
        result.References.Should().Contain(r => !r.IsNuGetPackage, "Should have system references");
        result.References.Should().Contain(r => r.IsNuGetPackage, "Should have NuGet references");
    }
}
