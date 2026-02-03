# Static Files & wwwroot Migration - Implementation Plan

> **Feature:** Automated migration of Content/Scripts folders to ASP.NET Core wwwroot structure

---

## Executive Summary

ASP.NET Core serves static files from the `wwwroot` folder by default, unlike MVC5 which uses `Content`, `Scripts`, and other folders. This migration is critical for web apps to function correctly.

**Key Transformations:**
- `Content/` folder → `wwwroot/css/`
- `Scripts/` folder → `wwwroot/js/`
- `Images/` folder → `wwwroot/images/`
- `fonts/` folder → `wwwroot/fonts/`
- Path references throughout codebase
- Static file middleware configuration

---

## Current MVC5 Structure

```
MyMvcApp/
├── Content/
│   ├── Site.css
│   ├── bootstrap.css
│   └── themes/
├── Scripts/
│   ├── jquery-3.x.js
│   ├── jquery.validate.js
│   └── app.js
├── Images/
│   └── logo.png
├── fonts/
│   └── glyphicons.woff
└── Views/
```

## Target ASP.NET Core Structure

```
MyMvcApp/
├── wwwroot/
│   ├── css/
│   │   ├── site.css
│   │   └── bootstrap.css
│   ├── js/
│   │   ├── jquery.min.js
│   │   └── app.js
│   ├── images/
│   │   └── logo.png
│   └── fonts/
│       └── glyphicons.woff
├── Views/
└── Program.cs (with app.UseStaticFiles())
```

---

## Architecture

### Models (NetLift.Core/Models/StaticFiles/)

```csharp
public record StaticFilesInfo
{
    public required string ProjectPath { get; init; }
    public IReadOnlyList<StaticFolder> Folders { get; init; } = [];
    public IReadOnlyList<StaticFileReference> References { get; init; } = [];
    public bool HasContentFolder { get; init; }
    public bool HasScriptsFolder { get; init; }
    public bool HasImagesFolder { get; init; }
    public bool HasBundleConfig { get; init; }
}

public record StaticFolder
{
    public required string SourcePath { get; init; }      // Content
    public required string TargetPath { get; init; }      // wwwroot/css
    public required StaticFolderType FolderType { get; init; }
    public IReadOnlyList<string> Files { get; init; } = [];
    public long TotalSizeBytes { get; init; }
}

public enum StaticFolderType
{
    Css,
    JavaScript,
    Images,
    Fonts,
    Other
}

public record StaticFileReference
{
    public required string FilePath { get; init; }
    public required string OriginalPath { get; init; }    // ~/Content/site.css
    public required string NewPath { get; init; }         // ~/css/site.css
    public int LineNumber { get; init; }
    public required ReferenceType ReferenceType { get; init; }
}

public enum ReferenceType
{
    CshtmlLink,         // <link href="~/Content/...">
    CshtmlScript,       // <script src="~/Scripts/...">
    CshtmlImage,        // <img src="~/Images/...">
    UrlContent,         // @Url.Content("~/Content/...")
    CssUrl,             // url('../Images/...')
    BundleConfig        // bundles.Add(new StyleBundle("~/Content/css"))
}
```

### Interfaces

```csharp
public interface IStaticFilesAnalyzer
{
    Task<StaticFilesInfo> AnalyzeAsync(
        string projectPath, CancellationToken ct = default);
    IReadOnlyList<StaticFolder> DetectStaticFolders(string projectPath);
    IReadOnlyList<StaticFileReference> DetectReferences(string projectPath);
}

public interface IStaticFilesMigrator
{
    Task<MigrationResult> MigrateAsync(
        StaticFilesInfo info, CancellationToken ct = default);
    Task CreateWwwrootStructureAsync(string projectPath);
    Task MoveFilesAsync(StaticFolder folder);
    Task UpdateReferencesAsync(IReadOnlyList<StaticFileReference> refs);
}

public interface IStaticFilesConfigGenerator
{
    string GenerateStaticFilesMiddleware();
    string GenerateWwwrootCsproj();
}
```

---

## Migration Rules

### 1. Folder Mapping (Confidence: 95%)

| Source Folder | Target Folder | Notes |
|--------------|---------------|-------|
| `Content/` | `wwwroot/css/` | CSS files |
| `Content/themes/` | `wwwroot/css/themes/` | Theme CSS |
| `Scripts/` | `wwwroot/js/` | JavaScript |
| `Images/` | `wwwroot/images/` | Images |
| `fonts/` | `wwwroot/fonts/` | Fonts |
| `Content/images/` | `wwwroot/images/` | Nested images |

### 2. Path Reference Updates (Confidence: 90%)

**Razor Views:**
```cshtml
<!-- Before -->
<link href="~/Content/Site.css" rel="stylesheet" />
<script src="~/Scripts/jquery.js"></script>
<img src="~/Images/logo.png" />

<!-- After -->
<link href="~/css/site.css" rel="stylesheet" />
<script src="~/js/jquery.js"></script>
<img src="~/images/logo.png" />
```

**Url.Content:**
```cshtml
<!-- Before -->
<img src="@Url.Content("~/Content/images/logo.png")" />

<!-- After -->
<img src="~/images/logo.png" />
```

### 3. CSS url() References (Confidence: 85%)

```css
/* Before (in Content/Site.css) */
background-image: url('../Images/bg.png');

/* After (in wwwroot/css/site.css) */
background-image: url('../images/bg.png');
```

### 4. BundleConfig Paths (Confidence: 80%)

```csharp
// Before
bundles.Add(new StyleBundle("~/Content/css").Include(
    "~/Content/bootstrap.css",
    "~/Content/site.css"));

// After - Generate TODO or Vite config
// TODO: BundleConfig removed. Consider using Vite:
// import './css/bootstrap.css'
// import './css/site.css'
```

### 5. Static Files Middleware (Confidence: 100%)

```csharp
// Add to Program.cs
app.UseStaticFiles();
```

### 6. Project File Updates (Confidence: 95%)

```xml
<!-- .csproj - wwwroot folder auto-published -->
<ItemGroup>
  <Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## Sprint Tasks

### Sprint 19: Static Files Migration (10 tasks)

| # | Task | Size | Description |
|---|------|------|-------------|
| 185 | StaticFilesInfo model | S | Analysis result model |
| 186 | StaticFileReference model | S | Reference tracking model |
| 187 | IStaticFilesAnalyzer interface | S | Analysis contract |
| 188 | IStaticFilesMigrator interface | S | Migration contract |
| 189 | StaticFilesAnalyzer | M | Detect folders & references |
| 190 | StaticFilesMigrator | L | Move files, update references |
| 191 | CSS url() parser | M | Update CSS references |
| 192 | Static files middleware generator | S | Program.cs updates |
| 193 | Unit tests (30+) | L | Path transformation tests |
| 194 | Integration tests | M | Full migration test |

---

## Test Strategy

### Unit Tests (30+ tests)

**FolderDetectionTests:**
- Detect_ContentFolder
- Detect_ScriptsFolder
- Detect_ImagesFolder
- Detect_NestedFolders
- Detect_NoStaticFolders

**PathTransformTests:**
- Transform_ContentPath_ToCss
- Transform_ScriptsPath_ToJs
- Transform_ImagesPath
- Transform_NestedPath
- Transform_UrlContent
- Transform_CssUrl

**ReferenceDetectionTests:**
- Detect_LinkHref_InCshtml
- Detect_ScriptSrc_InCshtml
- Detect_ImgSrc_InCshtml
- Detect_UrlContent_InCshtml
- Detect_CssUrl_InCss

**MigrationTests:**
- Move_ContentFolder_ToWwwroot
- Move_ScriptsFolder_ToWwwroot
- Preserve_FolderStructure
- Update_AllReferences

---

## Confidence Scoring

| Transformation | Confidence | Notes |
|---------------|-----------|-------|
| Folder detection | 95% | Well-known patterns |
| File move | 100% | Straightforward |
| Razor path updates | 90% | Regex-based |
| CSS url() updates | 85% | Relative path complexity |
| BundleConfig removal | 80% | Generate TODO |
| Static files middleware | 100% | Standard setup |

---

## Edge Cases

1. **Custom folder names** - Detect via project file includes
2. **CDN references** - Skip (already external)
3. **Embedded resources** - Flag for manual review
4. **Case sensitivity** - Normalize to lowercase (Linux compat)

---

*Last updated: 2026-02-03*
