# [TASK-011] Create Test Fixture: mvc5-basic

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

- **Depends on:** TASK-001
- **Blocks:** TASK-010 (for integration tests)

---

## Description

Create a minimal ASP.NET MVC 5 test fixture project that can be used to test the analysis and migration features.

---

## Acceptance Criteria

- [ ] Valid ASP.NET MVC 5 project structure
- [ ] Old-style .csproj format (non-SDK)
- [ ] packages.config with typical MVC packages
- [ ] At least one controller (HomeController)
- [ ] At least one view (Index.cshtml)
- [ ] Web.config with standard configuration
- [ ] Global.asax with route registration
- [ ] Builds successfully on .NET Framework 4.8

---

## Technical Notes

### Structure:

```
test-fixtures/
└── mvc5-basic/
    ├── Mvc5Basic.sln
    └── Mvc5Basic/
        ├── Mvc5Basic.csproj          # Old-style format
        ├── packages.config
        ├── Web.config
        ├── Global.asax
        ├── Global.asax.cs
        ├── App_Start/
        │   ├── RouteConfig.cs
        │   └── FilterConfig.cs
        ├── Controllers/
        │   └── HomeController.cs
        ├── Views/
        │   ├── Web.config
        │   ├── _ViewStart.cshtml
        │   ├── Shared/
        │   │   └── _Layout.cshtml
        │   └── Home/
        │       └── Index.cshtml
        └── Properties/
            └── AssemblyInfo.cs
```

### Mvc5Basic.csproj (old format):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('...')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProductVersion>
    </ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{GENERATE-NEW-GUID}</ProjectGuid>
    <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>Mvc5Basic</RootNamespace>
    <AssemblyName>Mvc5Basic</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
  <!-- ... rest of old-style project -->
</Project>
```

### packages.config:

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="Microsoft.AspNet.Mvc" version="5.2.7" targetFramework="net48" />
  <package id="Microsoft.AspNet.Razor" version="3.2.7" targetFramework="net48" />
  <package id="Microsoft.AspNet.WebPages" version="3.2.7" targetFramework="net48" />
  <package id="Microsoft.Web.Infrastructure" version="1.0.0.0" targetFramework="net48" />
</packages>
```

### HomeController.cs:

```csharp
using System.Web.Mvc;

namespace Mvc5Basic.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Welcome to MVC 5";
            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
}
```

### Web.config (key sections):

```xml
<configuration>
  <appSettings>
    <add key="webpages:Version" value="3.0.0.0" />
    <add key="webpages:Enabled" value="false" />
    <add key="ClientValidationEnabled" value="true" />
    <add key="UnobtrusiveJavaScriptEnabled" value="true" />
  </appSettings>
  <system.web>
    <compilation debug="true" targetFramework="4.8" />
    <httpRuntime targetFramework="4.8" />
  </system.web>
</configuration>
```

### Note:

This fixture does NOT need to actually build/run during NetLift development. It just needs to be valid enough for our parsers to analyze. We're testing OUR parsing, not the MVC app itself.

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
