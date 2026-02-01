# [TASK-018] Create NuGet Package Mapping Rules

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 2 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** (none)
- **Blocks:** TASK-017

---

## Description

Create a YAML-based configuration file containing mapping rules for converting legacy NuGet packages to modern equivalents. This provides a flexible, maintainable way to handle package migrations without hardcoding rules.

---

## Acceptance Criteria

- [ ] YAML schema for package mapping rules
- [ ] Default rules file with common package migrations
- [ ] Support for remove, replace, upgrade, keep actions
- [ ] Version suggestions per target framework
- [ ] Deprecation warnings and manual review flags
- [ ] Parser for YAML rules file
- [ ] Validation of rules schema
- [ ] Unit tests for rules parser
- [ ] Documentation for adding custom rules
- [ ] 50+ common package mappings included

---

## Technical Notes

### YAML Schema Design:

```yaml
# package-mappings.yml
version: 1.0
description: NuGet package migration rules for NetLift

# Global settings
settings:
  default_action: keep
  preserve_version: true
  warn_on_major_upgrade: true

# Package mapping rules
mappings:
  # Remove - package is now part of framework
  - old_package: Microsoft.Bcl
    action: remove
    reason: Functionality is now built into .NET Framework
    applies_to:
      - net45
      - net46
      - net47
      - net48
      - net8.0

  # Replace - package has been superseded
  - old_package: Microsoft.AspNet.Mvc
    action: replace
    new_package: Microsoft.AspNetCore.Mvc
    version_mapping:
      net48: "2.2.0"
      net6.0: "6.0.0"
      net8.0: "8.0.0"
    reason: ASP.NET MVC 5 → ASP.NET Core MVC
    requires_code_changes: true
    migration_guide: https://docs.microsoft.com/aspnet/core/migration
    notes: |
      Namespace changes required:
      - System.Web.Mvc → Microsoft.AspNetCore.Mvc
      - System.Web.Http → Microsoft.AspNetCore.Mvc

  # Upgrade - same package, different version
  - old_package: Newtonsoft.Json
    action: upgrade
    version_mapping:
      net48: "13.0.3"
      net6.0: "13.0.3"
      net8.0: "13.0.3"
    reason: Security and performance improvements
    breaking_changes: false

  # Manual review required
  - old_package: System.Web
    action: manual
    reason: System.Web has no direct replacement in .NET Core
    suggestion: Use ASP.NET Core equivalents for specific functionality
    related_packages:
      - Microsoft.AspNetCore.Http
      - Microsoft.AspNetCore.HttpContext
      - Microsoft.Extensions.Caching.Memory

# Framework-specific removals
framework_packages:
  net8.0:
    # These are built into .NET 8
    - System.Net.Http
    - System.ComponentModel.Annotations
    - System.ValueTuple

  net48:
    # These are built into .NET Framework 4.8
    - System.Runtime
    - System.Threading.Tasks

# Analyzer packages (special handling)
analyzers:
  - package: StyleCop.Analyzers
    action: keep
    latest_version: "1.2.0-beta.556"
    private_assets: all

  - package: Microsoft.CodeAnalysis.FxCopAnalyzers
    action: replace
    new_package: Microsoft.CodeAnalysis.NetAnalyzers
    reason: FxCopAnalyzers is deprecated
    version_mapping:
      "*": "8.0.0"

# ASP.NET specific migrations
aspnet_migrations:
  - old_package: Microsoft.AspNet.WebApi
    action: replace
    new_package: Microsoft.AspNetCore.Mvc
    framework_compatibility:
      net48:
        action: keep  # Can't migrate to .NET Core on .NET 4.8
      net6.0:
        action: replace
        version: "6.0.0"
      net8.0:
        action: replace
        version: "8.0.0"

  - old_package: Microsoft.AspNet.Identity.EntityFramework
    action: replace
    new_package: Microsoft.AspNetCore.Identity.EntityFrameworkCore
    version_mapping:
      net6.0: "6.0.0"
      net8.0: "8.0.0"
    requires_code_changes: true

# Entity Framework migrations
ef_migrations:
  - old_package: EntityFramework
    action: upgrade
    version_mapping:
      net48: "6.4.4"  # Latest EF6 for .NET Framework
      net6.0: "6.4.4"  # EF6 works on .NET Core too
      net8.0: "6.4.4"
    notes: |
      For new projects, consider Microsoft.EntityFrameworkCore instead.
      EF6 is maintained for compatibility.

  - old_package: EntityFramework
    action: replace
    new_package: Microsoft.EntityFrameworkCore
    framework_compatibility:
      net6.0:
        version: "6.0.33"
      net8.0:
        version: "8.0.0"
    requires_code_changes: true
    migration_guide: https://docs.microsoft.com/ef/core/porting/

# Testing frameworks
testing_migrations:
  - old_package: NUnit
    action: upgrade
    version_mapping:
      "*": "4.1.0"

  - old_package: xunit
    action: upgrade
    version_mapping:
      "*": "2.6.6"

  - old_package: Moq
    action: upgrade
    version_mapping:
      "*": "4.20.70"

  - old_package: FluentAssertions
    action: upgrade
    version_mapping:
      "*": "6.12.0"

# Logging migrations
logging_migrations:
  - old_package: log4net
    action: manual
    reason: Consider migrating to Microsoft.Extensions.Logging
    suggestion: Use Serilog or NLog with Microsoft.Extensions.Logging integration

  - old_package: NLog
    action: keep
    suggested_additional:
      - NLog.Extensions.Logging

  - old_package: Serilog
    action: keep
    suggested_additional:
      - Serilog.Extensions.Logging
      - Serilog.Sinks.Console
      - Serilog.Sinks.File

# Obsolete packages
obsolete_packages:
  - package: Microsoft.Net.Compilers
    reason: Roslyn compilers are built into SDK
    action: remove

  - package: Microsoft.CodeDom.Providers.DotNetCompilerPlatform
    reason: Not needed with SDK-style projects
    action: remove

  - package: Microsoft.Bcl.Async
    reason: Async/await is built into modern .NET
    action: remove

# Security packages
security_migrations:
  - old_package: System.IdentityModel.Tokens.Jwt
    action: upgrade
    version_mapping:
      "*": "7.3.1"
    security_update: true

  - old_package: Microsoft.IdentityModel.Tokens
    action: upgrade
    version_mapping:
      "*": "7.3.1"
    security_update: true
```

### Rules Parser Implementation:

```csharp
public class PackageMappingRules
{
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = string.Empty;
    public RuleSettings Settings { get; set; } = new();
    public List<PackageMappingRule> Mappings { get; set; } = new();
    public Dictionary<string, List<string>> FrameworkPackages { get; set; } = new();
    public List<AnalyzerRule> Analyzers { get; set; } = new();
    public List<PackageMappingRule> AspNetMigrations { get; set; } = new();
    public List<PackageMappingRule> EfMigrations { get; set; } = new();
    public List<PackageMappingRule> TestingMigrations { get; set; } = new();
    public List<PackageMappingRule> LoggingMigrations { get; set; } = new();
    public List<ObsoletePackage> ObsoletePackages { get; set; } = new();
}

public class RuleSettings
{
    public string DefaultAction { get; set; } = "keep";
    public bool PreserveVersion { get; set; } = true;
    public bool WarnOnMajorUpgrade { get; set; } = true;
}

public class PackageMappingRule
{
    public string OldPackage { get; set; } = string.Empty;
    public MappingAction Action { get; set; }
    public string? NewPackage { get; set; }
    public Dictionary<string, string>? VersionMapping { get; set; }
    public string? Reason { get; set; }
    public bool RequiresCodeChanges { get; set; }
    public string? MigrationGuide { get; set; }
    public string? Notes { get; set; }
    public string? Suggestion { get; set; }
    public List<string>? RelatedPackages { get; set; }
    public Dictionary<string, FrameworkAction>? FrameworkCompatibility { get; set; }
    public List<string>? SuggestedAdditional { get; set; }
    public bool SecurityUpdate { get; set; }
}

public class FrameworkAction
{
    public MappingAction Action { get; set; }
    public string? Version { get; set; }
}

public class AnalyzerRule
{
    public string Package { get; set; } = string.Empty;
    public MappingAction Action { get; set; }
    public string? NewPackage { get; set; }
    public string? LatestVersion { get; set; }
    public string? PrivateAssets { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, string>? VersionMapping { get; set; }
}

public class ObsoletePackage
{
    public string Package { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public MappingAction Action { get; set; }
}

public class PackageMappingRulesParser
{
    private readonly ILogger<PackageMappingRulesParser> _logger;

    public async Task<PackageMappingRules> LoadRulesAsync(string yamlPath)
    {
        var yaml = await File.ReadAllTextAsync(yamlPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var rules = deserializer.Deserialize<PackageMappingRules>(yaml);

        ValidateRules(rules);

        return rules;
    }

    private void ValidateRules(PackageMappingRules rules)
    {
        foreach (var mapping in rules.Mappings)
        {
            if (string.IsNullOrEmpty(mapping.OldPackage))
                throw new InvalidOperationException("old_package is required");

            if (mapping.Action == MappingAction.Replace &&
                string.IsNullOrEmpty(mapping.NewPackage))
                throw new InvalidOperationException(
                    $"new_package is required for replace action: {mapping.OldPackage}");
        }
    }

    public bool TryGetMapping(
        string packageId,
        string targetFramework,
        out PackageMappingRule? mapping)
    {
        // Check direct mappings first
        mapping = FindMapping(packageId, targetFramework);
        return mapping != null;
    }

    private PackageMappingRule? FindMapping(string packageId, string targetFramework)
    {
        // Check all mapping categories
        var allMappings = new[]
        {
            _rules.Mappings,
            _rules.AspNetMigrations,
            _rules.EfMigrations,
            _rules.TestingMigrations,
            _rules.LoggingMigrations
        }
        .SelectMany(m => m)
        .ToList();

        var mapping = allMappings.FirstOrDefault(m =>
            m.OldPackage.Equals(packageId, StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
            return null;

        // Check framework-specific compatibility
        if (mapping.FrameworkCompatibility != null &&
            mapping.FrameworkCompatibility.TryGetValue(targetFramework, out var frameworkAction))
        {
            // Override action for this framework
            return new PackageMappingRule
            {
                OldPackage = mapping.OldPackage,
                Action = frameworkAction.Action,
                NewPackage = mapping.NewPackage,
                VersionMapping = new Dictionary<string, string>
                {
                    [targetFramework] = frameworkAction.Version ?? "latest"
                },
                Reason = mapping.Reason,
                RequiresCodeChanges = mapping.RequiresCodeChanges,
                MigrationGuide = mapping.MigrationGuide,
                Notes = mapping.Notes
            };
        }

        return mapping;
    }
}
```

### Usage in Converter:

```csharp
public class PackageReferenceConverter
{
    private readonly PackageMappingRules _rules;

    public PackageReferenceConverter(PackageMappingRulesParser rulesParser)
    {
        _rules = rulesParser.LoadRulesAsync("package-mappings.yml").Result;
    }

    public bool TryGetMapping(string packageId, out PackageMappingRule? mapping)
    {
        return _rules.TryGetMapping(packageId, _targetFramework, out mapping);
    }
}
```

### Files to create/modify:

- `src/NetLift.Migration/Configuration/package-mappings.yml` - Default rules
- `src/NetLift.Migration/Models/PackageMappingRules.cs` - Rules model
- `src/NetLift.Migration/Parsers/PackageMappingRulesParser.cs` - YAML parser
- `tests/NetLift.Tests/Parsers/PackageMappingRulesParserTests.cs` - Unit tests
- `docs/CustomPackageMappings.md` - User documentation

### Key Decisions:

- Use YAML for readability and maintainability
- Support framework-specific rules (net48 vs net8.0)
- Include migration guides and documentation links
- Flag packages requiring code changes
- Allow users to provide custom rules files
- Use YamlDotNet library for parsing

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-01-31 | - | Created |
