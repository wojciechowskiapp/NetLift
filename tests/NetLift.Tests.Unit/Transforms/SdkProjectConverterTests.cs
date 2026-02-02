using System.Xml.Linq;
using FluentAssertions;
using NetLift.Core.Models;
using NetLift.Transforms;

namespace NetLift.Tests.Unit.Transforms;

public class SdkProjectConverterTests
{
    private readonly SdkProjectConverter _converter;

    public SdkProjectConverterTests()
    {
        _converter = new SdkProjectConverter();
    }

    #region Basic Conversion Tests

    [Fact]
    public void Convert_WithMinimalProject_ShouldGenerateSdkStyleProject()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            FilePath = "C:\\test\\TestProject.csproj",
            OutputType = "Library",
            TargetFramework = new TargetFramework
            {
                OriginalVersion = "v4.8"
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Should().NotBeNull();
        result.Root.Should().NotBeNull();
        result.Root!.Name.LocalName.Should().Be("Project");
        result.Root.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk");
    }

    [Fact]
    public void Convert_WithNullProjectInfo_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _converter.Convert(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Convert_ShouldIncludeXmlDeclaration()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Declaration.Should().NotBeNull();
        result.Declaration!.Version.Should().Be("1.0");
        result.Declaration.Encoding.Should().Be("utf-8");
    }

    #endregion

    #region SDK Type Detection Tests

    [Fact]
    public void Convert_ClassLibraryProject_ShouldUseMicrosoftNetSdk()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyLibrary",
            OutputType = "Library",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "System" },
                new AssemblyReference { Name = "System.Core" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk");
    }

    [Fact]
    public void Convert_WebProject_ShouldUseMicrosoftNetSdkWeb()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyWebApp",
            OutputType = "Library",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "System.Web" },
                new AssemblyReference { Name = "System.Web.Mvc" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk.Web");
    }

    [Fact]
    public void Convert_AspNetPackageReference_ShouldUseMicrosoftNetSdkWeb()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyMvcApp",
            OutputType = "Library",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            PackageReferences = new List<PackageReference>
            {
                new PackageReference { Id = "Microsoft.AspNet.Mvc", Version = "5.2.7" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk.Web");
    }

    [Fact]
    public void Convert_WpfProject_ShouldUseMicrosoftNetSdkWindowsDesktop()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyWpfApp",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "PresentationFramework" },
                new AssemblyReference { Name = "PresentationCore" },
                new AssemblyReference { Name = "WindowsBase" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk.WindowsDesktop");
    }

    [Fact]
    public void Convert_WinFormsProject_ShouldUseMicrosoftNetSdkWindowsDesktop()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyWinFormsApp",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "System.Windows.Forms" },
                new AssemblyReference { Name = "System.Drawing" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk.WindowsDesktop");
    }

    #endregion

    #region Target Framework Conversion Tests

    [Fact]
    public void Convert_Framework48_ShouldConvertToNet80()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var targetFramework = result.Root!
            .Elements("PropertyGroup")
            .Elements("TargetFramework")
            .FirstOrDefault();

        targetFramework.Should().NotBeNull();
        targetFramework!.Value.Should().Be("net8.0");
    }

    [Fact]
    public void Convert_Framework472_ShouldConvertToNet472()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.7.2" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var targetFramework = result.Root!
            .Elements("PropertyGroup")
            .Elements("TargetFramework")
            .FirstOrDefault();

        targetFramework.Should().NotBeNull();
        targetFramework!.Value.Should().Be("net472");
    }

    [Fact]
    public void Convert_Framework461_ShouldConvertToNet461()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.6.1" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var targetFramework = result.Root!
            .Elements("PropertyGroup")
            .Elements("TargetFramework")
            .FirstOrDefault();

        targetFramework.Should().NotBeNull();
        targetFramework!.Value.Should().Be("net461");
    }

    [Fact]
    public void Convert_NoTargetFramework_ShouldDefaultToNet80()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject"
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var targetFramework = result.Root!
            .Elements("PropertyGroup")
            .Elements("TargetFramework")
            .FirstOrDefault();

        targetFramework.Should().NotBeNull();
        targetFramework!.Value.Should().Be("net8.0");
    }

    [Fact]
    public void Convert_WithTargetFrameworkOverride_ShouldUseOverride()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo, "net6.0");

        // Assert
        var targetFramework = result.Root!
            .Elements("PropertyGroup")
            .Elements("TargetFramework")
            .FirstOrDefault();

        targetFramework.Should().NotBeNull();
        targetFramework!.Value.Should().Be("net6.0");
    }

    #endregion

    #region PropertyGroup Tests

    [Fact]
    public void Convert_ShouldIncludeTargetFrameworkInPropertyGroup()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        propertyGroup.Should().NotBeNull();
        propertyGroup!.Element("TargetFramework").Should().NotBeNull();
    }

    [Fact]
    public void Convert_LibraryOutputType_ShouldNotIncludeOutputType()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            OutputType = "Library",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        propertyGroup!.Element("OutputType").Should().BeNull();
    }

    [Fact]
    public void Convert_ExeOutputType_ShouldIncludeOutputType()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            OutputType = "Exe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var outputType = propertyGroup!.Element("OutputType");
        outputType.Should().NotBeNull();
        outputType!.Value.Should().Be("Exe");
    }

    [Fact]
    public void Convert_WinExeOutputType_ShouldIncludeOutputType()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var outputType = propertyGroup!.Element("OutputType");
        outputType.Should().NotBeNull();
        outputType!.Value.Should().Be("WinExe");
    }

    [Fact]
    public void Convert_RootNamespaceSameAsProjectName_ShouldNotIncludeRootNamespace()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            RootNamespace = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        propertyGroup!.Element("RootNamespace").Should().BeNull();
    }

    [Fact]
    public void Convert_RootNamespaceDifferentFromProjectName_ShouldIncludeRootNamespace()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            RootNamespace = "MyCompany.TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var rootNamespace = propertyGroup!.Element("RootNamespace");
        rootNamespace.Should().NotBeNull();
        rootNamespace!.Value.Should().Be("MyCompany.TestProject");
    }

    [Fact]
    public void Convert_AssemblyNameSameAsProjectName_ShouldNotIncludeAssemblyName()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            AssemblyName = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        propertyGroup!.Element("AssemblyName").Should().BeNull();
    }

    [Fact]
    public void Convert_AssemblyNameDifferentFromProjectName_ShouldIncludeAssemblyName()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            AssemblyName = "MyCustomAssembly",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var assemblyName = propertyGroup!.Element("AssemblyName");
        assemblyName.Should().NotBeNull();
        assemblyName!.Value.Should().Be("MyCustomAssembly");
    }

    #endregion

    #region Windows Desktop Properties Tests

    [Fact]
    public void Convert_WpfProject_ShouldIncludeUseWpfProperty()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "WpfApp",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "PresentationFramework" },
                new AssemblyReference { Name = "PresentationCore" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var useWpf = propertyGroup!.Element("UseWPF");
        useWpf.Should().NotBeNull();
        useWpf!.Value.Should().Be("true");
    }

    [Fact]
    public void Convert_WinFormsProject_ShouldIncludeUseWindowsFormsProperty()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "WinFormsApp",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "System.Windows.Forms" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        var useWindowsForms = propertyGroup!.Element("UseWindowsForms");
        useWindowsForms.Should().NotBeNull();
        useWindowsForms!.Value.Should().Be("true");
    }

    [Fact]
    public void Convert_WpfAndWinFormsProject_ShouldIncludeBothProperties()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "HybridApp",
            OutputType = "WinExe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "PresentationFramework" },
                new AssemblyReference { Name = "System.Windows.Forms" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");
        propertyGroup!.Element("UseWPF").Should().NotBeNull();
        propertyGroup.Element("UseWindowsForms").Should().NotBeNull();
    }

    #endregion

    #region Clean Output Tests

    [Fact]
    public void Convert_ShouldGenerateCleanMinimalXml()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "SimpleLib",
            OutputType = "Library",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root.Should().NotBeNull();

        // Should only have one PropertyGroup
        var propertyGroups = result.Root!.Elements("PropertyGroup").ToList();
        propertyGroups.Should().HaveCount(1);

        // Should not have Import elements
        result.Root.Elements("Import").Should().BeEmpty();

        // Should not have Target elements
        result.Root.Elements("Target").Should().BeEmpty();
    }

    [Fact]
    public void Convert_ShouldNotIncludeVerboseXml()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            Properties = new Dictionary<string, string>
            {
                ["Configuration"] = "Debug",
                ["Platform"] = "AnyCPU",
                ["ProjectGuid"] = "{12345678-1234-1234-1234-123456789012}"
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        var propertyGroup = result.Root!.Element("PropertyGroup");

        // Should not include old-style properties
        propertyGroup!.Element("Configuration").Should().BeNull();
        propertyGroup.Element("Platform").Should().BeNull();
        propertyGroup.Element("ProjectGuid").Should().BeNull();
        propertyGroup.Element("SchemaVersion").Should().BeNull();
        propertyGroup.Element("FileAlignment").Should().BeNull();
    }

    #endregion

    #region Complex Project Tests

    [Fact]
    public void Convert_ComplexMvcProject_ShouldGenerateCorrectStructure()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyMvcApp",
            OutputType = "Library",
            AssemblyName = "MyMvcApp",
            RootNamespace = "MyCompany.MyMvcApp",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            References = new List<AssemblyReference>
            {
                new AssemblyReference { Name = "System.Web.Mvc" },
                new AssemblyReference { Name = "System.Web" }
            },
            PackageReferences = new List<PackageReference>
            {
                new PackageReference { Id = "Microsoft.AspNet.Mvc", Version = "5.2.7" },
                new PackageReference { Id = "EntityFramework", Version = "6.4.4" }
            }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk.Web");

        var propertyGroup = result.Root.Element("PropertyGroup");
        propertyGroup.Should().NotBeNull();
        propertyGroup!.Element("TargetFramework")!.Value.Should().Be("net8.0");
        propertyGroup.Element("RootNamespace")!.Value.Should().Be("MyCompany.MyMvcApp");
        propertyGroup.Element("OutputType").Should().BeNull(); // Library is default
    }

    [Fact]
    public void Convert_ConsoleApp_ShouldGenerateCorrectStructure()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "MyConsoleApp",
            OutputType = "Exe",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.7.2" }
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert
        result.Root!.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk");

        var propertyGroup = result.Root.Element("PropertyGroup");
        propertyGroup!.Element("TargetFramework")!.Value.Should().Be("net472");
        propertyGroup.Element("OutputType")!.Value.Should().Be("Exe");
    }

    #endregion

    #region Project Reference Tests

    [Fact]
    public void Convert_WithNoProjectReferences_ShouldNotIncludeProjectReferenceItemGroup()
    {
        // Arrange
        var projectInfo = new ProjectInfo
        {
            Name = "TestProject",
            FilePath = "C:\\test\\TestProject.csproj",
            TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
            ProjectReferences = new List<ProjectReference>()
        };

        // Act
        var result = _converter.Convert(projectInfo);

        // Assert: Should have Compile Remove ItemGroup but no ProjectReference ItemGroup
        var projectRefItemGroups = result.Root!.Elements("ItemGroup")
            .Where(ig => ig.Elements("ProjectReference").Any())
            .ToList();
        projectRefItemGroups.Should().BeEmpty();
    }

    [Fact]
    public void Convert_WithProjectReferences_ShouldIncludeProjectReferenceItemGroup()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"NetLiftTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "TestProject.csproj");
            var referencedProjectPath = Path.Combine(tempDir, "ReferencedProject.csproj");

            File.WriteAllText(projectPath, "<Project />");
            File.WriteAllText(referencedProjectPath, "<Project />");

            var projectInfo = new ProjectInfo
            {
                Name = "TestProject",
                FilePath = projectPath,
                TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
                ProjectReferences = new List<ProjectReference>
                {
                    new()
                    {
                        Path = "ReferencedProject.csproj",
                        Name = "ReferencedProject",
                        Guid = "{12345678-1234-1234-1234-123456789012}"
                    }
                }
            };

            // Act
            var result = _converter.Convert(projectInfo);

            // Assert: Find the ItemGroup containing ProjectReferences
            var projectRefItemGroups = result.Root!.Elements("ItemGroup")
                .Where(ig => ig.Elements("ProjectReference").Any())
                .ToList();
            projectRefItemGroups.Should().HaveCount(1);

            var projectRefs = projectRefItemGroups[0].Elements("ProjectReference").ToList();
            projectRefs.Should().HaveCount(1);
            projectRefs[0].Attribute("Include")?.Value.Should().Be("ReferencedProject.csproj");

            // Should not include GUID or Name
            projectRefs[0].Element("Project").Should().BeNull();
            projectRefs[0].Element("Name").Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Convert_WithMultipleProjectReferences_ShouldIncludeAllReferences()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"NetLiftTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "TestProject.csproj");
            var lib1Path = Path.Combine(tempDir, "Library1.csproj");
            var lib2Path = Path.Combine(tempDir, "Library2.csproj");

            File.WriteAllText(projectPath, "<Project />");
            File.WriteAllText(lib1Path, "<Project />");
            File.WriteAllText(lib2Path, "<Project />");

            var projectInfo = new ProjectInfo
            {
                Name = "TestProject",
                FilePath = projectPath,
                TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
                ProjectReferences = new List<ProjectReference>
                {
                    new() { Path = "Library1.csproj", Name = "Library1" },
                    new() { Path = "Library2.csproj", Name = "Library2" }
                }
            };

            // Act
            var result = _converter.Convert(projectInfo);

            // Assert
            var itemGroup = result.Root!.Elements("ItemGroup").FirstOrDefault();
            itemGroup.Should().NotBeNull();

            var projectRefs = itemGroup!.Elements("ProjectReference").ToList();
            projectRefs.Should().HaveCount(2);

            // References should be ordered alphabetically
            projectRefs[0].Attribute("Include")?.Value.Should().Be("Library1.csproj");
            projectRefs[1].Attribute("Include")?.Value.Should().Be("Library2.csproj");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Convert_WithProjectReferenceMetadata_ShouldPreserveImportantMetadata()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"NetLiftTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "TestProject.csproj");
            var referencedProjectPath = Path.Combine(tempDir, "ReferencedProject.csproj");

            File.WriteAllText(projectPath, "<Project />");
            File.WriteAllText(referencedProjectPath, "<Project />");

            var projectInfo = new ProjectInfo
            {
                Name = "TestProject",
                FilePath = projectPath,
                TargetFramework = new TargetFramework { OriginalVersion = "v4.8" },
                ProjectReferences = new List<ProjectReference>
                {
                    new()
                    {
                        Path = "ReferencedProject.csproj",
                        Name = "ReferencedProject",
                        Metadata = new Dictionary<string, string>
                        {
                            ["ReferenceOutputAssembly"] = "false",
                            ["PrivateAssets"] = "all"
                        }
                    }
                }
            };

            // Act
            var result = _converter.Convert(projectInfo);

            // Assert
            var projectRef = result.Root!
                .Elements("ItemGroup")
                .Elements("ProjectReference")
                .FirstOrDefault();

            projectRef.Should().NotBeNull();
            projectRef!.Element("ReferenceOutputAssembly")?.Value.Should().Be("false");
            projectRef.Element("PrivateAssets")?.Value.Should().Be("all");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion
}
