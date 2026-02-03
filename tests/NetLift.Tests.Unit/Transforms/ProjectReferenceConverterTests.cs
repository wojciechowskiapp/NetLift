using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Transforms.Converters;

namespace NetLift.Tests.Unit.Transforms;

public class ProjectReferenceConverterTests : IDisposable
{
    private readonly IProjectReferenceConverter _converter;
    private readonly Mock<ILogger<ProjectReferenceConverter>> _loggerMock;
    private readonly string _testProjectPath;
    private readonly string _tempDirectory;

    public ProjectReferenceConverterTests()
    {
        _loggerMock = new Mock<ILogger<ProjectReferenceConverter>>();
        _converter = new ProjectReferenceConverter(_loggerMock.Object);

        // Create a temporary directory for test files
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"NetLiftTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _testProjectPath = Path.Combine(_tempDirectory, "TestProject.csproj");
        File.WriteAllText(_testProjectPath, "<Project />");
    }

    [Fact]
    public void ConvertProjectReferences_NullReferences_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _converter.ConvertProjectReferences(null!, _testProjectPath);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("references");
    }

    [Fact]
    public void ConvertProjectReferences_NullSourcePath_ThrowsArgumentException()
    {
        // Arrange
        var references = new List<ProjectReference>();

        // Act & Assert
        var act = () => _converter.ConvertProjectReferences(references, null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourceProjectPath");
    }

    [Fact]
    public void ConvertProjectReferences_EmptySourcePath_ThrowsArgumentException()
    {
        // Arrange
        var references = new List<ProjectReference>();

        // Act & Assert
        var act = () => _converter.ConvertProjectReferences(references, string.Empty);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourceProjectPath");
    }

    [Fact]
    public void ConvertProjectReferences_EmptyList_ReturnsNull()
    {
        // Arrange
        var references = new List<ProjectReference>();

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertProjectReferences_ValidReference_CreatesSimpleElement()
    {
        // Arrange
        var referencedProjectPath = Path.Combine(_tempDirectory, "ReferencedProject.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "ReferencedProject.csproj",
                Name = "ReferencedProject",
                Guid = "{12345678-1234-1234-1234-123456789012}"
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.LocalName.Should().Be("ItemGroup");

        var projectRefs = result.Elements("ProjectReference").ToList();
        projectRefs.Should().HaveCount(1);

        var projectRef = projectRefs[0];
        projectRef.Attribute("Include")?.Value.Should().Be("ReferencedProject.csproj");

        // GUID and Name should not be present
        projectRef.Elements().Should().BeEmpty();
    }

    [Fact]
    public void ConvertProjectReferences_WithBackslashes_NormalizesToForwardSlashes()
    {
        // Arrange
        var libDir = Path.Combine(_tempDirectory, "Lib");
        Directory.CreateDirectory(libDir);
        var referencedProjectPath = Path.Combine(libDir, "Library.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = @"Lib\Library.csproj",
                Name = "Library"
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();
        projectRef!.Attribute("Include")?.Value.Should().Be("Lib/Library.csproj");
    }

    [Fact]
    public void ConvertProjectReferences_WithRelativePath_SimplifiesPath()
    {
        // Arrange
        var parentDir = Path.GetDirectoryName(_tempDirectory);
        parentDir.Should().NotBeNullOrEmpty("temp directory should have a valid parent directory");
        var siblingDir = Path.Combine(parentDir!, "SiblingProject");
        Directory.CreateDirectory(siblingDir);
        var referencedProjectPath = Path.Combine(siblingDir, "Sibling.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = @"..\SiblingProject\Sibling.csproj",
                Name = "Sibling"
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();
        projectRef!.Attribute("Include")?.Value.Should().Be("../SiblingProject/Sibling.csproj");
    }

    [Fact]
    public void ConvertProjectReferences_PreservesReferenceOutputAssembly()
    {
        // Arrange
        var referencedProjectPath = Path.Combine(_tempDirectory, "BuildOnly.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "BuildOnly.csproj",
                Name = "BuildOnly",
                Metadata = new Dictionary<string, string>
                {
                    ["ReferenceOutputAssembly"] = "false"
                }
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();

        var refOutput = projectRef!.Element("ReferenceOutputAssembly");
        refOutput.Should().NotBeNull();
        refOutput!.Value.Should().Be("false");
    }

    [Fact]
    public void ConvertProjectReferences_PreservesPrivateAssets()
    {
        // Arrange
        var referencedProjectPath = Path.Combine(_tempDirectory, "Analyzer.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "Analyzer.csproj",
                Name = "Analyzer",
                Metadata = new Dictionary<string, string>
                {
                    ["PrivateAssets"] = "all"
                }
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();

        var privateAssets = projectRef!.Element("PrivateAssets");
        privateAssets.Should().NotBeNull();
        privateAssets!.Value.Should().Be("all");
    }

    [Fact]
    public void ConvertProjectReferences_IgnoresNonPreservedMetadata()
    {
        // Arrange
        var referencedProjectPath = Path.Combine(_tempDirectory, "OtherProject.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "OtherProject.csproj",
                Name = "OtherProject",
                Guid = "{12345678-1234-1234-1234-123456789012}",
                Metadata = new Dictionary<string, string>
                {
                    ["Project"] = "{12345678-1234-1234-1234-123456789012}",
                    ["Name"] = "OtherProject",
                    ["Private"] = "True"
                }
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();

        // Should have no child elements since none of the metadata should be preserved
        projectRef!.Elements().Should().BeEmpty();
    }

    [Fact]
    public void ConvertProjectReferences_MissingFile_CreatesCommentedReference()
    {
        // Arrange
        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "NonExistent.csproj",
                Name = "NonExistent"
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();

        // Should have a comment warning
        var comments = projectRef!.Nodes().OfType<XComment>().ToList();
        comments.Should().HaveCount(1);
        comments[0].Value.Should().Contain("MIGRATION WARNING");
        comments[0].Value.Should().Contain("Path not found");

        // Should still have the Include attribute
        projectRef.Attribute("Include")?.Value.Should().Be("NonExistent.csproj");
    }

    [Fact]
    public void ConvertProjectReferences_MissingFile_LogsWarning()
    {
        // Arrange
        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "Missing.csproj",
                Name = "Missing"
            }
        };

        // Act
        _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Project reference not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ConvertProjectReferences_MultipleReferences_OrdersAlphabetically()
    {
        // Arrange
        var projectB = Path.Combine(_tempDirectory, "ProjectB.csproj");
        var projectA = Path.Combine(_tempDirectory, "ProjectA.csproj");
        var projectC = Path.Combine(_tempDirectory, "ProjectC.csproj");

        File.WriteAllText(projectB, "<Project />");
        File.WriteAllText(projectA, "<Project />");
        File.WriteAllText(projectC, "<Project />");

        var references = new List<ProjectReference>
        {
            new() { Path = "ProjectB.csproj", Name = "ProjectB" },
            new() { Path = "ProjectA.csproj", Name = "ProjectA" },
            new() { Path = "ProjectC.csproj", Name = "ProjectC" }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRefs = result!.Elements("ProjectReference").ToList();
        projectRefs.Should().HaveCount(3);

        projectRefs[0].Attribute("Include")?.Value.Should().Be("ProjectA.csproj");
        projectRefs[1].Attribute("Include")?.Value.Should().Be("ProjectB.csproj");
        projectRefs[2].Attribute("Include")?.Value.Should().Be("ProjectC.csproj");
    }

    [Fact]
    public void ConvertProjectReferences_PreservesAllImportantMetadata()
    {
        // Arrange
        var referencedProjectPath = Path.Combine(_tempDirectory, "Complex.csproj");
        File.WriteAllText(referencedProjectPath, "<Project />");

        var references = new List<ProjectReference>
        {
            new()
            {
                Path = "Complex.csproj",
                Name = "Complex",
                Metadata = new Dictionary<string, string>
                {
                    ["ReferenceOutputAssembly"] = "false",
                    ["PrivateAssets"] = "all",
                    ["IncludeAssets"] = "runtime; build",
                    ["ExcludeAssets"] = "contentFiles",
                    ["Aliases"] = "global,myalias",
                    ["EmbedInteropTypes"] = "true"
                }
            }
        };

        // Act
        var result = _converter.ConvertProjectReferences(references, _testProjectPath);

        // Assert
        result.Should().NotBeNull();
        var projectRef = result!.Element("ProjectReference");
        projectRef.Should().NotBeNull();

        projectRef!.Element("ReferenceOutputAssembly")?.Value.Should().Be("false");
        projectRef.Element("PrivateAssets")?.Value.Should().Be("all");
        projectRef.Element("IncludeAssets")?.Value.Should().Be("runtime; build");
        projectRef.Element("ExcludeAssets")?.Value.Should().Be("contentFiles");
        projectRef.Element("Aliases")?.Value.Should().Be("global,myalias");
        projectRef.Element("EmbedInteropTypes")?.Value.Should().Be("true");
    }

    public void Dispose()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
