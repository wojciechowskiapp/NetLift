# Task 042: Transform BundleConfig.cs to Modern Asset Pipeline

## Meta
- **Priority**: P2
- **Estimate**: 8 points
- **Sprint**: 4
- **Dependencies**: 040
- **Status**: Not Started

## Description
Implement transformation logic to migrate ASP.NET MVC bundling and minification (BundleConfig.cs, @Styles.Render, @Scripts.Render) to modern asset pipeline alternatives. This includes generating Vite/Webpack configuration suggestions, updating Razor views to use modern asset references, and providing migration paths for different complexity levels.

## Acceptance Criteria
- [ ] BundleConfigParser class to extract bundle definitions
- [ ] Parse ScriptBundle and StyleBundle configurations
- [ ] Generate equivalent Vite configuration
- [ ] Generate equivalent Webpack configuration (alternative)
- [ ] Transform @Styles.Render() calls to modern equivalents
- [ ] Transform @Scripts.Render() calls to modern equivalents
- [ ] Handle CDN fallback patterns
- [ ] Support environment-specific bundles (debug vs release)
- [ ] Generate npm package.json dependencies
- [ ] Unit tests with 95%+ coverage

## Technical Notes

### Bundle Definition Model
```csharp
namespace NetLift.Mvc.Models;

public record BundleDefinition
{
    public string VirtualPath { get; init; } = "";
    public BundleType Type { get; init; }
    public List<string> IncludedFiles { get; init; } = new();
    public List<string> IncludedDirectories { get; init; } = new();
    public bool IsMinified { get; init; }
    public string? CdnPath { get; init; }
    public string? CdnFallbackExpression { get; init; }
}

public enum BundleType
{
    Script,
    Style
}
```

### Bundle Config Parser
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Parsers;

public class BundleConfigParser
{
    public List<BundleDefinition> ParseBundleConfig(SyntaxNode root)
    {
        var bundles = new List<BundleDefinition>();

        // Find RegisterBundles method
        var registerMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "RegisterBundles");

        if (registerMethod == null)
            return bundles;

        // Find all bundle.Add() or bundles.Add() calls
        var addCalls = registerMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsAddBundleCall);

        foreach (var call in addCalls)
        {
            var bundle = ParseBundleAddCall(call);
            if (bundle != null)
            {
                bundles.Add(bundle);
            }
        }

        return bundles;
    }

    private bool IsAddBundleCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "Add";
        }
        return false;
    }

    private BundleDefinition? ParseBundleAddCall(InvocationExpressionSyntax call)
    {
        var argument = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;

        if (argument is ObjectCreationExpressionSyntax objectCreation)
        {
            return ParseBundleCreation(objectCreation);
        }

        return null;
    }

    private BundleDefinition? ParseBundleCreation(
        ObjectCreationExpressionSyntax creation)
    {
        var typeName = creation.Type.ToString();
        var bundleType = typeName.Contains("Script")
            ? BundleType.Script
            : BundleType.Style;

        var virtualPath = ExtractVirtualPath(creation);
        var files = ExtractIncludedFiles(creation);

        return new BundleDefinition
        {
            VirtualPath = virtualPath ?? "",
            Type = bundleType,
            IncludedFiles = files
        };
    }

    private string? ExtractVirtualPath(ObjectCreationExpressionSyntax creation)
    {
        var firstArg = creation.ArgumentList?.Arguments.FirstOrDefault();
        if (firstArg?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }
        return null;
    }

    private List<string> ExtractIncludedFiles(ObjectCreationExpressionSyntax creation)
    {
        var files = new List<string>();

        // Find .Include() method chain
        var parent = creation.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == "Include")
                {
                    foreach (var arg in invocation.ArgumentList.Arguments)
                    {
                        if (arg.Expression is LiteralExpressionSyntax literal)
                        {
                            files.Add(literal.Token.ValueText);
                        }
                    }
                }
            }
            parent = parent.Parent;
        }

        return files;
    }
}
```

### Vite Configuration Generator
```csharp
namespace NetLift.Mvc.Generators;

public class ViteConfigGenerator
{
    public ViteConfiguration Generate(IEnumerable<BundleDefinition> bundles)
    {
        var config = new ViteConfiguration();

        foreach (var bundle in bundles)
        {
            var entryName = GetEntryName(bundle.VirtualPath);

            if (bundle.Type == BundleType.Script)
            {
                config.JsEntries[entryName] = bundle.IncludedFiles
                    .Select(ConvertToModernPath)
                    .ToList();
            }
            else
            {
                config.CssEntries[entryName] = bundle.IncludedFiles
                    .Select(ConvertToModernPath)
                    .ToList();
            }
        }

        return config;
    }

    public string GenerateViteConfigFile(ViteConfiguration config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("import { defineConfig } from 'vite';");
        sb.AppendLine("import { resolve } from 'path';");
        sb.AppendLine();
        sb.AppendLine("export default defineConfig({");
        sb.AppendLine("  build: {");
        sb.AppendLine("    outDir: 'wwwroot/dist',");
        sb.AppendLine("    manifest: true,");
        sb.AppendLine("    rollupOptions: {");
        sb.AppendLine("      input: {");

        var entries = config.JsEntries.Concat(config.CssEntries);
        var entryLines = entries.Select(e =>
            $"        '{e.Key}': resolve(__dirname, '{e.Value.First()}')");
        sb.AppendLine(string.Join(",\n", entryLines));

        sb.AppendLine("      },");
        sb.AppendLine("      output: {");
        sb.AppendLine("        entryFileNames: 'js/[name].[hash].js',");
        sb.AppendLine("        chunkFileNames: 'js/[name].[hash].js',");
        sb.AppendLine("        assetFileNames: 'assets/[name].[hash][extname]'");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  server: {");
        sb.AppendLine("    strictPort: true,");
        sb.AppendLine("    hmr: {");
        sb.AppendLine("      clientPort: 5173");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("});");

        return sb.ToString();
    }

    private string GetEntryName(string virtualPath)
    {
        // ~/bundles/jquery -> jquery
        return Path.GetFileNameWithoutExtension(virtualPath.TrimStart('~', '/'));
    }

    private string ConvertToModernPath(string legacyPath)
    {
        // ~/Scripts/jquery.js -> src/js/jquery.js
        return legacyPath
            .Replace("~/Scripts/", "src/js/")
            .Replace("~/Content/", "src/css/")
            .Replace("{version}", "*");
    }
}

public class ViteConfiguration
{
    public Dictionary<string, List<string>> JsEntries { get; } = new();
    public Dictionary<string, List<string>> CssEntries { get; } = new();
}
```

### View Transform for Asset References
```csharp
namespace NetLift.Mvc.Transformers;

public class AssetReferenceTransformer
{
    private readonly Dictionary<string, string> _bundleMapping;

    public AssetReferenceTransformer(IEnumerable<BundleDefinition> bundles)
    {
        _bundleMapping = bundles.ToDictionary(
            b => b.VirtualPath,
            b => GetModernAssetPath(b));
    }

    public string TransformRazorView(string viewContent)
    {
        var result = viewContent;

        // Transform @Styles.Render("~/bundles/css")
        result = TransformStylesRender(result);

        // Transform @Scripts.Render("~/bundles/jquery")
        result = TransformScriptsRender(result);

        return result;
    }

    private string TransformStylesRender(string content)
    {
        var pattern = @"@Styles\.Render\(""([^""]+)""\)";

        return Regex.Replace(content, pattern, match =>
        {
            var bundlePath = match.Groups[1].Value;

            if (_bundleMapping.TryGetValue(bundlePath, out var modernPath))
            {
                return $"<link rel=\"stylesheet\" href=\"{modernPath}\" asp-append-version=\"true\" />";
            }

            // Comment out unmapped bundles
            return $"@* TODO: Migrate bundle {bundlePath} *@\n" +
                   $"<link rel=\"stylesheet\" href=\"/dist/css/{GetBundleName(bundlePath)}.css\" asp-append-version=\"true\" />";
        });
    }

    private string TransformScriptsRender(string content)
    {
        var pattern = @"@Scripts\.Render\(""([^""]+)""\)";

        return Regex.Replace(content, pattern, match =>
        {
            var bundlePath = match.Groups[1].Value;

            if (_bundleMapping.TryGetValue(bundlePath, out var modernPath))
            {
                return $"<script src=\"{modernPath}\" asp-append-version=\"true\"></script>";
            }

            // Comment out unmapped bundles
            return $"@* TODO: Migrate bundle {bundlePath} *@\n" +
                   $"<script src=\"/dist/js/{GetBundleName(bundlePath)}.js\" asp-append-version=\"true\"></script>";
        });
    }

    private string GetModernAssetPath(BundleDefinition bundle)
    {
        var name = GetBundleName(bundle.VirtualPath);
        var ext = bundle.Type == BundleType.Script ? "js" : "css";
        return $"/dist/{ext}/{name}.{ext}";
    }

    private string GetBundleName(string virtualPath)
    {
        return Path.GetFileNameWithoutExtension(
            virtualPath.TrimStart('~', '/').Split('/').Last());
    }
}
```

### Package.json Generator
```csharp
public class PackageJsonGenerator
{
    public string Generate(bool useVite = true)
    {
        var packageJson = new
        {
            name = "aspnetcore-frontend",
            version = "1.0.0",
            scripts = new
            {
                dev = useVite ? "vite" : "webpack serve --mode development",
                build = useVite ? "vite build" : "webpack --mode production",
                watch = useVite ? "vite build --watch" : "webpack --watch"
            },
            devDependencies = useVite
                ? new Dictionary<string, string>
                {
                    ["vite"] = "^5.0.0",
                    ["sass"] = "^1.69.0"
                }
                : new Dictionary<string, string>
                {
                    ["webpack"] = "^5.89.0",
                    ["webpack-cli"] = "^5.1.4",
                    ["css-loader"] = "^6.8.1",
                    ["mini-css-extract-plugin"] = "^2.7.6",
                    ["sass"] = "^1.69.0",
                    ["sass-loader"] = "^13.3.2"
                }
        };

        return JsonSerializer.Serialize(packageJson, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
```

### Example Transformation

**Before (BundleConfig.cs):**
```csharp
public class BundleConfig
{
    public static void RegisterBundles(BundleCollection bundles)
    {
        bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
            "~/Scripts/jquery-{version}.js"));

        bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
            "~/Scripts/bootstrap.js"));

        bundles.Add(new StyleBundle("~/Content/css").Include(
            "~/Content/bootstrap.css",
            "~/Content/site.css"));
    }
}
```

**Before (_Layout.cshtml):**
```cshtml
<head>
    @Styles.Render("~/Content/css")
</head>
<body>
    @Scripts.Render("~/bundles/jquery")
    @Scripts.Render("~/bundles/bootstrap")
</body>
```

**After (vite.config.js):**
```javascript
import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig({
  build: {
    outDir: 'wwwroot/dist',
    manifest: true,
    rollupOptions: {
      input: {
        'main': resolve(__dirname, 'src/js/main.js'),
        'styles': resolve(__dirname, 'src/css/site.scss')
      }
    }
  }
});
```

**After (_Layout.cshtml):**
```cshtml
<head>
    <link rel="stylesheet" href="/dist/css/styles.css" asp-append-version="true" />
</head>
<body>
    <script type="module" src="/dist/js/main.js" asp-append-version="true"></script>
</body>
```

### Unit Tests
```csharp
public class BundleConfigParserTests
{
    [Fact]
    public async Task ParsesScriptBundle()
    {
        var source = @"
bundles.Add(new ScriptBundle(""~/bundles/jquery"").Include(
    ""~/Scripts/jquery.js""));";

        var bundles = ParseBundles(source);

        Assert.Single(bundles);
        Assert.Equal(BundleType.Script, bundles[0].Type);
        Assert.Equal("~/bundles/jquery", bundles[0].VirtualPath);
    }

    [Fact]
    public async Task ParsesStyleBundle()
    {
        var source = @"
bundles.Add(new StyleBundle(""~/Content/css"").Include(
    ""~/Content/site.css""));";

        var bundles = ParseBundles(source);

        Assert.Single(bundles);
        Assert.Equal(BundleType.Style, bundles[0].Type);
    }

    [Fact]
    public async Task ParsesMultipleIncludes()
    {
        var source = @"
bundles.Add(new ScriptBundle(""~/bundles/app"").Include(
    ""~/Scripts/app.js"",
    ""~/Scripts/utils.js""));";

        var bundles = ParseBundles(source);

        Assert.Equal(2, bundles[0].IncludedFiles.Count);
    }

    [Fact]
    public void GeneratesViteConfig()
    {
        var bundles = new[]
        {
            new BundleDefinition
            {
                VirtualPath = "~/bundles/app",
                Type = BundleType.Script,
                IncludedFiles = new() { "~/Scripts/app.js" }
            }
        };

        var generator = new ViteConfigGenerator();
        var config = generator.GenerateViteConfigFile(
            generator.Generate(bundles));

        Assert.Contains("defineConfig", config);
        Assert.Contains("rollupOptions", config);
    }

    [Fact]
    public void TransformsStylesRenderCall()
    {
        var view = @"@Styles.Render(""~/Content/css"")";
        var bundles = new[]
        {
            new BundleDefinition
            {
                VirtualPath = "~/Content/css",
                Type = BundleType.Style
            }
        };

        var transformer = new AssetReferenceTransformer(bundles);
        var result = transformer.TransformRazorView(view);

        Assert.Contains("<link rel=\"stylesheet\"", result);
        Assert.Contains("asp-append-version=\"true\"", result);
        Assert.DoesNotContain("@Styles.Render", result);
    }

    [Fact]
    public void TransformsScriptsRenderCall()
    {
        var view = @"@Scripts.Render(""~/bundles/jquery"")";
        var bundles = new[]
        {
            new BundleDefinition
            {
                VirtualPath = "~/bundles/jquery",
                Type = BundleType.Script
            }
        };

        var transformer = new AssetReferenceTransformer(bundles);
        var result = transformer.TransformRazorView(view);

        Assert.Contains("<script src=", result);
        Assert.DoesNotContain("@Scripts.Render", result);
    }
}
```

## Progress Log
- [Created] - Task definition with bundle migration implementation details
