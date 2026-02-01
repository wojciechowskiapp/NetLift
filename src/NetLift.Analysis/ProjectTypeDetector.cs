using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis;

/// <summary>
/// Detects project types and features by analyzing project information.
/// </summary>
public class ProjectTypeDetector : IProjectTypeDetector
{
    /// <summary>
    /// Detects the type and features of a .NET project.
    /// </summary>
    /// <param name="project">The project information to analyze.</param>
    /// <returns>A comprehensive detection result with confidence scores.</returns>
    public ProjectTypeResult Detect(ProjectInfo project)
    {
        var result = new ProjectTypeResult
        {
            ProjectPath = project.FilePath
        };

        // Run all detectors
        result.IsMvc = DetectMvc(project);
        result.IsWebApi = DetectWebApi(project);
        result.IsWebForms = DetectWebForms(project);
        result.IsWcfService = DetectWcfService(project);
        result.IsWcfClient = DetectWcfClient(project);
        result.UsesEntityFramework6 = DetectEntityFramework6(project);
        result.IsConsoleApp = DetectConsoleApp(project);
        result.IsClassLibrary = DetectClassLibrary(project);
        result.IsWpfApp = DetectWpfApp(project);
        result.IsWinFormsApp = DetectWinFormsApp(project);

        // Determine primary type
        result.PrimaryType = DeterminePrimaryType(result, project);

        return result;
    }

    private DetectionResult DetectMvc(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check assembly references
        if (project.References.Any(r => r.Name == "System.Web.Mvc"))
        {
            indicators.Add("System.Web.Mvc assembly reference");
            confidence += 40;
        }

        // Check package references
        if (project.PackageReferences.Any(p => p.Id == "Microsoft.AspNet.Mvc"))
        {
            indicators.Add("Microsoft.AspNet.Mvc NuGet package");
            confidence += 30;
        }

        // Check for Controllers folder
        if (project.CompileItems.Any(c => c.Include.Contains("Controllers\\") || c.Include.Contains("Controllers/")))
        {
            indicators.Add("Controllers folder present");
            confidence += 15;
        }

        // Check for Views folder
        if (project.ContentItems.Any(c => c.Include.Contains("Views\\") || c.Include.Contains("Views/")))
        {
            indicators.Add("Views folder present");
            confidence += 15;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWebApi(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check assembly references
        if (project.References.Any(r => r.Name == "System.Web.Http"))
        {
            indicators.Add("System.Web.Http assembly reference");
            confidence += 40;
        }

        // Check package references
        if (project.PackageReferences.Any(p => p.Id == "Microsoft.AspNet.WebApi" || p.Id == "Microsoft.AspNet.WebApi.Core"))
        {
            indicators.Add("Microsoft.AspNet.WebApi NuGet package");
            confidence += 30;
        }

        // Check for WebApiConfig.cs
        if (project.CompileItems.Any(c => c.Include.Contains("WebApiConfig.cs")))
        {
            indicators.Add("WebApiConfig.cs file present");
            confidence += 20;
        }

        // Check for ApiController classes
        if (project.CompileItems.Any(c => c.Include.Contains("ApiController.cs") || c.Include.EndsWith("ApiController.cs")))
        {
            indicators.Add("ApiController classes present");
            confidence += 10;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWebForms(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for System.Web reference (but not MVC or Web API)
        if (project.References.Any(r => r.Name == "System.Web"))
        {
            // Only count this if it's not MVC or Web API
            if (!project.References.Any(r => r.Name == "System.Web.Mvc" || r.Name == "System.Web.Http"))
            {
                indicators.Add("System.Web assembly reference");
                confidence += 30;
            }
        }

        // Check for .aspx files
        if (project.ContentItems.Any(c => c.Include.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".aspx files present");
            confidence += 40;
        }

        // Check for .ascx files (user controls)
        if (project.ContentItems.Any(c => c.Include.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".ascx user control files present");
            confidence += 15;
        }

        // Check for .master files
        if (project.ContentItems.Any(c => c.Include.EndsWith(".master", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".master page files present");
            confidence += 15;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWcfService(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for System.ServiceModel reference
        if (project.References.Any(r => r.Name == "System.ServiceModel"))
        {
            indicators.Add("System.ServiceModel assembly reference");
            confidence += 40;
        }

        // Check for .svc files
        if (project.ContentItems.Any(c => c.Include.EndsWith(".svc", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".svc service files present");
            confidence += 40;
        }

        // Check for WCF project type GUID
        if (project.ProjectTypeGuids.Any(g => g.Equals("{3D9AD99F-2412-4246-B90B-4EAA41C64699}", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add("WCF project type GUID");
            confidence += 20;
        }

        return new DetectionResult
        {
            Detected = confidence >= 60,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWcfClient(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for System.ServiceModel reference
        if (project.References.Any(r => r.Name == "System.ServiceModel"))
        {
            indicators.Add("System.ServiceModel assembly reference");
            confidence += 40;
        }

        // Penalize if .svc files are found (indicates service, not client)
        if (project.ContentItems.Any(c => c.Include.EndsWith(".svc", StringComparison.OrdinalIgnoreCase)))
        {
            confidence -= 50;
        }
        else
        {
            indicators.Add("No .svc files (client project)");
            confidence += 30;
        }

        // Check for service reference in project
        if (project.ContentItems.Any(c => c.Include.Contains("Service References") || c.Include.Contains("Connected Services")))
        {
            indicators.Add("Service References folder present");
            confidence += 30;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectEntityFramework6(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for EntityFramework package with version 6.x
        var efPackage = project.PackageReferences.FirstOrDefault(p => p.Id == "EntityFramework");
        if (efPackage != null)
        {
            if (efPackage.Version?.StartsWith("6.") == true)
            {
                indicators.Add($"EntityFramework {efPackage.Version} NuGet package");
                confidence += 50;
            }
            else
            {
                indicators.Add($"EntityFramework {efPackage.Version} NuGet package (not version 6.x)");
                confidence += 20;
            }
        }

        // Check for EntityFramework.SqlServer package (EF6 specific)
        if (project.PackageReferences.Any(p => p.Id == "EntityFramework.SqlServer"))
        {
            indicators.Add("EntityFramework.SqlServer NuGet package");
            confidence += 30;
        }

        // Check for System.Data.Entity reference (used by EF6)
        if (project.References.Any(r => r.Name == "System.Data.Entity"))
        {
            indicators.Add("System.Data.Entity assembly reference");
            confidence += 20;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectConsoleApp(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check output type
        if (project.OutputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true)
        {
            indicators.Add("OutputType is Exe");
            confidence += 60;
        }

        // Check that it's not a WPF or WinForms app
        if (project.OutputType?.Equals("WinExe", StringComparison.OrdinalIgnoreCase) == true)
        {
            confidence = 0; // WinExe is not a console app
        }

        // Check for typical console app indicators
        if (confidence > 0 &&
            !project.References.Any(r => r.Name == "System.Windows.Forms" || r.Name == "PresentationFramework"))
        {
            indicators.Add("No GUI framework references");
            confidence += 20;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectClassLibrary(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check output type
        if (project.OutputType?.Equals("Library", StringComparison.OrdinalIgnoreCase) == true)
        {
            indicators.Add("OutputType is Library");
            confidence += 80;
        }

        // Reduce confidence if it's a specialized library (web, WCF, etc.)
        if (project.References.Any(r => r.Name == "System.Web" || r.Name == "System.ServiceModel"))
        {
            confidence -= 30;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWpfApp(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for WPF-specific references
        if (project.References.Any(r => r.Name == "PresentationFramework" || r.Name == "PresentationCore"))
        {
            indicators.Add("WPF framework references (PresentationFramework/PresentationCore)");
            confidence += 50;
        }

        // Check for .xaml files
        if (project.ContentItems.Any(c => c.Include.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) ||
            project.CompileItems.Any(c => c.Include.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".xaml files present");
            confidence += 30;
        }

        // Check WPF project type GUID
        if (project.ProjectTypeGuids.Any(g => g.Equals("{60dc8134-eba5-43b8-bcc9-bb4bc16c2548}", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add("WPF project type GUID");
            confidence += 20;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private DetectionResult DetectWinFormsApp(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check for Windows Forms reference
        if (project.References.Any(r => r.Name == "System.Windows.Forms"))
        {
            indicators.Add("System.Windows.Forms assembly reference");
            confidence += 50;
        }

        // Check for .resx files (resource files common in WinForms)
        if (project.EmbeddedResources.Any(r => r.Include.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
        {
            indicators.Add(".resx resource files present");
            confidence += 20;
        }

        // Check output type
        if (project.OutputType?.Equals("WinExe", StringComparison.OrdinalIgnoreCase) == true)
        {
            indicators.Add("OutputType is WinExe");
            confidence += 30;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }

    private ProjectType DeterminePrimaryType(ProjectTypeResult result, ProjectInfo project)
    {
        // Priority order for determining primary type

        // WCF Service (highest priority for specialized projects)
        if (result.IsWcfService.Detected)
            return ProjectType.WcfService;

        // WCF Client
        if (result.IsWcfClient.Detected)
            return ProjectType.WcfClient;

        // ASP.NET MVC (takes precedence over Web API if both are present)
        if (result.IsMvc.Detected)
            return ProjectType.CSharpMvc;

        // ASP.NET Web API
        if (result.IsWebApi.Detected)
            return ProjectType.AspNetWebApi;

        // ASP.NET Web Forms
        if (result.IsWebForms.Detected)
            return ProjectType.AspNetWebForms;

        // WPF Application
        if (result.IsWpfApp.Detected)
            return ProjectType.CSharpWpf;

        // Windows Forms Application
        if (result.IsWinFormsApp.Detected)
            return ProjectType.CSharpWinForms;

        // Console Application
        if (result.IsConsoleApp.Detected)
            return ProjectType.CSharpConsole;

        // Class Library
        if (result.IsClassLibrary.Detected)
            return ProjectType.CSharpClassLibrary;

        // Fallback to legacy detection based on project type GUIDs
        if (project.ProjectTypeGuids.Any())
        {
            var firstGuid = project.ProjectTypeGuids.First().ToUpperInvariant();
            return firstGuid switch
            {
                "{349C5851-65DF-11DA-9384-00065B846F21}" => ProjectType.CSharpWeb,
                "{3D9AD99F-2412-4246-B90B-4EAA41C64699}" => ProjectType.WcfService,
                "{60DC8134-EBA5-43B8-BCC9-BB4BC16C2548}" => ProjectType.CSharpWpf,
                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" => ProjectType.CSharpClassLibrary,
                _ => ProjectType.Unknown
            };
        }

        return ProjectType.Unknown;
    }
}
