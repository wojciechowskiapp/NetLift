# [TASK-008] Detect Project Types (MVC, WCF, etc.)

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-005
- **Blocks:** TASK-009, TASK-010

---

## Description

Implement detectors to identify what type of .NET project we're dealing with: ASP.NET MVC, WCF Service, Web API, Class Library, etc.

---

## Acceptance Criteria

- [ ] Detects ASP.NET MVC 5 projects
- [ ] Detects ASP.NET Web API 2 projects
- [ ] Detects WCF Service projects
- [ ] Detects WCF Client projects
- [ ] Detects Entity Framework 6 usage
- [ ] Detects plain class libraries
- [ ] Detects console applications
- [ ] Returns confidence score for each detection
- [ ] Unit tests for each project type

---

## Technical Notes

### Detection strategies:

**ASP.NET MVC 5:**
- Reference to `System.Web.Mvc`
- Package `Microsoft.AspNet.Mvc`
- Files: `Global.asax`, `Web.config`
- Folders: `Controllers/`, `Views/`

**ASP.NET Web API 2:**
- Reference to `System.Web.Http`
- Package `Microsoft.AspNet.WebApi`
- `WebApiConfig.cs` in App_Start

**WCF Service:**
- Reference to `System.ServiceModel`
- Files with `[ServiceContract]` attribute
- `.svc` files

**WCF Client:**
- Reference to `System.ServiceModel`
- `app.config` with `<system.serviceModel>` section
- No `.svc` files

**Entity Framework 6:**
- Package `EntityFramework`
- Classes inheriting from `DbContext`
- `[DbSet<T>]` properties

### ProjectTypeDetector:

```csharp
public class ProjectTypeDetector
{
    public ProjectTypeResult Detect(ProjectInfo project)
    {
        var result = new ProjectTypeResult
        {
            ProjectPath = project.FilePath
        };

        // Run all detectors
        result.IsMvc = DetectMvc(project);
        result.IsWebApi = DetectWebApi(project);
        result.IsWcfService = DetectWcfService(project);
        result.IsWcfClient = DetectWcfClient(project);
        result.UsesEntityFramework6 = DetectEf6(project);

        // Determine primary type
        result.PrimaryType = DeterminePrimaryType(result);

        return result;
    }

    private DetectionResult DetectMvc(ProjectInfo project)
    {
        var indicators = new List<string>();
        var confidence = 0;

        // Check references
        if (project.References.Any(r => r.Name == "System.Web.Mvc"))
        {
            indicators.Add("System.Web.Mvc reference");
            confidence += 40;
        }

        // Check packages
        if (project.PackageReferences.Any(p => p.Id == "Microsoft.AspNet.Mvc"))
        {
            indicators.Add("Microsoft.AspNet.Mvc package");
            confidence += 30;
        }

        // Check for Controllers folder
        if (project.CompileItems.Any(c => c.Path.Contains("Controllers\\")))
        {
            indicators.Add("Controllers folder");
            confidence += 15;
        }

        // Check for Views folder
        if (project.ContentItems.Any(c => c.Path.Contains("Views\\")))
        {
            indicators.Add("Views folder");
            confidence += 15;
        }

        return new DetectionResult
        {
            Detected = confidence >= 50,
            Confidence = Math.Min(confidence, 100),
            Indicators = indicators
        };
    }
}
```

### ProjectTypeResult model:

```csharp
public class ProjectTypeResult
{
    public string ProjectPath { get; set; }
    public ProjectType PrimaryType { get; set; }

    public DetectionResult IsMvc { get; set; }
    public DetectionResult IsWebApi { get; set; }
    public DetectionResult IsWcfService { get; set; }
    public DetectionResult IsWcfClient { get; set; }
    public DetectionResult UsesEntityFramework6 { get; set; }
}

public class DetectionResult
{
    public bool Detected { get; set; }
    public int Confidence { get; set; }  // 0-100
    public List<string> Indicators { get; set; }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
