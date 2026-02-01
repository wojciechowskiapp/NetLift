# Task 040: Generate _ViewImports.cshtml with Tag Helpers

## Meta
- **Priority**: P1
- **Estimate**: 5 points
- **Sprint**: 4
- **Dependencies**: 038, 039
- **Status**: Not Started

## Description
Implement a generator to create ASP.NET Core _ViewImports.cshtml files with proper @addTagHelper directives, @using statements, and @inject declarations. This replaces the legacy web.config namespace registrations and enables Tag Helpers throughout the application.

## Acceptance Criteria
- [ ] ViewImportsGenerator class implemented
- [ ] Generate @addTagHelper directive for built-in tag helpers
- [ ] Generate @using statements from web.config namespaces
- [ ] Analyze existing views for namespace usage patterns
- [ ] Support area-specific _ViewImports.cshtml files
- [ ] Generate @inject declarations for common services
- [ ] Include @model directive recommendations
- [ ] Handle custom tag helper library references
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### ViewImports Generator
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NetLift.Mvc.Generators;

public class ViewImportsGenerator
{
    private readonly HashSet<string> _namespaces = new();
    private readonly HashSet<string> _tagHelperAssemblies = new();
    private readonly List<InjectDeclaration> _injectDeclarations = new();

    public ViewImportsGenerator()
    {
        // Default ASP.NET Core tag helpers
        _tagHelperAssemblies.Add("Microsoft.AspNetCore.Mvc.TagHelpers");
    }

    /// <summary>
    /// Analyzes legacy web.config for namespace registrations
    /// </summary>
    public void AnalyzeWebConfig(string webConfigPath)
    {
        if (!File.Exists(webConfigPath)) return;

        var doc = XDocument.Load(webConfigPath);

        // Find <pages><namespaces><add namespace="..."/></namespaces></pages>
        var namespaceElements = doc.Descendants("add")
            .Where(e => e.Parent?.Name == "namespaces");

        foreach (var ns in namespaceElements)
        {
            var namespaceName = ns.Attribute("namespace")?.Value;
            if (!string.IsNullOrEmpty(namespaceName))
            {
                var mappedNamespace = MapLegacyNamespace(namespaceName);
                if (mappedNamespace != null)
                {
                    _namespaces.Add(mappedNamespace);
                }
            }
        }
    }

    /// <summary>
    /// Analyzes Razor views for commonly used namespaces
    /// </summary>
    public void AnalyzeViews(IEnumerable<string> viewPaths)
    {
        foreach (var viewPath in viewPaths)
        {
            var content = File.ReadAllText(viewPath);
            ExtractNamespacesFromView(content);
        }
    }

    private void ExtractNamespacesFromView(string content)
    {
        // Find @using directives in views
        var usingPattern = new Regex(@"@using\s+([\w.]+)");
        var matches = usingPattern.Matches(content);

        foreach (Match match in matches)
        {
            var ns = match.Groups[1].Value;
            var mapped = MapLegacyNamespace(ns);
            if (mapped != null)
            {
                _namespaces.Add(mapped);
            }
        }

        // Detect common patterns that suggest needed namespaces
        if (content.Contains("@Html."))
        {
            _namespaces.Add("Microsoft.AspNetCore.Mvc.Rendering");
        }

        if (content.Contains("@Url."))
        {
            _namespaces.Add("Microsoft.AspNetCore.Mvc");
        }
    }

    private string? MapLegacyNamespace(string legacyNamespace)
    {
        // Map legacy namespaces to ASP.NET Core equivalents
        return legacyNamespace switch
        {
            "System.Web.Mvc" => "Microsoft.AspNetCore.Mvc",
            "System.Web.Mvc.Ajax" => "Microsoft.AspNetCore.Mvc",
            "System.Web.Mvc.Html" => "Microsoft.AspNetCore.Mvc.Rendering",
            "System.Web.Routing" => "Microsoft.AspNetCore.Routing",
            "System.Web.Optimization" => null, // Removed, handled differently
            "System.Web.WebPages" => null, // Removed
            var ns when ns.StartsWith("System.Web") => null, // Skip other System.Web
            _ => legacyNamespace // Keep non-System.Web namespaces
        };
    }

    /// <summary>
    /// Adds a custom tag helper assembly
    /// </summary>
    public void AddTagHelperAssembly(string assemblyName)
    {
        _tagHelperAssemblies.Add(assemblyName);
    }

    /// <summary>
    /// Adds an @inject declaration
    /// </summary>
    public void AddInjectDeclaration(string typeName, string propertyName)
    {
        _injectDeclarations.Add(new InjectDeclaration(typeName, propertyName));
    }

    /// <summary>
    /// Generates the _ViewImports.cshtml content
    /// </summary>
    public string Generate(string? rootNamespace = null)
    {
        var sb = new StringBuilder();

        // Add tag helper directives
        foreach (var assembly in _tagHelperAssemblies.OrderBy(a => a))
        {
            sb.AppendLine($"@addTagHelper *, {assembly}");
        }

        sb.AppendLine();

        // Add common using statements
        AddDefaultUsings(sb, rootNamespace);

        // Add analyzed namespaces
        foreach (var ns in _namespaces.OrderBy(n => n))
        {
            sb.AppendLine($"@using {ns}");
        }

        // Add inject declarations
        if (_injectDeclarations.Any())
        {
            sb.AppendLine();
            foreach (var inject in _injectDeclarations)
            {
                sb.AppendLine($"@inject {inject.TypeName} {inject.PropertyName}");
            }
        }

        return sb.ToString();
    }

    private void AddDefaultUsings(StringBuilder sb, string? rootNamespace)
    {
        // Standard ASP.NET Core namespaces
        var defaultUsings = new[]
        {
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Mvc.Rendering",
            "Microsoft.AspNetCore.Mvc.ViewFeatures"
        };

        foreach (var ns in defaultUsings)
        {
            sb.AppendLine($"@using {ns}");
        }

        // Add application namespace if provided
        if (!string.IsNullOrEmpty(rootNamespace))
        {
            sb.AppendLine($"@using {rootNamespace}");
            sb.AppendLine($"@using {rootNamespace}.Models");
            sb.AppendLine($"@using {rootNamespace}.ViewModels");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Generates area-specific _ViewImports.cshtml
    /// </summary>
    public string GenerateForArea(string areaName, string? rootNamespace = null)
    {
        var sb = new StringBuilder();

        // Inherit from root _ViewImports
        sb.AppendLine("@* This file inherits from root _ViewImports.cshtml *@");
        sb.AppendLine();

        // Area-specific namespace
        if (!string.IsNullOrEmpty(rootNamespace))
        {
            sb.AppendLine($"@using {rootNamespace}.Areas.{areaName}");
            sb.AppendLine($"@using {rootNamespace}.Areas.{areaName}.Models");
            sb.AppendLine($"@using {rootNamespace}.Areas.{areaName}.ViewModels");
        }

        return sb.ToString();
    }
}

public record InjectDeclaration(string TypeName, string PropertyName);
```

### View Start Generator
```csharp
public class ViewStartGenerator
{
    /// <summary>
    /// Generates _ViewStart.cshtml content
    /// </summary>
    public string Generate(string layoutPath = "~/Views/Shared/_Layout.cshtml")
    {
        return $@"@{{
    Layout = ""{layoutPath}"";
}}";
    }

    /// <summary>
    /// Generates area-specific _ViewStart.cshtml
    /// </summary>
    public string GenerateForArea(string areaName)
    {
        return $@"@{{
    Layout = ""~/Areas/{areaName}/Views/Shared/_Layout.cshtml"";
}}";
    }
}
```

### Example Output

**Generated _ViewImports.cshtml:**
```cshtml
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

@using Microsoft.AspNetCore.Mvc
@using Microsoft.AspNetCore.Mvc.Rendering
@using Microsoft.AspNetCore.Mvc.ViewFeatures

@using MyApplication
@using MyApplication.Models
@using MyApplication.ViewModels

@using System.Collections.Generic
@using System.Linq

@inject IConfiguration Configuration
@inject IUrlHelper Url
```

**Generated _ViewStart.cshtml:**
```cshtml
@{
    Layout = "~/Views/Shared/_Layout.cshtml";
}
```

### Legacy Web.config Analysis
```xml
<!-- Legacy Views/web.config -->
<configuration>
  <system.web.webPages.razor>
    <pages pageBaseType="System.Web.Mvc.WebViewPage">
      <namespaces>
        <add namespace="System.Web.Mvc" />
        <add namespace="System.Web.Mvc.Ajax" />
        <add namespace="System.Web.Mvc.Html" />
        <add namespace="System.Web.Routing" />
        <add namespace="System.Web.Optimization" />
        <add namespace="MyApplication" />
        <add namespace="MyApplication.Models" />
      </namespaces>
    </pages>
  </system.web.webPages.razor>
</configuration>
```

### Unit Tests
```csharp
public class ViewImportsGeneratorTests
{
    [Fact]
    public void GeneratesDefaultTagHelperDirective()
    {
        var generator = new ViewImportsGenerator();
        var result = generator.Generate();

        Assert.Contains("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers", result);
    }

    [Fact]
    public void GeneratesDefaultUsingStatements()
    {
        var generator = new ViewImportsGenerator();
        var result = generator.Generate();

        Assert.Contains("@using Microsoft.AspNetCore.Mvc", result);
        Assert.Contains("@using Microsoft.AspNetCore.Mvc.Rendering", result);
    }

    [Fact]
    public void AddsRootNamespaceUsings()
    {
        var generator = new ViewImportsGenerator();
        var result = generator.Generate("MyApp");

        Assert.Contains("@using MyApp", result);
        Assert.Contains("@using MyApp.Models", result);
        Assert.Contains("@using MyApp.ViewModels", result);
    }

    [Fact]
    public void AnalyzesWebConfigNamespaces()
    {
        var webConfig = CreateTempWebConfig(@"
<configuration>
  <system.web.webPages.razor>
    <pages>
      <namespaces>
        <add namespace=""System.Web.Mvc"" />
        <add namespace=""MyApp.Helpers"" />
      </namespaces>
    </pages>
  </system.web.webPages.razor>
</configuration>");

        var generator = new ViewImportsGenerator();
        generator.AnalyzeWebConfig(webConfig);
        var result = generator.Generate();

        Assert.Contains("@using Microsoft.AspNetCore.Mvc", result);
        Assert.Contains("@using MyApp.Helpers", result);
        Assert.DoesNotContain("System.Web.Mvc", result);
    }

    [Fact]
    public void GeneratesInjectDeclarations()
    {
        var generator = new ViewImportsGenerator();
        generator.AddInjectDeclaration("IConfiguration", "Configuration");

        var result = generator.Generate();

        Assert.Contains("@inject IConfiguration Configuration", result);
    }

    [Fact]
    public void GeneratesAreaSpecificViewImports()
    {
        var generator = new ViewImportsGenerator();
        var result = generator.GenerateForArea("Admin", "MyApp");

        Assert.Contains("@using MyApp.Areas.Admin", result);
        Assert.Contains("@using MyApp.Areas.Admin.Models", result);
    }

    [Fact]
    public void AddsCustomTagHelperAssembly()
    {
        var generator = new ViewImportsGenerator();
        generator.AddTagHelperAssembly("MyApp.CustomTagHelpers");

        var result = generator.Generate();

        Assert.Contains("@addTagHelper *, MyApp.CustomTagHelpers", result);
    }
}
```

## Progress Log
- [Created] - Task definition with ViewImports generator implementation details
