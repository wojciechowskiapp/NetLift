using FluentAssertions;
using NetLift.Transforms;
using Xunit;

namespace NetLift.Tests.Unit.Transforms;

public sealed class AssemblyInfoExtractorTests : IDisposable
{
    private readonly AssemblyInfoExtractor _extractor;
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles;

    public AssemblyInfoExtractorTests()
    {
        _extractor = new AssemblyInfoExtractor();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"NetLiftTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _tempFiles = new List<string>();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_WithStandardAttributes_ExtractsAllProperties()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(""TestProject"")]
[assembly: AssemblyDescription(""Test Description"")]
[assembly: AssemblyCompany(""Test Company"")]
[assembly: AssemblyProduct(""Test Product"")]
[assembly: AssemblyCopyright(""Copyright © 2025"")]
[assembly: AssemblyTrademark(""Test Trademark"")]
[assembly: AssemblyVersion(""1.2.3.4"")]
[assembly: AssemblyFileVersion(""1.2.3.4"")]
[assembly: AssemblyInformationalVersion(""1.2.3-beta"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Should().NotBeNull();
        result.FilePath.Should().Be(assemblyInfoPath);
        result.Title.Should().Be("TestProject");
        result.Description.Should().Be("Test Description");
        result.Company.Should().Be("Test Company");
        result.Product.Should().Be("Test Product");
        result.Copyright.Should().Be("Copyright © 2025");
        result.Trademark.Should().Be("Test Trademark");
        result.AssemblyVersion.Should().Be("1.2.3.4");
        result.FileVersion.Should().Be("1.2.3.4");
        result.InformationalVersion.Should().Be("1.2.3-beta");
    }

    [Fact]
    public async Task ExtractAsync_WithComVisibleAndGuid_ExtractsComProperties()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: Guid(""12345678-1234-1234-1234-123456789012"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.ComVisible.Should().BeFalse();
        result.Guid.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    [Fact]
    public async Task ExtractAsync_WithInternalsVisibleTo_ExtractsTestAssemblies()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""MyProject.Tests"")]
[assembly: InternalsVisibleTo(""MyProject.Integration.Tests"")]
[assembly: InternalsVisibleTo(""DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.InternalsVisibleTo.Should().HaveCount(3);
        result.InternalsVisibleTo.Should().Contain("MyProject.Tests");
        result.InternalsVisibleTo.Should().Contain("MyProject.Integration.Tests");
        result.InternalsVisibleTo.Should().Contain("DynamicProxyGenAssembly2");
    }

    [Fact]
    public async Task ExtractAsync_WithNeutralResourcesLanguage_ExtractsLanguage()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Resources;

[assembly: NeutralResourcesLanguage(""en-US"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.NeutralLanguage.Should().Be("en-US");
    }

    [Fact]
    public async Task ExtractAsync_WithCustomAttributes_PreservesCustomAttributes()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;
using CustomNamespace;

[assembly: AssemblyTitle(""Test"")]
[assembly: CustomAttribute(""CustomValue"")]
[assembly: AnotherCustomAttribute(""Value1"", ""Value2"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Title.Should().Be("Test");
        result.CustomAttributes.Should().HaveCount(2);
        result.CustomAttributes.Should().Contain(ca => ca.Name == "Custom");
        result.CustomAttributes.Should().Contain(ca => ca.Name == "AnotherCustom");
    }

    [Fact]
    public async Task ExtractAsync_WithEmptyAttributes_HandlesGracefully()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;

[assembly: AssemblyTitle("""")]
[assembly: AssemblyDescription("""")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Title.Should().BeEmpty();
        result.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WithMvc5BasicFixture_ExtractsCorrectly()
    {
        // Arrange
        var fixtureAssemblyInfoPath = @"F:\src\NetLift\tests\fixtures\mvc5-basic\Mvc5Basic\Properties\AssemblyInfo.cs";

        if (!File.Exists(fixtureAssemblyInfoPath))
        {
            // Skip test if fixture doesn't exist
            return;
        }

        // Act
        var result = await _extractor.ExtractAsync(fixtureAssemblyInfoPath);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Mvc5Basic");
        result.Description.Should().Be("ASP.NET MVC 5 Test Fixture");
        result.Company.Should().Be("NetLift");
        result.Product.Should().Be("Mvc5Basic");
        result.Copyright.Should().Be("Copyright © 2025");
        result.ComVisible.Should().BeFalse();
        result.Guid.Should().Be("a8b4d6f2-3c9e-4a1b-8d5f-2e7c4b9a1f3d");
        result.AssemblyVersion.Should().Be("1.0.0.0");
        result.FileVersion.Should().Be("1.0.0.0");
    }

    [Fact]
    public async Task ExtractAndMergeAsync_WithMultipleFiles_MergesCorrectly()
    {
        // Arrange
        var sharedAssemblyInfo = @"
using System.Reflection;

[assembly: AssemblyCompany(""Shared Company"")]
[assembly: AssemblyCopyright(""Copyright © 2025"")]
[assembly: AssemblyVersion(""1.0.0.0"")]
";

        var projectAssemblyInfo = @"
using System.Reflection;

[assembly: AssemblyTitle(""ProjectTitle"")]
[assembly: AssemblyDescription(""Project Description"")]
[assembly: AssemblyProduct(""ProjectProduct"")]
";

        var sharedPath = CreateTempFile("SharedAssemblyInfo.cs", sharedAssemblyInfo);
        var projectPath = CreateTempFile("AssemblyInfo.cs", projectAssemblyInfo);

        // Act
        var result = await _extractor.ExtractAndMergeAsync(new[] { sharedPath, projectPath });

        // Assert
        result.Company.Should().Be("Shared Company");
        result.Copyright.Should().Be("Copyright © 2025");
        result.AssemblyVersion.Should().Be("1.0.0.0");
        result.Title.Should().Be("ProjectTitle");
        result.Description.Should().Be("Project Description");
        result.Product.Should().Be("ProjectProduct");
    }

    [Fact]
    public async Task ExtractAndMergeAsync_WithDuplicateInternalsVisibleTo_Deduplicates()
    {
        // Arrange
        var file1 = @"
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""TestAssembly"")]
";

        var file2 = @"
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""TestAssembly"")]
[assembly: InternalsVisibleTo(""AnotherAssembly"")]
";

        var path1 = CreateTempFile("AssemblyInfo1.cs", file1);
        var path2 = CreateTempFile("AssemblyInfo2.cs", file2);

        // Act
        var result = await _extractor.ExtractAndMergeAsync(new[] { path1, path2 });

        // Assert
        result.InternalsVisibleTo.Should().HaveCount(2);
        result.InternalsVisibleTo.Should().Contain("TestAssembly");
        result.InternalsVisibleTo.Should().Contain("AnotherAssembly");
    }

    [Fact]
    public async Task ExtractAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "DoesNotExist.cs");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _extractor.ExtractAsync(nonExistentPath));
    }

    [Fact]
    public async Task ExtractAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _extractor.ExtractAsync(null!));
    }

    [Fact]
    public async Task ExtractAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _extractor.ExtractAsync(string.Empty));
    }

    [Fact]
    public async Task ExtractAndMergeAsync_WithNullPaths_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _extractor.ExtractAndMergeAsync(null!));
    }

    [Fact]
    public async Task ExtractAndMergeAsync_WithEmptyPaths_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _extractor.ExtractAndMergeAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task ExtractAndMergeAsync_WithSinglePath_ReturnsSingleResult()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;

[assembly: AssemblyTitle(""SingleFile"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAndMergeAsync(new[] { assemblyInfoPath });

        // Assert
        result.Title.Should().Be("SingleFile");
        result.FilePath.Should().Be(assemblyInfoPath);
    }

    [Fact]
    public void CanExtract_WithValidAssemblyInfoFile_ReturnsTrue()
    {
        // Arrange
        var validPath = CreateTempFile("AssemblyInfo.cs", "// test");

        // Act
        var result = _extractor.CanExtract(validPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanExtract_WithSharedAssemblyInfoFile_ReturnsTrue()
    {
        // Arrange
        var validPath = CreateTempFile("SharedAssemblyInfo.cs", "// test");

        // Act
        var result = _extractor.CanExtract(validPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanExtract_WithGlobalAssemblyInfoFile_ReturnsTrue()
    {
        // Arrange
        var validPath = CreateTempFile("GlobalAssemblyInfo.cs", "// test");

        // Act
        var result = _extractor.CanExtract(validPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanExtract_WithNonAssemblyInfoFile_ReturnsFalse()
    {
        // Arrange
        var invalidPath = CreateTempFile("SomeOtherFile.cs", "// test");

        // Act
        var result = _extractor.CanExtract(invalidPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanExtract_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "AssemblyInfo.cs");

        // Act
        var result = _extractor.CanExtract(nonExistentPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanExtract_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = _extractor.CanExtract(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanExtract_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = _extractor.CanExtract(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_WithAssemblyConfiguration_ExtractsConfiguration()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;

[assembly: AssemblyConfiguration(""Release"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Configuration.Should().Be("Release");
    }

    [Fact]
    public async Task ExtractAsync_WithAssemblyCulture_ExtractsCulture()
    {
        // Arrange
        var assemblyInfoContent = @"
using System.Reflection;

[assembly: AssemblyCulture(""en-US"")]
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Culture.Should().Be("en-US");
    }

    [Fact]
    public async Task ExtractAsync_WithNoAssemblyAttributes_ReturnsEmptyData()
    {
        // Arrange
        var assemblyInfoContent = @"
using System;

// No assembly attributes
public class SomeClass { }
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Title.Should().BeNull();
        result.Description.Should().BeNull();
        result.Company.Should().BeNull();
        result.InternalsVisibleTo.Should().BeEmpty();
        result.CustomAttributes.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WithClassLevelAttributes_IgnoresNonAssemblyAttributes()
    {
        // Arrange
        var assemblyInfoContent = @"
using System;
using System.Reflection;

[assembly: AssemblyTitle(""Test"")]

[Obsolete]
public class SomeClass { }
";
        var assemblyInfoPath = CreateTempFile("AssemblyInfo.cs", assemblyInfoContent);

        // Act
        var result = await _extractor.ExtractAsync(assemblyInfoPath);

        // Assert
        result.Title.Should().Be("Test");
        result.CustomAttributes.Should().BeEmpty(); // Should not include [Obsolete]
    }

    private string CreateTempFile(string fileName, string content)
    {
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }
}
